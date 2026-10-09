using System.Collections.Concurrent;
using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    private sealed class FakeDdcTransport : IDdcTransport
    {
        public readonly ConcurrentQueue<(Monitor Monitor, DdcWorker.Feature Feature, byte Code, int Value)> Writes = new();
        public readonly ConcurrentDictionary<(Monitor, DdcWorker.Feature), int> Hardware = new();
        public Func<Monitor, DdcWorker.Feature, byte, int, bool> OnWrite;
        public Func<Monitor, DdcWorker.Feature, (bool Read, int Value)> OnRead;
        public Func<Monitor, int, bool> OnRestore;
        public bool Available = true;
        public bool CanWrite(Monitor monitor) => Available;
        public bool Write(Monitor monitor, DdcWorker.Feature feature, byte code, int value)
        {
            Writes.Enqueue((monitor, feature, code, value));
            if (OnWrite != null) return OnWrite(monitor, feature, code, value);
            Hardware[(monitor, feature)] = value;
            return true;
        }
        public bool Read(Monitor monitor, DdcWorker.Feature feature, out int actual)
        {
            if (OnRead != null)
            {
                var read = OnRead(monitor, feature);
                actual = read.Value;
                return read.Read;
            }
            return Hardware.TryGetValue((monitor, feature), out actual);
        }
        public bool RestoreBrightness(Monitor monitor, int value)
        {
            bool restored = OnRestore?.Invoke(monitor, value) ?? true;
            if (restored) Hardware[(monitor, DdcWorker.Feature.Brightness)] = value;
            return restored;
        }
    }

    private static void WaitFor(ManualResetEventSlim signal, string description) =>
        Assert(signal.Wait(5000), description);

    private static void WaitIdle(DdcWorker worker) =>
        Assert(SpinWait.SpinUntil(() => worker.Idle, 5000), "worker drains queued and in-flight work");

    private static void Pause(ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        entered.Set();
        if (!release.Wait(5000)) throw new TimeoutException("Fake transport barrier was not released.");
    }

    private static void CheckDdcWorker()
    {
        CheckReadbackSupersession(true);
        CheckReadbackSupersession(false);
        CheckReadbackSupersession(false, true);
        CheckCoalescing();
        CheckFailureObservers();
        CheckVerificationRetries();
        CheckCancellationAndDisposal();
        CheckRestoreOrdering();
        CheckTrayReadOrdering();
    }

    private static void CheckReadbackSupersession(bool accepted, bool sameValue = false)
    {
        using var monitor = Display("worker-readback");
        using var reading = new ManualResetEventSlim();
        using var releaseRead = new ManualResetEventSlim();
        using var writingNew = new ManualResetEventSlim();
        using var releaseNew = new ManualResetEventSlim();
        var transport = new FakeDdcTransport();
        int newValue = sameValue ? 10 : 90;
        transport.OnWrite = (m, feature, code, value) =>
        {
            if (reading.IsSet) Pause(writingNew, releaseNew);
            if (!reading.IsSet && !accepted) return false;
            transport.Hardware[(m, feature)] = value;
            return true;
        };
        transport.OnRead = (m, feature) =>
        {
            if (!reading.IsSet)
            {
                Pause(reading, releaseRead);
                return (true, accepted ? 10 : 5);
            }
            return (true, transport.Hardware[(m, feature)]);
        };
        using var worker = new DdcWorker(transport);
        int failures = 0;
        worker.WriteFailed += (_, _) => Interlocked.Increment(ref failures);
        try
        {
            worker.Set(monitor, DdcWorker.Feature.Brightness, 10);
            WaitFor(reading, "old request reaches controlled readback");
            Assert(!worker.Idle, "dequeued but unfinished readback is not idle");
            var enqueue = Task.Run(() => worker.Set(monitor, DdcWorker.Feature.Brightness, newValue));
            Assert(enqueue.Wait(2000), "enqueue remains responsive while readback is blocked");
            Assert(monitor.Brightness == newValue, "enqueue publishes the new optimistic model atomically");
            releaseRead.Set();
            WaitFor(writingNew, "new request follows the superseded readback");
            Assert(monitor.Brightness == newValue, "old successful or failed-write readback cannot replace the newer value");
            releaseNew.Set();
            WaitIdle(worker);
            Assert(monitor.Brightness == newValue && failures == 0, "superseded verification neither rolls back nor raises a final failure");
            Assert(transport.Writes.Count(w => w.Value == 10) == (accepted ? 1 : 3) + (sameValue ? 1 : 0), "final refusal retries stay bounded");
        }
        finally { releaseRead.Set(); releaseNew.Set(); }
    }

    private static void CheckCoalescing()
    {
        using var monitor = Display("same-key");
        using var replacement = Display("same-key");
        using var writing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var transport = new FakeDdcTransport();
        transport.OnWrite = (m, feature, code, value) =>
        {
            if (ReferenceEquals(m, monitor) && feature == DdcWorker.Feature.Brightness && value == 1)
                Pause(writing, release);
            transport.Hardware[(m, feature)] = value;
            return true;
        };
        using var worker = new DdcWorker(transport);
        try
        {
            worker.Set(monitor, DdcWorker.Feature.Brightness, 1);
            WaitFor(writing, "coalescing starts with an in-flight request");
            worker.Set(monitor, DdcWorker.Feature.Brightness, 2);
            worker.Set(monitor, DdcWorker.Feature.Brightness, 3);
            worker.Set(monitor, DdcWorker.Feature.Contrast, 44);
            worker.SetVcp(monitor, 0x62, 4);
            worker.SetVcp(monitor, 0x62, 9);
            worker.SetVcp(monitor, 0x87, 6);
            worker.SetVcp(monitor, 0xD6, 1);
            worker.Set(replacement, DdcWorker.Feature.Brightness, 77);
            Assert(monitor.Brightness == 3 && monitor.Contrast == 44, "independent requested controls retain their values");
            release.Set();
            WaitIdle(worker);
            var writes = transport.Writes.ToArray();
            Assert(!writes.Any(w => w.Feature == DdcWorker.Feature.Brightness && w.Value == 2), "intermediate brightness is coalesced away");
            Assert(writes.Count(w => w.Feature == DdcWorker.Feature.Vcp && w.Code == 0x62) == 1 &&
                writes.Any(w => w.Code == 0x62 && w.Value == 9) && writes.Any(w => w.Code == 0x87 && w.Value == 6),
                "VCP codes coalesce independently");
            Assert(!writes.Any(w => w.Code == 0xD6), "hardware-off never reaches an injected transport");
            Assert(writes.Any(w => ReferenceEquals(w.Monitor, replacement) && w.Value == 77),
                "replacement identity is separate even when its stable key is reused");
        }
        finally { release.Set(); }
    }

    private static void CheckFailureObservers()
    {
        using var monitor = Display("worker-failure");
        using var notified = new ManualResetEventSlim();
        var transport = new FakeDdcTransport();
        transport.OnWrite = (m, feature, code, value) =>
        {
            if (feature == DdcWorker.Feature.Brightness) return false;
            transport.Hardware[(m, feature)] = value;
            return true;
        };
        transport.OnRead = (m, feature) => (true, feature == DdcWorker.Feature.Brightness ? 8 : transport.Hardware[(m, feature)]);
        using var worker = new DdcWorker(transport);
        int firstCalls = 0, secondCalls = 0;
        bool unlocked = false;
        worker.WriteFailed += (_, _) =>
        {
            Interlocked.Increment(ref firstCalls);
            var probe = Task.Run(() => { lock (monitor.WriteGate) worker.Set(monitor, DdcWorker.Feature.Contrast, 55); });
            unlocked = probe.Wait(2000);
            throw new InvalidOperationException("Injected observer error");
        };
        worker.WriteFailed += (_, _) => { Interlocked.Increment(ref secondCalls); notified.Set(); };
        worker.Set(monitor, DdcWorker.Feature.Brightness, 90);
        WaitFor(notified, "healthy observer receives the terminal failure despite another observer throwing");
        WaitIdle(worker);
        Assert(firstCalls == 1 && secondCalls == 1, "observer exceptions do not duplicate terminal failures");
        Assert(unlocked && monitor.Contrast == 55, "callbacks run outside both locks and the writer continues processing");
        Assert(monitor.Brightness == 8 && transport.Writes.Count(w => w.Feature == DdcWorker.Feature.Brightness) == 3,
            "final refusal restores observed value after exactly three attempts");

        using var unavailable = new DdcWorker(new FakeDdcTransport { Available = false });
        unavailable.Set(monitor, DdcWorker.Feature.Brightness, 25);
        WaitIdle(unavailable);
        using var selfStopped = new ManualResetEventSlim();
        var throwing = new FakeDdcTransport { OnWrite = (_, _, _, _) => throw new InvalidOperationException("Injected driver error") };
        using var selfDisposing = new DdcWorker(throwing);
        selfDisposing.WriteFailed += (_, _) => { selfDisposing.Dispose(); selfStopped.Set(); };
        selfDisposing.Set(monitor, DdcWorker.Feature.Brightness, 45);
        WaitFor(selfStopped, "driver exception is reported and a callback can dispose its own worker");
        selfDisposing.Set(monitor, DdcWorker.Feature.Contrast, 100);
        Assert(monitor.Contrast == 55, "enqueue after disposal is ignored without touching model or wait handle");
    }

    private static void CheckVerificationRetries()
    {
        foreach (string scenario in new[] { "mismatch", "unreadable", "eventual-success", "internal", "vcp" })
        {
            using var monitor = Display("worker-verification");
            monitor.IsInternalPanel = scenario == "internal";
            using var failed = new ManualResetEventSlim();
            int reads = 0, failures = 0;
            var transport = new FakeDdcTransport();
            transport.OnWrite = (_, _, _, _) => scenario is not ("internal" or "vcp");
            transport.OnRead = (_, _) =>
            {
                int count = Interlocked.Increment(ref reads);
                return (scenario != "unreadable", scenario == "eventual-success" && count == 3 ? 90 : 8);
            };
            using var worker = new DdcWorker(transport);
            worker.WriteFailed += (_, _) => { Interlocked.Increment(ref failures); failed.Set(); };
            if (scenario == "vcp") worker.SetVcp(monitor, 0x62, 90);
            else worker.Set(monitor, DdcWorker.Feature.Brightness, 90);
            if (scenario != "eventual-success") WaitFor(failed, "exhausted verification raises one failure");
            WaitIdle(worker);
            Assert(transport.Writes.Count == (scenario is "internal" or "vcp" ? 1 : 3), "verification preserves write-attempt bounds");
            Assert(reads == (scenario is "internal" or "vcp" ? 0 : 3), "only external brightness and contrast receive readback verification");
            Assert(failures == (scenario == "eventual-success" ? 0 : 1), "verification success or exhaustion produces the correct terminal outcome");
            if (scenario == "mismatch") Assert(monitor.Brightness == 8, "silent dropped writes roll back to observed brightness");
            if (scenario == "unreadable") Assert(monitor.Brightness == 90, "unreadable final state does not invent an observed value");
        }
    }

    private static void CheckCancellationAndDisposal()
    {
        foreach (bool dispose in new[] { false, true })
        {
            using var monitor = Display("worker-cancel");
            using var reading = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var transport = new FakeDdcTransport { OnRead = (_, _) => { Pause(reading, release); return (true, 5); } };
            using var worker = new DdcWorker(transport);
            int failures = 0;
            worker.WriteFailed += (_, _) => Interlocked.Increment(ref failures);
            try
            {
                worker.Set(monitor, DdcWorker.Feature.Brightness, 10);
                WaitFor(reading, "cancellation reaches an in-flight readback");
                worker.Set(monitor, DdcWorker.Feature.Contrast, 30);
                if (dispose)
                    Assert(Task.Run(worker.Dispose).Wait(3000), "disposal is bounded while transport is blocked");
                else worker.ClearPending();
                Assert(!worker.Idle, "cancellation does not falsely report blocked hardware as idle");
                release.Set();
                WaitIdle(worker);
                Assert(monitor.Brightness == 10 && failures == 0, "cancelled readback cannot commit or report failure");
                Assert(transport.Writes.Count == 1, "cancelled pending work and retries never reach transport");
            }
            finally { release.Set(); }
        }
    }

    private static void CheckTrayReadOrdering()
    {
        using var monitor = Display("worker-tray-read");
        monitor.SupportsBrightness = monitor.SupportsContrast = true;
        using var reading = new ManualResetEventSlim();
        using var releaseRead = new ManualResetEventSlim();
        using var writing = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        var transport = new FakeDdcTransport();
        transport.OnRead = (m, feature) =>
        {
            if (!reading.IsSet)
            {
                Pause(reading, releaseRead);
                return (true, 5);
            }
            return (transport.Hardware.TryGetValue((m, feature), out int actual), actual);
        };
        transport.OnWrite = (m, feature, code, value) =>
        {
            if (feature == DdcWorker.Feature.Brightness) Pause(writing, releaseWrite);
            transport.Hardware[(m, feature)] = value;
            return true;
        };
        using var worker = new DdcWorker(transport);
        try
        {
            var refresh = Task.Run(() => worker.RefreshValues(monitor));
            WaitFor(reading, "tray refresh reaches a controlled observation");
            Assert(!worker.Idle, "tray observation is included in active I/O state");
            worker.Set(monitor, DdcWorker.Feature.Brightness, 99);
            worker.Set(monitor, DdcWorker.Feature.Contrast, 84);
            releaseRead.Set();
            Assert(refresh.Wait(2000), "tray observation finishes without waiting for newer queued writes");
            WaitFor(writing, "new adjustment follows the tray observation");
            Assert(monitor.Brightness == 99 && monitor.Contrast == 84,
                "tray readback does not overwrite new brightness or queued contrast");
            releaseWrite.Set();
            WaitIdle(worker);
            Assert(monitor.Brightness == 99 && monitor.Contrast == 84, "new controls remain current after tray refresh and write verification");
        }
        finally { releaseRead.Set(); releaseWrite.Set(); }
    }

    private static void CheckRestoreOrdering()
    {
        using var monitor = Display("worker-restore");
        var transport = new FakeDdcTransport();
        using var worker = new DdcWorker(transport);
        lock (monitor.WriteGate)
        {
            worker.Set(monitor, DdcWorker.Feature.Brightness, 0);
            Assert(worker.RestoreBrightness(monitor, 70), "restore succeeds while cancelling queued minimum brightness");
        }
        WaitIdle(worker);
        Assert(transport.Writes.IsEmpty && monitor.Brightness == 70, "old minimum cannot run after synchronous restore");

        using var restoring = new ManualResetEventSlim();
        using var releaseRestore = new ManualResetEventSlim();
        using var writing = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        transport.OnRestore = (_, _) => { Pause(restoring, releaseRestore); return true; };
        transport.OnWrite = (m, feature, code, value) =>
        {
            Pause(writing, releaseWrite);
            transport.Hardware[(m, feature)] = value;
            return true;
        };
        try
        {
            var restore = Task.Run(() => worker.RestoreBrightness(monitor, 70));
            WaitFor(restoring, "synchronous restore reaches controlled transport");
            Assert(!worker.Idle, "restore I/O is included in idle state");
            var enqueue = Task.Run(() => worker.Set(monitor, DdcWorker.Feature.Brightness, 99));
            Assert(enqueue.Wait(2000), "enqueue does not wait for synchronous restore I/O");
            releaseRestore.Set();
            Assert(restore.Wait(2000) && restore.Result, "restore returns its confirmed transport result");
            WaitFor(writing, "post-restore request is preserved");
            Assert(monitor.Brightness == 99, "restore commit cannot overwrite a request queued during its I/O");
            releaseWrite.Set();
            WaitIdle(worker);
            Assert(transport.Hardware[(monitor, DdcWorker.Feature.Brightness)] == 99, "new requested brightness follows restore in hardware order");
            restoring.Reset();
            releaseRestore.Reset();
            var cancelledRestore = Task.Run(() => worker.RestoreBrightness(monitor, 70));
            WaitFor(restoring, "second restore starts before cancellation");
            worker.Set(monitor, DdcWorker.Feature.Brightness, 88);
            worker.ClearPending();
            releaseRestore.Set();
            Assert(cancelledRestore.Wait(2000) && cancelledRestore.Result, "in-flight restore reports hardware outcome after cancellation");
            WaitIdle(worker);
            Assert(monitor.Brightness == 88 && transport.Writes.Count == 1,
                "clear invalidates restore model commit as well as later queued writes");
        }
        finally { releaseRestore.Set(); releaseWrite.Set(); }
    }
}
