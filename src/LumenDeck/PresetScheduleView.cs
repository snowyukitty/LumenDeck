namespace LumenDeck;

internal sealed record ScheduleRowDraft(string Preset, bool Used, TimeSpan Start);

internal sealed record ScheduleDraft(bool Enabled, IReadOnlyList<ScheduleRowDraft> Rows);

internal sealed record ScheduleEditorContext(bool Enabled, IReadOnlyList<ScheduleRowDraft> Rows, IReadOnlyList<Presets.Level> Levels);

/// <summary>
/// What a schedule would do if it were active now: the period in force, and
/// whether that period would be requested immediately or once displays are
/// ready. The window computes the same outlook for its own checks, so the
/// preview cannot promise something the schedule will not do.
/// </summary>
internal sealed record ScheduleOutlook(PresetSchedule.Period Period, bool RequestsNow, bool Deferred);

/// <summary>
/// Local draft/preview UI. No clock, timer, settings, Monitor or hardware access:
/// the owner's outlook function supplies time and request state.
/// </summary>
internal sealed class PresetScheduleView : UserControl
{
    private const string Note = "Manual changes stay until the next scheduled time. Blanked displays are skipped. " +
        "The schedule works while LumenDeck runs, including from the notification area.";
    private readonly ScheduleEditorContext _context;
    private readonly Func<IReadOnlyCollection<PresetSchedule.Slot>, ScheduleOutlook> _outlook;
    private readonly Func<ScheduleDraft, Task<SettingsSaveResult>> _save;
    private readonly CheckBox _enabled;
    private readonly List<(string Preset, CheckBox Used, TextBox Start)> _rows = new();
    private readonly Label _preview;
    private readonly Label _status;
    private readonly FlatButton _saveButton;
    private readonly FlatButton _cancel;
    private readonly TableLayoutPanel _content;
    private readonly Panel _scroll;
    private string _saveFeedback;
    public event Action Saved;
    public event Action CancelRequested;
    public bool IsSaving { get; private set; }
    internal Task PendingSave { get; private set; } = Task.CompletedTask;

    public PresetScheduleView(ScheduleEditorContext context,
        Func<IReadOnlyCollection<PresetSchedule.Slot>, ScheduleOutlook> outlook, Func<ScheduleDraft, Task<SettingsSaveResult>> save)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _outlook = outlook ?? throw new ArgumentNullException(nameof(outlook));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Font = Theme.Body;
        Theme.Colors(this, back: () => Theme.Base, fore: () => Theme.Ink);
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, TabStop = false };
        _content = Theme.Colors(new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(18),
        }, back: () => Theme.Base);
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _scroll.Controls.Add(_content);
        Controls.Add(_scroll);
        Add(Copy("Preset schedule", Theme.H1));
        Add(Copy("Request a preset on every visible display when its time of day begins. " +
                 "Custom requests each display's remembered manual levels."));
        _enabled = Theme.Colors(new CheckBox
        {
            Text = "Change presets automatically at these times", AutoSize = true, Checked = context.Enabled, Margin = new Padding(0, 4, 0, 12),
            AccessibleName = "Change presets automatically",
        }, fore: () => Theme.Ink, back: () => Theme.Base);
        Add(_enabled);
        foreach (string preset in PresetSchedule.Names)
        {
            var row = context.Rows.FirstOrDefault(r => string.Equals(r.Preset, preset, StringComparison.OrdinalIgnoreCase))
                ?? new ScheduleRowDraft(preset, false, PresetSchedule.DefaultStarts[preset]);
            var line = Theme.Colors(new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
                Margin = new Padding(0, 0, 0, 6),
            }, back: () => Theme.Base);
            var used = Theme.Colors(new CheckBox
            {
                Text = preset, Checked = row.Used, Width = 96,
                AccessibleName = $"Use {preset} in the schedule", Margin = new Padding(0, 4, 6, 0), UseMnemonic = false,
            }, fore: () => Theme.Ink, back: () => Theme.Base);
            var start = Theme.Colors(new TextBox
            {
                Text = PresetSchedule.Format(row.Start), Width = 64,
                AccessibleName = $"{preset} start time", AccessibleDescription = "24-hour time, such as 21:30.",
                Margin = new Padding(0, 2, 10, 0), MaxLength = 5,
            }, back: () => Theme.Sunken, fore: () => Theme.Ink);
            var level = context.Levels.FirstOrDefault(l => l.Name == preset);
            line.Controls.AddRange([used, Muted("starts at"), start,
                Muted(level == null ? "your remembered levels" : $"{level.Nits} nits, {level.Kelvin} K")]);
            Add(line);
            used.CheckedChanged += (_, _) => UpdateDraft();
            start.TextChanged += (_, _) => UpdateDraft();
            _rows.Add((preset, used, start));
        }
        _preview = Copy("");
        _preview.AccessibleName = "Schedule preview";
        _preview.Margin = new Padding(0, 8, 0, 10);
        Add(_preview);
        Add(Copy(Note));
        _status = Copy("");
        _status.AccessibleRole = AccessibleRole.Alert;
        var footer = Theme.Colors(new TableLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(18, 10, 18, 18),
        }, back: () => Theme.Bar);
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.Controls.Add(_status, 0, 0);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0) };
        _saveButton = new FlatButton("Save schedule") { Width = 128, Primary = true, AccessibleName = "Save preset schedule" };
        _cancel = new FlatButton("Cancel") { Width = 90 };
        _saveButton.Click += (_, _) => { if (_saveButton.Enabled && !IsSaving) BeginCommit(); };
        _cancel.Click += (_, _) => { if (_cancel.Enabled && !IsSaving) CancelRequested?.Invoke(); };
        actions.Controls.AddRange([_saveButton, _cancel]);
        footer.Controls.Add(actions, 0, 1);
        Controls.Add(footer);
        _enabled.CheckedChanged += (_, _) => UpdateDraft();
        SizeChanged += (_, _) => ConstrainLabels();
        UpdateDraft();
    }

    private static Label Muted(string text) => Theme.Colors(new Label()
    {
        Text = text, AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 6, 8, 0),
    }, fore: () => Theme.InkMuted);

    private static Label Copy(string text, Font font = null) => Theme.Colors(new Label()
    {
        Text = text, AutoSize = true, Font = font ?? Theme.Body,
        Margin = new Padding(0, 0, 0, 10), UseMnemonic = false,
    }, fore: () => font == null ? Theme.InkMuted : Theme.Ink);

    private void Add(Control control) => _content.Controls.Add(control, 0, _content.RowCount++);

    private void ConstrainLabels()
    {
        int width = Math.Max(180, _scroll.ClientSize.Width - _content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (var label in _content.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
        foreach (var line in _content.Controls.OfType<FlowLayoutPanel>()) line.MaximumSize = new Size(width, 0);
        _enabled.MaximumSize = new Size(width, 0);
        _status.MaximumSize = new Size(Math.Max(180, ClientSize.Width - (_status.Parent?.Padding.Horizontal ?? _content.Padding.Horizontal)), 0);
    }

    /// <summary>The draft, or null with the reason it cannot be saved.</summary>
    private ScheduleDraft TryDraft(out string problem)
    {
        var rows = new List<ScheduleRowDraft>();
        var unreadable = new List<string>();
        foreach (var (preset, used, start) in _rows)
        {
            bool valid = PresetSchedule.TryParse(start.Text, out var time);
            Theme.Colors(start, fore: () => valid ? Theme.Ink : Theme.Warn);
            if (!valid) unreadable.Add(preset);
            rows.Add(new ScheduleRowDraft(preset, used.Checked, time));
        }
        problem = unreadable.Count > 0
            ? $"Check the {string.Join(", ", unreadable)} start time. Use 24-hour time from 00:00 to 23:59, such as 21:30."
            : _enabled.Checked ? PresetSchedule.Problem(Slots(rows)) : null;
        return problem == null ? new ScheduleDraft(_enabled.Checked, rows) : null;
    }

    private static PresetSchedule.Slot[] Slots(IEnumerable<ScheduleRowDraft> rows) =>
        rows.Where(r => r.Used).Select(r => new PresetSchedule.Slot(r.Preset, r.Start)).ToArray();

    /// <summary>Recompute the preview after the owner's time or request state changed.</summary>
    public void RefreshPreview() => UpdateDraft();

    private void UpdateDraft()
    {
        if (IsSaving) return;
        var draft = TryDraft(out string problem);
        _saveButton.Enabled = draft != null;
        if (draft == null)
        {
            _preview.Text = _preview.AccessibleDescription = "Preview unavailable until the schedule is valid.";
            Theme.Colors(_preview, fore: () => Theme.Warn);
            _status.Text = _saveButton.AccessibleDescription = problem + " Nothing has been saved.";
            Theme.Colors(_status, fore: () => Theme.Warn);
            return;
        }
        _preview.Text = _preview.AccessibleDescription = Preview(draft);
        Theme.Colors(_preview, fore: () => Theme.Info);
        _status.Text = _saveFeedback ?? (draft.Enabled
            ? "Saving takes effect once the settings file confirms it."
            : "Saving keeps these times for later.");
        Theme.Colors(_status, fore: () => _saveFeedback == null ? Theme.InkMuted : Theme.Warn);
        _saveButton.AccessibleDescription = "Save this schedule. " + _preview.Text;
    }

    private string Preview(ScheduleDraft draft)
    {
        if (!draft.Enabled) return "The schedule is off. Saving adjusts no display.";
        var outlook = _outlook(Slots(draft.Rows));
        var period = outlook.Period;
        string now = $"Now: {period.Preset} (since {PresetSchedule.Format(period.Start.TimeOfDay)}). " +
            $"Next: {period.NextPreset} at {PresetSchedule.Format(period.Next.TimeOfDay)}.";
        return now + "\n" + (outlook.RequestsNow
            ? $"Saving requests {period.Preset} on every visible display now."
            : outlook.Deferred
            ? $"Displays are not ready, so saving requests {period.Preset} once they are."
            : $"{period.Preset} was already requested for this period, so saving adjusts no display now.");
    }

    public void ShowSavePending()
    {
        if (!IsDisposed && !Disposing && IsSaving)
            _status.Text = "Finishing the schedule save. Wait for its result before closing; no display is being adjusted yet.";
    }

    private void BeginCommit()
    {
        var draft = TryDraft(out _);
        if (draft == null) { UpdateDraft(); return; }
        IsSaving = true;
        SetEditable(false);
        Theme.Colors(_status, fore: () => Theme.InkMuted);
        _status.Text = "Saving schedule… The previous schedule stays in effect until the save is confirmed.";
        PendingSave = CommitAsync(draft);
    }

    private void SetEditable(bool editable)
    {
        _enabled.Enabled = _saveButton.Enabled = _cancel.Enabled = editable;
        foreach (var (_, used, start) in _rows) used.Enabled = start.Enabled = editable;
    }

    private async Task CommitAsync(ScheduleDraft draft)
    {
        SettingsSaveResult result;
        try { result = await _save(draft); }
        catch (Exception) { result = SettingsSaveResult.Failed; }
        if (IsDisposed || Disposing) return;
        IsSaving = false;
        SetEditable(true);
        _saveFeedback = result switch
        {
            SettingsSaveResult.Saved => null,
            SettingsSaveResult.Busy => "Another settings save is in progress. The previous schedule remains in effect; try Save again shortly.",
            SettingsSaveResult.SourceChanged => "settings.json changed outside this window and was not overwritten. The previous schedule remains in effect. Exit from the tray and reopen LumenDeck before saving.",
            SettingsSaveResult.Unavailable => "Settings could not be loaded this session, so the schedule cannot be saved. The previous schedule remains in effect; repair settings storage and reopen.",
            _ => "Save was not confirmed. The previous schedule remains in effect and your draft remains here. Check storage permissions before retrying.",
        };
        UpdateDraft();
        if (result == SettingsSaveResult.Saved) Saved?.Invoke();
    }
}
