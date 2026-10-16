namespace LumenDeck;

internal partial class MainForm
{
    private bool _settingsWriteRunning;
    private bool _settingsDirty;
    private long _settingsSaveRevision;
    private long? _settingsConfirmedRevision;
    private SettingsSaveResult? _settingsSaveOutcome;
    private sealed record ShortcutChange(string Key, string Name, string Previous, string Wanted);

    private void ChangeTheme(string requested)
    {
        if (_reallyClosing || _settingsWriteRunning || SettingsSaveFeedback.RequiresReopen(CurrentSettingsSaveState())) return;
        string wanted = Theme.Normalize(requested);
        if (wanted == _settings.ThemeId) return;
        string previous = _settings.ThemeId;
        _ = SaveSettingsAsync(draft: new SettingsDraft(
            () => _settings.ThemeId = wanted, () => _settings.ThemeId = previous,
            "Saving theme…", "The previous theme is still active.", () =>
            {
                Theme.Select(wanted);
                SetStatus(Theme.HighContrast
                    ? "Theme saved. Windows high contrast is currently in use."
                    : $"{Theme.Palettes.First(p => p.Id == wanted).Name} theme saved.");
            }));
    }

    /// <summary>
    /// A settings value captured into a save without becoming active. Install
    /// puts the draft in place, Restore puts the active value back, and
    /// Activated runs once the draft is installed after a confirmed save.
    /// </summary>
    private sealed record SettingsDraft(Action Install, Action Restore, string Saving, string Kept, Action Activated);

    private void RequestShortcutSave(string key, string name, string wanted)
    {
        if (_reallyClosing || IsDisposed || Disposing) return;
        if (_settingsWriteRunning)
        {
            SetStatus("Settings save pending. Retry the shortcut after it finishes.", StatusKind.Warn);
            return;
        }
        _ = SaveSettingsAsync(shortcut: new ShortcutChange(key, name, _settings.ScreenBlankHotkeyFor(key), wanted));
    }

    private void RequestSettingsSave()
    {
        if (_reallyClosing || IsDisposed || Disposing) return;
        MarkSettingsChanged();
        if (_settingsWriteRunning) { _settingsDirty = true; return; }
        _ = SaveSettingsAsync();
    }

    private async Task<SettingsSaveResult> SaveSettingsAsync(AppPreference? preference = null, bool wanted = false,
        ShortcutChange shortcut = null, SettingsDraft draft = null)
    {
        _settingsWriteRunning = true;
        _settingsDirty = false;
        long capturedRevision = _settingsSaveRevision;
        bool previous = preference switch
        {
            AppPreference.StartMinimised => _settings.StartMinimised,
            AppPreference.KeepRunning => _settings.MinimiseToTray,
            AppPreference.ReapplyWarmth => _settings.ReapplyColourOnStart,
            AppPreference.MicaTitleBar => _settings.UseMicaTitleBar,
            _ => false,
        };
        SettingsSaveOperation operation = null;
        SettingsSaveResult result = SettingsSaveResult.Failed;
        try
        {
            // Capture the requested flag without activating it. Other messages
            // cannot observe the draft because no await/message pump intervenes.
            if (preference.HasValue) SetPreference(preference.Value, wanted);
            if (shortcut != null) _settings.SetScreenBlankHotkey(shortcut.Key, shortcut.Name, shortcut.Wanted);
            draft?.Install();
            try { operation = _settings.PrepareSave(); }
            finally
            {
                if (preference.HasValue) SetPreference(preference.Value, previous);
                if (shortcut != null) _settings.SetScreenBlankHotkey(shortcut.Key, shortcut.Name, shortcut.Previous);
                draft?.Restore();
            }
            SynchronizePreferences();
            if (preference.HasValue) SetStatus("Saving preference. Previous preference remains active.");
            if (shortcut != null) SetStatus("Saving shortcut. Previous shortcut remains active.");
            if (draft != null) SetStatus(draft.Saving);
            try
            {
                result = operation == null ? SettingsSaveResult.Busy
                    : await Task.Run(() => _host.WriteSettings(operation.Write));
            }
            catch { result = SettingsSaveResult.Failed; }
            if (result == SettingsSaveResult.Saved && operation?.WasSaved != true)
                result = SettingsSaveResult.Failed;

            if (!IsDisposed && !Disposing && !_reallyClosing && result == SettingsSaveResult.Saved)
            {
                if (preference.HasValue) SetPreference(preference.Value, wanted);
                if (preference == AppPreference.MicaTitleBar) WindowChrome.Apply(this, wanted);
                if (shortcut != null) _settings.SetScreenBlankHotkey(shortcut.Key, shortcut.Name, shortcut.Wanted);
                draft?.Install();
            }
        }
        finally
        {
            // Keep checkpoint admission closed until the active UI flag settles.
            operation?.Complete(result);
            _settingsWriteRunning = false;
        }
        if (IsDisposed || Disposing || _reallyClosing) return result;
        RecordSettingsSave(result, capturedRevision);
        SynchronizePreferences();
        if (result != SettingsSaveResult.Saved)
            SetStatus(SettingsFailureAdvice() + (preference.HasValue ? " Previous preference remains active."
                : shortcut != null ? " Previous shortcut remains active."
                : draft?.Kept ?? ""), StatusKind.Warn);
        else if (preference.HasValue)
            SetStatus(preference.Value switch
            {
                AppPreference.StartMinimised => wanted ? (_settings.MinimiseToTray ? "Next launch starts in the notification area." : "Next launch starts minimised on the taskbar.") : "Next launch shows the control window.",
                AppPreference.KeepRunning => wanted ? "Closing or minimising keeps LumenDeck running. Use tray Exit to quit." : "Closing exits LumenDeck; minimising keeps it on the taskbar.",
                AppPreference.MicaTitleBar => wanted
                    ? "Mica title bar preference saved. Windows uses the material where supported, with a solid fallback."
                    : "Mica title bar is off. The slate title bar is restored.",
                _ => wanted ? "Reapply warmth is on for the next scan. Current warmth is unchanged." : "Warmth reapplication is off. Current warmth is unchanged.",
            });
        else if (shortcut != null)
        {
            foreach (var card in _cards.Where(c => c.Monitor.StableKey == shortcut.Key))
                card.SetScreenBlankShortcutText(shortcut.Wanted);
            RegisterScreenBlankHotkeys();
            bool connected = _cards.Any(c => c.Monitor.StableKey == shortcut.Key);
            bool active = string.IsNullOrEmpty(shortcut.Wanted) ||
                _screenBlankHotkeys.Values.Any(c => c.Monitor.StableKey == shortcut.Key);
            SetStatus(string.IsNullOrEmpty(shortcut.Wanted) ? "Screen-blank shortcut cleared."
                : !connected ? "Shortcut saved. Reconnect the display to use it."
                : active ? $"{shortcut.Wanted} now toggles the display's reversible blackout."
                : $"{shortcut.Wanted} was saved, but Windows refused registration; another app may use it.",
                string.IsNullOrEmpty(shortcut.Wanted) || connected && active ? StatusKind.Info : StatusKind.Warn);
        }
        else draft?.Activated();
        // Capture again only after completion, so queued ordinary saves cannot
        // write the old preference or a pre-checkpoint recovery record later.
        if (_settingsDirty) RequestSettingsSave();
        return result;
    }

    private void ShowPresetEditor()
    {
        if (_reallyClosing || IsDisposed || Disposing || !Visible || WindowState == FormWindowState.Minimized ||
            _presetEditor != null || _scheduleEditor != null || _profileEditorOpening) return;
        // Capture values only: a display-change rebuild can dispose cards and
        // monitors while the modal editor's message loop is running.
        var cards = _cards.Where(c => !c.IsDisposed).ToList();
        var displays = cards.Where(c => c.Monitor.SupportsBrightness)
            .Select(c => new PresetPreviewDisplay(c.Monitor.DisplayName, PanelDatabase.For(c.Monitor),
                PanelDatabase.HasUserProfile(c.Monitor)))
            .ToArray();
        var context = new PresetEditorContext(_settings.PresetLevels, displays, cards.Count - displays.Length);
        try
        {
            using var editor = _host.CreatePresetEditor(context, SavePresetLevelsAsync);
            _presetEditor = editor;
            if (editor.ShowDialog(this) != DialogResult.OK && !_reallyClosing && !IsDisposed)
                SetStatus("Preset targets closed. Targets and displays were not changed.");
        }
        finally { _presetEditor = null; }
    }

    /// <summary>
    /// The editor's save. Targets activate only once this save is confirmed;
    /// until then presets keep requesting the previous targets.
    /// </summary>
    private Task<SettingsSaveResult> SavePresetLevelsAsync(IReadOnlyList<Presets.Level> levels)
    {
        if (_reallyClosing || IsDisposed || Disposing) return Task.FromResult(SettingsSaveResult.Failed);
        if (_settingsWriteRunning) return Task.FromResult(SettingsSaveResult.Busy);
        var previous = _settings.PresetTargets;
        var previousLevels = _settings.PresetLevels;
        var wanted = _settings.WithPresetLevels(levels);
        return SaveSettingsAsync(draft: new SettingsDraft(
            () => _settings.PresetTargets = wanted, () => _settings.PresetTargets = previous,
            "Saving preset targets. Previous targets remain active.", " Previous preset targets remain active.",
            () => PresetTargetsActivated(previousLevels)));
    }

    private void PresetTargetsActivated(IReadOnlyList<Presets.Level> previous)
    {
        var current = _settings.PresetLevels;
        _bar.SetPresetHelp(current);
        foreach (var card in _cards.Where(c => !c.IsDisposed)) card.SetPresetHelp(current);
        // A lit preset whose target moved no longer describes the desk: the
        // displays still hold the old request until the preset is chosen again.
        var lit = _modeButtons.FirstOrDefault(m => m.Value.Primary).Key;
        if (lit != null && previous.FirstOrDefault(l => l.Name == lit) is { } before && !current.Contains(before)) SetMode(null);
        SetStatus("Preset targets saved. No display was adjusted; choose a preset to request its new target.");
    }

    private void RetrySettingsSave()
    {
        if (_settingsWriteRunning || _reallyClosing || IsDisposed || Disposing ||
            !SettingsSaveFeedback.CanRetry(CurrentSettingsSaveState())) return;
        RequestSettingsSave();
    }

    private void MarkSettingsChanged()
    {
        _settingsSaveRevision++;
        UpdateSettingsSaveFeedback();
    }

    private void RecordSettingsSave(SettingsSaveResult result, long revision)
    {
        _settingsSaveOutcome = result;
        if (result == SettingsSaveResult.Saved) _settingsConfirmedRevision = revision;
        UpdateSettingsSaveFeedback();
    }

    private SettingsSaveState CurrentSettingsSaveState()
    {
        if (_settingsWriteRunning) return SettingsSaveState.Saving;
        return _settingsSaveOutcome switch
        {
            SettingsSaveResult.SourceChanged => SettingsSaveState.SourceChanged,
            SettingsSaveResult.Unavailable => SettingsSaveState.Unavailable,
            SettingsSaveResult.Busy => SettingsSaveState.Busy,
            SettingsSaveResult.Failed => SettingsSaveState.Failed,
            SettingsSaveResult.Saved when _settingsConfirmedRevision == _settingsSaveRevision => SettingsSaveState.Saved,
            _ => _settingsSaveRevision > 0 ? SettingsSaveState.Waiting : SettingsSaveState.None,
        };
    }

    private void UpdateSettingsSaveFeedback()
    {
        if (_preferencesMenu == null || _bar == null || IsDisposed || Disposing) return;
        var state = CurrentSettingsSaveState();
        _preferencesMenu.SetSaveState(state);
        bool attention = _settingsSaveOutcome.HasValue && _settingsSaveOutcome != SettingsSaveResult.Saved;
        _bar.SettingsButton.Text = attention ? "Settings !" : "Settings";
        _bar.SettingsButton.AccessibleDescription = SettingsSaveFeedback.Explanation(state);
    }
}
