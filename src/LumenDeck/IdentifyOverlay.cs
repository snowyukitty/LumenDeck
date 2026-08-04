namespace LumenDeck;

/// <summary>
/// Paints a large label on each monitor for a couple of seconds, the way the
/// Windows display settings "Identify" button does.
///
/// This is not a nicety. The `\\.\DISPLAYn` suffix is not a usable identity: it
/// carries gaps from past hot-plugging (a four-monitor setup can enumerate as
/// DISPLAY1, DISPLAY2, DISPLAY3, DISPLAY6) and it need not match the number
/// Windows paints on the screen. Acting on a guessed mapping adjusts the wrong
/// monitor and looks exactly like success. So this app never asks anyone to
/// trust a number: it shows each panel its own name, on its own glass.
/// </summary>
internal static class IdentifyOverlay
{
    private static bool _showing;

    public static void Show(IEnumerable<Monitor> monitors, int milliseconds = 2500)
    {
        // Clicking Identify repeatedly would otherwise stack overlay sets, each
        // with its own timer, and the earlier ones would close the later ones'
        // windows out from under them.
        if (_showing) return;
        _showing = true;

        var forms = new List<Form>();

        foreach (var m in monitors)
        {
            var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                TopMost = true,
                BackColor = Color.FromArgb(18, 18, 20),
                Opacity = 0.90,
                Bounds = new Rectangle(m.Rect.Left, m.Rect.Top, m.Rect.Width, m.Rect.Height),
            };

            string size = string.IsNullOrEmpty(m.SizeLabel) ? "" : "  -  " + m.SizeLabel;

            var name = new Label
            {
                Text = m.DisplayName,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", ScaleFont(m, 46f), FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            var sub = new Label
            {
                Text = m.PositionLabel + size,
                ForeColor = Color.FromArgb(150, 190, 255),
                Font = new Font("Segoe UI", ScaleFont(m, 20f)),
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = (int)(m.Rect.Height * 0.30),
                TextAlign = ContentAlignment.TopCenter,
            };

            f.Controls.Add(name);
            f.Controls.Add(sub);
            forms.Add(f);
            f.Show();
        }

        if (forms.Count == 0)
        {
            _showing = false;
            return;
        }

        var timer = new System.Windows.Forms.Timer { Interval = milliseconds };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            foreach (var f in forms)
            {
                // Disposing the form disposes its child labels; the Fonts those
                // labels were given are not owned by them, so drop them here.
                foreach (Control c in f.Controls) c.Font?.Dispose();
                f.Close();
                f.Dispose();
            }
            _showing = false;
        };
        timer.Start();
    }

    /// <summary>Keep the text readable on a small 1080p panel and on a large one alike.</summary>
    private static float ScaleFont(Monitor m, float baseSize)
    {
        float factor = Math.Clamp(m.Rect.Height / 1440f, 0.6f, 1.4f);
        return baseSize * factor;
    }
}
