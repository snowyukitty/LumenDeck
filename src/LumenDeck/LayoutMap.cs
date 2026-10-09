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
    private readonly Label _caption;

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
        _caption = new Label
        {
            Name = "display-map-caption", Dock = DockStyle.Bottom, Height = 24,
            Font = Theme.Small, ForeColor = Theme.InkMuted, BackColor = Theme.Bar,
            TextAlign = ContentAlignment.MiddleCenter, AutoSize = false, AutoEllipsis = true,
            UseMnemonic = false, Padding = new Padding(6, 0, 6, 0),
        };
        Controls.Add(_caption);
    }

    public void SetMonitors(IEnumerable<Monitor> monitors)
    {
        _monitors = monitors?.ToList() ?? new List<Monitor>();
        _hoverIndex = -1;
        _selectedIndex = -1;
        Cursor = Cursors.Default;
        UpdateCaption();
        Invalidate();
    }

    /// <summary>Repaint without rebuilding - call when brightness changes.</summary>
    public void Refresh(Monitor selected)
    {
        _selectedIndex = selected == null ? -1 : _monitors.IndexOf(selected);
        UpdateCaption();
        Invalidate();
    }

    private void UpdateCaption()
    {
        int index = _hoverIndex >= 0 ? _hoverIndex : _selectedIndex;
        string text = _monitors.Count == 0 ? "" : "Select a display to find its controls.";
        if (index >= 0 && index < _monitors.Count)
        {
            var monitor = _monitors[index];
            text = string.IsNullOrWhiteSpace(monitor.PositionLabel) ? monitor.DisplayName
                : $"{monitor.DisplayName} ({monitor.PositionLabel})";
        }
        _caption.Text = text;
        _caption.AccessibleName = text;
    }

    /// <summary>
    /// Map the virtual desktop into the control, preserving aspect ratio.
    /// Returns an empty list when there is nothing to draw.
    /// </summary>
    private List<RectangleF> Project()
    {
        var boxes = new List<RectangleF>();
        if (_monitors.Count == 0 || Width < 8 || Height - _caption.Height < 8) return boxes;

        int minX = _monitors.Min(m => m.Rect.Left);
        int minY = _monitors.Min(m => m.Rect.Top);
        int maxX = _monitors.Max(m => m.Rect.Right);
        int maxY = _monitors.Max(m => m.Rect.Bottom);

        float spanX = Math.Max(1, maxX - minX);
        float spanY = Math.Max(1, maxY - minY);

        const int pad = 10;
        float availW = Math.Max(1, Width - pad * 2);
        float availH = Math.Max(1, Height - _caption.Height - pad * 2);
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
            var canvas = new Rectangle(0, 0, Width, Math.Max(0, Height - _caption.Height));
            TextRenderer.DrawText(g, "No monitors detected", Theme.Small, canvas,
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
            int alpha = 28 + (int)(pct * 110);

            Color fill = m.SupportsBrightness
                ? Color.FromArgb(alpha, Theme.Amber)
                : Color.FromArgb(46, Theme.InkFaint);

            Theme.FillRound(g, r, 4, Theme.Sunken);
            using (var surface = new LinearGradientBrush(r, fill, Color.FromArgb(20, Theme.Amber), LinearGradientMode.Vertical))
            using (var shape = Theme.Round(r, 4))
                g.FillPath(surface, shape);

            bool active = i == _hoverIndex || i == _selectedIndex;
            Theme.StrokeRound(g, r, 4,
                active ? Theme.AmberLight : Color.FromArgb(120, Theme.Line),
                active ? 2f : 1f);

            if (r.Width > 20 && r.Height > 20)
            {
                int inset = LogicalToDeviceUnits(5);
                int railHeight = Math.Max(2, LogicalToDeviceUnits(3));
                var rail = new Rectangle(r.Left + inset, r.Bottom - inset - railHeight,
                    Math.Max(1, r.Width - inset * 2), railHeight);
                Theme.FillRound(g, rail, railHeight / 2, Theme.Line);
                if (m.SupportsBrightness && pct > 0)
                    Theme.FillRound(g, new Rectangle(rail.X, rail.Y, Math.Max(1, (int)Math.Round(rail.Width * pct)), rail.Height),
                        railHeight / 2, active ? Theme.AmberLight : Theme.Amber);
            }

            if (m.IsPrimary && r.Width > 18 && r.Height > 14)
            {
                // A small dot, not the word "primary" - at this size any label
                // would be unreadable and every rectangle would look the same.
                using var dot = new SolidBrush(Color.FromArgb(210, Theme.Ink));
                g.FillEllipse(dot, r.X + 4, r.Y + 4, 4, 4);
            }
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
        UpdateCaption();
        Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex < 0) return;
        _hoverIndex = -1;
        UpdateCaption();
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        int hit = HitTest(e.Location);
        if (hit < 0) return;
        _selectedIndex = hit;
        UpdateCaption();
        Invalidate();
        MonitorPicked?.Invoke(_monitors[hit]);
    }
}
