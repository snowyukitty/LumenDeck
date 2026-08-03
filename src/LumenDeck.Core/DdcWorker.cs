using System.Collections.Concurrent;

namespace LumenDeck;

/// <summary>
/// Serialises every brightness/contrast write onto one background thread.
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
    public enum Feature { Brightness, Contrast }

    private readonly record struct Target(Monitor Mon, Feature What);

    private readonly ConcurrentDictionary<Target, int> _pending = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private volatile bool _running = true;

    /// <summary>
    /// Held around every native write, and by anyone about to call
    /// DestroyPhysicalMonitor. Without it the UI thread can free a handle while
    /// this thread is part way through a write to it - a use-after-free into a
    /// driver, which does not fail politely.
    /// </summary>
    public object HandleLock { get; } = new();

    /// <summary>Minimum gap between two writes, per MCCS. 60 ms is comfortably above the 50 ms floor.</summary>
    private const int WriteGapMs = 60;

    /// <summary>
    /// Raised on the worker thread when a write reports failure.
    /// Subscribers must marshal to the UI thread themselves.
    /// </summary>
    public event Action<Monitor, Feature> WriteFailed;

    public DdcWorker()
    {
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "LumenDeckWriter",
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.Start();
    }

    /// <summary>Queue a value. Replaces any value not yet written for the same monitor and feature.</summary>
    public void Set(Monitor m, Feature what, int value)
    {
        if (m == null) return;
        _pending[new Target(m, what)] = value;
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

                bool ok = true;
                try
                {
                    if (key.Mon.IsInternalPanel)
                    {
                        // WMI, and only brightness: a laptop panel has no
                        // contrast control to speak to.
                        ok = key.What == Feature.Brightness
                             && WmiBrightness.Set(key.Mon.WmiInstanceName, value);
                    }
                    else
                    {
                        lock (HandleLock)
                        {
                            if (!_running) return;

                            // Dispose clears the flag, so this is the check that
                            // makes a stale queue entry harmless. It is a flag
                            // and not a handle comparison on purpose: a valid
                            // physical monitor handle can legitimately be 0.
                            if (!key.Mon.HasPhysicalHandle) continue;
                            IntPtr h = key.Mon.PhysicalHandle;

                            ok = key.What switch
                            {
                                Feature.Brightness => Native.SetMonitorBrightness(h, (uint)value),
                                Feature.Contrast => Native.SetMonitorContrast(h, (uint)value),
                                _ => true,
                            };
                        }
                    }

                    // A false return is a real failure - the monitor is asleep,
                    // on another input, or DDC dropped the request. Ignoring it
                    // is how a UI ends up showing a number the panel never took.
                    if (!ok) WriteFailed?.Invoke(key.Mon, key.What);
                }
                catch
                {
                    // A monitor unplugged mid-write throws through the P/Invoke.
                    // Dropping the write is correct: the enumeration is about to
                    // be rebuilt by the display-change handler.
                }

                // Outside the lock, so the UI thread is never blocked for the
                // full pacing delay while it waits to free handles.
                Thread.Sleep(WriteGapMs);
            }
        }
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
