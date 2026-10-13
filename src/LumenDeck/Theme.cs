namespace LumenDeck;

/// <summary>
/// Design tokens. One place, so nothing drifts.
///
/// The palette is the icon's palette: a warm amber ramp on a near-black
/// surface. That is not decoration - the product is about the light a screen
/// emits, and an amber accent on dark says so before a single label is read.
/// Blue identifies display context; neutral contrast controls stay separate
/// from the warm luminance and temperature controls.
/// </summary>
internal static class Theme
{
    internal static Func<bool> ContrastMode { get; set; } = () => SystemInformation.HighContrast;
    internal static bool HighContrast => ContrastMode();
    // Surfaces, darkest to lightest.
    public static Color Base => HighContrast ? SystemColors.Window : Color.FromArgb(19, 21, 26);
    public static Color Bar => HighContrast ? SystemColors.Window : Color.FromArgb(25, 28, 34);
    public static Color Card => HighContrast ? SystemColors.Window : Color.FromArgb(30, 34, 41);
    public static Color CardTop => HighContrast ? SystemColors.Window : Color.FromArgb(40, 45, 54);
    public static Color CardHover => HighContrast ? SystemColors.Window : Color.FromArgb(48, 54, 64);
    public static Color Sunken => HighContrast ? SystemColors.Window : Color.FromArgb(20, 23, 29);
    public static Color Line => HighContrast ? SystemColors.WindowText : Color.FromArgb(62, 70, 83);
    public static Color SurfaceLight => HighContrast ? SystemColors.Window : Color.FromArgb(16, 255, 255, 255);
    public static Color Shadow => HighContrast ? SystemColors.Window : Color.FromArgb(100, 0, 0, 0);

    // Text.
    public static Color Ink => HighContrast ? SystemColors.WindowText : Color.FromArgb(238, 240, 245);
    public static Color InkMuted => HighContrast ? SystemColors.WindowText : Color.FromArgb(169, 178, 193);
    // Lightened from 104,110,124 after measuring: that value gave 3.15:1 on
    // Card and 3.41:1 on Bar, below the 4.5:1 small-text floor - and it was
    // carrying real content, not decoration (the nits readout, "All monitors",
    // "No monitors detected"). This clears 4.5:1 on both surfaces.
    public static Color InkFaint => HighContrast ? SystemColors.WindowText : Color.FromArgb(145, 156, 172);

    // The brand ramp, straight off the icon.
    public static Color AmberLight => HighContrast ? SystemColors.WindowText : Color.FromArgb(255, 214, 143);
    public static Color Amber => HighContrast ? SystemColors.WindowText : Color.FromArgb(231, 169, 93);
    public static Color AmberDim => HighContrast ? SystemColors.WindowText : Color.FromArgb(130, 91, 45);
    public static Color AmberPressed => HighContrast ? SystemColors.WindowText : Color.FromArgb(204, 145, 74);
    public static Color OnAmber => HighContrast ? SystemColors.Window : Color.FromArgb(28, 20, 8);

    public static Color Info => HighContrast ? SystemColors.WindowText : Color.FromArgb(126, 168, 232);
    public static Color Warn => HighContrast ? SystemColors.WindowText : Color.FromArgb(236, 196, 88);
    public static Color Danger => HighContrast ? SystemColors.WindowText : Color.FromArgb(226, 108, 92);

    // Type. Created once and shared: a Font handed to a Control is not owned by
    // it, so per-control fonts leak until their finalizers run.
    public static readonly Font H1 = new("Segoe UI Semibold", 11.5f);
    public static readonly Font Brand = new("Segoe UI Semibold", 15f);
    public static readonly Font H2 = new("Segoe UI Semibold", 10.5f);
    public static readonly Font Body = new("Segoe UI", 9f);
    public static readonly Font Small = new("Segoe UI", 8.25f);
    public static readonly Font Value = new("Segoe UI Semibold", 9.5f);
    public static readonly Font PrincipalValue = new("Segoe UI Semibold", 32f);
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
