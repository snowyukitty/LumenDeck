namespace LumenDeck;

internal enum AdjustmentControl { Brightness, Contrast, Warmth, ExtraControls }

/// <summary>
/// Keeps an unconfirmed adjustment visible on its display until acknowledged.
/// Presentation only: dismissing a notice never retries or confirms a write.
/// </summary>
internal sealed class AdjustmentNotice : FlowLayoutPanel
{
    private readonly HashSet<AdjustmentControl> _failed = new();
    private readonly Label _message;
    private readonly string _displayContext;
    public event Action ConnectionHelpRequested;
    public string Message => _message.Text;

    public AdjustmentNotice(string displayContext, bool hasHelp)
    {
        _displayContext = string.IsNullOrWhiteSpace(displayContext) ? "Display" : displayContext;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = true;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 10);
        AccessibleRole = AccessibleRole.Alert;
        Visible = false;
        _message = new Label
        {
            AutoSize = true, Font = Theme.Small, ForeColor = Theme.Warn,
            BackColor = Color.Transparent, Margin = new Padding(0, 0, 8, 6),
        };
        Controls.Add(_message);
        var help = Link("Connection help");
        help.AccessibleName = $"{_displayContext}: Connection help";
        help.Visible = hasHelp;
        help.LinkClicked += (_, _) => ConnectionHelpRequested?.Invoke();
        Controls.Add(help);
        var dismiss = Link("Dismiss notice");
        dismiss.AccessibleName = $"{_displayContext}: Dismiss adjustment notice";
        dismiss.AccessibleDescription = "Hides this notice without retrying or confirming the adjustment.";
        dismiss.LinkClicked += (_, _) => Dismiss();
        Controls.Add(dismiss);
    }

    public void RecordFailure(AdjustmentControl control)
    {
        if (!Enum.IsDefined(control)) return;
        if (!_failed.Add(control)) return;
        var names = _failed.OrderBy(value => value).Select(value => value switch
        {
            AdjustmentControl.Brightness => "brightness",
            AdjustmentControl.Contrast => "contrast",
            AdjustmentControl.Warmth => "warmth",
            _ => "extra controls",
        });
        _message.Text = "Earlier adjustment not confirmed: " + string.Join(", ", names) +
            ".\nCheck the screen before relying on the displayed values.";
        AccessibleName = $"{_displayContext}: {_message.Text}";
        _message.AccessibleName = AccessibleName;
        Visible = true;
    }

    public void Dismiss()
    {
        _failed.Clear();
        _message.Text = "";
        AccessibleName = "";
        _message.AccessibleName = "";
        Visible = false;
    }

    private static LinkLabel Link(string text) => new()
    {
        Text = text, AutoSize = true, Font = Theme.Small,
        LinkColor = Theme.Info, ActiveLinkColor = Theme.AmberLight,
        VisitedLinkColor = Theme.Info, BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 14, 6),
    };
}
