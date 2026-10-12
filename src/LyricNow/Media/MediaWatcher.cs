using LyricNow.Lyrics;
using global::Windows.Media.Control;

namespace LyricNow.Media;

public sealed record PlaybackSnapshot(TimeSpan Position, bool IsPlaying);

/// <summary>
/// Reads "now playing" info from Windows' media sessions (the same data the volume-key media flyout shows).
/// Prefers Spotify's session; falls back to whatever Windows considers the current session.
/// </summary>
public sealed class MediaWatcher
{
    private readonly Lock _gate = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private TrackInfo? _track;
    private int _refreshVersion;

    /// <summary>Raised (on a background thread) when the track changes, or with null when nothing is playing.</summary>
    public event Action<TrackInfo?>? TrackChanged;

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => _ = SelectSessionAsync();
        _manager.CurrentSessionChanged += (_, _) => _ = SelectSessionAsync();
        await SelectSessionAsync();
    }

    /// <summary>
    /// Windows only reports the position now and then, so we add the time elapsed since its last update.
    /// </summary>
    public PlaybackSnapshot? GetPlayback()
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_gate) session = _session;
        if (session is null)
            return null;

        try
        {
            var timeline = session.GetTimelineProperties();
            var playing = session.GetPlaybackInfo().PlaybackStatus ==
                          GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var position = timeline.Position;
            if (playing)
                position += DateTimeOffset.Now - timeline.LastUpdatedTime;
            if (timeline.EndTime > TimeSpan.Zero && position > timeline.EndTime)
                position = timeline.EndTime;

            return new PlaybackSnapshot(position, playing);
        }
        catch
        {
            return null; // session went away mid-call
        }
    }

    private async Task SelectSessionAsync()
    {
        if (_manager is null)
            return;

        var sessions = _manager.GetSessions();
        var picked = sessions.FirstOrDefault(s => s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                     ?? _manager.GetCurrentSession();

        lock (_gate)
        {
            if (_session?.SourceAppUserModelId == picked?.SourceAppUserModelId && _session is not null)
                return;

            if (_session is not null)
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session = picked;
            if (_session is not null)
                _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        }

        await RefreshTrackAsync();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) =>
        _ = RefreshTrackAsync();

    private async Task RefreshTrackAsync()
    {
        // Spotify fires several property changes per track switch and updates the timeline slightly later,
        // so wait a moment and only act on the latest request.
        var version = Interlocked.Increment(ref _refreshVersion);
        await Task.Delay(350);
        if (version != Volatile.Read(ref _refreshVersion))
            return;

        GlobalSystemMediaTransportControlsSession? session;
        lock (_gate) session = _session;

        TrackInfo? track = null;
        if (session is not null)
        {
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                var timeline = session.GetTimelineProperties();
                if (!string.IsNullOrWhiteSpace(props?.Title))
                    track = new TrackInfo(props.Title, props.Artist ?? "", props.AlbumTitle ?? "",
                        timeline.EndTime - timeline.StartTime);
            }
            catch
            {
                // session closed while reading; treat as nothing playing
            }
        }

        lock (_gate)
        {
            if (_track?.Key == track?.Key)
                return;
            _track = track;
        }

        TrackChanged?.Invoke(track);
    }
}
