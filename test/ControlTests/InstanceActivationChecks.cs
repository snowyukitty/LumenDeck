using System.Diagnostics;
using System.IO.Pipes;
using LumenDeck;

internal static partial class Program
{
    private static void CheckInstanceActivation()
    {
        string name = "LumenDeck-test-" + Guid.NewGuid().ToString("N");
        int reveals = 0;
        using var activation = new InstanceActivation(() => { Interlocked.Increment(ref reveals); return true; }, name, 200);
        Assert(activation.Start(), "activation receiver starts on a unique test channel");
        Assert(!activation.Start(), "activation receiver cannot start twice");
        uint serverPid = 0;
        for (int request = 1; request <= 3; request++)
        {
            Assert(InstanceActivation.RequestAsync(name, 2000, pid => serverPid = pid).GetAwaiter().GetResult(),
                "relaunch receives an acknowledgement without constructing another app");
            Assert(reveals == request, "each completed reveal request invokes the inert callback once");
        }
        Assert(serverPid == (uint)Environment.ProcessId, "foreground permission uses OS-reported connected server identity");
        foreach (byte[] invalid in new byte[][] { [0, 0, 0, 0], [0x4c, 0x44, 2, 1], [0x4c, 0x44, 1, 2], [0x4c, 0x44, 1, 1, 9] })
        {
            Assert(!RawActivation(name, invalid), "unknown, wrong-version and oversized messages are refused");
            Assert(reveals == 3, "invalid messages do not dispatch UI work");
        }
        using (var abandoned = TestPipeClient(name))
        {
            abandoned.Connect(2000);
            abandoned.Write([0x4c]);
        }
        Assert(InstanceActivation.RequestAsync(name, 2000, _ => { }).GetAwaiter().GetResult(),
            "abrupt partial disconnect does not terminate the receiver");
        using (var stalled = TestPipeClient(name))
        {
            stalled.Connect(2000);
            Assert(InstanceActivation.RequestAsync(name, 2000, _ => { }).GetAwaiter().GetResult(),
                "an idle slow peer is evicted and the next valid request succeeds");
        }
        Assert(reveals == 5, "invalid, disconnected and idle peers do not cause a reveal");
        for (int request = 0; request < 12; request++)
            Assert(InstanceActivation.RequestAsync(name, 2000, _ => { }).GetAwaiter().GetResult(),
                "back-to-back relaunches keep their acknowledgements");
        Assert(reveals == 17, "back-to-back requests have one callback per accepted message");
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "LumenDeck.ControlTests.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
        };
        start.ArgumentList.Add("--activation-client");
        start.ArgumentList.Add(name);
        using (var clientProcess = Process.Start(start))
        {
            bool exited = clientProcess.WaitForExit(5000);
            if (!exited) { clientProcess.Kill(); clientProcess.WaitForExit(2000); }
            Assert(exited && clientProcess.ExitCode == 0, "a separate inert process receives the primary's activation acknowledgement");
        }
        Assert(reveals == 18, "cross-process activation invokes one callback without starting the real app");
        using var collision = new InstanceActivation(() => throw new InvalidOperationException(), name);
        Assert(!collision.Start(), "an occupied activation channel fails closed without replacing its receiver");
        activation.Dispose();
        Assert(activation.Completion.Wait(2000), "receiver shutdown cancels waiting I/O and completes");
        activation.Dispose();
        Assert(!activation.Start(), "disposed activation receiver cannot restart");
        var watch = Stopwatch.StartNew();
        Assert(!InstanceActivation.RequestAsync(name, 150, _ => { }).GetAwaiter().GetResult(), "absent primary has a bounded fallback result");
        Assert(watch.Elapsed < TimeSpan.FromSeconds(2), "absent-primary request does not wait indefinitely");

        CheckActivationFailureRecovery();
        CheckActivationUnresponsiveServer();
        CheckActivationSessionAndShutdown();
    }

    private static NamedPipeClientStream TestPipeClient(string name) => new(".", name,
        PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private static bool RawActivation(string name, byte[] request)
    {
        try
        {
            using var client = TestPipeClient(name);
            client.Connect(2000);
            client.Write(request);
            using var deadline = new CancellationTokenSource(2000);
            byte[] ack = new byte[1];
            client.ReadExactlyAsync(ack, deadline.Token).AsTask().GetAwaiter().GetResult();
            try { client.Write([2]); }
            catch (IOException) { /* An acknowledgement was already read. */ }
            return ack[0] == 1;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { return false; }
    }

    private static void CheckActivationFailureRecovery()
    {
        string name = "LumenDeck-test-" + Guid.NewGuid().ToString("N");
        int attempts = 0;
        using var activation = new InstanceActivation(() =>
        {
            int attempt = Interlocked.Increment(ref attempts);
            if (attempt == 1) throw new InvalidOperationException("Injected dispatcher failure");
            return attempt > 2;
        }, name);
        Assert(activation.Start(), "failure-recovery receiver starts");
        Assert(!InstanceActivation.RequestAsync(name, 2000, _ => { }).GetAwaiter().GetResult(), "throwing dispatcher refuses activation");
        Assert(!InstanceActivation.RequestAsync(name, 2000, _ => { }).GetAwaiter().GetResult(), "closing dispatcher can refuse activation");
        Assert(InstanceActivation.RequestAsync(name, 2000, _ => throw new InvalidOperationException()).GetAwaiter().GetResult(),
            "foreground grant refusal does not prevent reveal acknowledgement");
        Assert(attempts == 3, "dispatch failures do not terminate later activation requests");
    }

    private static void CheckActivationUnresponsiveServer()
    {
        string name = "LumenDeck-test-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var connected = server.WaitForConnectionAsync();
        var watch = Stopwatch.StartNew();
        Assert(!InstanceActivation.RequestAsync(name, 200, _ => { }).GetAwaiter().GetResult(), "connected server without an acknowledgement times out");
        Assert(connected.Wait(2000) && watch.Elapsed < TimeSpan.FromSeconds(2), "overall client deadline includes acknowledgement wait");
    }

    private static void CheckActivationSessionAndShutdown()
    {
        using var process = Process.GetCurrentProcess();
        string rejectedName = "LumenDeck-test-" + Guid.NewGuid().ToString("N");
        int reveals = 0;
        using var wrongSession = new InstanceActivation(() => { reveals++; return true; }, rejectedName,
            expectedSession: (uint)process.SessionId + 1);
        Assert(wrongSession.Start(), "injected session-boundary receiver starts");
        Assert(!InstanceActivation.RequestAsync(rejectedName, 2000, _ => { }).GetAwaiter().GetResult() && reveals == 0,
            "OS client-session result must match the receiver's expected session");

        string name = "LumenDeck-test-" + Guid.NewGuid().ToString("N");
        using var activation = new InstanceActivation(() => { reveals++; return true; }, name);
        Assert(activation.Start(), "shutdown receiver starts");
        using var partial = TestPipeClient(name);
        partial.Connect(2000);
        partial.Write([0x4c]);
        activation.Dispose();
        Assert(activation.Completion.Wait(2000) && reveals == 0, "shutdown terminates a partial exchange without accepting UI work");
    }
}
