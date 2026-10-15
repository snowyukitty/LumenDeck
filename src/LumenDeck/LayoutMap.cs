using System.Drawing.Drawing2D;

namespace LumenDeck;

/// <summary>A keyboard-selectable projection of Windows virtual-desktop coordinates.</summary>
internal sealed class LayoutMap : Control
{
    private List<Monitor> _monitors = new();
    private int _hoverIndex = -1;
    private int _selectedIndex = -1;
    private int FooterHeight => LogicalToDeviceUnits(Dock == DockStyle.Left ? 52 : 24);
    private readonly Label _caption;
    private readonly CheckBox _all;
    public event Action<Monitor> MonitorPicked;
    public event Action<Monitor> MonitorSelected;
    public event Action<bool> AllDisplaysChanged;

    public LayoutMap()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bar;
        Height = 140;
        TabStop = true;
        AccessibleName = "Desk displays — Windows desktop layout";
        AccessibleDescription = "Arrow keys select the nearest display in that direction without wrapping. Home and End select the first and last display. Enter opens its controls. Ctrl+Page Down and Ctrl+Page Up move between display controls.";
        _caption = new Label
        {
            Name = "display-map-caption", Height = 24,
            Font = Theme.Small, ForeColor = Theme.InkMuted, BackColor = Theme.Bar,
            TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, AutoEllipsis = true,
            UseMnemonic = false, Padding = new Padding(16, 0, 4, 0),
        };
        _all = new CheckBox
        {
            Text = "All displays", AutoSize = true, ForeColor = Theme.Ink,
            BackColor = Theme.Bar, AccessibleName = "Show controls for all displays",
        };
        _all.CheckedChanged += (_, _) => AllDisplaysChanged?.Invoke(_all.Checked);
        Controls.Add(_all);
        Controls.Add(_caption);
        _all.BringToFront();
    }

    internal void SetAllDisplays(bool show) => _all.Checked = show;

    internal int RequiredHeight(int minimum) => Math.Max(minimum, LogicalToDeviceUnits(128));
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_all != null)
        {
            _all.Location = new Point(Width - _all.Width - LogicalToDeviceUnits(16), Height - _all.Height - 2);
            _caption.SetBounds(0, Height - FooterHeight, Math.Max(1, Dock == DockStyle.Left ? Width : _all.Left - 8), _caption.Height);
        }
    }

    public void SetMonitors(IEnumerable<Monitor> monitors)
    {
        _monitors = monitors?.ToList() ?? new();
        _hoverIndex = _selectedIndex = -1;
        Cursor = Cursors.Default;
        UpdateCaption();
        Invalidate();
    }

    public void Refresh(Monitor selected)
    {
        // Value refreshes must not erase the user's selected display.
        if (selected != null) SetSelected(_monitors.IndexOf(selected));
        UpdateCaption();
        Invalidate();
    }

    private void UpdateCaption()
    {
        int index = _hoverIndex >= 0 ? _hoverIndex : _selectedIndex;
        string text = _monitors.Count == 0 ? "" : "Select a display · Windows desktop layout";
        if (index >= 0 && index < _monitors.Count)
        {
            var m = _monitors[index];
            text = $"Display {index + 1} · " + (string.IsNullOrWhiteSpace(m.PositionLabel) ? m.DisplayName : $"{m.DisplayName} ({m.PositionLabel})");
        }
        _caption.Text = _caption.AccessibleName = text;
    }

    private List<RectangleF> Project()
    {
        if (_monitors.Count == 0 || Width < 8 || Height - _caption.Height < 8) return new();
        // One affine transform preserves offsets, adjacency and pixel proportions.
        // Ranking distinct coordinates into slots invents gaps and separate rows.
        float left = _monitors.Min(m => m.Rect.Left), top = _monitors.Min(m => m.Rect.Top);
        float spanWidth = Math.Max(1, _monitors.Max(m => m.Rect.Right) - left);
        float spanHeight = Math.Max(1, _monitors.Max(m => m.Rect.Bottom) - top);
        float pad = LogicalToDeviceUnits(12);
        float availableWidth = Math.Max(1, Width - pad * 2);
        float availableHeight = Math.Max(1, Height - FooterHeight - pad * 2);
        float scale = Math.Min(availableWidth / spanWidth, availableHeight / spanHeight);
        float x = pad + (availableWidth - spanWidth * scale) / 2;
        float y = pad + (availableHeight - spanHeight * scale) / 2;
        return _monitors.Select(m => new RectangleF(x + (m.Rect.Left - left) * scale,
            y + (m.Rect.Top - top) * scale, m.Rect.Width * scale, m.Rect.Height * scale)).ToList();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        var boxes = Project();
        for (int i = 0; i < boxes.Count; i++)
        {
            var r = Rectangle.Round(boxes[i]);
            r.Inflate(-2, -2);
            if (r.Width < 3 || r.Height < 3) continue;
            bool selected = i == _selectedIndex;
            Theme.FillRound(g, r, 5, selected ? Theme.CardHover : Theme.Sunken);
            Theme.StrokeRound(g, r, 5, selected ? Theme.AmberLight : Theme.Line, selected ? LogicalToDeviceUnits(3) : 1);
            var m = _monitors[i];
            if (r.Height < LogicalToDeviceUnits(64) || r.Width < LogicalToDeviceUnits(110))
            {
                TextRenderer.DrawText(g, $"{i + 1}", Font, r, selected ? Theme.AmberLight : Theme.Ink,
                    TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                continue;
            }
            string title = $"{i + 1}" + (selected ? "  SELECTED" : "");
            if (TextRenderer.MeasureText(title, Font).Width > r.Width - 14) title = $"{i + 1}";
            var upper = new Rectangle(r.X + 7, r.Y + 3, r.Width - 14, Math.Min(r.Height - 4, LogicalToDeviceUnits(24)));
            TextRenderer.DrawText(g, title, Font, upper, selected ? Theme.AmberLight : Theme.Ink,
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            if (r.Height > LogicalToDeviceUnits(44))
            {
                // WordBreak can silently clip a word wider than a portrait tile.
                // Wrap only when every word fits; otherwise use a single ellipsis.
                bool wordsFit = m.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .All(word => TextRenderer.MeasureText(word, Font, Size.Empty, TextFormatFlags.NoPrefix).Width <= r.Width - 14);
                TextRenderer.DrawText(g, m.DisplayName, Font,
                    new Rectangle(r.X + 7, upper.Bottom, r.Width - 14, r.Bottom - upper.Bottom - 5), Theme.Ink,
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter |
                    (wordsFit ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine));
            }
        }
        if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 3, Width - 6, Height - _caption.Height - 6), Theme.Ink, BackColor);
    }

    private int HitTest(Point p)
    {
        var boxes = Project();
        for (int i = boxes.Count - 1; i >= 0; i--) if (boxes[i].Contains(p)) return i;
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
        _hoverIndex = -1;
        UpdateCaption();
        Cursor = Cursors.Default;
        Invalidate();
    }
    private bool CanPick => !IsDisposed && !Disposing && Enabled && Visible;

    private void SetSelected(int index)
    {
        if (_selectedIndex == index) return;
        _selectedIndex = index;
        UpdateCaption();
        Invalidate();
        if (IsHandleCreated && index >= 0)
        {
            // The accessible-object API emits events with the runtime's default
            // switch. Child indices are zero-based here (native IDs add one).
            var accessible = (ControlAccessibleObject)AccessibilityObject;
            accessible.NotifyClients(AccessibleEvents.Selection, index);
            if (Focused) accessible.NotifyClients(AccessibleEvents.Focus, index);
        }
    }

    private void Pick(int index, bool openControls = true)
    {
        if (!CanPick || index < 0 || index >= _monitors.Count) return;
        var monitor = _monitors[index];
        SetSelected(index);
        // Selecting on the map never transiently focuses a slider. Enter/click
        // is the explicit transition from choosing a screen to adjusting it.
        if (openControls) MonitorPicked?.Invoke(monitor);
        else MonitorSelected?.Invoke(monitor);
    }

    private int Neighbour(int index, Keys direction)
    {
        if (index < 0 || index >= _monitors.Count) return _monitors.Count == 0 ? -1 : 0;
        var origin = _monitors[index].Rect;
        double x = origin.Left + origin.Width / 2d, y = origin.Top + origin.Height / 2d;
        bool horizontal = direction is Keys.Left or Keys.Right;
        int sign = direction is Keys.Left or Keys.Up ? -1 : 1;
        return _monitors.Select((m, i) =>
        {
            double dx = m.Rect.Left + m.Rect.Width / 2d - x, dy = m.Rect.Top + m.Rect.Height / 2d - y;
            bool aligned = horizontal ? m.Rect.Top < origin.Bottom && m.Rect.Bottom > origin.Top
                : m.Rect.Left < origin.Right && m.Rect.Right > origin.Left;
            return new { Index = i, Forward = sign * (horizontal ? dx : dy), Aligned = aligned,
                Across = Math.Abs(horizontal ? dy : dx), Distance = dx * dx + dy * dy };
        }).Where(c => c.Forward > 0)
          .OrderBy(c => c.Aligned ? 0 : 1).ThenBy(c => c.Distance).ThenBy(c => c.Across).ThenBy(c => c.Index)
          .Select(c => c.Index).DefaultIfEmpty(index).First();
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left) Pick(HitTest(e.Location));
    }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.Modifiers) == Keys.None &&
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!CanPick || e.Handled || e.SuppressKeyPress || e.Modifiers != Keys.None || _monitors.Count == 0) return;
        if (e.KeyCode is Keys.Left or Keys.Up or Keys.Right or Keys.Down or Keys.Home or Keys.End)
        {
            int next = e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? _monitors.Count - 1 : Neighbour(_selectedIndex, e.KeyCode);
            if (next != _selectedIndex) Pick(next, openControls: false);
            e.Handled = e.SuppressKeyPress = true;
        }
        else if (e.KeyCode is Keys.Enter or Keys.Space) { Pick(Math.Max(0, _selectedIndex)); e.Handled = e.SuppressKeyPress = true; }
    }
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
        if (IsHandleCreated && _selectedIndex >= 0)
            ((ControlAccessibleObject)AccessibilityObject).NotifyClients(AccessibleEvents.Focus, _selectedIndex);
    }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override AccessibleObject CreateAccessibilityInstance() => new DeskAccessible(this);
    private sealed class DeskAccessible(LayoutMap owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.List;
        public override int GetChildCount() => owner._monitors.Count + 1;
        public override AccessibleObject GetSelected() => owner._selectedIndex >= 0 ? GetChild(owner._selectedIndex) : null;
        public override AccessibleObject GetFocused() => owner._all.Focused ? owner._all.AccessibilityObject : owner.Focused ? GetSelected() : null;
        public override AccessibleObject HitTest(int x, int y)
        {
            if (!owner.CanPick) return null;
            var point = owner.PointToClient(new Point(x, y));
            if (owner._all.Bounds.Contains(point)) return owner._all.AccessibilityObject;
            int index = owner.HitTest(point);
            return index >= 0 ? GetChild(index) : owner.ClientRectangle.Contains(point) ? this : null;
        }
        public override AccessibleObject GetChild(int index) => index == owner._monitors.Count ? owner._all.AccessibilityObject
            : index >= 0 && index < owner._monitors.Count ? new DisplayAccessible(owner, index) : null;
    }
    private sealed class DisplayAccessible : AccessibleObject
    {
        private readonly LayoutMap _owner;
        private readonly Monitor _monitor;
        private int Index => _owner._monitors.IndexOf(_monitor);
        public DisplayAccessible(LayoutMap owner, int index) { _owner = owner; _monitor = owner._monitors[index]; }
        public override string Name { get => string.IsNullOrWhiteSpace(_monitor.PositionLabel) ? _monitor.DisplayName : $"{_monitor.DisplayName} ({_monitor.PositionLabel})"; set { } }
        public override AccessibleRole Role => AccessibleRole.ListItem;
        public override AccessibleObject Parent => _owner.AccessibilityObject;
        public override Rectangle Bounds
        {
            get
            {
                int index = Index;
                if (index < 0 || _owner.IsDisposed || !_owner.Visible) return Rectangle.Empty;
                var boxes = _owner.Project();
                return index < boxes.Count ? _owner.RectangleToScreen(Rectangle.Round(boxes[index])) : Rectangle.Empty;
            }
        }
        public override AccessibleStates State => Index < 0 || !_owner.CanPick ? AccessibleStates.Unavailable
            : AccessibleStates.Selectable | AccessibleStates.Focusable |
                (Index == _owner._selectedIndex ? AccessibleStates.Selected | (_owner.Focused ? AccessibleStates.Focused : AccessibleStates.None) : AccessibleStates.None);
        public override string DefaultAction => "Select display";
        public override void DoDefaultAction() { if (!_owner.IsDisposed) _owner.Pick(Index); }
        public override void Select(AccessibleSelection flags)
        {
            if (!_owner.CanPick || Index < 0 || (flags & (AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus)) == 0) return;
            _owner.Pick(Index, openControls: false);
            if ((flags & AccessibleSelection.TakeFocus) != 0 && _owner.CanPick && Index >= 0 && Index == _owner._selectedIndex) _owner.Focus();
        }
    }
}
