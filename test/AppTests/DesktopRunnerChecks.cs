using System.Diagnostics;
using System.Text.Json;

internal static partial class Program
{
    private static readonly string[] QuotingProbe = ["space value", "quote\"value", "tail\\", "", "Unicode: 日本語"];

    private static int CheckDesktopRunner()
    {
        var echo = IsolatedDesktopRunner.Run(["--runner-probe", "echo", .. QuotingProbe], TimeSpan.FromSeconds(15));
        Assert(echo.ExitCode == 0 && !echo.TimedOut && echo.Output.Contains(JsonSerializer.Serialize(QuotingProbe)),
            "private-desktop child receives exact empty, Unicode, quoted and trailing-backslash arguments");
        var failure = IsolatedDesktopRunner.Run(["--runner-probe", "failure"], TimeSpan.FromSeconds(15));
        Assert(failure.ExitCode == 17 && !failure.TimedOut && failure.Output.Contains("synthetic worker failure"),
            "runner preserves a failing worker's exit code and stderr");
        var highBit = IsolatedDesktopRunner.Run(["--runner-probe", "high-bit-exit"], TimeSpan.FromSeconds(15));
        Assert(highBit.ExitCode == unchecked((int)0xA000002A) && !highBit.TimedOut,
            "runner preserves all bits of a Windows exit status");
        var forged = IsolatedDesktopRunner.Run(["--runner-probe", "wrong-desktop"], TimeSpan.FromSeconds(15));
        Assert(forged.ExitCode != 0 && forged.Output.Contains("not on its assigned private desktop"),
            "worker rejects a mismatched desktop identity before creating controls");
        var timeout = IsolatedDesktopRunner.Run(["--runner-probe", "hang"], TimeSpan.FromSeconds(5));
        Assert(timeout.TimedOut && timeout.ExitCode == 124, "runner stops a worker exceeding its deadline");
        var line = timeout.Output.Split('\n').Single(s => s.StartsWith("DESCENDANT:", StringComparison.Ordinal));
        int id = int.Parse(line["DESCENDANT:".Length..]);
        bool stopped;
        try { using var child = Process.GetProcessById(id); stopped = child.HasExited; }
        catch (ArgumentException) { stopped = true; }
        Assert(stopped, "timeout empties the job including the worker's descendant");
        FileStream held = null;
        string heldPath = null;
        try
        {
            IsolatedDesktopRunner.Run(["--runner-probe", "failure"], TimeSpan.FromSeconds(15), path =>
            {
                heldPath = path;
                held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            });
            throw new InvalidOperationException("Locked-log cleanup unexpectedly succeeded.");
        }
        catch (IsolatedDesktopRunner.CleanupException ex)
        {
            Assert(ex.WorkerResult.ExitCode == 17 && ex.WorkerResult.Output.Contains("synthetic worker failure"),
                "failed log cleanup retains the terminal worker's original exit code and stderr");
        }
        finally
        {
            held?.Dispose();
            if (heldPath != null) Directory.Delete(Path.GetDirectoryName(heldPath), recursive: true);
        }
        Console.WriteLine($"PASS: {_checks} private-desktop runner failure/lifetime checks.");
        return 0;
    }

    private static int RunDesktopProbe(string[] args)
    {
        switch (args[1])
        {
            case "echo": Console.WriteLine(JsonSerializer.Serialize(args[2..])); return 0;
            case "failure": Console.Error.WriteLine("synthetic worker failure"); return 17;
            case "high-bit-exit": return unchecked((int)0xA000002A);
            case "wrong-desktop":
                string[] forged = ["--desktop-worker", "LumenDeck-tests-" + Guid.NewGuid().ToString("N"), IsolatedDesktopRunner.WorkerLog];
                IsolatedDesktopRunner.EnterWorker(ref forged);
                throw new InvalidOperationException("Forged worker identity was accepted.");
            case "descendant":
                Console.WriteLine("descendant-ready");
                Thread.Sleep(60000);
                return 0;
            case "hang":
                string log = Path.Combine(Path.GetDirectoryName(IsolatedDesktopRunner.WorkerLog), "descendant.log");
                var start = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false, CreateNoWindow = true };
                if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add(typeof(Program).Assembly.Location);
                foreach (string arg in new[] { "--desktop-worker", IsolatedDesktopRunner.WorkerDesktop, log, "--runner-probe", "descendant" })
                    start.ArgumentList.Add(arg);
                using (var child = Process.Start(start))
                {
                    var ready = Stopwatch.StartNew();
                    while (!File.Exists(log) || !ReadLiveLog(log).Contains("descendant-ready"))
                    {
                        if (ready.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("Descendant probe did not start.");
                        Thread.Sleep(10);
                    }
                    Console.WriteLine($"DESCENDANT:{child.Id}");
                    Thread.Sleep(60000);
                }
                return 0;
            default: throw new ArgumentException("Unknown runner probe.");
        }
    }

    private static string ReadLiveLog(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
