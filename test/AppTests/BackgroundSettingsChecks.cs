using System.Reflection;
using System.Text.Json;
using LumenDeck;

internal static partial class Program
{
    private static void SettleSettings(Fixture fixture) => PumpUntil(
        () => !Private<bool>(fixture.Form, "_settingsWriteRunning") && !fixture.Settings.IsSavePending,
        "settings write and UI completion");

    private static void RequestSettings(Fixture fixture) => typeof(MainForm)
        .GetMethod("RequestSettingsSave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, null);

    private static void CheckBackgroundSettings(string directory)
    {
        foreach (bool loseResult in new[] { false, true })
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int writes = 0, writerThread = 0;
            using var fixture = new Fixture(directory, writeSettings: write =>
            {
                int count = Interlocked.Increment(ref writes);
                writerThread = Environment.CurrentManagedThreadId;
                if (count == 1)
                {
                    entered.Set();
                    if (!release.Wait(10000)) throw new TimeoutException("isolated settings barrier");
                }
                var result = write();
                return count == 1 && loseResult ? SettingsSaveResult.Failed : result;
            });
            try
            {
                var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
                var tray = Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip;
                MenuChoice(menu, "Start minimised").PerformClick();
                PumpUntil(() => entered.IsSet, "background settings writer admission");
                Assert(writerThread != Environment.CurrentManagedThreadId && fixture.Settings.IsSavePending &&
                    !fixture.Settings.StartMinimised && !MenuChoice(menu, "Start minimised").Checked,
                    "preference disk work runs off-thread while the active flag/check remains unchanged");
                Assert(!MenuChoice(menu, "Start minimised").Enabled && !MenuChoice(tray, "Start minimised").Enabled &&
                    !MenuChoice(menu, "Keep running").Enabled && MenuChoice(menu, "Keep running").AccessibleDescription.Contains("save is pending"),
                    "JSON preferences are disabled on both surfaces with a pending-save explanation");
                MenuChoice(menu, "Start minimised").PerformClick();
                Assert(writes == 1, "pending preference cannot enqueue a duplicate write");
                bool messageRan = false;
                fixture.Form.BeginInvoke((Action)(() => messageRan = true));
                PumpUntil(() => messageRan, "UI message while settings storage is paused");
                var monitor = fixture.LastScan[0];
                Assert(!Private<ScreenBlanker>(fixture.Form, "_blanker").Blank(monitor, activate: false) &&
                    fixture.Settings.LastSaveResult == SettingsSaveResult.Busy && fixture.Transport.Writes == 0,
                    "pending settings admission refuses blackout before any dimming request");
                fixture.Settings.NightWorkMonitorKey = "latest synthetic choice";
                fixture.Settings.Monitors[0].CustomBrightnessPercent = 42;
                RequestSettings(fixture);
                RequestSettings(fixture);
                Assert(writes == 1, "ordinary updates coalesce without capturing stale queued snapshots");
                int gamma = fixture.GammaSaves;
                MenuChoice(tray, "Exit").PerformClick();
                Assert(!fixture.Form.IsDisposed && !fixture.TrayDisposed && fixture.GammaSaves == gamma &&
                    !Private<bool>(fixture.Form, "_reallyClosing"), "pending settings save cancels tray exit without resource cleanup");
                foreach (var reason in new[] { CloseReason.ApplicationExitCall, CloseReason.WindowsShutDown })
                {
                    var closing = new FormClosingEventArgs(reason, false);
                    typeof(MainForm).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(fixture.Form, [closing]);
                    Assert(closing.Cancel && !fixture.TrayDisposed,
                        $"actual close override gates simulated {reason} before the final checkpoint");
                }
                release.Set();
                SettleSettings(fixture);
                Assert(writes == 2 && fixture.Settings.StartMinimised == !loseResult &&
                    MenuChoice(menu, "Start minimised").Checked == !loseResult && MenuChoice(menu, "Start minimised").Enabled,
                    "completion settles the preference before one coalesced latest-state write");
                using (var saved = JsonDocument.Parse(File.ReadAllText(fixture.SettingsPath)))
                    Assert(saved.RootElement.GetProperty("StartMinimised").GetBoolean() == !loseResult &&
                        saved.RootElement.GetProperty("NightWorkMonitorKey").GetString() == "latest synthetic choice" &&
                        saved.RootElement.GetProperty("Monitors")[0].GetProperty("CustomBrightnessPercent").GetDouble() == 42,
                        "latest snapshot preserves UI edits and the confirmed active preference even after a lost result");
                fixture.Settings.NightWorkMonitorKey = "final checkpoint choice";
                MenuChoice(tray, "Exit").PerformClick();
                Assert(fixture.Form.IsDisposed && !fixture.Settings.IsSavePending && writes == 2 &&
                    AppSettings.Load(fixture.SettingsPath).NightWorkMonitorKey == "final checkpoint choice",
                    "accepted exit captures the final live state after every earlier worker and UI completion");
            }
            finally { release.Set(); }
        }
        CheckSettingsAfterWorkerCompletion(directory);
        CheckBackgroundShortcut(directory);
    }

    private static void CheckSettingsAfterWorkerCompletion(string directory)
    {
        using var committed = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, writeSettings: write =>
        {
            var result = write();
            committed.Set();
            if (!release.Wait(10000)) throw new TimeoutException("post-commit settings barrier");
            return result;
        });
        try
        {
            MenuChoice(Private<PreferencesMenu>(fixture.Form, "_preferencesMenu"), "Start minimised").PerformClick();
            PumpUntil(() => committed.IsSet, "disk completion before UI delivery");
            Assert(fixture.Settings.IsSavePending && !fixture.Settings.Save() && !fixture.Settings.StartMinimised,
                "disk completion alone does not reopen checkpoint admission ahead of UI settlement");
            fixture.Form.Dispose();
            release.Set();
            SettleSettings(fixture);
            Assert(!fixture.Settings.StartMinimised && fixture.Settings.LastSaveResult == SettingsSaveResult.Saved,
                "late completion releases admission without activating a preference on a disposed owner");
        }
        finally
        {
            release.Set();
            // Direct forced disposal bypasses normal closing in this scenario.
            Private<NotifyIcon>(fixture.Form, "_tray").Dispose();
            Private<PreferencesMenu>(fixture.Form, "_preferencesMenu").Dispose();
        }
    }
}
