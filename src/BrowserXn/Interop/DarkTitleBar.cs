using System.Windows;
using System.Windows.Interop;

namespace GDD.Interop;

internal static class DarkTitleBar
{
    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == nint.Zero) return;

        // #090B1A → COLORREF (BGR)
        var color = 0x09 | (0x0B << 8) | (0x1A << 16);
        DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_CAPTION_COLOR, ref color, sizeof(int));

        var darkMode = 1;
        DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
    }
}
