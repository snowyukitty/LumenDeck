namespace LumenDeck;

/// <summary>Shared command presentation, with no monitor I/O or settings side effects.</summary>
internal sealed class DeskCommandBar : Panel
{
    private readonly ToolTip _tips = new();
    private CommandSection _modes;
    private CommandSection _night;
    private bool _expanded;
    private int _heightLimit;
    private int _heightLimitLogical;
    private bool _nightActive;
    private readonly FlatButton _expand = new("Desk actions…") { Width = 120, Quiet = true };
    public Dictionary<string, FlatButton> ModeButtons { get; } = new(StringComparer.OrdinalIgnoreCase);
    public FlatButton IdentifyButton { get; } = new("Identify") { Width = 72 };
    public FlatButton RefreshButton { get; } = new("Refresh") { Width = 72 };
    public FlatButton WarmthOffButton { get; } = new("Warmth off") { Width = 100 };
    public FlatButton HelpButton { get; } = new("Help") { Width = 52, Quiet = true, AccessibleName = "Connection help" };
    public FlatButton SettingsButton { get; } = new("Settings") { Width = 76, Quiet = true };
    public FlatButton NightButton { get; } = new("Night saver") { Width = 138 };
    public ComboBox WorkMonitor { get; } = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 260,
        BackColor = Theme.Base, ForeColor = Theme.Ink,
        AccessibleName = "Keep working on",
    };
    /// <summary>Carries the preset name; the owner resolves its active target at click time.</summary>
    public event Action<string> LevelPicked;
    public event Action CustomPicked;

    public DeskCommandBar()
    {
        Dock = DockStyle.Top;
        DoubleBuffered = true;
        BackColor = Theme.Bar;
        var modes = new CommandSection("All monitors") { TabIndex = 1 };
        var tools = new CommandSection("") { TabIndex = 0 };
        var night = new CommandSection("Night saver") { TabIndex = 2 };
        _modes = modes;
        _night = night;
        _expand.Click += (_, _) => SetExpanded(!_expanded);
        _expand.SetHelp(_tips, "Show or hide desk-wide presets, warmth restoration and the Night saver work-display choice. Night saver and its restore command stay in the toolbar.");
        foreach (var level in Presets.Levels)
        {
            string name = level.Name;
            var button = new FlatButton(level.Name) { Width = 82 };
            button.Click += (_, _) => LevelPicked?.Invoke(name);
            ModeButtons[level.Name] = button;
            modes.Commands.Controls.Add(button);
        }
        var custom = new FlatButton(Presets.CustomName) { Width = 82, Margin = new Padding(10, 3, 3, 3) };
        custom.Click += (_, _) => CustomPicked?.Invoke();
        ModeButtons[Presets.CustomName] = custom;
        modes.Commands.Controls.Add(custom);
        custom.SetHelp(_tips,
            "Request remembered manual levels on each visible monitor.\n" +
            "Slider edits replace them; presets keep them.\n" +
            "Changes save in the background; check Settings for save status.");
        tools.Commands.Controls.AddRange([NightButton, IdentifyButton, RefreshButton, HelpButton, SettingsButton, _expand]);
        modes.Commands.Controls.Add(WarmthOffButton);
        SetPresetHelp(Presets.Levels);
        SettingsButton.SetHelp(_tips, "Windows startup, notification-area behavior, warmth reapplication and preset targets. Does not adjust the screen.");
        HelpButton.SetHelp(_tips, "See which controls responded and how to check a monitor connection. Works offline.");
        IdentifyButton.SetHelp(_tips, "Show each monitor's name on its own screen");
        RefreshButton.SetHelp(_tips, "Re-read displays. This may reapply saved warmth or adjust brightness for active blackouts and pending recovery.");
        WarmthOffButton.SetHelp(_tips, "Restore each display's original gamma, including its calibration profile");
        SetNightSaverState(false);
        WorkMonitor.SelectedIndexChanged += (_, _) => SetNightSaverState(_nightActive);
        // The label travels with its selector when the row wraps.
        var workChoice = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, Margin = new Padding(3),
        };
        workChoice.Controls.Add(new Label
        {
            Text = "Keep working on", AutoSize = true, ForeColor = Theme.InkMuted,
            Margin = new Padding(0, 5, 8, 0),
        });
        workChoice.Controls.Add(WorkMonitor);
        night.Commands.Controls.Add(workChoice);
        night.Commands.Controls.Add(new Label
        {
            Text = $"Work brightness: {NightSaver.WorkBrightnessPercent}% or lower",
            AutoSize = true, ForeColor = Theme.InkMuted, Margin = new Padding(3, 8, 3, 3),
        });
        foreach (var section in new[] { modes, tools, night })
            foreach (Control control in section.Commands.Controls)
                if (control is FlatButton button)
                {
                    button.MinimumSize = button.Size;
                    button.AutoSize = true;
                }
        Controls.Add(night);
        Controls.Add(modes);
        Controls.Add(tools);
        SetExpanded(true);
    }

    internal void SetHeightLimit(int limit)
    {
        if (_heightLimit == limit) return;
        _heightLimit = limit;
        _heightLimitLogical = limit > 0 ? (int)Math.Round(limit * 96d / DeviceDpi) : 0;
        MaximumSize = limit > 0 ? new Size(int.MaxValue, limit) : Size.Empty;
        AutoScroll = limit > 0;
        PerformLayout();
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        // Scaling an already rounded 125% height to 150% can add a pixel,
        // followed by an immediate correction. Clamp to the destination limit
        // during the native scaling pass instead of publishing that extra size.
        if (_heightLimitLogical > 0) height = Math.Min(height, LogicalToDeviceUnits(_heightLimitLogical));
        base.SetBoundsCore(x, y, width, height, specified);
    }

    internal void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        _expand.Text = expanded ? "Less desk actions" : "Desk actions…";
        _modes.Visible = expanded;
        _night.Visible = expanded;
        if (!expanded) AutoScrollPosition = Point.Empty;
        PerformLayout();
    }

    /// <summary>Describe the active targets, which can change after Settings > Preset targets.</summary>
    internal void SetPresetHelp(IEnumerable<Presets.Level> levels)
    {
        foreach (var level in levels)
            if (ModeButtons.TryGetValue(level.Name, out var button))
                button.SetHelp(_tips, PresetHelp(level, "on every visible panel"));
    }

    internal static string PresetHelp(Presets.Level level, string scope) =>
        (Presets.IsBuiltIn(level) ? level.Description : $"{level.Description} (your target)") +
        $"\nTargets an estimated {level.Nits} nits and {level.Kelvin}K {scope}.\nChange targets in Settings > Preset targets.";

    internal void SetNightSaverState(bool active)
    {
        _nightActive = active;
        // Reserve the longer action in both states so starting a night cannot
        // move its neighbours or create a new toolbar line.
        NightButton.MinimumSize = new Size(
            TextRenderer.MeasureText("End Night saver", NightButton.Font).Width + NightButton.LogicalToDeviceUnits(22),
            NightButton.MinimumSize.Height);
        NightButton.Text = active ? "End Night saver" : "Night saver";
        NightButton.Primary = active;
        NightButton.SetHelp(_tips, active
            ? "Close this night desk's blackouts, return the work display to its saved brightness and retry saved brightness recovery. Failed or disconnected restores stay pending; reconnect and press again. Separately blanked displays stay separate."
            : $"Keep {(WorkMonitor.SelectedItem == null ? "your selected work display" : WorkMonitor.Text)} on. Request {NightSaver.WorkBrightnessPercent}% hardware brightness only if it is brighter; the panel's minimum light output varies. Change the work display under Desk actions. Cover the other displays in black, lowering brightness only where supported. Displays already blanked separately stay separate. Press again to restore this night desk. Does not put Windows to sleep.");
        NightButton.Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        SetNightSaverState(_nightActive);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int height = 0;
        foreach (Control section in Controls) if (section.Visible) height += section.Height;
        int wanted = _heightLimit > 0 ? Math.Min(height, _heightLimit) : height;
        if (AutoScrollMinSize.Height != (wanted < height ? height : 0))
            AutoScrollMinSize = new Size(0, wanted < height ? height : 0);
        if (Height != wanted) Height = wanted;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tips.Dispose();
        base.Dispose(disposing);
    }
}
