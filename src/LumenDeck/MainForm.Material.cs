using LumenDeck.Material;

namespace LumenDeck;

internal partial class MainForm : IMaterialHost
{
    private MaterialWindow _materialWindow;
    private bool _materialActive;
    private string _materialStatus = "Reading displays…";
    private bool _materialWarning;
    private bool UiVisible => _materialActive
        ? _materialWindow is { IsVisible: true } && _materialWindow.WindowState != System.Windows.WindowState.Minimized
        : Visible && WindowState != FormWindowState.Minimized;
    private IWin32Window DialogOwner => _materialActive && _materialWindow != null
        ? new MaterialOwner(new System.Windows.Interop.WindowInteropHelper(_materialWindow).Handle) : this;
    private sealed record MaterialOwner(IntPtr Handle) : IWin32Window;

    internal MaterialWindow OpenMaterialWindow(bool show = true)
    {
        if (_materialWindow == null)
        {
            _materialWindow = new MaterialWindow(this);
            _materialWindow.Title = Text;
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(_materialWindow);
        }
        _materialActive = true;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Hide();
        if (show) _materialWindow.Reveal();
        return _materialWindow;
    }

    private void ShowClassicWindow(MonitorCard advanced = null)
    {
        _materialActive = false;
        _materialWindow?.Hide();
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        if (advanced != null && !advanced.IsDisposed)
        {
            SetAllDisplays(false);
            PickDisplay(advanced.Monitor);
            advanced.OpenExtraControls();
        }
    }

    private void RequestMaterialInterface(bool enabled)
    {
        if (_settingsWriteRunning || _reallyClosing || SettingsSaveFeedback.RequiresReopen(CurrentSettingsSaveState())) return;
        bool previous = _settings.UseMaterialInterface;
        _ = SaveSettingsAsync(draft: new SettingsDraft(
            () => _settings.UseMaterialInterface = enabled, () => _settings.UseMaterialInterface = previous,
            "Saving interface preference…", "Previous interface retained.",
            () => { if (enabled) OpenMaterialWindow(); else ShowClassicWindow(); }));
    }

    private void ChangeMaterialAppearance(string finish = null, string backdrop = null)
    {
        if (_settingsWriteRunning || _reallyClosing || SettingsSaveFeedback.RequiresReopen(CurrentSettingsSaveState())) return;
        string oldFinish = _settings.MaterialFinish, oldBackdrop = _settings.MaterialBackdrop;
        string wantedFinish = finish ?? oldFinish, wantedBackdrop = backdrop ?? oldBackdrop;
        _ = SaveSettingsAsync(draft: new SettingsDraft(
            () => { _settings.MaterialFinish = wantedFinish; _settings.MaterialBackdrop = wantedBackdrop; },
            () => { _settings.MaterialFinish = oldFinish; _settings.MaterialBackdrop = oldBackdrop; },
            "Saving appearance…", "Previous appearance retained.",
            () => { SetStatus("Appearance saved. Displays are unchanged."); _materialWindow?.RefreshState(); }));
    }

    MaterialSnapshot IMaterialHost.Capture()
    {
        var saveState = CurrentSettingsSaveState();
        var displays = _cards.Where(c => !c.IsDisposed).Select(c =>
        {
            var m = c.Monitor;
            return new MaterialDisplay(m.StableKey, m.DisplayName, m.PositionLabel,
                new DisplayBounds(m.Rect.Left, m.Rect.Top, m.Rect.Right - m.Rect.Left, m.Rect.Bottom - m.Rect.Top),
                m.IsPrimary, m.SupportsBrightness, m.BrightnessMin, m.BrightnessMax, m.Brightness,
                $"{Presets.RawToPercent(m, m.Brightness):0}%", c.BrightnessStatus,
                m.SupportsBrightness ? Presets.EstimateFor(m, m.Brightness).DisplayText : "",
                m.SupportsContrast, m.ContrastMin, m.ContrastMax, m.Contrast, c.ContrastStatus, m.Kelvin,
                _blanker.IsBlanked(m.StableKey) || _settings.ScreenBlankActiveFor(m.StableKey), c.WarningText,
                _settings.ScreenBlankHotkeyFor(m.StableKey));
        }).ToArray();
        return new MaterialSnapshot(_generation, _selectedMonitorKey, displays,
            _scanState == DisplayScanState.Reading, _materialStatus, _materialWarning,
            _nightSaver.Count > 0, _nightButton.Enabled, (_workMonitor.SelectedItem as WorkScreenChoice)?.Key,
            _settings.MaterialFinish, _settings.MaterialBackdrop, _settingsWriteRunning,
            _settings.MinimiseToTray, _settings.StartMinimised, _startupPreference.Enabled ? _startupPreference.Checked : null,
            _settings.ReapplyColourOnStart, SettingsSaveFeedback.Caption(saveState),
            _materialScheduleSummary) { PreferencesLocked = SettingsSaveFeedback.RequiresReopen(saveState) };
    }

    void IMaterialHost.Execute(MaterialAction action, string key, int generation, int value)
    {
        if (_reallyClosing || IsDisposed || Disposing || !Enabled || !UiVisible ||
            _profileEditor != null || _presetEditor != null || _scheduleEditor != null || _profileEditorOpening) return;
        // Commands always resolve against the current scan, never a retained Monitor reference.
        var card = generation == _generation && CanAdjustDisplays
            ? _cards.SingleOrDefault(c => !c.IsDisposed && c.Monitor.StableKey == key) : null;
        switch (action)
        {
            case MaterialAction.Refresh: if (_refreshButton.Enabled) _ = RebuildAsync(); return;
            case MaterialAction.Identify: if (CanAdjustDisplays) IdentifyOverlay.Show(_monitors); return;
            case MaterialAction.NightSaver: if (_nightButton.Enabled) ToggleNightSaver(); return;
            case MaterialAction.ConnectionHelp: ShowConnectionHelp(); return;
            case MaterialAction.KeyboardHelp: ShowKeyboardHelp(); return;
            case MaterialAction.About: ShowAbout(); return;
            case MaterialAction.PresetTargets: ShowPresetEditor(); return;
            case MaterialAction.Schedule: ShowScheduleEditor(); return;
            case MaterialAction.RetrySave: RetrySettingsSave(); return;
            case MaterialAction.Startup: ChangePreference(AppPreference.WindowsStartup, value != 0); return;
            case MaterialAction.StartMinimised: ChangePreference(AppPreference.StartMinimised, value != 0); return;
            case MaterialAction.KeepRunning: ChangePreference(AppPreference.KeepRunning, value != 0); return;
            case MaterialAction.ReapplyWarmth: ChangePreference(AppPreference.ReapplyWarmth, value != 0); return;
            case MaterialAction.FinishSmoke: ChangeMaterialAppearance(finish: "smoke"); return;
            case MaterialAction.FinishFrost: ChangeMaterialAppearance(finish: "frost"); return;
            case MaterialAction.FinishCeramic: ChangeMaterialAppearance(finish: "ceramic"); return;
            case MaterialAction.BackdropMica: ChangeMaterialAppearance(backdrop: "mica"); return;
            case MaterialAction.BackdropGlass: ChangeMaterialAppearance(backdrop: "glass"); return;
            case MaterialAction.BackdropSolid: ChangeMaterialAppearance(backdrop: "solid"); return;
            case MaterialAction.Classic: RequestMaterialInterface(false); return;
            case MaterialAction.Exit: _reallyClosing = true; Close(); return;
        }
        if (generation != _generation || !CanAdjustDisplays) return;
        switch (action)
        {
            case MaterialAction.AllDay: ApplyLevel("Day"); return;
            case MaterialAction.AllEvening: ApplyLevel("Evening"); return;
            case MaterialAction.AllNight: ApplyLevel("Night"); return;
            case MaterialAction.AllCustom: ApplyCustom(); return;
            case MaterialAction.WarmthOff: WarmthOff(); return;
        }
        if (card == null) return;
        switch (action)
        {
            case MaterialAction.SelectDisplay: _selectedMonitorKey = key; ApplyDisplayView(); break;
            case MaterialAction.Brightness: card.RequestManualAdjustment(AdjustmentControl.Brightness, value); break;
            case MaterialAction.Contrast: card.RequestManualAdjustment(AdjustmentControl.Contrast, value); break;
            case MaterialAction.Warmth: card.RequestManualAdjustment(AdjustmentControl.Warmth, value); break;
            case MaterialAction.PresetDay: card.RequestPreset("Day"); break;
            case MaterialAction.PresetEvening: card.RequestPreset("Evening"); break;
            case MaterialAction.PresetNight: card.RequestPreset("Night"); break;
            case MaterialAction.Custom: card.RequestPreset(Presets.CustomName); break;
            case MaterialAction.Blank: ToggleScreenBlank(card); break;
            case MaterialAction.Shortcut: ConfigureScreenBlankHotkey(card); break;
            case MaterialAction.Profile: ShowProfileEditor(card); break;
            case MaterialAction.Advanced: ShowClassicWindow(card); break;
            case MaterialAction.DismissWarning: card.DismissWarning(); break;
            case MaterialAction.WorkDisplay:
                if (_nightSaver.Count > 0 || _settingsWriteRunning) break;
                string previous = _settings.NightWorkMonitorKey;
                _ = SaveSettingsAsync(draft: new SettingsDraft(
                    () => _settings.NightWorkMonitorKey = key, () => _settings.NightWorkMonitorKey = previous,
                    "Saving Night saver work display…", "Previous work display retained.",
                    () => { PopulateWorkScreens(); UpdateNightSaver(); SetStatus("Night saver work display saved."); }));
                break;
        }
    }

    void IMaterialHost.CloseRequested() { if (!_reallyClosing && !IsDisposed) Close(); }
    void IMaterialHost.MinimiseRequested()
    {
        if (_settings.MinimiseToTray) _materialWindow?.Hide();
    }
    void IMaterialHost.VisibilityChanged() { if (_materialWindow?.IsVisible != true) _visibilityGeneration++; }
}
