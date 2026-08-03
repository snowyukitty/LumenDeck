using System.Drawing.Drawing2D;

namespace LumenDeck;

/// <summary>
/// Draws the tray and window icon at runtime, so the project carries no binary
/// assets and builds from source alone.
/// </summary>
internal static class AppIcon
{
    public static Icon Create(int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float c = size / 2f;
            float r = size * 0.26f;

            // A sun: filled core, rays around it - reads as "brightness" at 16px.
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(c - r * 1.7f, c - r * 1.7f, r * 3.4f, r * 3.4f);
                using var brush = new PathGradientBrush(glow)
                {
                    CenterColor = Color.FromArgb(120, 255, 214, 120),
                    SurroundColors = new[] { Color.FromArgb(0, 255, 214, 120) },
                };
                g.FillPath(brush, glow);
            }

            using (var core = new SolidBrush(Color.FromArgb(255, 250, 196, 70)))
                g.FillEllipse(core, c - r, c - r, r * 2, r * 2);

            using (var pen = new Pen(Color.FromArgb(255, 250, 196, 70), Math.Max(1.6f, size * 0.055f))
                   { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4.0;
                    float inner = r * 1.45f;
                    float outer = r * 1.95f;
                    g.DrawLine(pen,
                        c + (float)(Math.Cos(a) * inner), c + (float)(Math.Sin(a) * inner),
                        c + (float)(Math.Cos(a) * outer), c + (float)(Math.Sin(a) * outer));
                }
            }
        }

        IntPtr h = bmp.GetHicon();
        try
        {
            // Clone, because the Icon returned by FromHandle does not own the
            // handle and the handle must be destroyed to avoid a GDI leak.
            using var tmp = Icon.FromHandle(h);
            return (Icon)tmp.Clone();
        }
        finally
        {
            Native.DestroyIcon(h);
        }
    }
}
