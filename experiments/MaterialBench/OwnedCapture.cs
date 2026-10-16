using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LumenDeck.MaterialBench;

internal static class OwnedCapture
{
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    public static bool CompositionEnabled() => DwmIsCompositionEnabled(out bool enabled) == 0 && enabled;
    public static bool IsForeground(Window window) => GetForegroundWindow() == new WindowInteropHelper(window).Handle;
    public static string ForegroundState(Window window)
    {
        IntPtr foreground = GetForegroundWindow();
        return foreground == IntPtr.Zero ? "unavailable" : foreground == new WindowInteropHelper(window).Handle ? "target" : "other";
    }
    public static int? Backdrop(IntPtr window) => DwmGetWindowAttribute(window, 38, out int value, 4) == 0 ? value : null;
    public static Rectangle Capture(Window window, FrameworkElement root, string destination, bool requireActive)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        var start = root.PointToScreen(new System.Windows.Point(0, 0));
        var end = root.PointToScreen(new System.Windows.Point(root.ActualWidth, root.ActualHeight));
        var bounds = new Rectangle((int)Math.Ceiling(start.X), (int)Math.Ceiling(start.Y),
            (int)Math.Floor(end.X - start.X), (int)Math.Floor(end.Y - start.Y));
        if (bounds.Width < 500 || bounds.Height < 400 || bounds.Width > 4000 || bounds.Height > 4000)
            throw new InvalidOperationException("Unexpected owned client bounds.");
        void Guard()
        {
            if (!window.IsVisible || requireActive && GetForegroundWindow() != handle)
                throw new InvalidOperationException("The material scene is not the foreground window; capture refused.");
            // Refuse before asking for any screen pixels if another native window overlaps the scene.
            for (int y = 2; y < bounds.Height; y += 24)
                for (int x = 2; x < bounds.Width; x += 24)
                    if (GetAncestor(WindowFromPoint(new NativePoint { X = bounds.Left + x, Y = bounds.Top + y }), 2) != handle)
                        throw new InvalidOperationException("The controlled scene is occluded; capture refused.");
        }
        DwmFlush(); Guard();
        using var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(image))
            graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        Guard(); image.Save(destination, ImageFormat.Png);
        return bounds;
    }
}
