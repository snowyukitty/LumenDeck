namespace LumenDeck;

/// <summary>Own only the blackouts created by this night session.</summary>
internal sealed class NightSaver
{
    private readonly ScreenBlanker _blanker;
    private readonly AppSettings _settings;
    private readonly HashSet<string> _screens = new(StringComparer.Ordinal);

    public NightSaver(ScreenBlanker blanker, AppSettings settings)
    {
        _blanker = blanker;
        _settings = settings;
    }

    public int Count => _screens.Count;

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
        return failed;
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
    }

    public void ForgetRestored(string key)
    {
        if (!_settings.ScreenBlankActiveFor(key)) _screens.Remove(key);
    }
}
