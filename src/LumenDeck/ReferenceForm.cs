namespace LumenDeck;

/// <summary>Shared offline reference window with scrolling and a pinned Close action.</summary>
internal class ReferenceForm : Form
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this);
    }

    private readonly ReferenceView _guide;
    protected FlowLayoutPanel Footer { get; }

    protected ReferenceForm(string title, string introduction, IEnumerable<ReferenceSection> sections)
    {
        Text = $"LumenDeck — {title}";
        Font = Theme.Body;
        BackColor = Theme.Base;
        ForeColor = Theme.Ink;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(460, 380);
        Size = new Size(640, 720);
        ShowInTaskbar = false;
        ShowIcon = false;
        MinimizeBox = false;
        _guide = new ReferenceView(title, introduction, sections) { Dock = DockStyle.Fill };
        Footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = Theme.Bar };
        var close = new Button { Text = "Close", AutoSize = true, MinimumSize = new Size(100, 32), DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close();
        CancelButton = close;
        Footer.Controls.Add(close);
        Controls.Add(_guide);
        Controls.Add(Footer);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        if (keyData is Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End)
        { _guide.ScrollReference(keyData); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    internal virtual void Reveal()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        Activate();
    }
}
