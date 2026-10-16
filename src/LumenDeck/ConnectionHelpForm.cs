namespace LumenDeck;

internal class ConnectionHelpForm : Form
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this);
    }

    private readonly ConnectionGuide _guide = new() { Dock = DockStyle.Fill };

    public ConnectionHelpForm()
    {
        Text = "LumenDeck — Connection help";
        Font = Theme.Body;
        Theme.Colors(this, back: () => Theme.Base, fore: () => Theme.Ink);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(460, 400);
        Size = new Size(640, 720);
        ShowInTaskbar = false;
        MinimizeBox = false;
        var footer = Theme.Colors(new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12),}, back: () => Theme.Bar);
        var close = new Button { Text = "Close", AutoSize = true, MinimumSize = new Size(100, 32), DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close();
        CancelButton = close;
        footer.Controls.Add(close);
        _guide.ReturnRequested += Close;
        _guide.ReportRequested += report =>
        {
            using var preview = new SupportReportForm(report);
            preview.ShowDialog(this);
        };
        Controls.Add(_guide);
        Controls.Add(footer);
    }

    public void SetMonitors(IEnumerable<Monitor> monitors, DisplayScanState state) => _guide.SetMonitors(monitors, state);

    internal virtual void Reveal()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        Activate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        if (keyData is Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End)
        { _guide.ScrollReference(keyData); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
