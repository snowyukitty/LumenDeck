namespace LumenDeck;

internal sealed record ReferenceSection(string Heading, string Body);

/// <summary>Read-only, scrollable reference content with no application collaborators.</summary>
internal sealed class ReferenceView : UserControl
{
    private readonly TableLayoutPanel _content;
    private bool _layingOut;

    public ReferenceView(string title, string introduction, IEnumerable<ReferenceSection> sections)
    {
        BackColor = Theme.Base;
        ForeColor = Theme.Ink;
        Font = Theme.Body;
        AutoScroll = true;
        Padding = new Padding(20);
        AccessibleName = title;
        AccessibleRole = AccessibleRole.Grouping;
        _content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, BackColor = Color.Transparent };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddText(title, Theme.H1, Theme.Ink);
        AddText(introduction, Theme.Body, Theme.InkMuted);
        foreach (var section in sections)
        {
            AddText(section.Heading, Theme.H2, Theme.Ink);
            AddText(section.Body, Theme.Body, Theme.InkMuted);
        }
        Controls.Add(_content);
    }

    private void AddText(string text, Font font, Color color) => _content.Controls.Add(new Label
    {
        Text = text, Font = font, ForeColor = color, AutoSize = true, UseMnemonic = false, BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 14),
    });

    public void ScrollReference(Keys key)
    {
        int end = Math.Max(0, _content.Height + Padding.Vertical - ClientSize.Height);
        int page = Math.Max(1, ClientSize.Height - LogicalToDeviceUnits(40));
        int next = key switch
        {
            Keys.Home => 0, Keys.End => end,
            Keys.PageUp => -AutoScrollPosition.Y - page,
            Keys.PageDown => -AutoScrollPosition.Y + page,
            _ => -AutoScrollPosition.Y,
        };
        AutoScrollPosition = new Point(0, Math.Clamp(next, 0, end));
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_content != null && !_layingOut)
        {
            _layingOut = true;
            try
            {
                int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                foreach (var label in _content.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
            }
            finally { _layingOut = false; }
        }
        base.OnLayout(e);
    }
}
