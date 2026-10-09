namespace LumenDeck;

/// <summary>
/// Reversible per-monitor blackout.
///
/// A black topmost window hides the selected desktop without changing display
/// topology, while brightness is lowered to the monitor's supported minimum.
/// Unlike MCCS D6 hardware-off, the restore path never depends on sleeping
/// monitor firmware: a click, key, shortcut, app exit, or next launch can undo
/// the operation entirely from Windows.
/// </summary>
internal sealed class ScreenBlanker : IDisposable
{
    private sealed class Session
    {
        public Monitor Monitor;
        public double? OriginalBrightnessPercent;
        public int? OriginalBrightnessRaw;
        public BlankOverlay Overlay;
    }

    private readonly AppSettings _settings;
    private readonly DdcWorker _worker;
    private readonly Action<string, bool, bool> _stateChanged;
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    public ScreenBlanker(
        AppSettings settings, DdcWorker worker, Action<string, bool, bool> stateChanged)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _stateChanged = stateChanged ?? ((_, _, _) => { });
    }

    public bool IsBlanked(string key) =>
        !string.IsNullOrEmpty(key) && _sessions.ContainsKey(key);

    public bool Blank(Monitor monitor, bool activate = true)
    {
        if (monitor == null || string.IsNullOrEmpty(monitor.StableKey) ||
            monitor.Rect.Width <= 0 || monitor.Rect.Height <= 0)
            return false;

        if (IsBlanked(monitor.StableKey)) return true;
        if (_settings.ScreenBlankActiveFor(monitor.StableKey)) return false;

        double? original = monitor.SupportsBrightness
            ? Presets.RawToPercent(monitor, monitor.Brightness)
            : null;

        var overlay = new BlankOverlay(monitor.DisplayName, BoundsOf(monitor), activate);
        var session = new Session
        {
            Monitor = monitor,
            OriginalBrightnessPercent = original,
            OriginalBrightnessRaw = monitor.SupportsBrightness ? monitor.Brightness : null,
            Overlay = overlay,
        };

        overlay.RestoreRequested += () => Restore(monitor.StableKey);
        _sessions[monitor.StableKey] = session;

        // Persist first. If the process is killed between lowering brightness
        // and drawing the overlay, the next launch still knows what to restore.
        _settings.SetScreenBlank(monitor.StableKey, monitor.DisplayName, true, original,
            session.OriginalBrightnessRaw);
        if (!_settings.Save())
        {
            _sessions.Remove(monitor.StableKey);
            overlay.Dispose();
            _settings.SetScreenBlank(monitor.StableKey, monitor.DisplayName, false, null);
            return false;
        }

        try
        {
            overlay.Show();
            if (activate) overlay.Activate();
        }
        catch
        {
            _sessions.Remove(monitor.StableKey);
            overlay.CloseForRestore();
            _settings.SetScreenBlank(monitor.StableKey, monitor.DisplayName, false, null);
            _settings.Save();
            return false;
        }

        if (monitor.SupportsBrightness)
        {
            _worker.Set(monitor, DdcWorker.Feature.Brightness, monitor.BrightnessMin);
        }

        _stateChanged(monitor.StableKey, true, true);
        return true;
    }

    public bool Restore(string key)
    {
        if (string.IsNullOrEmpty(key) || !_sessions.Remove(key, out var session))
            return false;

        session.Overlay.CloseForRestore();
        bool restored = RestoreBrightness(
            session.Monitor, session.OriginalBrightnessPercent, session.OriginalBrightnessRaw);
        if (restored)
            _settings.SetScreenBlank(key, session.Monitor?.DisplayName, false, null);
        _settings.Save();
        _stateChanged(key, false, restored);
        return restored;
    }

    /// <summary>
    /// Make a blackout restore an earlier brightness than the one it saved.
    /// Night saver uses this when its dimmed work display was then blanked on
    /// its own: the blackout recorded the dimmed level, but the brightness the
    /// person should get back is the one from before the night began.
    /// </summary>
    public bool AdoptOriginal(string key, double? percent, int? raw)
    {
        if (string.IsNullOrEmpty(key) || !_settings.ScreenBlankActiveFor(key)) return false;
        if (_sessions.TryGetValue(key, out var session))
        {
            session.OriginalBrightnessPercent = percent;
            session.OriginalBrightnessRaw = raw;
        }
        _settings.SetScreenBlank(key, _settings.Find(key)?.LastSeenName, true, percent, raw);
        return true;
    }

    /// <summary>Attach an existing blackout to the fresh Monitor after Refresh.</summary>
    public void Rebind(Monitor monitor)
    {
        if (monitor == null || !_sessions.TryGetValue(monitor.StableKey, out var session)) return;
        session.Monitor = monitor;
        session.Overlay.Bounds = BoundsOf(monitor);
        // A session begun without brightness is overlay-only. Newly discovered
        // controls must not introduce a write that has no recovery snapshot.
        if (monitor.SupportsBrightness &&
            (session.OriginalBrightnessRaw.HasValue || session.OriginalBrightnessPercent.HasValue))
        {
            _worker.Set(monitor, DdcWorker.Feature.Brightness, monitor.BrightnessMin);
        }
    }

    /// <summary>
    /// Recover brightness left by a killed process. An overlay cannot survive
    /// process exit, so replaying the blank would be surprising; restoration is
    /// the only safe startup interpretation of a stale active marker.
    /// </summary>
    public bool RecoverInterruptedBlank(Monitor monitor)
    {
        if (monitor == null || IsBlanked(monitor.StableKey) ||
            !_settings.ScreenBlankActiveFor(monitor.StableKey))
            return false;

        bool restored = RestoreBrightness(
            monitor, _settings.ScreenBlankBrightnessFor(monitor.StableKey),
            _settings.Find(monitor.StableKey)?.ScreenBlankBrightnessRaw);
        if (!restored) return false;

        _settings.SetScreenBlank(monitor.StableKey, monitor.DisplayName, false, null);
        _settings.Save();
        _stateChanged(monitor.StableKey, false, true);
        return true;
    }

    public void RestoreAll()
    {
        foreach (string key in _sessions.Keys.ToArray()) Restore(key);
    }

    private bool RestoreBrightness(Monitor monitor, double? percent, int? originalRaw)
    {
        // Null means this was an overlay-only display, so there is no hardware
        // state to recover. A saved value plus unavailable DDC is different:
        // keep the persisted marker and retry after the monitor responds again.
        if (percent == null && originalRaw == null) return true;
        if (monitor == null || !monitor.SupportsBrightness) return false;
        int raw = originalRaw is int saved
            ? Math.Clamp(saved, monitor.BrightnessMin, monitor.BrightnessMax)
            : Presets.PercentToRaw(monitor, percent.Value);
        return _worker.RestoreBrightness(monitor, raw);
    }

    private static Rectangle BoundsOf(Monitor monitor) =>
        new(monitor.Rect.Left, monitor.Rect.Top, monitor.Rect.Width, monitor.Rect.Height);

    public void Dispose() => RestoreAll();

    private sealed class BlankOverlay : Form
    {
        private bool _closingForRestore;
        private readonly bool _activate;
        protected override bool ShowWithoutActivation => !_activate;

        public event Action RestoreRequested;

        public BlankOverlay(string monitorName, Rectangle bounds, bool activate)
        {
            _activate = activate;
            Text = $"{monitorName} — click or press a key to restore";
            Name = "LumenDeckScreenBlank";
            AccessibleName = $"Blanked {monitorName}; click or press a key to restore";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            BackColor = Color.Black;
            ForeColor = Color.Black;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Cursor = Cursors.Hand;

            MouseDown += (_, _) => RequestRestore();
            KeyDown += (_, _) => RequestRestore();
        }

        private void RequestRestore()
        {
            if (!_closingForRestore) RestoreRequested?.Invoke();
        }

        public void CloseForRestore()
        {
            _closingForRestore = true;
            Close();
            Dispose();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_closingForRestore && e.CloseReason != CloseReason.ApplicationExitCall)
            {
                e.Cancel = true;
                BeginInvoke(new Action(RequestRestore));
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
