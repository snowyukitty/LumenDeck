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
    // Surfaces, darkest to lightest.
    public static readonly Color Base = Color.FromArgb(18, 20, 25);
    public static readonly Color Bar = Color.FromArgb(23, 26, 32);
    public static readonly Color Card = Color.FromArgb(29, 33, 41);
    public static readonly Color CardTop = Color.FromArgb(34, 39, 48);
    public static readonly Color CardHover = Color.FromArgb(42, 48, 59);
    public static readonly Color Sunken = Color.FromArgb(14, 17, 22);
    public static readonly Color Line = Color.FromArgb(53, 61, 74);

    // Text.
    public static readonly Color Ink = Color.FromArgb(238, 240, 245);
    public static readonly Color InkMuted = Color.FromArgb(169, 178, 193);
    // Lightened from 104,110,124 after measuring: that value gave 3.15:1 on
    // Card and 3.41:1 on Bar, below the 4.5:1 small-text floor - and it was
    // carrying real content, not decoration (the nits readout, "All monitors",
    // "No monitors detected"). This clears 4.5:1 on both surfaces.
    public static readonly Color InkFaint = Color.FromArgb(145, 156, 172);

    // The brand ramp, straight off the icon.
    public static readonly Color AmberLight = Color.FromArgb(255, 214, 143);
    public static readonly Color Amber = Color.FromArgb(231, 169, 93);
    public static readonly Color AmberDim = Color.FromArgb(130, 91, 45);

    public static readonly Color Info = Color.FromArgb(126, 168, 232);
    public static readonly Color Warn = Color.FromArgb(226, 160, 92);
    public static readonly Color Danger = Color.FromArgb(226, 108, 92);

    // Type. Created once and shared: a Font handed to a Control is not owned by
    // it, so per-control fonts leak until their finalizers run.
    public static readonly Font H1 = new("Segoe UI Semibold", 11.5f);
    public static readonly Font Brand = new("Segoe UI Semibold", 13f);
    public static readonly Font H2 = new("Segoe UI Semibold", 10f);
    public static readonly Font Body = new("Segoe UI", 9f);
    public static readonly Font Small = new("Segoe UI", 8.25f);
    public static readonly Font Value = new("Segoe UI Semibold", 9.5f);
    public static readonly Font Mono = new("Consolas", 9.5f);

    public const int Radius = 10;
    public const int CardPad = 16;
    public const int Gap = 10;

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
