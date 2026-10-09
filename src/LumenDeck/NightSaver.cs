namespace LumenDeck;

/// <summary>What Night saver did to the work display it keeps on.</summary>
internal enum NightWorkDim { None, Dimmed, AlreadyDim, Unsupported, NotSaved }

/// <summary>
/// Own only the blackouts created by this night session, plus the temporary
/// dimming of the work display it keeps on.
/// </summary>
internal sealed class NightSaver
{
    private readonly ScreenBlanker _blanker;
    private readonly AppSettings _settings;
    private readonly DdcWorker _worker;
    private readonly HashSet<string> _screens = new(StringComparer.Ordinal);

    /// <summary>The work display this session dimmed, until its brightness is restored.</summary>
    private string _dimmed;

    /// <summary>The level this session requested for that display.</summary>
    private int _dimTarget;

    public NightSaver(ScreenBlanker blanker, AppSettings settings, DdcWorker worker)
    {
        _blanker = blanker;
        _settings = settings;
        _worker = worker;
    }

    /// <summary>Restores still owed by this session, including the work display's brightness.</summary>
    public int Count => _screens.Count + (_dimmed == null ? 0 : 1);

    public NightWorkDim WorkDim { get; private set; }

    /// <returns>The number of failed blackouts, or -1 if the work display is unavailable.</returns>
    public int Start(IEnumerable<Monitor> monitors, string workKey)
    {
        var desk = monitors.ToList();
        var work = desk.FirstOrDefault(m => m.StableKey == workKey);
        if (work == null || _blanker.IsBlanked(workKey) || _settings.ScreenBlankActiveFor(workKey))
            return -1;
        if (Count > 0) return 0;

        int failed = 0;
        foreach (var m in desk.Where(m => m.StableKey != workKey))
        {
            if (_blanker.IsBlanked(m.StableKey)) continue;
            if (_blanker.Blank(m, activate: false)) _screens.Add(m.StableKey);
            else failed++;
        }
        WorkDim = DimWork(work);
        return failed;
    }

    /// <summary>
    /// Lower the work display to the Night preset's estimated target, never
    /// raise it. The target is the person's own Night setting, so "dim enough
    /// to work in the dark" is already something they can tune.
    /// </summary>
    private NightWorkDim DimWork(Monitor work)
    {
        if (!work.SupportsBrightness) return NightWorkDim.Unsupported;
        int target = Presets.BrightnessFor(work, _settings.PresetLevel("Night").Nits);
        // A marker left by an earlier, unrecovered dim already holds the real
        // original brightness; the current level is not it, so keep the marker.
        bool owed = _settings.NightDimActiveFor(work.StableKey);
        if (!owed)
        {
            if (target >= work.Brightness) return NightWorkDim.AlreadyDim;
            // Persist the way back before changing anything, as a blackout does.
            _settings.SetNightDim(work.StableKey, work.DisplayName, true, Presets.RawToPercent(work, work.Brightness), work.Brightness);
            if (!_settings.Save())
            {
                _settings.SetNightDim(work.StableKey, work.DisplayName, false, null, null);
                return NightWorkDim.NotSaved;
            }
        }
        _dimmed = work.StableKey;
        _dimTarget = Math.Min(target, work.Brightness);
        if (target < work.Brightness) _worker.Set(work, DdcWorker.Feature.Brightness, target);
        return NightWorkDim.Dimmed;
    }

    public void Restore(IEnumerable<Monitor> monitors)
    {
        var desk = monitors.ToList();
        foreach (string key in _screens.ToArray())
        {
            if (_blanker.IsBlanked(key)) _blanker.Restore(key);
            else if (desk.FirstOrDefault(m => m.StableKey == key) is { } m)
                _blanker.RecoverInterruptedBlank(m);
            ForgetRestored(key);
        }
        RestoreWorkDim(desk);
    }

    /// <summary>Return the dimmed work display to its saved brightness; also used on exit.</summary>
    public void RestoreWorkDim(IEnumerable<Monitor> monitors)
    {
        // A disconnected display keeps its marker and stays owed.
        if (_dimmed != null && monitors.FirstOrDefault(m => m.StableKey == _dimmed) is { } work && RestoreDim(work))
            _dimmed = null;
    }

    /// <summary>
    /// The person chose a brightness for the dimmed work display during the
    /// night: a slider, preset, Custom or the schedule. That newer choice wins,
    /// so ending the night must not overwrite it. The cleared marker is saved
    /// with the change that caused it.
    /// </summary>
    /// <returns>True when this released the session's dim.</returns>
    public bool TakeOver(string key)
    {
        if (key == null || key != _dimmed) return false;
        _settings.SetNightDim(key, null, false, null, null);
        _dimmed = null;
        return true;
    }

    /// <summary>
    /// Attach this session's dim to the fresh Monitor after a scan. Refresh drops
    /// queued writes, including a dim not yet sent, so request it again.
    /// </summary>
    /// <returns>True when this session owns the display's dim.</returns>
    public bool Rebind(Monitor monitor)
    {
        if (monitor == null || monitor.StableKey != _dimmed) return false;
        if (monitor.SupportsBrightness && monitor.Brightness > _dimTarget)
            _worker.Set(monitor, DdcWorker.Feature.Brightness, _dimTarget);
        return true;
    }

    /// <summary>
    /// Recover a dim left by a killed process. Like an interrupted blackout,
    /// restoring is the only safe interpretation at the next scan.
    /// </summary>
    public bool RecoverInterruptedDim(Monitor monitor) =>
        monitor != null && monitor.StableKey != _dimmed &&
        _settings.NightDimActiveFor(monitor.StableKey) && RestoreDim(monitor);

    private bool RestoreDim(Monitor monitor)
    {
        var entry = _settings.Find(monitor.StableKey);
        if (entry is not { NightDimActive: true }) return true;
        double? percent = entry.NightDimBrightnessPercent;
        int? saved = entry.NightDimBrightnessRaw;
        if (_settings.ScreenBlankActiveFor(monitor.StableKey))
        {
            // Blanked on its own during the night: that blackout saved the
            // dimmed level. Hand it the original instead of writing under it.
            _blanker.AdoptOriginal(monitor.StableKey, percent, saved);
        }
        else if (saved.HasValue || percent.HasValue)
        {
            if (!monitor.SupportsBrightness) return false;
            int raw = saved is int value ? Math.Clamp(value, monitor.BrightnessMin, monitor.BrightnessMax)
                : Presets.PercentToRaw(monitor, percent.Value);
            if (!_worker.RestoreBrightness(monitor, raw)) return false;
        }
        // A marker with no saved level cannot be restored; clearing it beats
        // inventing a brightness.
        _settings.SetNightDim(monitor.StableKey, monitor.DisplayName, false, null, null);
        if (_settings.Save()) return true;
        // Keep the debt where the next attempt can see it.
        _settings.SetNightDim(monitor.StableKey, monitor.DisplayName, true, percent, saved);
        return false;
    }

    public void ForgetRestored(string key)
    {
        if (!_settings.ScreenBlankActiveFor(key)) _screens.Remove(key);
    }
}
