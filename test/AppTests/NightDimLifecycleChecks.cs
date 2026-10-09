using System.Reflection;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    /// <summary>
    /// Night saver's work-display dimming through the actual window: dimmed on
    /// start, dimmed again after Refresh drops the queued write, released when
    /// the person chooses a preset, and restored on exit.
    /// </summary>
    private static void CheckNightDimLifecycle(string directory)
    {
        var restores = new List<(string Key, int Value)>();
        using var fixture = new Fixture(directory, enumerate: scan =>
        [
            new Display
            {
                StableKey = "night-work", Description = "Synthetic work model", IsPrimary = true,
                Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29200, Bottom = -29400 },
                SupportsBrightness = true, Brightness = 80, BrightnessMin = 0, BrightnessMax = 100,
            },
            new Display
            {
                StableKey = "night-side", Description = "Synthetic side model",
                Rect = new Native.RECT { Left = -29200, Top = -30000, Right = -28400, Bottom = -29400 },
                SupportsBrightness = true, Brightness = 60, BrightnessMin = 0, BrightnessMax = 100,
            },
        ], onRestore: (monitor, value) => { lock (restores) restores.Add((monitor.StableKey, value)); return true; });
        var form = fixture.Form;
        var night = Private<NightSaver>(form, "_nightSaver");
        var bar = Private<DeskCommandBar>(form, "_bar");
        var status = Private<StatusStripe>(form, "_status");
        MonitorCard Card(string key) => Controls(form).OfType<MonitorCard>().Single(c => c.Monitor.StableKey == key);
        int target = Presets.BrightnessFor(Card("night-work").Monitor, fixture.Settings.PresetLevel("Night").Nits);

        bar.NightButton.PerformClick();
        PumpUntil(() => fixture.Transport.Writes >= 2 && fixture.Worker.Idle, "night saver work-display write settles");
        Assert(Card("night-work").Monitor.IsPrimary && Card("night-work").Monitor.Brightness == target && target < 80 &&
            fixture.Settings.Find("night-work").NightDimBrightnessRaw == 80,
            "the kept primary display receives the lower Night brightness and retains its original level for restoration");
        Assert(night.WorkDim == NightWorkDim.Dimmed && night.Count == 2 && fixture.Settings.NightDimActiveFor("night-work") &&
            status.AccessibleName.Contains("dimmed toward the Night target") && bar.NightButton.Text == "Restore night desk",
            "Night saver dims the kept work display and says so");
        Assert(bar.NightButton.AccessibleDescription.Contains("return the work display to its saved brightness"),
            "the restore command explains that the work display comes back too");

        int writes = fixture.Transport.Writes;
        var refresh = (Task)typeof(MainForm).GetMethod("RebuildAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, [null]);
        PumpUntil(() => refresh.IsCompleted, "refresh during night");
        PumpUntil(() => fixture.Transport.Writes > writes, "re-dim after refresh");
        Assert(fixture.Settings.NightDimActiveFor("night-work") && night.Count == 2 && restores.Count == 0,
            "Refresh keeps the session's dim instead of recovering it as interrupted");

        Controls(Card("night-work")).OfType<FlatButton>().Single(b => b.Name == "preset-Day").PerformClick();
        Assert(!fixture.Settings.NightDimActiveFor("night-work") && night.Count == 1 && bar.NightButton.Text == "Restore night desk",
            "choosing a preset on the dimmed display hands its brightness back to the person");
        bar.NightButton.PerformClick();
        Assert(night.Count == 0 && restores.Select(r => r.Key).SequenceEqual(["night-side"]),
            "ending the night restores the blanked display and leaves the person's choice on the work display");

        lock (restores) restores.Clear();
        int beforeNight = Card("night-work").Monitor.Brightness;
        Assert(beforeNight > target, "the person's Day choice is brighter than the Night target");
        bar.NightButton.PerformClick();
        Assert(fixture.Settings.NightDimActiveFor("night-work"), "a second night dims the work display again");
        Private<NotifyIcon>(form, "_tray").ContextMenuStrip.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Exit").PerformClick();
        Assert(form.IsDisposed && restores.Contains(("night-work", beforeNight)) && !fixture.Settings.NightDimActiveFor("night-work"),
            "exit returns the work display to its pre-night brightness");
    }
}
