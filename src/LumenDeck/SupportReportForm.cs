namespace LumenDeck;

internal sealed class SupportReportForm : Form
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this);
    }

    public SupportReportForm(SupportReport.Document report)
    {
        Text = "LumenDeck — Support report";
        Font = Theme.Body;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(480, 420);
        Size = new Size(720, 620);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        var view = new SupportReportView { Dock = DockStyle.Fill };
        view.SetReport(report);
        view.CloseRequested += Close;
        Controls.Add(view);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
