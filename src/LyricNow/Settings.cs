using System.Text.Json;

namespace LyricNow;

/// <summary>Remembers widget position and per-song timing nudges, in %LOCALAPPDATA%\LyricNow\settings.json.</summary>
public sealed class Settings
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyricNow", "settings.json");

    public double? Left { get; set; }

    /// <summary>The widget grows upwards as lines wrap, so we store its bottom edge rather than its top.</summary>
    public double? Bottom { get; set; }

    public WidgetTheme Theme { get; set; } = WidgetTheme.Classic;

    /// <summary>Also search Genius (plain lyrics, wide coverage including Persian songs).</summary>
    public bool UseGenius { get; set; } = true;

    /// <summary>Timing offset in seconds per track key; positive shows lyrics sooner.</summary>
    public Dictionary<string, double> TrackOffsets { get; set; } = [];

    public static Settings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings()
                : new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch
        {
            // Not worth crashing over.
        }
    }
}
