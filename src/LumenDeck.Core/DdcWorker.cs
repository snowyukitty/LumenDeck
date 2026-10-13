namespace LumenDeck;

/// <summary>
/// Coalesces writes per monitor/control on one background thread. A short queue
/// lock owns request identity and optimistic/readback model commits; hardware
/// pacing and synchronous restore ordering belong to Monitor.WriteGate.
/// </summary>
internal sealed class DdcWorker : IDisposable
{
    public enum Feature { Brightness, Contrast, Vcp }
    internal enum ObservationKind { Observed, Pending, Confirmed, Unconfirmed, Failed }
    internal sealed record Observation(int Requested, int? LastObserved, ObservationKind Kind);
    private readonly Dictionary<Target, Observation> _observations = new();

    internal Observation Observe(Monitor monitor, Feature feature)
    {
        lock (_gate)
        {
            var target = new Target(monitor, feature, 0);
            if (_observations.TryGetValue(target, out var observation)) return observation;
            int value = feature == Feature.Brightness ? monitor.Brightness : monitor.Contrast;
            return new Observation(value, value, ObservationKind.Observed);
        }
    }
    private readonly record struct Target(Monitor Mon, Feature What, byte Code);
    private sealed record Request(int Value);
    private readonly object _gate = new();
    private readonly Dictionary<Target, Request> _pending = new();
    private readonly Dictionary<Target, Request> _current = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private readonly IDdcTransport _transport;
    private readonly int _settleMs;
    private volatile bool _running = true;
    private int _active;
    internal const int VerifyAttempts = 3;
    internal const int VerifySettleMilliseconds = 80;

    /// <summary>Raised once for a terminal failure, outside hardware and queue locks.</summary>
    public event Action<Monitor, Feature> WriteFailed;

    public DdcWorker() : this(new NativeDdcTransport(), VerifySettleMilliseconds) { }

    internal DdcWorker(IDdcTransport transport, int settleMs = 0)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        if (settleMs < 0) throw new ArgumentOutOfRangeException(nameof(settleMs));
        _settleMs = settleMs;
        _thread = new Thread(Loop)
        {
            IsBackground = true, Name = "LumenDeckWriter", Priority = ThreadPriority.Normal,
        };
        _thread.Start();
    }

    /// <summary>Atomically publish the requested model value and replace queued work.</summary>
    public void Set(Monitor m, Feature what, int value)
    {
        if (m == null || what is not (Feature.Brightness or Feature.Contrast)) return;
        Enqueue(new Target(m, what, 0), value);
    }

    /// <summary>Queue only advertised codes. D6 hardware-off remains unavailable.</summary>
    public void SetVcp(Monitor m, byte code, int value)
    {
        if (m == null || code == 0xD6) return;
        Enqueue(new Target(m, Feature.Vcp, code), value);
    }

    private void Enqueue(Target target, int value)
    {
        lock (_gate)
        {
            if (!_running) return;
            var request = new Request(value);
            int? observed = _observations.TryGetValue(target, out var previous) ? previous.LastObserved
                : target.What == Feature.Brightness ? target.Mon.Brightness : target.Mon.Contrast;
            _observations[target] = new Observation(value, observed, ObservationKind.Pending);
            _current[target] = _pending[target] = request;
            StoreActual(target, value);
            _signal.Set();
        }
    }

    /// <summary>No pending or active I/O/model finalization; observer delivery is excluded.</summary>
    public bool Idle
    {
        get { lock (_gate) return _pending.Count == 0 && _active == 0; }
    }

    /// <summary>
    /// Discard pending work and invalidate in-flight commits/retries. Already
    /// authorized native I/O cannot be cancelled; disposal still waits on its monitor.
    /// </summary>
    public void ClearPending()
    {
        lock (_gate) { _pending.Clear(); _current.Clear(); _observations.Clear(); }
    }

    private void Loop()
    {
        try
        {
            while (_running)
            {
                Target[] targets;
                lock (_gate) targets = _pending.Keys.ToArray();
                if (targets.Length == 0) { _signal.WaitOne(250); continue; }
                foreach (var target in targets)
                {
                    if (!_running) return;
                    Request request;
                    bool failed;
                    // Enqueue never takes WriteGate. No I/O, callbacks or waits
                    // run while holding _gate, and restore uses the same order.
                    lock (target.Mon.WriteGate)
                    {
                        lock (_gate)
                        {
                            if (!_running) return;
                            if (!_pending.Remove(target, out request)) continue;
                            _active++;
                        }
                        try { failed = Process(target, request); }
                        finally
                        {
                            lock (_gate)
                            {
                                _active--;
                                if (_current.TryGetValue(target, out var current) && ReferenceEquals(current, request))
                                    _current.Remove(target);
                            }
                        }
                    }
                    if (failed && _running) PublishFailure(target);
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _running = false;
                _pending.Clear();
                _current.Clear();
                // Only this thread releases its wait handle, after its last use.
                _signal.Dispose();
            }
        }
    }

    private bool Process(Target target, Request request)
    {
        try
        {
            bool verify = !target.Mon.IsInternalPanel &&
                          target.What is Feature.Brightness or Feature.Contrast;
            for (int attempt = 0; attempt < (verify ? VerifyAttempts : 1); attempt++)
            {
                if (!IsCurrent(target, request)) return false;
                if (!_transport.CanWrite(target.Mon))
                {
                    FinishObservation(target, request, ObservationKind.Unconfirmed);
                    return false;
                }
                bool written = _transport.Write(target.Mon, target.What, target.Code, request.Value);
                if (!IsCurrent(target, request)) return false;
                if (!verify)
                {
                    FinishObservation(target, request, written ? ObservationKind.Unconfirmed : ObservationKind.Failed);
                    return !written;
                }
                if (written)
                {
                    Thread.Sleep(_settleMs);
                    if (!IsCurrent(target, request)) return false;
                    bool read = _transport.Read(target.Mon, target.What, out int actual);
                    if (read && actual == request.Value)
                    {
                        CommitActual(target, request, actual);
                        return false;
                    }
                    if (attempt + 1 == VerifyAttempts)
                        return FinishFailure(target, request, read ? actual : null);
                }
                else if (attempt + 1 == VerifyAttempts)
                {
                    bool read = _transport.Read(target.Mon, target.What, out int actual);
                    return FinishFailure(target, request, read ? actual : null);
                }
            }
            return false;
        }
        catch
        {
            // Only transport failures enter this path; observer exceptions are
            // isolated after Process and cannot repeat a hardware request.
            FinishObservation(target, request, ObservationKind.Failed);
            return IsCurrent(target, request);
        }
    }

    private bool IsCurrent(Target target, Request request)
    {
        lock (_gate) return IsCurrentLocked(target, request);
    }

    private bool IsCurrentLocked(Target target, Request request) =>
        _running && _current.TryGetValue(target, out var current) && ReferenceEquals(current, request);

    private void CommitActual(Target target, Request request, int actual)
    {
        lock (_gate)
            if (IsCurrentLocked(target, request))
            {
                StoreActual(target, actual);
                _observations[target] = new Observation(actual, actual, ObservationKind.Confirmed);
            }
    }

    private void FinishObservation(Target target, Request request, ObservationKind kind)
    {
        lock (_gate)
            if (IsCurrentLocked(target, request))
                _observations[target] = new Observation(request.Value,
                    _observations.GetValueOrDefault(target)?.LastObserved, kind);
    }

    private bool FinishFailure(Target target, Request request, int? actual)
    {
        lock (_gate)
        {
            if (!IsCurrentLocked(target, request)) return false;
            if (actual is int observed) StoreActual(target, observed);
            _observations[target] = new Observation(request.Value,
                actual ?? _observations.GetValueOrDefault(target)?.LastObserved, ObservationKind.Failed);
            return true;
        }
    }

    private void PublishFailure(Target target)
    {
        var observers = WriteFailed;
        if (observers == null) return;
        foreach (Action<Monitor, Feature> observer in observers.GetInvocationList())
        {
            try { observer(target.Mon, target.What); }
            catch { /* One UI observer cannot terminate the writer or suppress another observer. */ }
        }
    }

    /// <summary>
    /// Re-read already available controls when returning from the tray. Preserve
    /// queued requests and invalidate observed values if a new adjustment arrives.
    /// Availability/range changes still require a full display scan.
    /// </summary>
    public void RefreshValues(Monitor m)
    {
        if (m == null) return;
        lock (m.WriteGate)
        {
            lock (_gate)
            {
                if (!_running) return;
                _active++;
            }
            try
            {
                foreach (var feature in new[] { Feature.Brightness, Feature.Contrast })
                {
                    if (feature == Feature.Brightness ? !m.SupportsBrightness : !m.SupportsContrast) continue;
                    var target = new Target(m, feature, 0);
                    var readRequest = new Request(0);
                    lock (_gate)
                    {
                        if (!_running) return;
                        if (_current.ContainsKey(target)) continue;
                        _current[target] = readRequest;
                    }
                    try
                    {
                        if (_transport.CanWrite(m) && _transport.Read(m, feature, out int actual))
                            CommitActual(target, readRequest, actual);
                    }
                    catch { /* A failed observation leaves the existing value; it is not a failed write. */ }
                    finally
                    {
                        lock (_gate)
                            if (_current.TryGetValue(target, out var current) && ReferenceEquals(current, readRequest))
                                _current.Remove(target);
                    }
                }
            }
            finally { lock (_gate) _active--; }
        }
    }

    /// <summary>Wait for the monitor's in-flight write, discard old brightness, then restore.</summary>
    public bool RestoreBrightness(Monitor m, int value)
    {
        if (m == null) return false;
        var target = new Target(m, Feature.Brightness, 0);
        var restoreRequest = new Request(value);
        lock (m.WriteGate)
        {
            lock (_gate)
            {
                if (!_running) return false;
                _pending.Remove(target);
                _current[target] = restoreRequest;
                _observations[target] = new Observation(value,
                    _observations.GetValueOrDefault(target)?.LastObserved ?? m.Brightness, ObservationKind.Pending);
                _active++;
            }
            try
            {
                bool restored = _transport.RestoreBrightness(m, value);
                if (restored && !m.IsInternalPanel) CommitActual(target, restoreRequest, value);
                else if (restored)
                {
                    lock (_gate) if (IsCurrentLocked(target, restoreRequest)) StoreActual(target, value);
                    FinishObservation(target, restoreRequest, ObservationKind.Unconfirmed);
                }
                else FinishObservation(target, restoreRequest, ObservationKind.Failed);
                return restored;
            }
            catch { FinishObservation(target, restoreRequest, ObservationKind.Failed); return false; }
            finally
            {
                lock (_gate)
                {
                    _active--;
                    if (_current.TryGetValue(target, out var current) && ReferenceEquals(current, restoreRequest))
                        _current.Remove(target);
                }
            }
        }
    }

    private static void StoreActual(Target target, int actual)
    {
        if (target.What == Feature.Brightness) target.Mon.Brightness = actual;
        else if (target.What == Feature.Contrast) target.Mon.Contrast = actual;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_running)
            {
                _running = false;
                _pending.Clear();
                _current.Clear();
                _observations.Clear();
                _signal.Set();
            }
        }
        // A callback may dispose its worker. Joining that same thread deadlocks.
        if (Thread.CurrentThread != _thread) _thread.Join(1500);
    }
}
