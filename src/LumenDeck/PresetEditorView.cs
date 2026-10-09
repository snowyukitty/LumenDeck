using System.Globalization;

namespace LumenDeck;

/// <summary>Captured values for previewing one display; never a live Monitor.</summary>
internal sealed record PresetPreviewDisplay(string Name, PanelDatabase.Profile Profile, bool UserProfile);

internal sealed record PresetEditorContext(IReadOnlyList<Presets.Level> Current,
    IReadOnlyList<PresetPreviewDisplay> Displays, int WithoutBrightness = 0);

/// <summary>
/// Local draft/preview UI for Day, Evening and Night targets. It has no Monitor,
/// worker, settings or hardware access: the owner supplies the save and decides
/// when the saved targets become active.
/// </summary>
internal sealed class PresetEditorView : UserControl
{
    private const string SaveNote = "Saving does not adjust any display. Choose a preset afterwards to request its new target. Custom levels are unaffected.";
    private readonly Func<IReadOnlyList<Presets.Level>, Task<SettingsSaveResult>> _save;
    private readonly PresetEditorContext _context;
    private readonly List<Row> _rows = new();
    private readonly Label _status;
    private readonly FlatButton _saveButton;
    private readonly FlatButton _defaults;
    private readonly FlatButton _cancel;
    private readonly TableLayoutPanel _content;
    private readonly Panel _scroll;
    private string _saveFeedback;
    public event Action Saved;
    public event Action CancelRequested;
    public bool IsSaving { get; private set; }
    internal Task PendingSave { get; private set; } = Task.CompletedTask;

    private sealed record Row(Presets.Level BuiltIn, WholeNumberInput Nits, WholeNumberInput Kelvin, Label Preview);

    public PresetEditorView(PresetEditorContext context, Func<IReadOnlyList<Presets.Level>, Task<SettingsSaveResult>> save)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Font = Theme.Body;
        BackColor = Theme.Base;
        ForeColor = Theme.Ink;
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, TabStop = false };
        _content = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(18), BackColor = Theme.Base,
        };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _scroll.Controls.Add(_content);
        Controls.Add(_scroll);
        Add(Copy("Preset targets", Theme.H1));
        Add(Copy("Day, Evening and Night request an estimated luminance and a warmth on every visible display. " +
                 "Each display's brightness estimate converts the target into its own brightness percentage."));
        Add(Copy(_context.Displays.Count == 0
            ? "No connected display reports a brightness control, so no brightness requests are previewed. Warmth still applies."
            : "Previews use the brightness estimate of each display connected when this window opened. They are model estimates, not measurements."));
        foreach (var builtIn in Presets.Levels)
        {
            var current = _context.Current.FirstOrDefault(l => string.Equals(l.Name, builtIn.Name, StringComparison.OrdinalIgnoreCase)) ?? builtIn;
            Add(Copy(builtIn.Name, Theme.H2));
            Add(Copy($"{builtIn.Description}. Default: {builtIn.Nits} nits, {builtIn.Kelvin} K."));
            var line = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
                Margin = new Padding(0, 0, 0, 4), BackColor = Theme.Base,
            };
            var nits = Number($"{builtIn.Name} target luminance (nits)", current.Nits, Presets.MinTargetNits, Presets.MaxTargetNits, 10);
            var kelvin = Number($"{builtIn.Name} warmth (kelvin)", current.Kelvin, GammaControl.MinKelvin, GammaControl.MaxKelvin, 100);
            line.Controls.AddRange([Unit("Target"), nits, Unit("nits"), Unit("Warmth", 18), kelvin, Unit("K")]);
            Add(line);
            var preview = Copy("");
            preview.AccessibleName = $"{builtIn.Name} brightness requests";
            preview.Margin = new Padding(0, 0, 0, 14);
            Add(preview);
            var row = new Row(builtIn, nits, kelvin, preview);
            nits.TextChanged += (_, _) => UpdateDraft();
            kelvin.TextChanged += (_, _) => UpdateDraft();
            _rows.Add(row);
        }
        if (_context.WithoutBrightness > 0)
            Add(Copy($"{_context.WithoutBrightness} display{(_context.WithoutBrightness == 1 ? " has" : "s have")} no brightness control; presets request only warmth there."));
        _status = Copy(SaveNote);
        _status.AccessibleRole = AccessibleRole.Alert;
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(18, 10, 18, 18), BackColor = Theme.Bar,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.Controls.Add(_status, 0, 0);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0) };
        _saveButton = new FlatButton("Save targets") { Width = 124, Primary = true, AccessibleName = "Save preset targets" };
        _defaults = new FlatButton("Use defaults") { Width = 118 };
        _cancel = new FlatButton("Cancel") { Width = 90 };
        _saveButton.Click += (_, _) => { if (_saveButton.Enabled && !IsSaving) BeginCommit(); };
        _defaults.Click += (_, _) => { if (_defaults.Enabled && !IsSaving) UseDefaults(); };
        _cancel.Click += (_, _) => { if (_cancel.Enabled && !IsSaving) CancelRequested?.Invoke(); };
        _defaults.AccessibleDescription = "Fill every field with the built-in targets. Nothing is saved until you choose Save targets.";
        actions.Controls.AddRange([_saveButton, _defaults, _cancel]);
        footer.Controls.Add(actions, 0, 1);
        Controls.Add(footer);
        SizeChanged += (_, _) => ConstrainLabels();
        UpdateDraft();
    }

    private static WholeNumberInput Number(string name, int value, int minimum, int maximum, int step) => new()
    {
        Minimum = minimum, Maximum = maximum, Value = Math.Clamp(value, minimum, maximum), Increment = step,
        Width = 92, BackColor = Theme.Sunken, ForeColor = Theme.Ink, AccessibleName = name,
        AccessibleDescription = $"Whole number from {minimum} to {maximum}.", Margin = new Padding(0, 0, 6, 4),
    };

    private static Label Unit(string text, int left = 0) => new()
    {
        Text = text, AutoSize = true, ForeColor = Theme.InkMuted, UseMnemonic = false,
        Margin = new Padding(left, 6, 6, 0),
    };

    private static Label Copy(string text, Font font = null) => new()
    {
        Text = text, AutoSize = true, Font = font ?? Theme.Body, ForeColor = font == null ? Theme.InkMuted : Theme.Ink,
        Margin = new Padding(0, 0, 0, 10), UseMnemonic = false,
    };

    private void Add(Control control) => _content.Controls.Add(control, 0, _content.RowCount++);

    private void ConstrainLabels()
    {
        int width = Math.Max(180, _scroll.ClientSize.Width - _content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (var label in _content.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
        foreach (var line in _content.Controls.OfType<FlowLayoutPanel>()) line.MaximumSize = new Size(width, 0);
        _status.MaximumSize = new Size(Math.Max(180, ClientSize.Width - (_status.Parent?.Padding.Horizontal ?? _content.Padding.Horizontal)), 0);
    }

    private bool TryDraft(out List<Presets.Level> levels, out List<string> invalid)
    {
        levels = new();
        invalid = new();
        foreach (var row in _rows)
        {
            bool nitsValid = row.Nits.TryGetDraft(out int nits);
            bool kelvinValid = row.Kelvin.TryGetDraft(out int kelvin);
            if (!nitsValid) invalid.Add($"{row.BuiltIn.Name} target");
            if (!kelvinValid) invalid.Add($"{row.BuiltIn.Name} warmth");
            row.Nits.ForeColor = nitsValid ? Theme.Ink : Theme.Warn;
            row.Kelvin.ForeColor = kelvinValid ? Theme.Ink : Theme.Warn;
            if (nitsValid && kelvinValid) levels.Add(row.BuiltIn with { Nits = nits, Kelvin = kelvin });
        }
        return invalid.Count == 0;
    }

    private void UpdateDraft()
    {
        if (IsSaving) return;
        bool valid = TryDraft(out var levels, out var invalid);
        foreach (var row in _rows)
        {
            var level = levels.FirstOrDefault(l => l.Name == row.BuiltIn.Name);
            // The stable accessible name replaces Text for screen readers, so
            // the changing estimate travels in the description.
            row.Preview.Text = row.Preview.AccessibleDescription =
                level == null ? "Preview unavailable until this preset's values are valid." : Preview(level);
            row.Preview.ForeColor = level == null ? Theme.Warn : Theme.Info;
        }
        _saveButton.Enabled = valid;
        if (!valid)
        {
            string correction = $"Check {string.Join(", ", invalid)}. Use whole numbers: " +
                $"{Presets.MinTargetNits}-{Presets.MaxTargetNits} nits and {GammaControl.MinKelvin}-{GammaControl.MaxKelvin} K. Nothing has been saved.";
            _status.Text = _saveButton.AccessibleDescription = correction;
            _status.ForeColor = Theme.Warn;
            return;
        }
        _status.Text = _saveFeedback ?? SaveNote;
        _status.ForeColor = _saveFeedback == null ? Theme.InkMuted : Theme.Warn;
        _saveButton.AccessibleDescription = "Save these preset targets. Saving does not adjust any display.";
    }

    private string Preview(Presets.Level level)
    {
        if (_context.Displays.Count == 0) return $"Requests {level.Kelvin} K warmth.";
        var lines = _context.Displays.Select(display =>
        {
            if (Presets.PercentFor(display.Profile, level.Nits) is not double percent)
                return $"{display.Name}: brightness unchanged (estimate has no range)";
            string limit = percent < 0 ? " (below floor)" : percent > 100 ? " (above peak)" : "";
            string generic = display.UserProfile ? "" : ", generic estimate";
            return $"{display.Name}: {Math.Clamp(percent, 0, 100):0}% brightness{limit}{generic}";
        });
        return $"Requests {level.Kelvin} K warmth and:\n" + string.Join("\n", lines);
    }

    private void UseDefaults()
    {
        _saveFeedback = null;
        foreach (var row in _rows)
        {
            Set(row.Nits, row.BuiltIn.Nits);
            Set(row.Kelvin, row.BuiltIn.Kelvin);
        }
        UpdateDraft();
    }

    private static void Set(WholeNumberInput input, int value)
    {
        // Replace typed text as well: a malformed draft keeps its text until
        // the user repairs it, and Value alone would not overwrite it.
        input.Value = value;
        input.Text = value.ToString(CultureInfo.CurrentCulture);
    }

    public void ShowSavePending()
    {
        if (!IsDisposed && !Disposing && IsSaving)
            _status.Text = "Finishing the preset save. Wait for its result before closing; no display is being adjusted.";
    }

    private void BeginCommit()
    {
        if (!TryDraft(out var levels, out _)) { UpdateDraft(); return; }
        IsSaving = true;
        foreach (var row in _rows) row.Nits.Enabled = row.Kelvin.Enabled = false;
        _saveButton.Enabled = _defaults.Enabled = _cancel.Enabled = false;
        _status.ForeColor = Theme.InkMuted;
        _status.Text = "Saving preset targets… Previous targets stay active until the save is confirmed.";
        PendingSave = CommitAsync(levels);
    }

    private async Task CommitAsync(IReadOnlyList<Presets.Level> levels)
    {
        SettingsSaveResult result;
        // The owner's save must always settle this window, even if it throws.
        try { result = await _save(levels); }
        catch (Exception) { result = SettingsSaveResult.Failed; }
        if (IsDisposed || Disposing) return;
        IsSaving = false;
        foreach (var row in _rows) row.Nits.Enabled = row.Kelvin.Enabled = true;
        _defaults.Enabled = _cancel.Enabled = true;
        _saveFeedback = result switch
        {
            SettingsSaveResult.Saved => null,
            SettingsSaveResult.Busy => "Another settings save is in progress. Previous targets remain active; try Save again shortly.",
            SettingsSaveResult.SourceChanged => "settings.json changed outside this window and was not overwritten. Previous targets remain active. Exit from the tray and reopen LumenDeck before saving.",
            SettingsSaveResult.Unavailable => "Settings could not be loaded this session, so targets cannot be saved. Previous targets remain active; repair settings storage and reopen.",
            _ => "Save was not confirmed. Previous targets remain active and your draft remains here. Check storage permissions before retrying.",
        };
        UpdateDraft();
        if (result == SettingsSaveResult.Saved) Saved?.Invoke();
    }
}
