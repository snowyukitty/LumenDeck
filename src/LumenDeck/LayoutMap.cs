using System.Drawing.Drawing2D;

namespace LumenDeck;

/// <summary>
/// A small scale drawing of the actual desk: every monitor as a rectangle in
/// its real position and proportion.
///
/// This is the app's answer to the problem it was built around. `\\.\DISPLAYn`
/// is not an identity, and the number Windows paints during *Identify* need not
/// match it either - so the only reliable way to say "this card is that screen"
/// is to show where the screen physically is. Click a rectangle and its card
/// highlights; hover one and it lights up. The brightness of each rectangle
/// tracks the monitor's actual brightness, so the map doubles as a glance-value
/// readout of whether the desk is level.
/// </summary>
internal sealed class LayoutMap : Control
{
    private List<Monitor> _monitors = new();
    private int _hoverIndex = -1;
    private int _selectedIndex = -1;

    /// <summary>Raised when a monitor rectangle is clicked.</summary>
    public event Action<Monitor> MonitorPicked;

    public LayoutMap()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bar;
        Height = 108;
        // A convenience shortcut for the mouse, not a control in its own right:
        // everything it does is also reachable from the cards below, so it must
        // not sit in the Tab order with no focus cue.
        TabStop = false;
    }

    public void SetMonitors(IEnumerable<Monitor> monitors)
    {
        _monitors = monitors?.ToList() ?? new List<Monitor>();
        _hoverIndex = -1;
        _selectedIndex = -1;
        Invalidate();
    }

    /// <summary>Repaint without rebuilding - call when brightness changes.</summary>
    public void Refresh(Monitor selected)
    {
        _selectedIndex = selected == null ? -1 : _monitors.IndexOf(selected);
        Invalidate();
    }

    /// <summary>
    /// Map the virtual desktop into the control, preserving aspect ratio.
    /// Returns an empty list when there is nothing to draw.
    /// </summary>
    private List<RectangleF> Project()
    {
        var boxes = new List<RectangleF>();
        if (_monitors.Count == 0 || Width < 8 || Height < 8) return boxes;

        int minX = _monitors.Min(m => m.Rect.Left);
        int minY = _monitors.Min(m => m.Rect.Top);
        int maxX = _monitors.Max(m => m.Rect.Right);
        int maxY = _monitors.Max(m => m.Rect.Bottom);

        float spanX = Math.Max(1, maxX - minX);
        float spanY = Math.Max(1, maxY - minY);

        const int pad = 10;
        float availW = Math.Max(1, Width - pad * 2);
        float availH = Math.Max(1, Height - pad * 2);
        float scale = Math.Min(availW / spanX, availH / spanY);

        float offX = pad + (availW - spanX * scale) / 2f;
        float offY = pad + (availH - spanY * scale) / 2f;

        foreach (var m in _monitors)
        {
            boxes.Add(new RectangleF(
                offX + (m.Rect.Left - minX) * scale,
                offY + (m.Rect.Top - minY) * scale,
                Math.Max(3, m.Rect.Width * scale),
                Math.Max(3, m.Rect.Height * scale)));
        }
        return boxes;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var boxes = Project();
        if (boxes.Count == 0)
        {
            TextRenderer.DrawText(g, "No monitors detected", Theme.Small, ClientRectangle,
                Theme.InkFaint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        for (int i = 0; i < boxes.Count; i++)
        {
            var m = _monitors[i];
            var r = Rectangle.Round(boxes[i]);
            r.Inflate(-1, -1);
            if (r.Width < 3 || r.Height < 3) continue;

            // Fill brightness mirrors the panel's own brightness, so a screen
            // that is out of step with the others is visible at a glance.
            double pct = m.SupportsBrightness ? Presets.RawToPercent(m, m.Brightness) / 100.0 : 0.25;
            int alpha = 40 + (int)(pct * 175);

            Color fill = m.SupportsBrightness
                ? Color.FromArgb(alpha, Theme.Amber)
                : Color.FromArgb(46, Theme.InkFaint);

            Theme.FillRound(g, r, 4, fill);

            bool active = i == _hoverIndex || i == _selectedIndex;
            Theme.StrokeRound(g, r, 4,
                active ? Theme.AmberLight : Color.FromArgb(120, Theme.Line),
                active ? 2f : 1f);

            if (m.IsPrimary && r.Width > 18 && r.Height > 14)
            {
                // A small dot, not the word "primary" - at this size any label
                // would be unreadable and every rectangle would look the same.
                using var dot = new SolidBrush(Color.FromArgb(210, Theme.Ink));
                g.FillEllipse(dot, r.X + 4, r.Y + 4, 4, 4);
            }
        }

        if (_hoverIndex >= 0 && _hoverIndex < _monitors.Count)
        {
            var m = _monitors[_hoverIndex];
            string label = m.DisplayName + "  -  " + m.PositionLabel;
            var size = TextRenderer.MeasureText(label, Theme.Small);
            var box = new Rectangle(6, Height - size.Height - 6, size.Width + 12, size.Height + 4);
            if (box.Right > Width) box.X = Math.Max(0, Width - box.Width - 6);
            Theme.FillRound(g, box, 5, Color.FromArgb(235, Theme.Sunken));
            TextRenderer.DrawText(g, label, Theme.Small, box, Theme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private int HitTest(Point p)
    {
        var boxes = Project();
        // Reverse order so a monitor drawn later (on top) wins the hit.
        for (int i = boxes.Count - 1; i >= 0; i--)
            if (boxes[i].Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = HitTest(e.Location);
        if (hit == _hoverIndex) return;
        _hoverIndex = hit;
        Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex < 0) return;
        _hoverIndex = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int hit = HitTest(e.Location);
        if (hit < 0) return;
        _selectedIndex = hit;
        Invalidate();
        MonitorPicked?.Invoke(_monitors[hit]);
    }
}
