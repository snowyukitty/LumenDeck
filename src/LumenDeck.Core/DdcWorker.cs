using System.Collections.Concurrent;

namespace LumenDeck;

/// <summary>
/// Serialises every brightness/contrast/power write onto one background thread.
///
/// Three facts drive this design, all of them learned the hard way:
///
///  1. A DDC write takes 50-150 ms. Doing one per slider tick on the UI thread
///     freezes the window solid while a slider is dragged.
///  2. MCCS requires a gap between consecutive writes to the same monitor.
///     Writing red, green and blue back to back really does get commands
///     dropped - silently, with every call still reporting success.
///  3. Only the newest value matters. Dragging a slider from 20 to 80 produces
///     sixty intermediate values; writing all sixty would take ten seconds and
///     end in the same place. So this coalesces: one pending value per
///     (monitor, feature), last write wins.
///
/// Queue entries key on the Monitor object rather than a raw handle. Windows
/// recycles handle values, so a stale write addressed to a numeric handle can
/// land on a *different* monitor after a re-enumeration. Keying on identity and
/// re-checking the handle inside the lock makes that impossible.
///
/// A true return from SetMonitorBrightness means the request reached the driver
/// and nothing more - MCCS Set is fire-and-forget with no acknowledgement. Any
/// caller that needs certainty has to read the value back.
/// </summary>
internal sealed class DdcWorker : IDisposable
{
    public enum Feature { Brightness, Contrast, Vcp, Power }

    // Code is part of the key so two different VCP features on one monitor
    // coalesce independently - dragging the volume slider must not discard a
    // pending input-source change.
    private readonly record struct Target(Monitor Mon, Feature What, byte Code);

    private readonly ConcurrentDictionary<Target, int> _pending = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private volatile bool _running = true;

    // The Monitor owns per-device locking and MCCS pacing. Keeping that state on
    // the monitor is what lets a slow request on one display coexist with an
    // immediate brightness write to another.

    /// <summary>Time for firmware to make a set visible to a following read.</summary>
    private const int VerifySettleMs = 80;

    /// <summary>One initial write plus two retries for a silently dropped final value.</summary>
    private const int VerifyAttempts = 3;

    /// <summary>
    /// Raised on the worker thread when a write reports failure.
    /// Subscribers must marshal to the UI thread themselves.
    /// </summary>
    public event Action<Monitor, Feature, int> WriteFailed;

    /// <summary>Raised after a power request has been verified or dispatched.</summary>
    public event Action<Monitor, int> PowerCompleted;

    public DdcWorker()
    {
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "LumenDeckWriter",
            // Slider feedback is latency-sensitive and this thread spends most
            // of its life asleep. BelowNormal can starve it behind unrelated CPU
            // work for no useful saving.
            Priority = ThreadPriority.Normal,
        };
        _thread.Start();
    }

    /// <summary>Queue a value. Replaces any value not yet written for the same monitor and feature.</summary>
    public void Set(Monitor m, Feature what, int value)
    {
        if (m == null) return;
        _pending[new Target(m, what, 0)] = value;
        _signal.Set();
    }

    /// <summary>
    /// Queue any VCP code. Callers must only pass codes the monitor advertised -
    /// writing an unadvertised code is not rejected, it is simply obeyed by
    /// whatever that code happens to mean on that firmware.
    /// </summary>
    public void SetVcp(Monitor m, byte code, int value)
    {
        // D6 is intentionally not a generic extra control. It must pass through
        // SetPower so wake verification, persisted intent, and model safety
        // checks cannot be bypassed by a future caller.
        if (m == null || code == MonitorPower.VcpCode) return;
        _pending[new Target(m, Feature.Vcp, code)] = value;
        _signal.Set();
    }

    /// <summary>
    /// Queue an absolute DPM power target. The UI persists that target before
    /// calling this method, so a rebuild or restart can never turn a requested
    /// Wake back into another Off.
    /// </summary>
    public void SetPower(Monitor m, int target)
    {
        if (m == null || target is not (MonitorPower.On or MonitorPower.Off)) return;

        var key = new Target(m, Feature.Power, MonitorPower.VcpCode);
        _pending[key] = target;
        if (target == MonitorPower.Off) m.PowerMode = MonitorPower.Off;
        _signal.Set();
    }

    public bool Idle => _pending.IsEmpty;

    /// <summary>
    /// Drop everything still queued. Call before tearing down monitor handles:
    /// a write aimed at a handle that is about to be destroyed is worthless, and
    /// dropping it shortens the window the lock has to cover.
    /// </summary>
    public void ClearPending() => _pending.Clear();

    private void Loop()
    {
        while (_running)
        {
            if (_pending.IsEmpty)
            {
                _signal.WaitOne(250);
                continue;
            }

            foreach (var key in _pending.Keys)
            {
                if (!_running) return;
                if (!_pending.TryRemove(key, out int value)) continue;

                Process(key, value);
            }
        }
    }

    private void Process(Target key, int value)
    {
        try
        {
            bool verify = !key.Mon.IsInternalPanel &&
                          key.What is Feature.Brightness or Feature.Contrast;

            for (int attempt = 0; attempt < (verify ? VerifyAttempts : 1); attempt++)
            {
                if (!_running) return;
                if (!key.Mon.IsInternalPanel && !key.Mon.HasPhysicalHandle) return;

                bool ok = Write(key, value);
                if (!ok)
                {
                    // A transient driver refusal is no more authoritative than
                    // a silently dropped write. Give a final slider value the
                    // same bounded retry treatment, then restore the value the
                    // panel reports so the UI never claims success.
                    if (verify && attempt + 1 < VerifyAttempts &&
                        !_pending.ContainsKey(key))
                        continue;

                    if (verify && !_pending.ContainsKey(key) &&
                        ReadBack(key, out int reported))
                        StoreActual(key, reported);

                    WriteFailed?.Invoke(key.Mon, key.What, value);
                    return;
                }

                if (!verify)
                {
                    if (key.What == Feature.Power) PowerCompleted?.Invoke(key.Mon, value);
                    return;
                }

                // During a drag, the newer pending value is the verification:
                // do not spend another DDC round trip confirming an intermediate
                // pixel that is already obsolete.
                if (_pending.ContainsKey(key)) return;
                Thread.Sleep(VerifySettleMs);
                if (_pending.ContainsKey(key)) return;

                bool read = ReadBack(key, out int actual);
                if (_pending.ContainsKey(key)) return;

                if (read && actual == value)
                {
                    StoreActual(key, actual);
                    return;
                }

                // A true SetMonitorBrightness return only means the command was
                // handed to the driver. Firmware commonly drops it while still
                // reporting success. Retry the final value, then make the UI
                // honest if the panel continues to report something else.
                if (attempt + 1 == VerifyAttempts)
                {
                    if (read) StoreActual(key, actual);
                    WriteFailed?.Invoke(key.Mon, key.What, value);
                    return;
                }
            }
        }
        catch
        {
            // A monitor unplugged mid-request can throw through P/Invoke. Its
            // display-change rebuild will dispose this Monitor object and drop
            // the stale queue entry. Surface the failed request in the meantime
            // rather than leaving an optimistic slider value unchallenged.
            WriteFailed?.Invoke(key.Mon, key.What, value);
        }
    }

    private static bool Write(Target key, int value)
    {
        if (key.Mon.IsInternalPanel)
        {
            // WMI, and only brightness: a laptop panel has no contrast or DDC
            // power mode to speak to.
            return key.What == Feature.Brightness &&
                   WmiBrightness.Set(key.Mon.WmiInstanceName, value);
        }

        return key.What switch
        {
            Feature.Brightness => key.Mon.UseDdc(
                h => Native.SetMonitorBrightness(h, (uint)value), false),
            Feature.Contrast => key.Mon.UseDdc(
                h => Native.SetMonitorContrast(h, (uint)value), false),
            Feature.Vcp => key.Mon.UseDdc(
                h => Native.SetVCPFeature(h, key.Code, (uint)value), false),
            Feature.Power => MonitorPower.Set(key.Mon, value),
            _ => true,
        };
    }

    private static bool ReadBack(Target key, out int actual)
    {
        int observed = 0;
        bool ok = key.Mon.UseDdc(h =>
        {
            uint min = 0, current = 0, max = 0;
            bool read = key.What switch
            {
                Feature.Brightness => Native.GetMonitorBrightness(h, ref min, ref current, ref max),
                Feature.Contrast => Native.GetMonitorContrast(h, ref min, ref current, ref max),
                _ => false,
            };
            if (read) observed = (int)current;
            return read;
        }, false);
        actual = observed;
        return ok;
    }

    private static void StoreActual(Target key, int actual)
    {
        if (key.What == Feature.Brightness) key.Mon.Brightness = actual;
        else if (key.What == Feature.Contrast) key.Mon.Contrast = actual;
    }

    public void Dispose()
    {
        _running = false;
        _pending.Clear();
        _signal.Set();

        bool stopped = _thread.Join(1500);

        // Only dispose the handle if the thread is provably gone. Disposing it
        // underneath a still-running WaitOne throws ObjectDisposedException on a
        // background thread - an unhandled crash during shutdown, exactly when
        // nobody is watching. Leaking one event handle until process exit is the
        // better trade.
        if (stopped) _signal.Dispose();
    }
}
