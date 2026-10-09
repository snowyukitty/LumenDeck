namespace LumenDeck;

/// <summary>
/// A bounded status reader. Long messages wrap and scroll rather than losing
/// their retry instructions; reading or selecting text has no app callbacks.
/// </summary>
internal sealed class StatusStripe : UserControl
{
    private readonly TextBox _message;
    private StatusKind _kind = StatusKind.Info;

    public StatusStripe()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 46;
        BackColor = Theme.Bar;
        Font = Theme.Small;
        Padding = new Padding(14, 7, 10, 7);
        TabStop = false;
        AccessibleRole = AccessibleRole.StatusBar;
        _message = new TextBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            Multiline = true, ReadOnly = true, WordWrap = true,
            ScrollBars = ScrollBars.Vertical, BackColor = Theme.Bar, ForeColor = Theme.InkMuted,
            AccessibleName = "Current status",
            AccessibleDescription = "Read-only status. Use arrow keys or scroll to read the full message; select text to copy it.",
            TabIndex = 0,
        };
        Controls.Add(_message);
    }

    public void Set(string text, StatusKind kind)
    {
        _kind = kind;
        _message.ForeColor = kind == StatusKind.Warn ? Theme.Warn : Theme.InkMuted;
        _message.Text = text ?? "";
        AccessibleName = _message.Text;
        if (IsHandleCreated) ((ControlAccessibleObject)AccessibilityObject).NotifyClients(AccessibleEvents.NameChange, -1);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var line = new Pen(Theme.Line))
            e.Graphics.DrawLine(line, 0, 0, Width, 0);
        using var marker = new SolidBrush(_kind == StatusKind.Warn ? Theme.Warn : Theme.AmberDim);
        e.Graphics.FillRectangle(marker, 0, 1, 3, Math.Max(0, Height - 1));
    }
}
