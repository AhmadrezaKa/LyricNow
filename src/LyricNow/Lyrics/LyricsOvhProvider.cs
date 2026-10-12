using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LyricNow.Lyrics;

/// <summary>
/// lyrics.ovh: a free fallback that only has plain (unsynced) lyrics.
/// It's sometimes slow or down, so failures are treated as "not found".
/// </summary>
public sealed class LyricsOvhProvider(HttpClient http) : ILyricsProvider
{
    public string Name => "lyrics.ovh";

    public bool ProvidesSyncedLyrics => false;

    public async Task<LyricsResult?> FindAsync(TrackInfo track, CancellationToken ct)
    {
        var artist = TitleCleaner.PrimaryArtist(track.Artist);
        var title = TitleCleaner.CleanTitle(track.Title);
        var url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));

        try
        {
            using var response = await http.GetAsync(url, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<Response>(timeout.Token);
            return LyricsResult.FromText(null, body?.Lyrics, track.Duration, Name);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // our own timeout, not the caller cancelling
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private sealed class Response
    {
        [JsonPropertyName("lyrics")] public string? Lyrics { get; set; }
    }
}
