namespace LumenDeck;

internal sealed record ProfileEditorContext(string ModelName, bool InternalPanel, double? BrightnessPercent,
    string DisplayName = null, IReadOnlyList<Presets.Level> PresetLevels = null);

/// <summary>Local draft/preview UI. No Monitor, worker, settings or hardware API.</summary>
internal sealed class ProfileEditorView : UserControl
{
    private const string SaveNote = "Save keeps a backup of an existing file. Notes are stored with the profile and excluded from support reports.";
    private string _saveFeedback;
    private readonly Func<PanelDatabase.Profile, bool, ProfileSaveResult> _write;
    private readonly ProfileEditorContext _context;
    private readonly WholeNumberInput _peak;
    private readonly WholeNumberInput _floor;
    private readonly CheckBox _measured;
    private readonly TextBox _note;
    private readonly Label _preview;
    private readonly Label _status;
    private readonly FlatButton _save;
    private readonly FlatButton _remove;
    private readonly FlatButton _cancel;
    private readonly TableLayoutPanel _content;
    private readonly Panel _scroll;
    public event Action Saved;
    public event Action CancelRequested;
    public bool IsSaving { get; private set; }
    internal Task PendingSave { get; private set; } = Task.CompletedTask;

    public ProfileEditorView(PanelProfileStore store, ProfileEditorContext context,
        Func<PanelDatabase.Profile, bool, ProfileSaveResult> write = null)
    {
        _write = write ?? ((profile, remove) => store.Save(profile, remove));
        HasRemovableEntry = store.HasExactEntry;
        _context = context;
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
        Add(Copy("Brightness estimate", Theme.H1));
        Add(Copy(context.DisplayName ?? context.ModelName, Theme.H2));
        Add(Copy("Saved for model names containing this full name. Identical models share it; a more specific existing match can take precedence."));
        Add(Copy("This changes estimated nits and future preset calculations. Saving does not adjust the screen. All figures below are model estimates."));
        Add(Copy(store.CurrentSource switch
        {
            "exact" => "Starting from this model's full-name profile. Save updates that entry only.",
            "broader" => "Starting from a broader matching profile. Save creates a full-name entry and keeps the broader profile.",
            _ => "Starting from generic estimates. Save creates a full-name user profile.",
        }));
        var profile = store.CurrentProfile;
        Add(Copy("Peak luminance (nits)", Theme.H2));
        _peak = Number("Peak luminance", profile.PeakNits);
        Add(_peak);
        Add(Copy("Floor luminance at brightness 0 (nits)", Theme.H2));
        _floor = Number("Floor luminance", profile.FloorNits);
        Add(_floor);
        _measured = Theme.Colors(new CheckBox
        {
            Text = "I measured both endpoints with an instrument", AutoSize = true,
            Checked = profile.Measured,
            AccessibleName = "Declare measured peak and floor endpoints", Margin = new Padding(0, 8, 0, 6),
        }, fore: () => Theme.Ink, back: () => Theme.Base);
        Add(_measured);
        Add(Copy("Leave unchecked for spec-sheet values or assumptions. The app cannot verify measurements or the panel's full brightness curve."));
        Add(Copy("Sources, picture mode and assumptions", Theme.H2));
        _note = Theme.Colors(new TextBox
        {
            Multiline = true, Height = 78, Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical,
            Text = store.CurrentSource == "generic" ? "" : profile.Note ?? "",
            AccessibleName = "Profile sources and conditions", Margin = new Padding(0, 0, 0, 10),
        }, back: () => Theme.Sunken, fore: () => Theme.Ink);
        Add(_note);
        _preview = Copy("");
        _preview.AccessibleName = "Draft luminance and preset estimates";
        Add(_preview);
        var fallback = store.RemovalProfile;
        if (store.HasExactEntry)
            Add(Copy($"Remove affects only this full-name entry. The next match uses {(store.RemovalUsesUserProfile ? "a broader user profile" : "a generic profile")}: peak {fallback.PeakNits}, floor {fallback.FloorNits} nits. No screen adjustment."));
        _status = Copy(SaveNote);
        _status.AccessibleRole = AccessibleRole.Alert;
        var footer = Theme.Colors(new TableLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(18, 10, 18, 18),
        }, back: () => Theme.Bar);
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.Controls.Add(_status, 0, 0);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0),
        };
        _save = new FlatButton("Save estimate") { Width = 132, Primary = true, AccessibleName = "Save brightness estimate profile" };
        _remove = new FlatButton("Remove this profile") { Width = 160, Enabled = store.HasExactEntry };
        _cancel = new FlatButton("Cancel") { Width = 90 };
        _save.Click += (_, _) => { if (_save.Enabled && !IsSaving) BeginCommit(remove: false); };
        _remove.Click += (_, _) => { if (_remove.Enabled && !IsSaving) BeginCommit(remove: true); };
        _cancel.Click += (_, _) => { if (_cancel.Enabled && !IsSaving) CancelRequested?.Invoke(); };
        actions.Controls.AddRange([_save, _remove, _cancel]);
        footer.Controls.Add(actions, 0, 1);
        Controls.Add(footer);
        _peak.TextChanged += (_, _) => UpdatePreview();
        _floor.TextChanged += (_, _) => UpdatePreview();
        SizeChanged += (_, _) => ConstrainLabels();
        UpdatePreview();
    }

    private WholeNumberInput Number(string name, int value) => Theme.Colors(new WholeNumberInput()
    {
        Minimum = 0, Maximum = int.MaxValue, Value = Math.Max(0, value), Width = 156, AccessibleName = name,
        Margin = new Padding(0, 0, 0, 8), ThousandsSeparator = true,
    }, back: () => Theme.Sunken, fore: () => Theme.Ink);

    private Label Copy(string text, Font font = null) => Theme.Colors(new Label()
    {
        Text = text, AutoSize = true, Font = font ?? Theme.Body,
        Margin = new Padding(0, 0, 0, 10), UseMnemonic = false,
    }, fore: () => font == null ? Theme.InkMuted : Theme.Ink);

    private void Add(Control control) => _content.Controls.Add(control, 0, _content.RowCount++);

    private void ConstrainLabels()
    {
        int width = Math.Max(180, _scroll.ClientSize.Width - _content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (var label in _content.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
        _measured.MaximumSize = new Size(width, 0);
        _status.MaximumSize = new Size(Math.Max(180, ClientSize.Width - (_status.Parent?.Padding.Horizontal ?? _content.Padding.Horizontal)), 0);
    }

    private bool TryDraft(out PanelDatabase.Profile draft)
    {
        bool peakValid = _peak.TryGetDraft(out int peak);
        bool floorValid = _floor.TryGetDraft(out int floor);
        draft = new(peak, floor, _measured.Checked, _note.Text);
        return peakValid && floorValid;
    }

    private void UpdatePreview()
    {
        if (IsSaving) return;
        bool numericDraft = TryDraft(out var draft);
        _save.Enabled = numericDraft && PanelDatabase.IsValid(draft);
        _peak.AccessibleDescription = "Peak luminance must be above zero nits (cd/m\u00b2).";
        _floor.AccessibleDescription = "Floor luminance must be below the peak, in nits (cd/m\u00b2).";
        if (!_save.Enabled)
        {
            string correction = !numericDraft
                ? "Enter a whole number of nits for each endpoint. Fractions, negative values and oversized numbers cannot be saved."
                : draft.PeakNits <= 0
                ? "Enter a peak above zero nits (cd/m\u00b2) to enable Save."
                : "Enter a floor below the peak to enable Save. Both use nits (cd/m\u00b2).";
            _preview.Text = correction;
            Theme.Colors(_preview, fore: () => Theme.Warn);
            _status.Text = correction;
            Theme.Colors(_status, fore: () => Theme.Warn);
            _save.AccessibleDescription = correction;
            return;
        }
        _status.Text = _saveFeedback ?? SaveNote;
        Theme.Colors(_status, fore: () => _saveFeedback == null ? Theme.InkMuted : Theme.Warn);
        _save.AccessibleDescription = "Save this valid brightness estimate. Saving does not adjust the screen.";
        string current = _context.BrightnessPercent is double percent
            ? $"Brightness captured at opening: estimated {Math.Round(draft.FloorNits + (draft.PeakNits - draft.FloorNits) * percent / 100):0} nits."
            : "Current luminance unavailable: no brightness control was detected.";
        var presets = (_context.PresetLevels ?? Presets.Levels).Select(level =>
        {
            double percent = Presets.PercentFor(draft, level.Nits) ?? 0;
            string limit = percent < 0 ? " (below floor)" : percent > 100 ? " (above peak)" : "";
            return $"{level.Name}: {Math.Clamp(percent, 0, 100):0}%{limit}";
        });
        _preview.Text = current + "\nPreset brightness targets: " + string.Join(" · ", presets);
        Theme.Colors(_preview, fore: () => Theme.Info);
    }

    public void ShowSavePending()
    {
        if (!IsDisposed && !Disposing && IsSaving)
            _status.Text = "Finishing the profile save. Wait for its result before closing; the screen is not being adjusted.";
    }

    private void BeginCommit(bool remove)
    {
        bool numericDraft = TryDraft(out var draft);
        if (!remove && (!numericDraft || !PanelDatabase.IsValid(draft))) { UpdatePreview(); return; }
        IsSaving = true;
        _peak.Enabled = _floor.Enabled = _measured.Enabled = _note.Enabled = false;
        _save.Enabled = _remove.Enabled = _cancel.Enabled = false;
        Theme.Colors(_status, fore: () => Theme.InkMuted);
        _status.Text = "Saving profile… Wait for the result before closing. The screen is not being adjusted.";
        PendingSave = CommitAsync(draft, remove);
    }

    private async Task CommitAsync(PanelDatabase.Profile draft, bool remove)
    {
        ProfileSaveResult result;
        var writer = _write;
        try { result = await Task.Run(() => writer(draft, remove)); }
        // The boundary must recover UI state even if writer construction or an
        // unexpected filesystem exception escapes the core result contract.
        catch (Exception)
        { result = ProfileSaveResult.Failed; }
        if (IsDisposed || Disposing) return;
        IsSaving = false;
        _peak.Enabled = _floor.Enabled = _measured.Enabled = _note.Enabled = _cancel.Enabled = true;
        _remove.Enabled = HasRemovableEntry;
        _saveFeedback = result == ProfileSaveResult.Saved ? null : result switch
        {
            ProfileSaveResult.SourceChanged => "The profile file changed while this editor was open. Cancel and reopen to review the latest file; your draft has not overwritten it.",
            ProfileSaveResult.Busy => "Another profile save is in progress. Try again shortly.",
            ProfileSaveResult.InvalidValues => "Use a positive peak and a floor below it, or reopen to review the entry.",
            _ => "Save was not confirmed. Your draft remains here. Check panels.json and profile-backups before retrying; the editor did not adjust the screen.",
        };
        UpdatePreview();
        if (result == ProfileSaveResult.Saved) Saved?.Invoke();
    }

    private bool HasRemovableEntry { get; }
}
