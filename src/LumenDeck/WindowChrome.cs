using System.Runtime.InteropServices;

namespace LumenDeck;

/// <summary>
/// Native frame and system scrollbars/combo boxes follow the current palette.
/// Passing null to SetWindowTheme removes the previous dark association.
/// Every call is cosmetic: on a Windows version without
/// an attribute the call fails and the default appearance remains.
/// </summary>
internal static class WindowChrome
{
    private sealed class FramePreference { internal bool Mica; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, FramePreference> Frames = new();
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // Windows 10 1809-1909
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;                // Windows 11
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_NONE = 1;
    private const int DWMSBT_MAINWINDOW = 2;
    private const int DWMWA_COLOR_DEFAULT = -1;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

    /// <summary>Call from OnHandleCreated; the frame belongs to that handle.</summary>
    public static void Apply(Form form, bool micaTitleBar = false)
    {
        if (form == null || !form.IsHandleCreated) return;
        var preference = Frames.GetValue(form, owner =>
        {
            var value = new FramePreference();
            Theme.Observe(owner, () => Apply(owner, value.Mica));
            return value;
        });
        preference.Mica = micaTitleBar;
        IntPtr hwnd = form.Handle;
        try
        {
            int on = Theme.IsDark ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));
            int caption = ColorRef(Theme.Bar), text = ColorRef(Theme.Ink), border = ColorRef(Theme.Line);
            int round = 2; // DWMWCP_ROUND; unsupported systems keep their frame.
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            if (Environment.OSVersion.Version.Build >= 22621)
            {
                int material = micaTitleBar && SupportsMica(Environment.OSVersion.Version, Theme.HighContrast)
                    ? DWMSBT_MAINWINDOW : DWMSBT_NONE;
                if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref material, sizeof(int)) == 0 &&
                    material == DWMSBT_MAINWINDOW)
                    caption = text = DWMWA_COLOR_DEFAULT;
            }
            if (Theme.HighContrast) caption = text = border = DWMWA_COLOR_DEFAULT;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        DarkenSystemParts(form);
    }

    // No frame extension or alpha tricks: WinForms content remains opaque.
    internal static bool SupportsMica(Version version, bool highContrast) =>
        version.Major >= 10 && version.Build >= 22621 && !highContrast;

    /// <summary>DWM's configured intent, not proof of the material's visible appearance.</summary>
    internal static int? BackdropType(Form form)
    {
        if (!form.IsHandleCreated) return null;
        try
        {
            return DwmGetWindowAttribute(form.Handle, DWMWA_SYSTEMBACKDROP_TYPE, out int value, sizeof(int)) == 0
                ? value : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return null; }
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
        if (control is Button button) Theme.StyleButton(button);
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
        try { SetWindowTheme(control.Handle, Theme.IsDark ? theme : null, null); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    private static int ColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
}
