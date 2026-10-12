namespace LyricNow.Lyrics;

public sealed record TrackInfo(string Title, string Artist, string Album, TimeSpan Duration)
{
    /// <summary>Identity of a track, ignoring duration (which can arrive late from the media session).</summary>
    public string Key => $"{Artist}|{Title}|{Album}".ToLowerInvariant();
}

public sealed record LyricLine(TimeSpan Time, string Text);

public sealed class LyricsResult
{
    public required IReadOnlyList<LyricLine> Lines { get; init; }

    /// <summary>True when line times come from the source; false when they are spread evenly over the song.</summary>
    public bool IsSynced { get; init; }

    public bool IsInstrumental { get; init; }

    public required string Source { get; init; }

    public static LyricsResult Instrumental(string source) =>
        new() { Lines = [], IsInstrumental = true, Source = source };

    /// <summary>Builds a result from synced LRC text if present, otherwise from plain text.</summary>
    public static LyricsResult? FromText(string? synced, string? plain, TimeSpan duration, string source)
    {
        if (!string.IsNullOrWhiteSpace(synced))
        {
            var lines = LrcParser.Parse(synced);
            if (lines.Count > 0)
                return new() { Lines = lines, IsSynced = true, Source = source };
        }

        if (!string.IsNullOrWhiteSpace(plain))
        {
            var lines = SpreadPlainLines(plain, duration);
            if (lines.Count > 0)
                return new() { Lines = lines, IsSynced = false, Source = source };
        }

        return null;
    }

    /// <summary>
    /// Plain lyrics have no timestamps, so we spread the lines evenly over the song.
    /// It's only approximate, but it's better than showing nothing.
    /// </summary>
    private static List<LyricLine> SpreadPlainLines(string plain, TimeSpan duration)
    {
        var texts = plain.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (texts.Count == 0)
            return [];

        // Without a known duration, assume a typical song length.
        var total = duration > TimeSpan.Zero ? duration : TimeSpan.FromMinutes(3.5);
        var step = total / texts.Count;
        return texts.Select((t, i) => new LyricLine(step * i, t)).ToList();
    }
}
