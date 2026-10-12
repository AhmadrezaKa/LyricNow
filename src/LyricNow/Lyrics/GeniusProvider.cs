using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LyricNow.Lyrics;

/// <summary>
/// Genius (genius.com) has wide coverage, including many Persian songs, but only plain (unsynced) lyrics.
/// Its public API returns search results but not lyrics, so the lyrics are read from the song's web page.
/// That's outside Genius's official API terms; it's meant for personal use and may break if Genius changes its pages.
/// </summary>
public sealed partial class GeniusProvider(HttpClient http) : ILyricsProvider
{
    [GeneratedRegex(@"^\[.*\]$")]
    private static partial Regex SectionHeader();

    public string Name => "Genius";

    public bool ProvidesSyncedLyrics => false;

    public async Task<LyricsResult?> FindAsync(TrackInfo track, CancellationToken ct)
    {
        var title = TitleCleaner.CleanTitle(track.Title);
        var artist = TitleCleaner.PrimaryArtist(track.Artist);

        var hit = await SearchAsync($"{artist} {title}", title, artist, ct)
                  ?? await SearchAsync(title, title, artist, ct);
        if (hit is null)
            return null;
        if (hit.Instrumental)
            return LyricsResult.Instrumental(Name);

        var html = await http.GetStringAsync(hit.Url, ct);
        var lyrics = ExtractLyrics(html);
        return LyricsResult.FromText(null, lyrics, track.Duration, Name);
    }

    private async Task<GeniusHit?> SearchAsync(string query, string title, string artist, CancellationToken ct)
    {
        var url = $"https://genius.com/api/search/song?per_page=10&q={Uri.EscapeDataString(query)}";
        using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));

        if (!doc.RootElement.TryGetProperty("response", out var response) ||
            !response.TryGetProperty("sections", out var sections))
            return null;

        foreach (var section in sections.EnumerateArray())
        {
            if (!section.TryGetProperty("hits", out var hits))
                continue;

            foreach (var h in hits.EnumerateArray())
            {
                if (!h.TryGetProperty("result", out var r))
                    continue;

                var hitTitle = GetString(r, "title");
                var hitTitleFull = GetString(r, "title_with_featured");
                var hitArtist = r.TryGetProperty("primary_artist", out var pa) ? GetString(pa, "name") : "";
                var hitUrl = GetString(r, "url");

                // Skip Genius's own translation pages (e.g. "Genius English Translations").
                if (hitArtist.StartsWith("Genius", StringComparison.OrdinalIgnoreCase) || hitUrl.Length == 0)
                    continue;

                // Genius often lists Persian names in both scripts, e.g. "Name (نام)", so a loose
                // contains-match covers Latin and Persian spellings.
                var titleOk = TitleCleaner.LooselyMatches(hitTitle, title) || TitleCleaner.LooselyMatches(hitTitleFull, title);
                var artistOk = TitleCleaner.LooselyMatches(hitArtist, artist);
                if (!titleOk || !artistOk)
                    continue;

                var instrumental = r.TryGetProperty("instrumental", out var inst) && inst.ValueKind == JsonValueKind.True;
                return new GeniusHit(hitUrl, instrumental);
            }
        }

        return null;
    }

    /// <summary>Pulls lyric text out of a Genius song page.</summary>
    internal static string? ExtractLyrics(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var containers = document.QuerySelectorAll("[data-lyrics-container='true']");
        if (containers.Length == 0)
            return null;

        var lines = new List<string>();
        foreach (var container in containers)
        {
            // Page furniture (contributor counts, "Read more" headers) lives inside the container but is marked.
            foreach (var junk in container.QuerySelectorAll("[data-exclude-from-selection='true']").ToList())
                junk.Remove();
            foreach (var br in container.QuerySelectorAll("br").ToList())
                br.Replace(document.CreateTextNode("\n"));

            lines.AddRange(container.TextContent.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !SectionHeader().IsMatch(l))); // drop "[Chorus]" etc.
        }

        return lines.Count > 0 ? string.Join('\n', lines) : null;
    }

    private static string GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private sealed record GeniusHit(string Url, bool Instrumental);
}
