using System.Drawing.Drawing2D;

namespace LumenDeck;

internal sealed class FlatButton : Control
{
    private bool _hover;
    private bool _down;
    private Keys _activationKey;
    private bool _disposeStarted;
    private bool _dispatchingClick;
    private bool CanActivate => Enabled && !IsDisposed && !Disposing && !_disposeStarted;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }

    public FlatButton(string text)
    {
        Text = text;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Body;
        Height = 30;
        Width = 84;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    /// <summary>Keep hover and accessibility explanations on the same presentation path.</summary>
    internal void SetHelp(ToolTip tips, string description)
    {
        ArgumentNullException.ThrowIfNull(tips);
        tips.SetToolTip(this, description);
        AccessibleDescription = description;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (!AutoSize) return base.GetPreferredSize(proposedSize);
        Size label = TextRenderer.MeasureText(Text, Font, Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        Padding insets = TextInsets;
        int horizontal = insets.Horizontal + 1;
        int vertical = insets.Vertical + 1;
        // FlowLayout constrains the first command on a row and measures later
        // commands unbounded before deciding whether to move them to a new row.
        int availableWidth = proposedSize.Width > 0 ? proposedSize.Width : int.MaxValue;
        int width = Math.Max(MinimumSize.Width,
            Math.Min(label.Width + horizontal, availableWidth));
        label = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(1, width - horizontal), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        return new Size(width, Math.Max(MinimumSize.Height, label.Height + vertical));
    }

    private Padding TextInsets => AutoSize
        ? new Padding(Padding.Left + LogicalToDeviceUnits(10), Padding.Top + LogicalToDeviceUnits(6),
            Padding.Right + LogicalToDeviceUnits(10), Padding.Bottom + LogicalToDeviceUnits(6))
        : Padding;

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (AutoSize) Parent?.PerformLayout(this, nameof(Text));
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (AutoSize) Parent?.PerformLayout(this, nameof(Font));
    }

    // A custom Control is not a Button: it gets no Space/Enter handling, no
    // focus rectangle and no accessible role for free. Without these, every
    // preset - global and per-monitor - plus Identify, Refresh and Warmth off
    // was mouse-only, and Narrator announced them as anonymous client areas.
    protected override bool IsInputKey(Keys key) =>
        IsActivationKey(key) || base.IsInputKey(key);

    private static bool IsActivationKey(Keys key) =>
        (key & (Keys.Control | Keys.Alt)) == Keys.None &&
        (key & Keys.KeyCode) is Keys.Space or Keys.Enter;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.SuppressKeyPress || !CanActivate) { CancelPress(); return; }
        if (!IsActivationKey(e.KeyData)) return;
        if (_activationKey == Keys.None) _activationKey = e.KeyCode;
        SetPressed(true);
        e.Handled = e.SuppressKeyPress = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Handled || e.SuppressKeyPress || !CanActivate) { CancelPress(); return; }
        if (e.KeyCode is not (Keys.Space or Keys.Enter)) return;
        bool activate = Enabled && IsActivationKey(e.KeyData) && _activationKey == e.KeyCode;
        if (_activationKey != Keys.None && _activationKey != e.KeyCode) return;
        _activationKey = Keys.None;
        if (activate) PerformClick();
        else SetPressed(false);
        e.Handled = e.SuppressKeyPress = true;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { CancelPress(); base.OnLostFocus(e); }

    private void CancelPress()
    {
        _activationKey = Keys.None;
        SetPressed(false);
        Invalidate();
    }

    private void SetPressed(bool pressed)
    {
        pressed = pressed && CanActivate;
        if (_down == pressed) return;
        _down = pressed;
        Invalidate();
        if (IsHandleCreated && !IsDisposed && !Disposing)
            ((ControlAccessibleObject)AccessibilityObject).NotifyClients(AccessibleEvents.StateChange, -1);
    }

    protected override void OnClick(EventArgs e)
    {
        if (e is MouseEventArgs mouse && mouse.Button != MouseButtons.Left) return;
        if (!CanActivate || _dispatchingClick) return;
        // An assistive/direct click can arrive while a keyboard press is armed.
        // Consume the press before callbacks so its later release is inert.
        _dispatchingClick = true;
        try
        {
            CancelPress();
            if (CanActivate) base.OnClick(e);
        }
        finally { _dispatchingClick = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposeStarted)
        {
            _disposeStarted = true;
            CancelPress();
        }
        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x001F) CancelPress(); // WM_CANCELMODE
        base.WndProc(ref m);
    }

    protected override AccessibleObject CreateAccessibilityInstance() =>
        new ButtonAccessibleObject(this);

    /// <summary>Lets assistive tech press the button, not merely describe it.</summary>
    internal void PerformClick() => OnClick(EventArgs.Empty);

    private sealed class ButtonAccessibleObject : ControlAccessibleObject
    {
        private readonly FlatButton _owner;
        public ButtonAccessibleObject(FlatButton owner) : base(owner) => _owner = owner;
        public override string Name
        {
            get => _owner.AccessibleName ?? _owner.Text;
            set => base.Name = value;
        }
        public override AccessibleRole Role => AccessibleRole.PushButton;
        public override string KeyboardShortcut => null;
        public override string DefaultAction => "Press";
        public override AccessibleStates State => (base.State & ~AccessibleStates.Pressed) |
            (!_owner.CanActivate ? AccessibleStates.Unavailable :
                _owner._down ? AccessibleStates.Pressed : AccessibleStates.None);
        public override void DoDefaultAction() => _owner.PerformClick();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);

        Color face = !Enabled ? Theme.Card : Primary
            ? (_down ? Theme.Amber : _hover ? Theme.AmberLight : Theme.Amber)
            : (_down ? Theme.Sunken : _hover ? Theme.CardHover : Theme.Card);
        Color text = !Enabled ? Theme.InkFaint : Primary ? Color.FromArgb(28, 20, 8) : Theme.Ink;
        Color edge = !Enabled ? Theme.Line : Primary ? Color.FromArgb(0, 0, 0, 0) : (_hover ? Theme.AmberDim : Theme.Line);

        Theme.FillRound(g, r, 7, face);
        if (edge.A > 0) Theme.StrokeRound(g, r, 7, edge);
        if (Enabled && !Primary && !_down && r.Width > 18 && r.Height > 12)
        {
            using var sheen = new Pen(Color.FromArgb(24, Theme.Ink));
            int inset = LogicalToDeviceUnits(9);
            g.DrawLine(sheen, inset, 1, Math.Max(inset, r.Right - inset), 1);
        }
        if (Focused)
        {
            var inner = Rectangle.Inflate(r, -3, -3);
            Theme.StrokeRound(g, inner, 5, Color.FromArgb(190, Theme.AmberLight), 1.5f);
        }
        Padding insets = TextInsets;
        var labelBounds = new Rectangle(r.Left + insets.Left, r.Top + insets.Top,
            Math.Max(0, r.Width - insets.Horizontal), Math.Max(0, r.Height - insets.Vertical));
        TextRenderer.DrawText(g, Text, Font, labelBounds, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; SetPressed(false); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); SetPressed(e.Button == MouseButtons.Left); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); SetPressed(false); }
    protected override void OnEnabledChanged(EventArgs e) { CancelPress(); base.OnEnabledChanged(e); }
}
