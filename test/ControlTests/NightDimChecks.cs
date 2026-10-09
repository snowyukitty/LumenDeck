using System.Text.Json.Nodes;
using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    /// <summary>
    /// Night saver's work display: dimmed toward the Night target, never
    /// brightened, with its original brightness persisted before any write and
    /// restored on Restore, on exit or after an interrupted session.
    /// </summary>
    private static void CheckNightDim(string directory)
    {
        string path = Path.Combine(directory, "night-dim.json");
        var settings = new AppSettings(path);
        var transport = new FakeDdcTransport();
        using var worker = new DdcWorker(transport, 0);
        using var blanker = new ScreenBlanker(settings, worker, (_, _, _) => { });
        var night = new NightSaver(blanker, settings, worker);
        using var work = Display("dim-work");
        using var side = Display("dim-side");
        work.SupportsBrightness = true;
        work.BrightnessMax = 100;
        work.Brightness = 80;
        Monitor[] desk = [work, side];
        int target = Presets.BrightnessFor(work, settings.PresetLevel("Night").Nits);
        Assert(target < 80, "the fixture's Night target is below the work display's brightness");

        settings.SeedCustom(work);
        double custom = settings.Find(work.StableKey).CustomBrightnessPercent.Value;
        bool? markerBeforeWrite = null;
        transport.OnWrite = (monitor, feature, _, value) =>
        {
            if (monitor == work && feature == DdcWorker.Feature.Brightness && markerBeforeWrite == null)
                markerBeforeWrite = AppSettings.Load(path).Find(work.StableKey) is { NightDimActive: true, NightDimBrightnessRaw: 80 };
            transport.Hardware[(monitor, feature)] = value;
            return true;
        };
        Assert(night.Start(desk, work.StableKey) == 0 && night.WorkDim == NightWorkDim.Dimmed && night.Count == 2 &&
            blanker.IsBlanked(side.StableKey) && !blanker.IsBlanked(work.StableKey),
            "night saver blanks the other display and dims the work display it keeps on");
        var stored = JsonNode.Parse(File.ReadAllText(path))["Monitors"].AsArray().Single(m => m["Key"].GetValue<string>() == work.StableKey);
        Assert(stored["NightDimActive"].GetValue<bool>() && stored["NightDimBrightnessRaw"].GetValue<int>() == 80,
            "the original work brightness is on disk before the dimming write");
        WaitFor(() => markerBeforeWrite != null, "work display dimming write");
        Assert(markerBeforeWrite == true, "the dimming write happens only after the original brightness is on disk");
        transport.OnWrite = null;
        Assert(!transport.Writes.Any(w => w.Monitor == work && w.Value > target), "dimming never requests a brighter level");
        work.Contrast = 30;
        settings.CaptureCustom(work);
        Assert(settings.Find(work.StableKey).CustomBrightnessPercent == custom,
            "a dimmed level is never remembered as the person's Custom brightness");

        // Refresh replaces the Monitor and drops queued writes; a dropped dim
        // shows up as the fresh object still at the original level.
        using (var rescanned = Display(work.StableKey))
        {
            rescanned.SupportsBrightness = true;
            rescanned.BrightnessMax = 100;
            rescanned.Brightness = 80;
            int before = transport.Writes.Count(w => w.Monitor == rescanned);
            Assert(night.Rebind(rescanned) && !night.RecoverInterruptedDim(rescanned) && settings.NightDimActiveFor(work.StableKey),
                "a rescanned work display stays owned by the session instead of being treated as interrupted");
            WaitFor(() => transport.Writes.Count(w => w.Monitor == rescanned && w.Value == target) > before, "re-dim after rescan");
        }

        night.Restore(desk);
        Assert(night.Count == 0 && !settings.NightDimActiveFor(work.StableKey) &&
            transport.Hardware[(work, DdcWorker.Feature.Brightness)] == 80,
            "Restore night desk returns the work display to its saved brightness and clears the marker");

        work.Brightness = 10;
        int quiet = transport.Writes.Count(w => w.Monitor == work);
        Assert(night.Start(desk, work.StableKey) == 0 && night.WorkDim == NightWorkDim.AlreadyDim && night.Count == 1 &&
            !settings.NightDimActiveFor(work.StableKey), "a work display already below the Night target is left alone");
        System.Threading.Thread.Sleep(50);
        Assert(transport.Writes.Count(w => w.Monitor == work) == quiet, "an already-dim work display receives no write");
        night.Restore(desk);

        // The person changes the dimmed display during the night: their choice wins.
        work.Brightness = 80;
        night.Start(desk, work.StableKey);
        WaitFor(() => transport.Hardware[(work, DdcWorker.Feature.Brightness)] == target, "dim before takeover");
        Assert(night.TakeOver(work.StableKey) && night.Count == 1 && !settings.NightDimActiveFor(work.StableKey) &&
            !night.TakeOver(side.StableKey), "a brightness choice during the night releases only the work display's dim");
        int beforeRestore = transport.Writes.Count(w => w.Monitor == work);
        night.Restore(desk);
        Assert(night.Count == 0 && transport.Writes.Count(w => w.Monitor == work) == beforeRestore &&
            transport.Hardware[(work, DdcWorker.Feature.Brightness)] == target,
            "ending the night does not overwrite a brightness the person chose");

        // Blanked on its own during the night: the blackout must bring back the pre-night level.
        work.Brightness = 80;
        night.Start(desk, work.StableKey);
        WaitFor(() => transport.Hardware[(work, DdcWorker.Feature.Brightness)] == target, "dim before blackout");
        work.Brightness = target;
        Assert(blanker.Blank(work, activate: false) && settings.Find(work.StableKey).ScreenBlankBrightnessRaw == target,
            "a blackout of the dimmed display records the dimmed level");
        night.Restore(desk);
        Assert(night.Count == 0 && blanker.IsBlanked(work.StableKey) && !settings.NightDimActiveFor(work.StableKey) &&
            settings.Find(work.StableKey).ScreenBlankBrightnessRaw == 80,
            "ending the night leaves the separate blackout in place and hands it the pre-night brightness");
        Assert(blanker.Restore(work.StableKey) && transport.Hardware[(work, DdcWorker.Feature.Brightness)] == 80,
            "restoring that blackout returns the pre-night brightness, not the dimmed one");

        // A side display with an unrecovered blackout keeps its original recovery value.
        settings.SetScreenBlank(side.StableKey, "side", true, 64, 160);
        Assert(night.Start(desk, work.StableKey) == 1 && settings.Find(side.StableKey).ScreenBlankBrightnessRaw == 160,
            "night saver does not re-blank a display whose earlier blackout is still owed");
        night.Restore(desk);
        settings.SetScreenBlank(side.StableKey, "side", false, null);

        work.Brightness = 80;
        settings.PresetTargets = settings.WithPresetLevels([Presets.ByName("Night") with { Nits = 40 }]);
        int customTarget = Presets.BrightnessFor(work, 40);
        transport.OnRestore = (_, _) => false;
        night.Start(desk, work.StableKey);
        WaitFor(() => transport.Writes.Any(w => w.Monitor == work && w.Value == customTarget), "edited Night target dimming");
        night.Restore(desk);
        Assert(night.Count == 1 && settings.NightDimActiveFor(work.StableKey),
            "a refused restore keeps the work display owed and its marker kept for retry");
        night.Restore([side]);
        Assert(night.Count == 1, "a disconnected work display stays owed");
        transport.OnRestore = null;
        night.RestoreWorkDim(desk);
        Assert(night.Count == 0 && !settings.NightDimActiveFor(work.StableKey) &&
            transport.Hardware[(work, DdcWorker.Feature.Brightness)] == 80,
            "the exit path restores the work display through the same saved brightness");

        settings.SetNightDim(work.StableKey, "work", true, 80, 77);
        var restarted = new NightSaver(blanker, settings, worker);
        Assert(restarted.RecoverInterruptedDim(work) && !settings.NightDimActiveFor(work.StableKey) &&
            transport.Hardware[(work, DdcWorker.Feature.Brightness)] == 77,
            "a dim left by a killed process is restored at the next scan");
        Assert(!restarted.RecoverInterruptedDim(work), "a display without a marker needs no recovery");
        int unrecoverable = transport.Writes.Count(w => w.Monitor == work);
        settings.SetNightDim(work.StableKey, "work", true, null, null);
        Assert(restarted.RecoverInterruptedDim(work) && !settings.NightDimActiveFor(work.StableKey) &&
            transport.Writes.Count(w => w.Monitor == work) == unrecoverable,
            "a marker without a saved level is cleared rather than inventing a brightness");

        settings.SetNightDim(work.StableKey, "work", true, 80, 77);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert(!restarted.RecoverInterruptedDim(work) && settings.NightDimActiveFor(work.StableKey),
                "a marker that cannot be cleared on disk stays owed even after the brightness write");
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
        Assert(restarted.RecoverInterruptedDim(work) && !settings.NightDimActiveFor(work.StableKey), "the retry clears it");

        settings.SetNightDim(work.StableKey, "work", true, 80, 75);
        work.Brightness = 30;
        Assert(restarted.Start(desk, work.StableKey) == 0 && restarted.WorkDim == NightWorkDim.Dimmed &&
            !restarted.RecoverInterruptedDim(work) && settings.Find(work.StableKey).NightDimBrightnessRaw == 75,
            "a new session keeps an unrecovered marker's original brightness and is not undone by recovery");
        restarted.Restore(desk);
        Assert(transport.Hardware[(work, DdcWorker.Feature.Brightness)] == 75, "the earlier original brightness is what comes back");

        work.SupportsBrightness = false;
        Assert(restarted.Start(desk, work.StableKey) == 0 && restarted.WorkDim == NightWorkDim.Unsupported &&
            !settings.NightDimActiveFor(work.StableKey), "a work display without brightness control stays unchanged");
        restarted.Restore(desk);
        work.SupportsBrightness = true;
        work.Brightness = 80;

        string blocked = Path.Combine(directory, "night-dim-blocked");
        File.WriteAllText(blocked, "a file cannot be a directory");
        var unsaved = new AppSettings(Path.Combine(blocked, "settings.json"));
        using var unsavedBlanker = new ScreenBlanker(unsaved, worker, (_, _, _) => { });
        int writes = transport.Writes.Count;
        var refused = new NightSaver(unsavedBlanker, unsaved, worker);
        Assert(refused.Start([work], work.StableKey) == 0 && refused.WorkDim == NightWorkDim.NotSaved &&
            !unsaved.NightDimActiveFor(work.StableKey) && refused.Count == 0,
            "an unsaved recovery marker leaves the work display unchanged");
        System.Threading.Thread.Sleep(50);
        Assert(transport.Writes.Count == writes, "no dimming write happens without a saved way back");
        Assert(AppSettings.Load(path) is { } reloaded && reloaded.Find(work.StableKey).NightDimBrightnessPercent == null,
            "a cleared marker reloads cleared");
    }

    private static void WaitFor(Func<bool> condition, string name)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(5)) System.Threading.Thread.Sleep(5);
        Assert(condition(), name + " completes within the test deadline");
    }
}
