namespace LumenDeck;

/// <summary>
/// A labelled command row that grows vertically when its commands wrap.
/// Width belongs to the window, never to the sum of all available commands.
/// </summary>
internal sealed class CommandSection : Panel
{
    private readonly Label _caption;
    public FlowLayoutPanel Commands { get; }
    private bool _layingOut;

    public CommandSection(string caption)
    {
        BackColor = Theme.Bar;
        Padding = new Padding(14, 8, 14, 8);
        Dock = DockStyle.Top;
        _caption = new Label
        {
            Text = caption, AutoSize = true, Font = Theme.Small,
            ForeColor = Theme.InkFaint, BackColor = Color.Transparent,
        };
        Commands = new FlowLayoutPanel
        {
            WrapContents = true, BackColor = Color.Transparent,
            Margin = Padding.Empty, Padding = Padding.Empty,
            AccessibleName = caption, AccessibleRole = AccessibleRole.Grouping,
        };
        Commands.Layout += (_, _) => { if (!_layingOut) PerformLayout(); };
        Controls.Add(Commands);
        Controls.Add(_caption);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_layingOut || Commands == null) return;
        _layingOut = true;
        try
        {
            int captionWidth = Math.Max(LogicalToDeviceUnits(104), _caption.PreferredWidth + LogicalToDeviceUnits(12));
            int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - captionWidth);
            _caption.Location = new Point(Padding.Left, Padding.Top + LogicalToDeviceUnits(8));
            Commands.SetBounds(Padding.Left + captionWidth, Padding.Top, width,
                Math.Max(LogicalToDeviceUnits(30), Commands.GetPreferredSize(new Size(width, 0)).Height));
            Height = Commands.Height + Padding.Vertical;
        }
        finally { _layingOut = false; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Commands == null) return;
        using var divider = new Pen(Theme.Line);
        int x = Commands.Left - LogicalToDeviceUnits(10);
        e.Graphics.DrawLine(divider, x, Padding.Top + LogicalToDeviceUnits(6),
            x, Height - Padding.Bottom - LogicalToDeviceUnits(6));
    }
}
