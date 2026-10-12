using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LyricNow.Lyrics;
using LyricNow.Media;

namespace LyricNow;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 24;
    private const double OffsetStepSeconds = 0.5;

    // Bricolage Grotesque has no Persian letters, so WPF falls back to Vazirmatn for those automatically.
    private static readonly FontFamily AppFont =
        new(new Uri("pack://application:,,,/"), "./Fonts/#Bricolage Grotesque, ./Fonts/#Vazirmatn");
    private static readonly FontFamily ClassicFont = new("Segoe UI");

    private static readonly Brush SolidFrostedFallback =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E61C1F2A")).Frozen();
    private static readonly Brush DefaultCover =
        new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#E9A35E"),
            (Color)ColorConverter.ConvertFromString("#7B4FA8"), 45).Frozen();

    private readonly MediaWatcher _media = new();
    private readonly LyricsService _lyricsService;
    private readonly DispatcherTimer _lineTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _spotifyTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Settings _settings = Settings.Load();
    private readonly bool _backgroundMode;

    // The karaoke fill: stops 1 and 2 move together to form a hard edge between sung and unsung.
    private readonly LinearGradientBrush _karaokeBrush = new(
        [
            new GradientStop(ThemeSpec.KaraokeColor, 0),
            new GradientStop(ThemeSpec.KaraokeColor, 0),
            new GradientStop(ThemeSpec.KaraokeDim, 0),
            new GradientStop(ThemeSpec.KaraokeDim, 1),
        ],
        new Point(0, 0), new Point(1, 0));

    private ThemeSpec _theme = ThemeSpec.All[0];
    private CancellationTokenSource? _lookupCts;
    private TrackInfo? _track;
    private byte[]? _cover;
    private LyricsResult? _lyrics;
    private TimeSpan _offset;
    private int _lineIndex = int.MinValue;
    private double _bottom;
    private bool _positioned;
    private bool _spotifySeen;
    private bool _showingStatus = true;

    /// <param name="backgroundMode">
    /// Started by autostart: stay hidden until Spotify runs. A manual launch shows the widget right away.
    /// </param>
    public MainWindow(bool backgroundMode)
    {
        _backgroundMode = backgroundMode;
        _lyricsService = new LyricsService(() => _settings.UseGenius);

        InitializeComponent();
        BuildStyleMenu();
        SizeChanged += (_, _) => OnSizeChanged();

        ApplyTheme(ThemeSpec.For(_settings.Theme));
        PlaceWindow();
    }

    public async void Start()
    {
        _media.TrackChanged += (track, cover) => Dispatcher.InvokeAsync(() => OnTrackChanged(track, cover));
        _lineTimer.Tick += (_, _) => OnTick();
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

    // ---------------------------------------------------------------- Spotify visibility

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

    // ---------------------------------------------------------------- Track and lyrics

    private async void OnTrackChanged(TrackInfo? track, byte[]? cover)
    {
        _lookupCts?.Cancel();
        _lookupCts = new CancellationTokenSource();
        var ct = _lookupCts.Token;

        _track = track;
        _cover = cover;
        _lyrics = null;
        _lineIndex = int.MinValue;
        _offset = track is not null && _settings.TrackOffsets.TryGetValue(track.Key, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
        UpdateOffsetText();
        UpdateCover();
        SourceText.Text = "";

        if (track is null)
        {
            SetTrackText("");
            ShowStatus("Waiting for Spotify…");
            return;
        }

        SetTrackText(string.IsNullOrEmpty(track.Artist) ? track.Title : $"{track.Title} — {track.Artist}");
        Layout.FlowDirection = IsRtl(track.Title) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
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
                Layout.FlowDirection = MostlyRtl(result.Lines) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                SourceText.Text = result.IsSynced ? result.Source : $"{result.Source} · unsynced, timing approximate";
                OnTick();
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

    private void OnTick()
    {
        var playback = _media.GetPlayback();
        UpdateProgressBar(playback);

        var lyrics = _lyrics;
        if (playback is null || lyrics is null || lyrics.IsInstrumental || lyrics.Lines.Count == 0)
            return;

        var lines = lyrics.Lines;
        var position = playback.Position + _offset;
        var index = FindLineIndex(lines, position);

        if (index != _lineIndex)
        {
            _lineIndex = index;
            _showingStatus = false;
            SetLine(PrevLine, index - 1 >= 0 ? DisplayText(lines[index - 1]) : "");
            SetLine(CurrentLine, index >= 0 ? DisplayText(lines[index]) : "♪");
            SetLine(NextLine, index + 1 < lines.Count ? DisplayText(lines[index + 1]) : "");
            CurrentLine.Foreground = _theme.KaraokeFill ? _karaokeBrush : Brushes.White;
            CurrentLine.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(180)));
        }

        if (_theme.KaraokeFill)
            SetKaraokeProgress(LineProgress(lines, index, position));
    }

    /// <summary>How far through the current line we are (0..1), measured until the next line starts.</summary>
    private static double LineProgress(IReadOnlyList<LyricLine> lines, int index, TimeSpan position)
    {
        if (index < 0)
            return 0;
        var start = lines[index].Time;
        var end = index + 1 < lines.Count ? lines[index + 1].Time : start + TimeSpan.FromSeconds(5);
        var length = (end - start).TotalSeconds;
        return length <= 0 ? 1 : Math.Clamp((position - start).TotalSeconds / length, 0, 1);
    }

    private void SetKaraokeProgress(double progress)
    {
        _karaokeBrush.GradientStops[1].Offset = progress;
        _karaokeBrush.GradientStops[2].Offset = progress;
    }

    private void UpdateProgressBar(PlaybackSnapshot? playback)
    {
        if (!_theme.ShowProgress)
            return;
        var fraction = playback is { Duration.TotalSeconds: > 0 }
            ? Math.Clamp(playback.Position / playback.Duration, 0, 1)
            : 0;
        ProgressFill.Width = ProgressTrack.ActualWidth * fraction;
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
        _showingStatus = true;
        SetLine(PrevLine, "");
        SetLine(CurrentLine, message);
        SetLine(NextLine, "");
        CurrentLine.Foreground = Brushes.White;
    }

    // ---------------------------------------------------------------- Right-to-left text

    /// <summary>
    /// Sets a line's text and aligns Persian/Arabic lines to the right. Lines without letters
    /// (like "♪") follow the song's overall direction instead.
    /// </summary>
    private static void SetLine(TextBlock block, string text)
    {
        block.Text = text;
        switch (DirectionOf(text))
        {
            case true: block.FlowDirection = FlowDirection.RightToLeft; break;
            case false: block.FlowDirection = FlowDirection.LeftToRight; break;
            default: block.ClearValue(FlowDirectionProperty); break;
        }
    }

    private void SetTrackText(string text)
    {
        TrackText.Text = _theme.UppercaseHeader && !IsRtl(text) ? text.ToUpperInvariant() : text;
        TrackText.Tag = text; // the original, so switching styles can restore the casing
        // The whole row follows the title, so the cover stays on the side the title starts from.
        HeaderRow.FlowDirection = IsRtl(text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    private static bool IsRtl(string text) => DirectionOf(text) == true;

    /// <summary>True for right-to-left, false for left-to-right, null when the text has no letters.</summary>
    private static bool? DirectionOf(string text)
    {
        foreach (var ch in text)
        {
            if (ch is >= '؀' and <= 'ۿ' or >= 'ݐ' and <= 'ݿ'
                or >= 'ﭐ' and <= '﷿' or >= 'ﹰ' and <= '﻿')
                return true;
            if (char.IsLetter(ch))
                return false; // the first letter decides
        }
        return null;
    }

    private static bool MostlyRtl(IReadOnlyList<LyricLine> lines) =>
        lines.Count(l => IsRtl(l.Text)) * 2 > lines.Count(l => !string.IsNullOrWhiteSpace(l.Text));

    // ---------------------------------------------------------------- Styles

    private void BuildStyleMenu()
    {
        foreach (var spec in ThemeSpec.All)
        {
            var item = new MenuItem { Header = spec.DisplayName, Tag = spec.Theme, IsCheckable = true };
            item.Click += (_, _) =>
            {
                _settings.Theme = spec.Theme;
                _settings.Save();
                ApplyTheme(spec);
            };
            StyleMenu.Items.Add(item);
        }
    }

    private void ApplyTheme(ThemeSpec spec)
    {
        _theme = spec;

        Width = spec.Width;
        FontFamily = spec.UseAppFont ? AppFont : ClassicFont;

        Root.Background = spec.Background;
        Root.BorderBrush = spec.BorderBrush;
        Root.BorderThickness = new Thickness(spec.BorderThickness);
        Root.CornerRadius = new CornerRadius(spec.CornerRadius);
        Root.Padding = spec.Padding;
        Layout.Effect = spec.TextShadow
            ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 1, Opacity = 0.85 }
            : null;

        HeaderRow.Visibility = Show(spec.ShowHeader);
        Cover.Visibility = Show(spec.ShowCover);
        TrackText.FontSize = spec.HeaderSize;
        TrackText.Foreground = spec.HeaderBrush;
        TrackText.FontWeight = spec.HeaderWeight;
        SetTrackText(TrackText.Tag as string ?? "");

        PrevLine.Visibility = Show(spec.ShowPrevious);
        PrevLine.Margin = new Thickness(0, spec.PreviousTopMargin, 0, 0);
        foreach (var context in new[] { PrevLine, NextLine })
        {
            context.FontSize = spec.ContextSize;
            context.Foreground = spec.ContextBrush;
            context.TextWrapping = spec.SingleLine ? TextWrapping.NoWrap : TextWrapping.Wrap;
            context.TextTrimming = spec.SingleLine ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        }

        CurrentLine.FontSize = spec.CurrentSize;
        CurrentLine.FontWeight = spec.CurrentWeight;
        CurrentLine.Margin = spec.SingleLine ? new Thickness(0) : new Thickness(0, 4, 0, 4);
        CurrentLine.TextWrapping = spec.SingleLine ? TextWrapping.NoWrap : TextWrapping.Wrap;
        CurrentLine.TextTrimming = spec.SingleLine ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        // Karaoke: size the line to its text so the fill sweeps across the words, not the empty space.
        CurrentLine.HorizontalAlignment = spec.KaraokeFill ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        CurrentLine.Foreground = spec.KaraokeFill && !_showingStatus ? _karaokeBrush : Brushes.White;

        Dot.Visibility = Show(spec.ShowDot);
        Dot.Fill = new SolidColorBrush(ThemeSpec.KaraokeColor);

        ProgressTrack.Visibility = Show(spec.ShowProgress);
        ProgressTrack.Height = spec.ProgressHeight;
        ProgressTrack.CornerRadius = new CornerRadius(spec.ProgressHeight / 2);
        ProgressTrack.Background = spec.ProgressTrack;
        ProgressTrack.Margin = new Thickness(0, spec.SingleLine ? 7 : 12, 0, 0);
        ProgressFill.CornerRadius = ProgressTrack.CornerRadius;
        ProgressFill.Background = spec.ProgressFill;

        FooterRow.Visibility = Show(spec.ShowFooter);

        // The karaoke fill needs frequent updates to look smooth; the other looks don't.
        _lineTimer.Interval = TimeSpan.FromMilliseconds(spec.KaraokeFill ? 50 : 200);

        ApplyBlur();
        UpdateCover();
        _lineIndex = int.MinValue; // redraw with the new look
        OnTick();
    }

    private void ApplyBlur()
    {
        new WindowInteropHelper(this).EnsureHandle();
        if (_theme.BlurBehind)
        {
            if (!WindowEffects.SetBlur(this, true))
                Root.Background = SolidFrostedFallback;
            WindowEffects.SetRoundedClip(this, _theme.CornerRadius);
        }
        else
        {
            WindowEffects.SetBlur(this, false);
            WindowEffects.SetRoundedClip(this, 0);
        }
    }

    private void UpdateCover()
    {
        if (!_theme.ShowCover)
            return;

        Cover.Background = DefaultCover;
        if (_cover is null)
            return;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = new MemoryStream(_cover);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 96;
            image.EndInit();
            image.Freeze();
            Cover.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        }
        catch
        {
            // unreadable image; keep the placeholder
        }
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    // ---------------------------------------------------------------- Timing offset

    private void NudgeOffset(double seconds) => SetOffset(_offset + TimeSpan.FromSeconds(seconds));

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
        OnTick();
    }

    private void UpdateOffsetText() =>
        OffsetText.Text = _offset == TimeSpan.Zero ? "timing" : $"{_offset.TotalSeconds:+0.0;-0.0}s";

    // ---------------------------------------------------------------- Position

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

    private void OnSizeChanged()
    {
        if (_positioned)
            Top = _bottom - ActualHeight;
        if (_theme.BlurBehind)
            WindowEffects.SetRoundedClip(this, _theme.CornerRadius);
    }

    // ---------------------------------------------------------------- Mouse and menu

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

    private void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        foreach (MenuItem item in StyleMenu.Items)
            item.IsChecked = (WidgetTheme)item.Tag == _theme.Theme;
        GeniusItem.IsChecked = _settings.UseGenius;
        AutostartItem.IsChecked = Autostart.IsEnabled;
    }

    private void OnToggleGenius(object sender, RoutedEventArgs e)
    {
        _settings.UseGenius = GeniusItem.IsChecked;
        _settings.Save();
        _lyricsService.ForgetMisses();

        // If the current song had nothing, try again with the new set of sources.
        if (_track is not null && _lyrics is null)
            OnTrackChanged(_track, _cover);
    }

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
