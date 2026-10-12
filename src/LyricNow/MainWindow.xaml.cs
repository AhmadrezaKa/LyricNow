using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LyricNow.Lyrics;
using LyricNow.Media;

namespace LyricNow;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 24;
    private const double OffsetStepSeconds = 0.5;

    private readonly MediaWatcher _media = new();
    private readonly LyricsService _lyricsService = new();
    private readonly DispatcherTimer _lineTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _spotifyTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Settings _settings = Settings.Load();
    private readonly bool _backgroundMode;

    private CancellationTokenSource? _lookupCts;
    private TrackInfo? _track;
    private LyricsResult? _lyrics;
    private TimeSpan _offset;
    private int _lineIndex = int.MinValue;
    private double _bottom;
    private bool _positioned;
    private bool _spotifySeen;

    /// <param name="backgroundMode">
    /// Started by autostart: stay hidden until Spotify runs. A manual launch shows the widget right away.
    /// </param>
    public MainWindow(bool backgroundMode)
    {
        _backgroundMode = backgroundMode;
        InitializeComponent();
        SizeChanged += (_, _) => KeepBottomAnchored();
        PlaceWindow();
    }

    public async void Start()
    {
        _media.TrackChanged += track => Dispatcher.InvokeAsync(() => OnTrackChanged(track));
        _lineTimer.Tick += (_, _) => UpdateCurrentLine();
        _lineTimer.Start();
        _spotifyTimer.Tick += (_, _) => UpdateVisibility();
        _spotifyTimer.Start();

        ShowStatus("Waiting for Spotify…");
        UpdateVisibility();

        try
        {
            await _media.StartAsync();
        }
        catch (Exception ex)
        {
            ShowStatus("Couldn't connect to Windows media sessions");
            SourceText.Text = ex.Message;
            Show();
        }
    }

    /// <summary>The widget lives alongside Spotify: visible while it runs, hidden once it closes.</summary>
    private void UpdateVisibility()
    {
        var running = IsSpotifyRunning();
        if (running)
            _spotifySeen = true;

        // After a manual launch, show the widget until Spotify has been seen at least once,
        // so it's obvious the app started.
        var shouldShow = running || (!_backgroundMode && !_spotifySeen);

        if (shouldShow && !IsVisible)
            Show();
        else if (!shouldShow && IsVisible)
            Hide();
    }

    private static bool IsSpotifyRunning()
    {
        var processes = Process.GetProcessesByName("Spotify");
        foreach (var p in processes)
            p.Dispose();
        return processes.Length > 0;
    }

    private async void OnTrackChanged(TrackInfo? track)
    {
        _lookupCts?.Cancel();
        _lookupCts = new CancellationTokenSource();
        var ct = _lookupCts.Token;

        _track = track;
        _lyrics = null;
        _lineIndex = int.MinValue;
        _offset = track is not null && _settings.TrackOffsets.TryGetValue(track.Key, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
        UpdateOffsetText();
        SourceText.Text = "";

        if (track is null)
        {
            TrackText.Text = "";
            ShowStatus("Waiting for Spotify…");
            return;
        }

        TrackText.Text = string.IsNullOrEmpty(track.Artist) ? track.Title : $"{track.Title} — {track.Artist}";
        ShowStatus("Searching for lyrics…");

        try
        {
            var result = await _lyricsService.FindAsync(track, ct);
            if (ct.IsCancellationRequested)
                return;

            _lyrics = result;
            if (result is null)
            {
                ShowStatus("No lyrics found");
            }
            else if (result.IsInstrumental)
            {
                ShowStatus("♪ Instrumental ♪");
                SourceText.Text = result.Source;
            }
            else
            {
                SourceText.Text = result.IsSynced ? result.Source : $"{result.Source} · unsynced, timing approximate";
                UpdateCurrentLine();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // a newer track took over
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested)
                return;
            ShowStatus("Couldn't load lyrics");
            SourceText.Text = ex.Message;
        }
    }

    private void UpdateCurrentLine()
    {
        var lyrics = _lyrics;
        if (lyrics is null || lyrics.IsInstrumental || lyrics.Lines.Count == 0)
            return;

        var playback = _media.GetPlayback();
        if (playback is null)
            return;

        var lines = lyrics.Lines;
        var index = FindLineIndex(lines, playback.Position + _offset);
        if (index == _lineIndex)
            return;
        _lineIndex = index;

        PrevLine.Text = index - 1 >= 0 ? DisplayText(lines[index - 1]) : "";
        CurrentLine.Text = index >= 0 ? DisplayText(lines[index]) : "♪";
        NextLine.Text = index + 1 < lines.Count ? DisplayText(lines[index + 1]) : "";

        CurrentLine.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(180)));
    }

    /// <summary>Index of the last line whose timestamp has passed, or -1 before the first line.</summary>
    private static int FindLineIndex(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        int lo = 0, hi = lines.Count - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (lines[mid].Time <= position)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return found;
    }

    // Synced lyrics use empty lines to mark instrumental breaks.
    private static string DisplayText(LyricLine line) => string.IsNullOrWhiteSpace(line.Text) ? "♪" : line.Text;

    private void ShowStatus(string message)
    {
        PrevLine.Text = "";
        CurrentLine.Text = message;
        NextLine.Text = "";
    }

    private void NudgeOffset(double seconds)
    {
        SetOffset(_offset + TimeSpan.FromSeconds(seconds));
    }

    /// <summary>Applies a timing offset to the current song and remembers it for next time.</summary>
    private void SetOffset(TimeSpan offset)
    {
        _offset = offset;
        if (_track is not null)
        {
            if (offset == TimeSpan.Zero)
                _settings.TrackOffsets.Remove(_track.Key);
            else
                _settings.TrackOffsets[_track.Key] = offset.TotalSeconds;
            _settings.Save();
        }

        UpdateOffsetText();
        _lineIndex = int.MinValue; // force a redraw
        UpdateCurrentLine();
    }

    private void UpdateOffsetText() =>
        OffsetText.Text = _offset == TimeSpan.Zero ? "timing" : $"{_offset.TotalSeconds:+0.0;-0.0}s";

    private void PlaceWindow()
    {
        var area = SystemParameters.WorkArea;
        var left = _settings.Left ?? area.Right - Width - ScreenMargin;
        var bottom = _settings.Bottom ?? area.Bottom - ScreenMargin;

        // A saved position may be on a monitor that's no longer connected.
        var onScreen = left >= SystemParameters.VirtualScreenLeft &&
                       left + Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                       bottom >= SystemParameters.VirtualScreenTop + 50 &&
                       bottom <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        if (!onScreen)
        {
            left = area.Right - Width - ScreenMargin;
            bottom = area.Bottom - ScreenMargin;
        }

        Left = left;
        _bottom = bottom;
        _positioned = true;
        // Before the first show the height is unknown; SizeChanged corrects this once it's measured.
        Top = _bottom - (ActualHeight > 0 ? ActualHeight : 150);
    }

    private void KeepBottomAnchored()
    {
        if (_positioned)
            Top = _bottom - ActualHeight;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
        _bottom = Top + ActualHeight;
        _settings.Left = Left;
        _settings.Bottom = _bottom;
        _settings.Save();
    }

    private void OnMouseEnter(object sender, MouseEventArgs e) => TimingPanel.Opacity = 1;

    private void OnMouseLeave(object sender, MouseEventArgs e) => TimingPanel.Opacity = 0;

    private void OnDelayLyrics(object sender, RoutedEventArgs e) => NudgeOffset(-OffsetStepSeconds);

    private void OnAdvanceLyrics(object sender, RoutedEventArgs e) => NudgeOffset(OffsetStepSeconds);

    private void OnResetTiming(object sender, RoutedEventArgs e) => SetOffset(TimeSpan.Zero);

    private void OnMenuOpened(object sender, RoutedEventArgs e) => AutostartItem.IsChecked = Autostart.IsEnabled;

    private void OnToggleAutostart(object sender, RoutedEventArgs e)
    {
        try
        {
            Autostart.SetEnabled(AutostartItem.IsChecked);
        }
        catch (Exception ex)
        {
            AutostartItem.IsChecked = Autostart.IsEnabled;
            MessageBox.Show(this, $"Couldn't change the startup setting:\n{ex.Message}", "LyricNow");
        }
    }

    private void OnResetPosition(object sender, RoutedEventArgs e)
    {
        _settings.Left = null;
        _settings.Bottom = null;
        _settings.Save();
        PlaceWindow();
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();
}
