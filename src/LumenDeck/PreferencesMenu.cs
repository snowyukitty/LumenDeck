namespace LumenDeck;

internal enum AppPreference { WindowsStartup, StartMinimised, KeepRunning, ReapplyWarmth, MicaTitleBar }
internal sealed record PreferenceState(bool? WindowsStartup, bool StartMinimised, bool KeepRunning, bool ReapplyWarmth,
    bool MicaTitleBar = false);

/// <summary>Preference presentation only; requests do not optimistically change checks.</summary>
internal sealed class PreferencesMenu : ContextMenuStrip
{
    private readonly Dictionary<AppPreference, ToolStripMenuItem> _choices = new();
    private readonly Dictionary<AppPreference, string> _explanations = new();
    private readonly ToolStripLabel _saveInfo;
    private readonly ToolStripMenuItem _retrySave;
    private SettingsSaveState _saveState;
    public event Action<AppPreference, bool> ChangeRequested;
    public event Action RetrySaveRequested;
    public event Action KeyboardHelpRequested;
    public event Action AboutRequested;
    public event Action PresetTargetsRequested;
    public event Action ScheduleRequested;
    private readonly ToolStripMenuItem _schedule;

    public PreferencesMenu()
    {
        ShowImageMargin = false;
        ShowCheckMargin = true;
        ShowItemToolTips = true;
        Font = Theme.Body;
        ForeColor = Theme.Ink;
        BackColor = Theme.Card;
        Renderer = new ToolStripProfessionalRenderer(new MenuColors());
        AccessibleName = "Application settings";
        Add(AppPreference.WindowsStartup, "Start with Windows",
            "Register LumenDeck to start at sign-in for this Windows account.");
        Add(AppPreference.StartMinimised, "Start minimised",
            "The next launch starts minimised. This does not enable Windows startup or hide the current window.");
        Items.Add(new ToolStripSeparator());
        Add(AppPreference.KeepRunning, "Keep running when closed or minimised",
            "Keep the notification icon available when you close or minimise. Choose Exit in the tray menu to quit. When off, closing exits and minimising stays on the taskbar.");
        Add(AppPreference.ReapplyWarmth, "Reapply saved warmth after a display scan",
            "Restore saved warmth on startup and after display configuration changes or Refresh. Changing this option does not adjust current warmth; turning it off does not restore a neutral screen.");
        Items.Add(new ToolStripSeparator());
        Add(AppPreference.MicaTitleBar, "Use Windows Mica title bar",
            "Use the Windows material in the main title bar on Windows 11 22H2 or later. Content stays opaque. Windows may show a solid fallback when transparency is off or the window is inactive. Does not adjust displays.");
        Items.Add(new ToolStripSeparator());
        _saveInfo = new ToolStripLabel { ForeColor = Theme.InkMuted, AccessibleRole = AccessibleRole.StaticText };
        _retrySave = new ToolStripMenuItem { ForeColor = Theme.Ink };
        _retrySave.Click += (_, _) => { if (_retrySave.Enabled) RetrySaveRequested?.Invoke(); };
        Items.Add(_saveInfo);
        Items.Add(_retrySave);
        Items.Add(new ToolStripSeparator());
        var presets = new ToolStripMenuItem("Preset targets…")
        {
            ToolTipText = "Choose the estimated luminance and warmth that Day, Evening and Night request. Opening it does not adjust displays.",
            AccessibleDescription = "Edit Day, Evening and Night targets. Opening it does not adjust displays.",
            ForeColor = Theme.Ink,
        };
        presets.Click += (_, _) => PresetTargetsRequested?.Invoke();
        Items.Add(presets);
        _schedule = new ToolStripMenuItem("Preset schedule…") { ForeColor = Theme.Ink };
        _schedule.Click += (_, _) => ScheduleRequested?.Invoke();
        Items.Add(_schedule);
        SetScheduleSummary(null);
        Items.Add(new ToolStripSeparator());
        var keyboard = new ToolStripMenuItem("Keyboard controls…")
        {
            ShortcutKeyDisplayString = "F1",
            ToolTipText = "Offline key reference for the focused control. Opening it does not adjust displays.",
            AccessibleDescription = "Open the offline keyboard reference.",
        };
        keyboard.Click += (_, _) => KeyboardHelpRequested?.Invoke();
        Items.Add(keyboard);
        var about = new ToolStripMenuItem("About LumenDeck…")
        {
            ToolTipText = "Version, build and offline help information.",
            AccessibleDescription = "Open version, build and offline help information.",
        };
        about.Click += (_, _) => AboutRequested?.Invoke();
        Items.Add(about);
        SetSaveState(SettingsSaveState.None);
    }

    /// <summary>Null when the schedule is off; otherwise what it is doing now.</summary>
    public void SetScheduleSummary(string summary)
    {
        _schedule.Checked = summary != null;
        _schedule.ToolTipText = _schedule.AccessibleDescription = summary == null
            ? "Off. Request presets automatically at times of day. Opening it does not adjust displays."
            : summary + " Opening the schedule does not adjust displays.";
    }

    private void Add(AppPreference preference, string text, string explanation)
    {
        var item = new ToolStripMenuItem(text)
        {
            CheckOnClick = false, ToolTipText = explanation, AccessibleDescription = explanation,
            ForeColor = Theme.Ink,
        };
        item.Click += (_, _) => { if (item.Enabled) ChangeRequested?.Invoke(preference, !item.Checked); };
        _choices.Add(preference, item);
        _explanations.Add(preference, explanation);
        Items.Add(item);
    }

    public void Synchronize(PreferenceState state, bool saving = false)
    {
        var startup = _choices[AppPreference.WindowsStartup];
        startup.Checked = state.WindowsStartup == true;
        startup.Enabled = state.WindowsStartup.HasValue;
        startup.Text = state.WindowsStartup.HasValue ? "Start with Windows" : "Start with Windows (unavailable)";
        startup.AccessibleDescription = startup.ToolTipText = state.WindowsStartup.HasValue
            ? "Register LumenDeck to start at sign-in for this Windows account."
            : "Windows startup registration could not be read. This option is unavailable; it is not confirmed disabled.";
        var minimised = _choices[AppPreference.StartMinimised];
        minimised.AccessibleDescription = minimised.ToolTipText = state.KeepRunning
            ? "The next launch starts in the notification area. This does not enable Windows startup or hide the current window."
            : "The next launch starts minimised on the taskbar. Enable keep running to use the notification area instead. This does not enable Windows startup or hide the current window.";
        _choices[AppPreference.StartMinimised].Checked = state.StartMinimised;
        _choices[AppPreference.KeepRunning].Checked = state.KeepRunning;
        _choices[AppPreference.ReapplyWarmth].Checked = state.ReapplyWarmth;
        _choices[AppPreference.MicaTitleBar].Checked = state.MicaTitleBar;
        foreach (var preference in new[] { AppPreference.StartMinimised, AppPreference.KeepRunning, AppPreference.ReapplyWarmth, AppPreference.MicaTitleBar })
        {
            var item = _choices[preference];
            item.Enabled = !saving;
            string explanation = preference == AppPreference.StartMinimised ? minimised.AccessibleDescription : _explanations[preference];
            item.AccessibleDescription = item.ToolTipText = explanation +
                (saving ? " A settings save is pending. The check shows the active preference." : "");
        }
        SetSaveState(saving ? SettingsSaveState.Saving : _saveState);
    }

    public void SetSaveState(SettingsSaveState state)
    {
        _saveState = state;
        _saveInfo.Text = _saveInfo.AccessibleName = SettingsSaveFeedback.Caption(state);
        _saveInfo.ToolTipText = _saveInfo.AccessibleDescription = SettingsSaveFeedback.Explanation(state);
        _saveInfo.ForeColor = SettingsSaveFeedback.NeedsAttention(state) ? Theme.Warn : Theme.InkMuted;
        _retrySave.Text = state == SettingsSaveState.Waiting ? "Save active settings now" : "Retry saving active settings";
        _retrySave.Enabled = SettingsSaveFeedback.CanRetry(state);
        _retrySave.ToolTipText = _retrySave.AccessibleDescription =
            "Saves currently active settings without adjusting monitors. Failed preference or shortcut choices must be selected again. " +
            SettingsSaveFeedback.Explanation(state);
        foreach (var item in _choices.Where(c => c.Key != AppPreference.WindowsStartup).Select(c => c.Value))
        {
            item.Enabled = state != SettingsSaveState.Saving && !SettingsSaveFeedback.RequiresReopen(state);
            if (SettingsSaveFeedback.RequiresReopen(state))
            {
                item.ToolTipText = item.AccessibleDescription = SettingsSaveFeedback.Explanation(state);
            }
        }
    }

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Card;
        public override Color MenuBorder => Theme.Line;
        public override Color MenuItemSelected => Theme.CardHover;
        public override Color MenuItemBorder => Theme.Line;
        public override Color CheckBackground => Theme.CardHover;
        public override Color CheckSelectedBackground => Theme.CardHover;
        public override Color CheckPressedBackground => Theme.CardHover;
        public override Color SeparatorDark => Theme.Line;
        public override Color SeparatorLight => Theme.Line;
        public override Color ImageMarginGradientBegin => Theme.Card;
        public override Color ImageMarginGradientMiddle => Theme.Card;
        public override Color ImageMarginGradientEnd => Theme.Card;
    }
}
