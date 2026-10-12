using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LyricNow.Lyrics;

/// <summary>
/// Asks each provider in turn until one has lyrics, and caches results on disk
/// (%LOCALAPPDATA%\LyricNow\cache) so a song is only looked up once.
/// </summary>
public sealed class LyricsService
{
    private static readonly string CacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyricNow", "cache");

    // Synced-capable providers first: once only plain-text providers remain, the first plain result wins.
    private readonly List<ILyricsProvider> _providers;
    private readonly Func<bool> _useGenius;

    // Tracks nobody had lyrics for; kept in memory only so they're retried on the next app start.
    private readonly HashSet<string> _notFound = [];

    public LyricsService(Func<bool> useGenius)
    {
        _useGenius = useGenius;

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // LRCLIB asks clients to identify themselves.
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LyricNow", "0.1"));
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(https://github.com/AhmadrezaKa/LyricNow)"));

        _providers = [new LrcLibProvider(http), new GeniusProvider(http), new LyricsOvhProvider(http)];
    }

    /// <summary>Call when the set of sources changes, so songs that had no lyrics are searched again.</summary>
    public void ForgetMisses() => _notFound.Clear();

    public async Task<LyricsResult?> FindAsync(TrackInfo track, CancellationToken ct)
    {
        if (_notFound.Contains(track.Key))
            return null;

        var cachePath = Path.Combine(CacheDir, Hash(track.Key) + ".json");
        if (TryReadCache(cachePath) is { } cached)
            return cached;

        LyricsResult? best = null;
        Exception? lastError = null;

        var providers = _providers.Where(p => p is not GeniusProvider || _useGenius()).ToList();

        for (var i = 0; i < providers.Count; i++)
        {
            var provider = providers[i];

            // We already have plain lyrics and nobody further down can do better.
            if (best is not null && !providers.Skip(i).Any(p => p.ProvidesSyncedLyrics))
                break;

            try
            {
                var result = await provider.FindAsync(track, ct);
                if (result is null)
                    continue;
                if (result.IsSynced || result.IsInstrumental)
                {
                    best = result;
                    break;
                }
                // Plain lyrics: keep them, but a later provider might still have synced ones.
                best ??= result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex; // try the next provider
            }
        }

        if (best is not null)
            WriteCache(cachePath, best);
        else if (lastError is not null)
            throw lastError; // network trouble: don't remember this track as "no lyrics"
        else
            _notFound.Add(track.Key);

        return best;
    }

    private static LyricsResult? TryReadCache(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<LyricsResult>(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null; // corrupt cache entry; just fetch again
        }
    }

    private static void WriteCache(string path, LyricsResult result)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(path, JsonSerializer.Serialize(result));
        }
        catch
        {
            // Caching is best-effort.
        }
    }

    private static string Hash(string s) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)));
}
