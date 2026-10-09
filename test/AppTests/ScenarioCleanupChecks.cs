internal static partial class Program
{
    private static void CheckScenarioCleanup(string directory)
    {
        foreach (int original in new[] { 0, 17, unchecked((int)0xA000002A) })
        {
            string target = Path.Combine(directory, "cleanup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(target);
            using var errors = new StringWriter();
            Assert(ScenarioCleanup.Complete(target, original, TimeSpan.Zero, errors) == original
                && !Directory.Exists(target) && errors.ToString() == "",
                "successful scenario cleanup preserves every bit of the original exit status");
        }
        foreach (int original in new[] { 0, 17 })
        {
            string target = Path.Combine(directory, "cleanup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(target);
            using var errors = new StringWriter();
            using (var held = new FileStream(Path.Combine(target, "held.txt"), FileMode.Create, FileAccess.Write, FileShare.None))
                Assert(ScenarioCleanup.Complete(target, original, TimeSpan.Zero, errors) == 1
                    && errors.ToString().Contains($"after scenario exit {original}") && Directory.Exists(target),
                    "locked scenario cleanup fails explicitly and reports the original result instead of crashing");
            Assert(ScenarioCleanup.Complete(target, original, TimeSpan.FromSeconds(1), TextWriter.Null) == original,
                "scenario cleanup succeeds after the file lock is released");
        }
        string retry = Path.Combine(directory, "cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(retry);
        var transient = new FileStream(Path.Combine(retry, "held.txt"), FileMode.Create, FileAccess.Write, FileShare.None);
        var release = Task.Run(() => { Thread.Sleep(80); transient.Dispose(); });
        try
        {
            using var errors = new StringWriter();
            Assert(ScenarioCleanup.Complete(retry, 0, TimeSpan.FromSeconds(2), errors) == 0
                && !Directory.Exists(retry) && errors.ToString() == "",
                "bounded cleanup retries a transient real file lock before reporting success");
        }
        finally { release.GetAwaiter().GetResult(); }
    }
}
