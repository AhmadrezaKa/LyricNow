using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LyricNow.Lyrics;

/// <summary>
/// LRCLIB (https://lrclib.net) is a free, open lyrics database with time-synced lyrics. No API key needed.
/// Tries an exact lookup first, then progressively looser searches.
/// </summary>
public sealed class LrcLibProvider(HttpClient http) : ILyricsProvider
{
    private const string BaseUrl = "https://lrclib.net/api";

    // How far a search result's duration may differ from the track before we treat it as a different recording.
    private static readonly TimeSpan MaxDurationDiff = TimeSpan.FromSeconds(8);

    public string Name => "LRCLIB";

    public async Task<LyricsResult?> FindAsync(TrackInfo track, CancellationToken ct)
    {
        // 1. Exact match on title/artist/album/duration.
        var exact = await GetExactAsync(track, ct);
        if (exact is { Instrumental: true })
            return LyricsResult.Instrumental(Name);
        if (exact?.SyncedLyrics is { Length: > 0 })
            return LyricsResult.FromText(exact.SyncedLyrics, null, track.Duration, Name);

        // 2. Fuzzy searches with cleaned-up title/artist, preferring synced lyrics.
        var title = TitleCleaner.CleanTitle(track.Title);
        var artist = TitleCleaner.PrimaryArtist(track.Artist);

        var searches = new List<string>
        {
            $"track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(track.Artist)}",
        };
        if (!artist.Equals(track.Artist, StringComparison.OrdinalIgnoreCase))
            searches.Add($"track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}");
        searches.Add($"q={Uri.EscapeDataString($"{artist} {title}")}");

        LrcLibRecord? plainFallback = exact?.PlainLyrics is { Length: > 0 } ? exact : null;

        foreach (var query in searches)
        {
            var best = PickBest(await SearchAsync(query, ct), track, title);
            if (best is null)
                continue;
            if (best.SyncedLyrics is { Length: > 0 })
                return LyricsResult.FromText(best.SyncedLyrics, null, track.Duration, Name);
            if (best.Instrumental && plainFallback is null)
                return LyricsResult.Instrumental(Name);
            plainFallback ??= best;
        }

        // 3. No synced lyrics anywhere; plain lyrics are still better than nothing.
        return plainFallback is null
            ? null
            : LyricsResult.FromText(null, plainFallback.PlainLyrics, track.Duration, Name);
    }

    private async Task<LrcLibRecord?> GetExactAsync(TrackInfo track, CancellationToken ct)
    {
        var url = $"{BaseUrl}/get?track_name={Uri.EscapeDataString(track.Title)}" +
                  $"&artist_name={Uri.EscapeDataString(track.Artist)}" +
                  $"&album_name={Uri.EscapeDataString(track.Album)}";
        if (track.Duration > TimeSpan.Zero)
            url += $"&duration={(int)Math.Round(track.Duration.TotalSeconds)}";

        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LrcLibRecord>(ct);
    }

    private async Task<List<LrcLibRecord>> SearchAsync(string query, CancellationToken ct)
    {
        var results = await http.GetFromJsonAsync<List<LrcLibRecord>>($"{BaseUrl}/search?{query}", ct);
        return results ?? [];
    }

    private static LrcLibRecord? PickBest(List<LrcLibRecord> candidates, TrackInfo track, string cleanTitle)
    {
        var hasDuration = track.Duration > TimeSpan.Zero;

        return candidates
            .Where(c => c.Instrumental || c.SyncedLyrics is { Length: > 0 } || c.PlainLyrics is { Length: > 0 })
            .Where(c => TitleCleaner.LooselyMatches(c.TrackName ?? "", cleanTitle))
            .Where(c => !hasDuration || (TimeSpan.FromSeconds(c.Duration) - track.Duration).Duration() <= MaxDurationDiff)
            .OrderByDescending(c => c.SyncedLyrics is { Length: > 0 })
            .ThenBy(c => hasDuration ? Math.Abs(c.Duration - track.Duration.TotalSeconds) : 0)
            .FirstOrDefault();
    }

    private sealed class LrcLibRecord
    {
        [JsonPropertyName("trackName")] public string? TrackName { get; set; }
        [JsonPropertyName("artistName")] public string? ArtistName { get; set; }
        [JsonPropertyName("duration")] public double Duration { get; set; }
        [JsonPropertyName("instrumental")] public bool Instrumental { get; set; }
        [JsonPropertyName("plainLyrics")] public string? PlainLyrics { get; set; }
        [JsonPropertyName("syncedLyrics")] public string? SyncedLyrics { get; set; }
    }
}
