namespace LumenDeck;

/// <summary>Display discovery guidance without storage or monitor collaborators.</summary>
internal sealed class DisplayScanView : Panel
{
    private readonly TableLayoutPanel _content;
    private readonly Label _heading;
    private readonly Label _description;
    private readonly Label _refreshNote;
    private readonly FlowLayoutPanel _actions;
    private readonly FlatButton _refresh;
    private readonly FlatButton _help;
    private bool _layingOut;
    public event Action RefreshRequested;
    public event Action ConnectionHelpRequested;

    public DisplayScanView(DisplayScanState state)
    {
        Theme.Colors(this, back: () => Theme.Card);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(20);
        Margin = new Padding(0, 0, 0, 12);
        AccessibleRole = AccessibleRole.Grouping;
        _content = new TableLayoutPanel
        {
            ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent, Margin = Padding.Empty,
        };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _heading = TextLine(Theme.H1, () => Theme.Ink);
        _description = TextLine(Theme.Body, () => Theme.InkMuted);
        _actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = Padding.Empty };
        _refresh = new FlatButton("Refresh") { AutoSize = true, MinimumSize = new Size(100, 30), Primary = true, Margin = new Padding(0, 0, 12, 8) };
        _help = new FlatButton("Connection help") { AutoSize = true, MinimumSize = new Size(150, 30), Margin = new Padding(0, 0, 0, 8) };
        _refresh.Click += (_, _) => { if (_refresh.Enabled) RefreshRequested?.Invoke(); };
        _help.Click += (_, _) => ConnectionHelpRequested?.Invoke();
        _actions.Controls.AddRange([_refresh, _help]);
        _content.Controls.Add(_actions);
        _refreshNote = TextLine(Theme.Small, () => Theme.InkMuted);
        Controls.Add(_content);
        SetState(state);
    }

    private Label TextLine(Font font, Func<Color> color)
    {
        var label = Theme.Colors(new Label { AutoSize = true, Font = font, BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12) }, fore: () => color());
        _content.Controls.Add(label);
        return label;
    }

    public void SetState(DisplayScanState state)
    {
        _heading.Text = state switch
        {
            DisplayScanState.Reading => "Reading your displays",
            DisplayScanState.Failed => "Could not read your displays",
            DisplayScanState.Ready => "No displays found",
            _ => "Your display controls will appear here",
        };
        _description.Text = state switch
        {
            DisplayScanState.Reading => "Monitor-control queries can take time. You can open Connection help while LumenDeck reads your displays.",
            DisplayScanState.Failed => "This scan did not finish. Try Refresh, or open Connection help for offline checks and a support report.",
            DisplayScanState.Ready => "Check that your display is connected and enabled in Windows Settings > System > Display. Then try Refresh. A missing slider on a detected display is a different issue; Connection help covers both.",
            _ => "LumenDeck checks the displays when this window opens. Connection help is available offline.",
        };
        Theme.Colors(_heading, fore: () => state == DisplayScanState.Failed ? Theme.Warn : Theme.Ink);
        _refresh.Enabled = state is DisplayScanState.Ready or DisplayScanState.Failed;
        _refresh.AccessibleDescription = _refreshNote.Text =
            "Refresh reads displays again and may reapply saved warmth or recover active blackouts.";
        _help.AccessibleDescription = "Open offline connection checks and a minimal support report. Opening help does not start another display scan.";
        AccessibleName = _heading.Text;
        AccessibleDescription = _description.Text;
        PerformLayout();
    }

    public void SetWidth(int width)
    {
        MinimumSize = new Size(width, 0);
        MaximumSize = new Size(width, 0);
        Width = width;
        PerformLayout();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (_content == null) return base.GetPreferredSize(proposedSize);
        int width = MaximumSize.Width > 0 ? MaximumSize.Width : Width;
        return new Size(width, _content.GetPreferredSize(new Size(Math.Max(1, width - Padding.Horizontal), 0)).Height + Padding.Vertical);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_content == null || _layingOut) return;
        _layingOut = true;
        try
        {
            int width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            _content.Location = new Point(Padding.Left, Padding.Top);
            _content.MaximumSize = new Size(width, 0);
            _content.Width = width;
            foreach (var label in _content.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
            _actions.MaximumSize = new Size(width, 0);
            foreach (var button in _actions.Controls.OfType<FlatButton>())
                button.MaximumSize = new Size(Math.Max(button.MinimumSize.Width, width - button.Margin.Horizontal), 0);
            _content.PerformLayout();
            Height = _content.GetPreferredSize(new Size(width, 0)).Height + Padding.Vertical;
        }
        finally { _layingOut = false; }
    }
}
