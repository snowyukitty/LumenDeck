using LumenDeck;

internal static partial class Program
{
    private static void CheckCapturedSettings(string directory)
    {
        string path = Path.Combine(directory, "captured-settings.json");
        var settings = AppSettings.Load(path);
        settings.StartMinimised = true;
        settings.Monitors.Add(new AppSettings.MonitorSetting { Key = "captured", CustomBrightnessPercent = 58 });
        var operation = settings.PrepareSave();
        settings.StartMinimised = false;
        settings.Monitors[0].CustomBrightnessPercent = 73;
        settings.Monitors.Add(new AppSettings.MonitorSetting { Key = "later" });
        bool wrongThreadRejected = Task.Run(() =>
        {
            try { operation.Complete(SettingsSaveResult.Failed); return false; }
            catch (InvalidOperationException) { return true; }
        }).GetAwaiter().GetResult();
        Assert(wrongThreadRejected && settings.IsSavePending, "foreign-thread completion cannot release settings admission");
        var result = Task.Run(operation.Write).GetAwaiter().GetResult();
        Assert(result == SettingsSaveResult.Saved && settings.IsSavePending && !settings.Save(),
            "completed disk write retains admission until capturing-thread completion");
        var persisted = AppSettings.Load(path);
        Assert(persisted.StartMinimised && persisted.Find("captured").CustomBrightnessPercent == 58 && persisted.Monitors.Count == 1,
            "worker writes the captured payload instead of rereading changed flags or monitor entries");
        operation.Complete(result);
        operation.Complete(result);
        Assert(!settings.IsSavePending && settings.Save() && AppSettings.Load(path).Find("captured").CustomBrightnessPercent == 73,
            "idempotent completion releases admission for a fresh live-state snapshot");
        bool repeatRejected = false;
        try { operation.Write(); }
        catch (InvalidOperationException) { repeatRejected = true; }
        Assert(repeatRejected, "an old captured write cannot run again after a newer save");
        var abandoned = settings.PrepareSave();
        abandoned.Complete(SettingsSaveResult.Saved);
        bool abandonedRejected = false;
        try { abandoned.Write(); }
        catch (InvalidOperationException) { abandonedRejected = true; }
        Assert(abandonedRejected && settings.LastSaveResult == SettingsSaveResult.Failed && !settings.IsSavePending,
            "unstarted completion cannot claim success or later run its abandoned snapshot");

        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int completions = 0;
        var running = new SettingsSaveOperation(() =>
        {
            entered.Set();
            if (!release.Wait(5000)) throw new TimeoutException("isolated write barrier");
            return SettingsSaveResult.Saved;
        }, _ => completions++);
        var writer = Task.Run(running.Write);
        try
        {
            Assert(entered.Wait(2000), "controlled settings writer enters its barrier");
            bool earlyRejected = false;
            try { running.Complete(SettingsSaveResult.Saved); }
            catch (InvalidOperationException) { earlyRejected = true; }
            Assert(earlyRejected && completions == 0, "capturing thread cannot release a write still in progress");
        }
        finally { release.Set(); }
        running.Complete(writer.GetAwaiter().GetResult());
        Assert(completions == 1, "settled write delivers completion exactly once");
    }
}
