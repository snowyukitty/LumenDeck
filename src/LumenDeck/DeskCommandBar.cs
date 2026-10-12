namespace LumenDeck;

/// <summary>Shared command presentation, with no monitor I/O or settings side effects.</summary>
internal sealed class DeskCommandBar : Panel
{
    private readonly ToolTip _tips = new();
    public Dictionary<string, FlatButton> ModeButtons { get; } = new(StringComparer.OrdinalIgnoreCase);
    public FlatButton IdentifyButton { get; } = new("Identify") { Width = 84 };
    public FlatButton RefreshButton { get; } = new("Refresh") { Width = 82 };
    public FlatButton WarmthOffButton { get; } = new("Warmth off") { Width = 100 };
    public FlatButton HelpButton { get; } = new("Connection help") { Width = 136, Quiet = true };
    public FlatButton SettingsButton { get; } = new("Settings") { Width = 90, Quiet = true };
    public FlatButton NightButton { get; } = new("Night saver") { Width = 148 };
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
        var modes = new CommandSection("All monitors") { TabIndex = 0 };
        var tools = new CommandSection("Tools") { TabIndex = 1 };
        var night = new CommandSection("Night desk") { TabIndex = 2 };
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
        tools.Commands.Controls.AddRange([IdentifyButton, RefreshButton, WarmthOffButton, HelpButton, SettingsButton]);
        SetPresetHelp(Presets.Levels);
        SettingsButton.SetHelp(_tips, "Windows startup, notification-area behavior, warmth reapplication and preset targets. Does not adjust the screen.");
        HelpButton.SetHelp(_tips, "See which controls responded and how to check a monitor connection. Works offline.");
        IdentifyButton.SetHelp(_tips, "Show each monitor's name on its own screen");
        RefreshButton.SetHelp(_tips, "Re-read displays. This may reapply saved warmth or adjust brightness for active blackouts and pending recovery.");
        WarmthOffButton.SetHelp(_tips, "Restore each display's original gamma, including its calibration profile");
        SetNightSaverState(false);
        night.Commands.Controls.Add(NightButton);
        // The label travels with its selector when the row wraps.
        var workChoice = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false, Margin = new Padding(12, 3, 3, 3),
        };
        workChoice.Controls.Add(new Label
        {
            Text = "Keep working on", AutoSize = true, ForeColor = Theme.InkMuted,
            Margin = new Padding(0, 5, 8, 0),
        });
        workChoice.Controls.Add(WorkMonitor);
        night.Commands.Controls.Add(workChoice);
        foreach (var section in new[] { modes, tools, night })
            foreach (Control control in section.Commands.Controls)
                if (control is FlatButton button)
                {
                    button.MinimumSize = button.Size;
                    button.AutoSize = true;
                }
        Controls.Add(night);
        Controls.Add(tools);
        Controls.Add(modes);
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
        NightButton.Text = active ? "Restore night desk" : "Night saver";
        NightButton.Primary = active;
        NightButton.SetHelp(_tips, active
            ? "Close this night desk's blackouts, return the work display to its saved brightness and retry saved brightness recovery. Failed or disconnected restores stay pending; reconnect and press again. Separately blanked displays stay separate."
            : "Keep your selected work display on, dimmed toward the Night preset's target if it is brighter. Cover the other displays in black, lowering brightness only where supported. Displays already blanked separately stay separate. Press again to restore this night desk.");
        NightButton.Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int height = 0;
        foreach (Control section in Controls) height += section.Height;
        if (Height != height) Height = height;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tips.Dispose();
        base.Dispose(disposing);
    }
}
