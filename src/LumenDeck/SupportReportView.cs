using System.Windows.Forms.Automation;

namespace LumenDeck;

/// <summary>Exact report preview; clipboard access occurs only through the Copy action.</summary>
internal sealed class SupportReportView : UserControl
{
    private readonly Action<string> _copy;
    private readonly TextBox _preview;
    private readonly Label _status;
    private readonly FlatButton _copyButton;
    public event Action CloseRequested;

    public SupportReportView(Action<string> copy = null)
    {
        _copy = copy ?? (text => Clipboard.SetText(text));
        Theme.Colors(this, back: () => Theme.Base, fore: () => Theme.Ink);
        Font = Theme.Body;
        Padding = new Padding(16);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true,
            Padding = new Padding(0, 10, 0, 0),
        };
        _copyButton = new FlatButton("Copy report") { AutoSize = true, MinimumSize = new Size(120, 30), Enabled = false, Primary = true };
        _copyButton.Click += (_, _) => CopyReport();
        var close = new FlatButton("Close") { AutoSize = true, MinimumSize = new Size(84, 30) };
        close.Click += (_, _) => CloseRequested?.Invoke();
        actions.Controls.AddRange([_copyButton, close]);
        _status = Theme.Colors(new Label
        {
            Text = "Review the report, then copy it to your issue. Nothing is uploaded.",
            Dock = DockStyle.Bottom, AutoSize = true,
            UseMnemonic = false, AccessibleRole = AccessibleRole.StatusBar,
        }, fore: () => Theme.InkMuted);
        _preview = Theme.Colors(new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, WordWrap = true, Font = Theme.Mono, BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = "Support report preview", TabIndex = 0,
        }, back: () => Theme.Sunken, fore: () => Theme.Ink);
        Controls.Add(_preview);
        Controls.Add(_status);
        Controls.Add(actions);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_status != null)
        {
            int width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            if (_status.MaximumSize.Width != width) _status.MaximumSize = new Size(width, 0);
        }
        base.OnLayout(e);
    }

    public void SetReport(SupportReport.Document report)
    {
        _preview.Text = SupportReport.Text(report);
        _preview.SelectionStart = 0;
        _copyButton.Enabled = true;
        _status.Text = "Review the report, then copy it to your issue. Nothing is uploaded.";
    }

    private void CopyReport()
    {
        if (!_copyButton.Enabled) return;
        bool copied = false;
        try
        {
            _copy(_preview.Text);
            copied = true;
        }
        catch (Exception)
        {
            // Clipboard failures can contain system or application paths.
        }
        // Clipboard callbacks can close the preview before returning.
        if (IsDisposed || Disposing || _status.IsDisposed) return;
        _status.Text = copied ? "Report copied. Paste it into your issue when ready."
            : "Could not copy. Select the preview and press Ctrl+C, or try again.";
        _status.AccessibilityObject.RaiseAutomationNotification(
            copied ? AutomationNotificationKind.ActionCompleted : AutomationNotificationKind.ActionAborted,
            AutomationNotificationProcessing.ImportantMostRecent, _status.Text);
    }
}
