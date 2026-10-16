namespace LumenDeck;

internal sealed record ExactValueContext(string Target, string Unit, int Minimum, int Maximum, int Current);

/// <summary>Numeric-entry presentation; the originating slider validates target lifetime.</summary>
internal class ExactValueForm : Form
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this);
    }

    private readonly WholeNumberInput _input;
    private readonly Label _feedback;
    private readonly Button _apply;
    private int _selectedValue;
    private readonly Label _target;
    private readonly Label _range;
    private readonly Label _note;
    private readonly TableLayoutPanel _content;
    private readonly Panel _body;
    public int SelectedValue => _selectedValue;

    public ExactValueForm(ExactValueContext context)
    {
        _selectedValue = context.Current;
        Text = "LumenDeck — Exact value";
        Theme.Colors(this, back: () => Theme.Base, fore: () => Theme.Ink);
        Font = Theme.Body;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ShowIcon = false;
        MinimizeBox = MaximizeBox = false;
        MinimumSize = new Size(400, 300);
        ClientSize = new Size(440, 280);
        _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
        _content = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _target = Label(context.Target, Theme.H2, () => Theme.Ink);
        _range = Label($"Range: {context.Minimum}–{context.Maximum} {context.Unit}", Theme.Body, () => Theme.InkMuted);
        _input = new WholeNumberInput
        {
            Minimum = context.Minimum, Maximum = context.Maximum, Value = context.Current,
            Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16),
            AccessibleName = $"{context.Target}: exact value in {context.Unit}",
            AccessibleDescription = _range.Text,
        };
        _content.Controls.Add(_input);
        _note = Label("Apply sends an adjustment request. Check the screen and any card warnings afterward. Cancel leaves this entry unapplied.", Theme.Small, () => Theme.InkMuted);
        _body.Controls.Add(_content);
        var footer = Theme.Colors(new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(12),}, back: () => Theme.Bar);
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _feedback = Theme.Colors(new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 8),
            AccessibleRole = AccessibleRole.Alert, UseMnemonic = false }, fore: () => Theme.Warn);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty };
        var cancel = new Button { Text = "Cancel", CausesValidation = false, DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 32) };
        _apply = new Button { Text = "Apply", AutoSize = true, MinimumSize = new Size(90, 32) };
        foreach (var button in new[] { cancel, _apply }) Theme.StyleButton(button);
        _apply.Click += (_, _) =>
        {
            if (!_input.TryGetDraft(out int value)) { UpdateValidation(context); return; }
            _selectedValue = value;
            DialogResult = DialogResult.OK;
        };
        _input.TextChanged += (_, _) => UpdateValidation(context);
        actions.Controls.AddRange([cancel, _apply]);
        footer.Controls.Add(_feedback, 0, 0);
        footer.Controls.Add(actions, 0, 1);
        AcceptButton = _apply;
        CancelButton = cancel;
        Controls.Add(_body);
        Controls.Add(footer);
        UpdateValidation(context);
        Shown += (_, _) => { _input.Focus(); _input.Select(0, _input.Text.Length); };
    }

    private void UpdateValidation(ExactValueContext context)
    {
        bool valid = _input.TryGetDraft(out _);
        _apply.Enabled = valid;
        _feedback.Text = valid ? "" : $"Enter a whole number from {context.Minimum} to {context.Maximum} {context.Unit}. No adjustment has been sent.";
        _input.AccessibleDescription = valid ? _range.Text : _feedback.Text;
        _apply.AccessibleDescription = valid ? "Apply this exact value to the selected control." : _feedback.Text;
    }

    private Label Label(string text, Font font, Func<Color> colour)
    {
        var label = Theme.Colors(new Label { Text = text, Font = font, AutoSize = true,
            Margin = new Padding(0, 0, 0, 14), UseMnemonic = false }, fore: () => colour());
        _content.Controls.Add(label);
        return label;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_body == null || _content == null) return;
        int width = Math.Max(1, _body.ClientSize.Width - _body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (var label in _content.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
        if (_feedback != null) _feedback.MaximumSize = new Size(Math.Max(1, ClientSize.Width - _feedback.Parent.Padding.Horizontal), 0);
    }
}
