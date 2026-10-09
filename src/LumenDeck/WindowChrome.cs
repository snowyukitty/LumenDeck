using System.Runtime.InteropServices;

namespace LumenDeck;

/// <summary>
/// Native dark window frame and dark system scrollbars/combo boxes, so the
/// operating system's own parts match the app instead of framing a dark window
/// in a light title bar. Every call is cosmetic: on a Windows version without
/// an attribute the call fails and the default appearance remains.
/// </summary>
internal static class WindowChrome
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // Windows 10 1809-1909
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;                // Windows 11
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

    /// <summary>Call from OnHandleCreated; the frame belongs to that handle.</summary>
    public static void Apply(Form form)
    {
        if (form == null || !form.IsHandleCreated) return;
        IntPtr hwnd = form.Handle;
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));
            int caption = ColorRef(Theme.Bar), text = ColorRef(Theme.Ink), border = ColorRef(Theme.Line);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        DarkenSystemParts(form);
    }

    /// <summary>True when Windows reports the dark frame for this window.</summary>
    internal static bool HasDarkFrame(Form form)
    {
        try
        {
            return form.IsHandleCreated &&
                DwmGetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, out int value, sizeof(int)) == 0 && value != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }

    /// <summary>
    /// Theme scrollable areas, multi-line text and combo boxes, now and for
    /// children added later. Child handles are created after the form's, so
    /// each part is themed when its own handle appears.
    /// </summary>
    private static void DarkenSystemParts(Control root)
    {
        foreach (Control child in root.Controls) Darken(child);
        root.ControlAdded -= OnControlAdded;
        root.ControlAdded += OnControlAdded;
    }

    private static void OnControlAdded(object sender, ControlEventArgs e) => Darken(e.Control);

    private static void Darken(Control control)
    {
        string theme = control switch
        {
            ComboBox => "DarkMode_CFD",
            TextBoxBase { Multiline: true } or ScrollableControl { AutoScroll: true } => "DarkMode_Explorer",
            _ => null,
        };
        if (theme != null)
        {
            control.HandleCreated -= theme == "DarkMode_CFD" ? OnComboHandle : OnScrollHandle;
            control.HandleCreated += theme == "DarkMode_CFD" ? OnComboHandle : OnScrollHandle;
            if (control.IsHandleCreated) SetTheme(control, theme);
        }
        // Cards and their contents arrive after the window exists.
        if (control is ContainerControl or Panel or FlowLayoutPanel or TableLayoutPanel or UserControl)
            DarkenSystemParts(control);
    }

    private static void OnComboHandle(object sender, EventArgs e) => SetTheme((Control)sender, "DarkMode_CFD");
    private static void OnScrollHandle(object sender, EventArgs e) => SetTheme((Control)sender, "DarkMode_Explorer");

    private static void SetTheme(Control control, string theme)
    {
        try { SetWindowTheme(control.Handle, theme, null); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    private static int ColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
}
