namespace LumenDeck;

internal partial class MainForm
{
    private readonly System.Windows.Forms.Timer _scheduleTimer;
    private PresetScheduleForm _scheduleEditor;
    private string _materialScheduleSummary;

    /// <summary>
    /// The period this window last requested. Kept in memory on purpose: a new
    /// launch requests the period in force once its first scan is ready, which
    /// is what a schedule is for after a restart; afterwards nothing repeats
    /// until a new period begins, so manual changes stay until then.
    /// </summary>
    private PresetSchedule.Period _scheduledPeriod;

    private PresetSchedule.Period CurrentSchedulePeriod() => ActiveOutlook()?.Period;

    private ScheduleOutlook ActiveOutlook() => _settings.Schedule.Enabled ? Outlook(_settings.ScheduleSlots()) : null;

    /// <summary>
    /// The single rule for whether a schedule requests its period: used by the
    /// checks below and by the editor's preview, from the live clock and the
    /// live request state, so the two cannot disagree. Displays that cannot be
    /// adjusted - during a scan, or with none connected - defer the request
    /// instead of consuming the period.
    /// </summary>
    private ScheduleOutlook Outlook(IReadOnlyCollection<PresetSchedule.Slot> slots)
    {
        var period = PresetSchedule.At(slots, _host.Now());
        if (period == null) return null;
        bool pending = !SamePeriod(period, _scheduledPeriod);
        return new ScheduleOutlook(period, pending && CanAdjustDisplays, pending && !CanAdjustDisplays);
    }

    private void CheckSchedule()
    {
        if (_reallyClosing || IsDisposed || Disposing) return;
        var outlook = ActiveOutlook();
        UpdateScheduleSummary(outlook?.Period);
        if (outlook is { RequestsNow: true })
        {
            _scheduledPeriod = outlook.Period;
            if (Presets.IsCustom(outlook.Period.Preset)) ApplyCustom("Schedule: ", scheduled: true);
            else ApplyLevel(outlook.Period.Preset, "Schedule: ", scheduled: true);
        }
        // An open editor previews against the same state; keep it truthful.
        _scheduleEditor?.RefreshPreview();
    }

    private static bool SamePeriod(PresetSchedule.Period a, PresetSchedule.Period b) =>
        a != null && b != null && a.Preset == b.Preset && a.Start == b.Start;

    private void UpdateScheduleSummary(PresetSchedule.Period period = null)
    {
        period ??= CurrentSchedulePeriod();
        string summary = period == null ? null
            : $"On: {period.Preset} since {PresetSchedule.Format(period.Start.TimeOfDay)}; {period.NextPreset} at {PresetSchedule.Format(period.Next.TimeOfDay)}.";
        _preferencesMenu.SetScheduleSummary(summary);
        _materialScheduleSummary = summary;
        // NotifyIcon.Text throws above 127 characters; this stays well below.
        _tray.Text = period == null ? Text
            : $"{Text} - {period.Preset} until {PresetSchedule.Format(period.Next.TimeOfDay)}";
    }

    private void ShowScheduleEditor()
    {
        if (_reallyClosing || IsDisposed || Disposing || !UiVisible ||
            _scheduleEditor != null || _presetEditor != null || _profileEditorOpening) return;
        var rows = PresetSchedule.Names.Select(name =>
        {
            var (used, start) = _settings.ScheduleRow(name);
            return new ScheduleRowDraft(name, used, start);
        }).ToArray();
        var context = new ScheduleEditorContext(_settings.Schedule.Enabled, rows, _settings.PresetLevels);
        try
        {
            using var editor = _host.CreateScheduleEditor(context, Outlook, SaveScheduleAsync);
            _scheduleEditor = editor;
            if (editor.ShowDialog(DialogOwner) != DialogResult.OK && !_reallyClosing && !IsDisposed)
                SetStatus("Preset schedule closed. The schedule and displays were not changed.");
        }
        finally { _scheduleEditor = null; }
    }

    private Task<SettingsSaveResult> SaveScheduleAsync(ScheduleDraft draft)
    {
        if (_reallyClosing || IsDisposed || Disposing) return Task.FromResult(SettingsSaveResult.Failed);
        if (_settingsWriteRunning) return Task.FromResult(SettingsSaveResult.Busy);
        var previous = _settings.Schedule;
        var wanted = _settings.WithSchedule(draft.Enabled, draft.Rows.Select(r => (r.Preset, r.Used, r.Start)));
        return SaveSettingsAsync(draft: new SettingsDraft(
            () => _settings.Schedule = wanted, () => _settings.Schedule = previous,
            "Saving schedule. The previous schedule remains in effect.", " The previous schedule remains in effect.",
            ScheduleActivated));
    }

    private void ScheduleActivated()
    {
        var outlook = ActiveOutlook();
        SetStatus(outlook == null
            ? "Preset schedule saved and off. No display was adjusted."
            : outlook.Deferred
            ? $"Preset schedule saved. {outlook.Period.Preset} is requested once displays are ready."
            : $"Preset schedule saved. {outlook.Period.NextPreset} is requested at {PresetSchedule.Format(outlook.Period.Next.TimeOfDay)}.");
        // Requests the current period now when the outlook says so; its own
        // status then replaces the one above.
        CheckSchedule();
    }
}
