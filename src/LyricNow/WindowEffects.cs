using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LyricNow;

/// <summary>
/// Blurs whatever is behind the window (for the Frosted look), clipped to a rounded rectangle.
/// Uses SetWindowCompositionAttribute, which Windows 10 and 11 support but don't document.
/// </summary>
internal static class WindowEffects
{
    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;
    private const int AccentEnableBlurBehind = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr hRgn, bool redraw);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    /// <summary>Turns the blur on or off. Returns false if Windows refused.</summary>
    public static bool SetBlur(Window window, bool enabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        var accent = new AccentPolicy { AccentState = enabled ? AccentEnableBlurBehind : AccentDisabled };
        var size = Marshal.SizeOf(accent);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData { Attribute = WcaAccentPolicy, Data = ptr, SizeOfData = size };
            return SetWindowCompositionAttribute(hwnd, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// Clips the window to a rounded rectangle so the blur doesn't show square corners.
    /// Pass a radius of 0 to remove the clip.
    /// </summary>
    public static void SetRoundedClip(Window window, double cornerRadius)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        if (cornerRadius <= 0)
        {
            SetWindowRgn(hwnd, IntPtr.Zero, true);
            return;
        }

        // Regions are in physical pixels, WPF sizes are in 96-dpi units.
        var toDevice = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice;
        var scaleX = toDevice?.M11 ?? 1;
        var scaleY = toDevice?.M22 ?? 1;

        var width = (int)Math.Ceiling(window.ActualWidth * scaleX);
        var height = (int)Math.Ceiling(window.ActualHeight * scaleY);
        var diameter = (int)Math.Round(cornerRadius * 2 * scaleX);

        // The window takes ownership of the region, so it isn't deleted here.
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
        SetWindowRgn(hwnd, region, true);
    }
}
