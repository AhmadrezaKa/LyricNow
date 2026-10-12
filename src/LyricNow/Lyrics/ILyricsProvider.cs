namespace LyricNow.Lyrics;

public interface ILyricsProvider
{
    string Name { get; }

    /// <summary>Returns lyrics for the track, or null when this provider has none.</summary>
    Task<LyricsResult?> FindAsync(TrackInfo track, CancellationToken ct);
}
