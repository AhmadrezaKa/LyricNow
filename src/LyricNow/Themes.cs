using System.Windows;
using System.Windows.Media;

namespace LyricNow;

public enum WidgetTheme
{
    Classic,
    Frosted,
    Floating,
    Karaoke,
    Strip,
}

/// <summary>Everything that differs between the widget's looks. Applied by MainWindow.ApplyTheme.</summary>
public sealed record ThemeSpec
{
    public required WidgetTheme Theme { get; init; }
    public required string DisplayName { get; init; }

    public double Width { get; init; } = 420;
    public required Brush Background { get; init; }
    public Brush BorderBrush { get; init; } = Brushes.Transparent;
    public double BorderThickness { get; init; }
    public double CornerRadius { get; init; } = 12;
    public Thickness Padding { get; init; } = new(16, 12, 16, 10);
    public bool BlurBehind { get; init; }
    public bool TextShadow { get; init; }

    public bool UseAppFont { get; init; } = true;

    public bool ShowHeader { get; init; } = true;
    public bool ShowCover { get; init; }
    public bool ShowPrevious { get; init; } = true;
    public bool ShowDot { get; init; }
    public bool ShowProgress { get; init; }
    public bool ShowFooter { get; init; } = true;
    public bool SingleLine { get; init; }
    public bool UppercaseHeader { get; init; }
    public bool KaraokeFill { get; init; }

    public double HeaderSize { get; init; } = 11;
    public Brush HeaderBrush { get; init; } = Frozen("#99FFFFFF");
    public FontWeight HeaderWeight { get; init; } = FontWeights.Normal;
    public double ContextSize { get; init; } = 13;
    public Brush ContextBrush { get; init; } = Frozen("#88FFFFFF");
    public double CurrentSize { get; init; } = 19;
    public FontWeight CurrentWeight { get; init; } = FontWeights.SemiBold;
    public double PreviousTopMargin { get; init; } = 10;

    public double ProgressHeight { get; init; } = 3;
    public Brush ProgressTrack { get; init; } = Frozen("#26FFFFFF");
    public Brush ProgressFill { get; init; } = Frozen("#BFFFFFFF");

    public static readonly Color KaraokeColor = (Color)ColorConverter.ConvertFromString("#F2B26B");
    public static readonly Color KaraokeDim = (Color)ColorConverter.ConvertFromString("#66FFFFFF");

    public static readonly IReadOnlyList<ThemeSpec> All =
    [
        new()
        {
            Theme = WidgetTheme.Classic,
            DisplayName = "Classic",
            Background = Frozen("#D9121418"),
            UseAppFont = false,
        },
        new()
        {
            Theme = WidgetTheme.Frosted,
            DisplayName = "Frosted",
            // Light tint over the blur; if the blur isn't available, ApplyTheme uses a solid version.
            Background = Frozen("#701C1F2A"),
            BorderBrush = Frozen("#24FFFFFF"),
            BorderThickness = 1,
            CornerRadius = 16,
            Padding = new(16, 14, 16, 12),
            BlurBehind = true,
            ShowCover = true,
            ShowProgress = true,
            HeaderSize = 12,
            HeaderBrush = Frozen("#C0FFFFFF"),
            CurrentSize = 22,
            PreviousTopMargin = 12,
        },
        new()
        {
            Theme = WidgetTheme.Floating,
            DisplayName = "Floating",
            // Almost invisible, but not fully transparent, so the whole area can still be dragged.
            Background = Frozen("#01000000"),
            CornerRadius = 0,
            Padding = new(6, 4, 6, 4),
            TextShadow = true,
            ShowFooter = false,
            UppercaseHeader = true,
            HeaderBrush = Frozen("#B3FFFFFF"),
            ContextSize = 14,
            ContextBrush = Frozen("#A0FFFFFF"),
            CurrentSize = 26,
            CurrentWeight = FontWeights.Bold,
            PreviousTopMargin = 8,
        },
        new()
        {
            Theme = WidgetTheme.Karaoke,
            DisplayName = "Karaoke",
            Background = Frozen("#F20F1117"),
            BorderBrush = Frozen("#10FFFFFF"),
            BorderThickness = 1,
            CornerRadius = 14,
            Padding = new(18, 14, 18, 12),
            KaraokeFill = true,
            HeaderBrush = new SolidColorBrush(KaraokeColor).Frozen(),
            HeaderWeight = FontWeights.SemiBold,
            CurrentSize = 21,
            CurrentWeight = FontWeights.Bold,
        },
        new()
        {
            Theme = WidgetTheme.Strip,
            DisplayName = "Strip",
            Width = 440,
            Background = Frozen("#E6101218"),
            CornerRadius = 30,
            Padding = new(20, 9, 20, 9),
            ShowHeader = false,
            ShowPrevious = false,
            ShowDot = true,
            ShowProgress = true,
            ShowFooter = false,
            SingleLine = true,
            ContextSize = 12,
            ContextBrush = Frozen("#80FFFFFF"),
            CurrentSize = 16,
            ProgressHeight = 2,
            ProgressTrack = Frozen("#1FFFFFFF"),
            ProgressFill = new SolidColorBrush(KaraokeColor).Frozen(),
        },
    ];

    public static ThemeSpec For(WidgetTheme theme) => All.FirstOrDefault(t => t.Theme == theme) ?? All[0];

    private static Brush Frozen(string color) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)).Frozen();
}

internal static class BrushExtensions
{
    public static T Frozen<T>(this T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
