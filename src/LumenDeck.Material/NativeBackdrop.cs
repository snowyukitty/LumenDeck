using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace LumenDeck.Material;

/// <summary>Documented DWM backdrop request; success is not a visual acceptance result.</summary>
internal static class NativeBackdrop
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    internal sealed record Result(bool Requested, int? Readback, string Description);
    internal static bool TransparencyAllowed()
    {
        if (SystemParameters.HighContrast) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int enabled || enabled != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        { return false; }
    }

    internal static Result Apply(Window window, bool dark, int kind, bool forceOpaque)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return new(false, null, "Waiting for native window");
        int mode = dark && !SystemParameters.HighContrast ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, 20, ref mode, sizeof(int));
        bool supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        bool allowed = supported && !forceOpaque && TransparencyAllowed() &&
            DwmIsCompositionEnabled(out bool composed) == 0 && composed;
        int desired = allowed ? kind : 1;
        int set = supported ? DwmSetWindowAttribute(handle, 38, ref desired, sizeof(int)) : -1;
        int? readback = supported && DwmGetWindowAttribute(handle, 38, out int actual, sizeof(int)) == 0 ? actual : null;
        bool requested = allowed && kind != 1 && set == 0 && readback == kind;
        var margins = new Margins
        {
            Left = requested ? -1 : 0, Right = requested ? -1 : 0,
            Top = requested ? -1 : 0, Bottom = requested ? -1 : 0,
        };
        if (DwmExtendFrameIntoClientArea(handle, ref margins) != 0)
        {
            requested = false;
            int none = 1;
            if (supported) _ = DwmSetWindowAttribute(handle, 38, ref none, sizeof(int));
        }
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
            target.BackgroundColor = requested ? Colors.Transparent : (dark ? Color.FromRgb(25, 29, 35) : Color.FromRgb(241, 240, 237));
        string material = kind == 3 ? "Acrylic" : "Mica";
        return new(requested, readback, requested
            ? $"{material} requested from Windows · appearance follows system policy"
            : "Opaque surface · system preference, selected finish or compatibility fallback");
    }
}
