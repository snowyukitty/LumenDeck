namespace LumenDeck;

/// <summary>Semantic palettes and live, disposal-bound colour subscriptions.</summary>
internal static class Theme
{
    internal static Func<bool> ContrastMode { get; set; } = () => SystemInformation.HighContrast;
    internal static bool HighContrast => ContrastMode();
    internal sealed record Palette(string Id, string Name, bool Dark,
        int Base, int Bar, int Card, int CardTop, int CardHover, int Sunken, int Line,
        int Ink, int InkMuted, int InkFaint, int AmberLight, int Amber, int AmberDim,
        int AmberPressed, int OnAmber, int Info, int Warn, int Danger);
    internal static readonly IReadOnlyList<Palette> Palettes = Array.AsReadOnly(new[]
    {
        new Palette("amber", "Amber · warm dark", true,
            0x13151A, 0x191C22, 0x1E2229, 0x282D36, 0x303640, 0x14171D, 0x3E4653,
            0xEEF0F5, 0xA9B2C1, 0x919CAC, 0xFFD68F, 0xE7A95D, 0x825B2D, 0xCC914A,
            0x1C1408, 0x7EA8E8, 0xECC458, 0xE26C5C),
        new Palette("paper", "Paper · soft light", false,
            0xF3F5F8, 0xE9EDF2, 0xFFFFFF, 0xF8FAFC, 0xE0E6EE, 0xEEF1F5, 0x778496,
            0x192331, 0x404E61, 0x526176, 0x784300, 0x8A5000, 0xBE955E, 0x6F3E00,
            0xFFFFFF, 0x265CA0, 0x815400, 0xAD3429),
        new Palette("ocean", "Ocean · cool dark", true,
            0x0C1823, 0x101F2B, 0x142532, 0x1B2E3C, 0x213847, 0x0B1A25, 0x405968,
            0xEDF5F8, 0xADC1CE, 0x9BAEBB, 0x86DEEC, 0x48BCD3, 0x356B7A, 0x36A5BD,
            0x041922, 0x8CB9F9, 0xEFCC77, 0xFA9587),
        new Palette("violet", "Violet · quiet dark", true,
            0x181520, 0x201C2A, 0x252031, 0x2E283C, 0x373046, 0x191420, 0x594C6A,
            0xF4EFFA, 0xBCB0CC, 0xAA9DBD, 0xDBC1FF, 0xB997EB, 0x77608F, 0xA37CDA,
            0x180E26, 0x97B9F3, 0xEBC77E, 0xF796A0),
    });
    private static Palette _palette = Palettes[0];
    internal static string Id => _palette.Id;
    internal static bool IsDark => _palette.Dark && !HighContrast;
    internal static int Revision { get; private set; }
    private static event Action Changed;
    internal static string Normalize(string id) => Palettes.FirstOrDefault(p => p.Id == id)?.Id ?? "amber";
    internal static void Select(string id)
    {
        var palette = Palettes.First(p => p.Id == Normalize(id));
        if (_palette == palette) return;
        _palette = palette;
        Refresh();
    }
    internal static void Refresh()
    {
        Revision++;
        Changed?.Invoke();
    }
    // All subscriptions are UI-thread-only and end with the owning component.
    internal static void Observe(System.ComponentModel.Component owner, Action refresh)
    {
        Changed += refresh;
        owner.Disposed += (_, _) => Changed -= refresh;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.ComponentModel.Component, ColorBinding> Bindings = new();
    internal static T Colors<T>(T owner, Func<Color> back = null, Func<Color> fore = null,
        Func<Color> link = null, Func<Color> activeLink = null, Func<Color> visitedLink = null)
        where T : System.ComponentModel.Component
    {
        var binding = Bindings.GetValue(owner, component =>
        {
            var created = new ColorBinding(component);
            Observe(component, created.Apply);
            return created;
        });
        if (back != null) binding.Back = back;
        if (fore != null) binding.Fore = fore;
        if (link != null) binding.Link = link;
        if (activeLink != null) binding.ActiveLink = activeLink;
        if (visitedLink != null) binding.VisitedLink = visitedLink;
        binding.Apply();
        return owner;
    }
    internal static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        Colors(button, back: () => Card, fore: () => Ink);

    }
    private sealed class ColorBinding(System.ComponentModel.Component owner)
    {
        internal Func<Color> Back, Fore, Link, ActiveLink, VisitedLink;
        internal void Apply()
        {
            if (owner is Control control && !control.IsDisposed)
            {
                if (Back != null) control.BackColor = Back();
                if (Fore != null) control.ForeColor = Fore();
                if (control is Button { FlatStyle: FlatStyle.Flat } button) button.FlatAppearance.BorderColor = Line;
                if (control is LinkLabel label)
                {
                    if (Link != null) label.LinkColor = Link();
                    if (ActiveLink != null) label.ActiveLinkColor = ActiveLink();
                    if (VisitedLink != null) label.VisitedLinkColor = VisitedLink();
                }
                control.Invalidate();
            }
            else if (owner is ToolStripItem item && !item.IsDisposed)
            {
                if (Back != null) item.BackColor = Back();
                if (Fore != null) item.ForeColor = Fore();
                item.Invalidate();
            }
        }
    }
    private static Color Token(int rgb, bool surface = false) => HighContrast
        ? (surface ? SystemColors.Window : SystemColors.WindowText)
        : Color.FromArgb(unchecked((int)0xFF000000) | rgb);
    public static Color Base => Token(_palette.Base, true);
    public static Color Bar => Token(_palette.Bar, true);
    public static Color Card => Token(_palette.Card, true);
    public static Color CardTop => Token(_palette.CardTop, true);
    public static Color CardHover => Token(_palette.CardHover, true);
    public static Color Sunken => Token(_palette.Sunken, true);
    public static Color Line => Token(_palette.Line);
    public static Color SurfaceLight => HighContrast ? SystemColors.Window : Color.FromArgb(IsDark ? 16 : 70, 255, 255, 255);
    public static Color Shadow => HighContrast ? SystemColors.Window : Color.FromArgb(IsDark ? 100 : 22, 0, 0, 0);
    public static Color Ink => Token(_palette.Ink);
    public static Color InkMuted => Token(_palette.InkMuted);
    public static Color InkFaint => Token(_palette.InkFaint);
    public static Color AmberLight => Token(_palette.AmberLight);
    public static Color Amber => Token(_palette.Amber);
    public static Color AmberDim => Token(_palette.AmberDim);
    public static Color AmberPressed => Token(_palette.AmberPressed);
    public static Color OnAmber => Token(_palette.OnAmber, true);
    public static Color Info => Token(_palette.Info);
    public static Color Warn => Token(_palette.Warn);
    public static Color Danger => Token(_palette.Danger);
    public static Color ChipFill(Color ink) => HighContrast ? SystemColors.Window : Color.FromArgb(24, ink);

    // Type. Created once and shared: a Font handed to a Control is not owned by
    // it, so per-control fonts leak until their finalizers run.
    public static readonly Font H1 = new("Segoe UI Semibold", 11.5f);
    public static readonly Font Brand = new("Segoe UI Semibold", 15f);
    public static readonly Font H2 = new("Segoe UI Semibold", 10.5f);
    public static readonly Font Body = new("Segoe UI", 9f);
    public static readonly Font Small = new("Segoe UI", 8.25f);
    public static readonly Font Value = new("Segoe UI Semibold", 9.5f);
    public static readonly Font Mono = new("Consolas", 9.5f);

    public const int Radius = 12;
    public const int CardPad = 16;
    public const int Gap = 12;

    /// <summary>Opaque material shading; this does not sample or blur the desktop.</summary>
    public static void FillSurface(Graphics g, Rectangle bounds, int radius, Color top, Color bottom)
    {
        if (bounds.Width < 1 || bounds.Height < 1) return;
        using var shape = Round(bounds, radius);
        using var surface = new System.Drawing.Drawing2D.LinearGradientBrush(
            bounds, top, bottom, System.Drawing.Drawing2D.LinearGradientMode.Vertical);
        g.FillPath(surface, shape);
    }

    /// <summary>A fading inner rim makes raised surfaces legible without a heavy outline.</summary>
    public static void SurfaceRim(Graphics g, Rectangle bounds, int radius)
    {
        bounds.Inflate(-1, -1);
        if (bounds.Width < 2 || bounds.Height < 2) return;
        using var shape = Round(bounds, Math.Max(1, radius - 1));
        using var fade = new System.Drawing.Drawing2D.LinearGradientBrush(
            bounds, SurfaceLight, Color.FromArgb(0, 255, 255, 255),
            System.Drawing.Drawing2D.LinearGradientMode.Vertical);
        using var pen = new Pen(fade);
        g.DrawPath(pen, shape);
    }

    /// <summary>Rounded-rectangle path, used by every custom-drawn surface.</summary>
    public static System.Drawing.Drawing2D.GraphicsPath Round(Rectangle r, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = Math.Max(1, radius * 2);
        d = Math.Min(d, Math.Min(r.Width, r.Height));
        if (d <= 1) { path.AddRectangle(r); return path; }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
    {
        using var path = Round(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRound(Graphics g, Rectangle r, int radius, Color color, float width = 1f)
    {
        using var path = Round(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }
}
