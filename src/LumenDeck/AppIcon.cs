using System.Reflection;

namespace LumenDeck;

/// <summary>
/// Loads the app icon from the embedded .ico.
///
/// It used to be drawn at runtime with GDI+ - a sun with rays. That was a
/// placeholder in two ways: it kept the project asset-free, and a sun is the
/// single most generic brightness glyph there is, indistinguishable from every
/// OS brightness control at 16px. The shipped mark is a real designed icon with
/// proper hinted frames at 16/24/32/48/64/256, which a runtime drawing cannot
/// match: small sizes need hand-tuned geometry, not a scaled-down circle.
/// </summary>
internal static class AppIcon
{
    private const string ResourceName = "LumenDeck.assets.LumenDeck.ico";

    /// <summary>
    /// The window icon, for the taskbar and Alt-Tab.
    ///
    /// The size argument is not optional. `new Icon(stream)` is documented to
    /// return the **smallest** image in the file, which here is the 16px frame -
    /// and WinForms passes whatever it gets straight to WM_SETICON as ICON_BIG,
    /// so the taskbar would upscale a 16px bitmap while a hinted 32/48/256 frame
    /// sat unused in the same file. Asking for the large icon size selects the
    /// right frame.
    /// </summary>
    public static Icon Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream != null) return new Icon(stream, SystemInformation.IconSize);
        }
        catch
        {
            // Fall through: a missing resource must not stop the app starting.
        }
        return Fallback();
    }

    /// <summary>
    /// The tray needs the small frame specifically. Asking for 16x16 makes
    /// Windows select the hinted frame instead of downscaling the 256 one,
    /// which is the difference between a crisp tray icon and a smudge.
    /// </summary>
    public static Icon LoadTray()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream != null) return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch
        {
        }
        return Fallback();
    }

    /// <summary>
    /// A disposable stand-in.
    ///
    /// Returning SystemIcons.Application directly would be a trap: it is a
    /// process-wide shared static, and every caller here owns and disposes what
    /// it is given. Disposing the shared instance corrupts it for the rest of
    /// the process - including WinForms itself - and the symptom appears far
    /// from the cause. Hand back a clone instead.
    /// </summary>
    private static Icon Fallback() => (Icon)SystemIcons.Application.Clone();
}
