using Microsoft.Win32;

namespace LyricNow;

/// <summary>
/// Registers LyricNow to start (hidden) when the user logs in. It then stays invisible
/// until Spotify is running, so in practice it appears alongside Spotify.
/// </summary>
public static class Autostart
{
    public const string BackgroundArg = "--background";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LyricNow";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {BackgroundArg}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
