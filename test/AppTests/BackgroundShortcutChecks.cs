using System.Reflection;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckBackgroundShortcut(string directory)
    {
        foreach (int outcome in new[] { 0, 1, 2 })
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var fixture = new Fixture(directory, enumerate: scan => [new Display
            {
                StableKey = "synthetic-shortcut", Description = "Example shortcut display " + scan,
                Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29200, Bottom = -29400 },
                SupportsBrightness = true, Brightness = 50, BrightnessMax = 100,
            }], writeSettings: write =>
            {
                entered.Set();
                if (!release.Wait(10000)) throw new TimeoutException("shortcut storage barrier");
                return outcome == 1 ? SettingsSaveResult.Failed : write();
            });
            try
            {
                fixture.RefuseShortcutRegistration = outcome == 2;
                var old = Controls(fixture.Form).OfType<MonitorCard>().Single();
                typeof(MainForm).GetMethod("RequestShortcutSave", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(fixture.Form, ["synthetic-shortcut", "Example shortcut display", "Ctrl+Alt+F9"]);
                PumpUntil(() => entered.IsSet, "shortcut background writer");
                Assert(fixture.Settings.ScreenBlankHotkeyFor("synthetic-shortcut") == "" && fixture.ShortcutRegistrations == 0,
                    "pending shortcut save retains the current runtime assignment without registration");
                int scans = fixture.Scans;
                Controls(fixture.Form).OfType<FlatButton>().Single(b => b.Text == "Refresh").PerformClick();
                PumpUntil(() => fixture.Scans > scans && old.IsDisposed &&
                    Controls(fixture.Form).OfType<MonitorCard>().Any(c => c.Monitor.StableKey == "synthetic-shortcut"),
                    "display rebuild during pending shortcut save");
                release.Set();
                SettleSettings(fixture);
                Assert(fixture.Settings.ScreenBlankHotkeyFor("synthetic-shortcut") == (outcome == 1 ? "" : "Ctrl+Alt+F9") &&
                    fixture.ShortcutRegistrations == (outcome == 1 ? 0 : 1) && fixture.Transport.Writes == 0,
                    "shortcut completion uses current stable-key cards and registers only after a confirmed write");
                var registrations = Private<Dictionary<int, MonitorCard>>(fixture.Form, "_screenBlankHotkeys");
                Assert(outcome == 0 ? registrations.Count == 1 && registrations.Values.All(c => !c.IsDisposed && c != old)
                    : registrations.Count == 0,
                    "shortcut registry binds the rebuilt card or retains an explicit refused/unconfirmed state");
                if (outcome == 2)
                    Assert(Private<StatusStripe>(fixture.Form, "_status").AccessibleName.Contains("refused registration"),
                        "saved shortcut and registration refusal remain separate outcomes");
            }
            finally { release.Set(); }
        }
        foreach (bool pretendSuccess in new[] { false, true })
        {
            using var fixture = new Fixture(directory, writeSettings: _ => pretendSuccess
                ? SettingsSaveResult.Saved : throw new IOException("isolated writer refusal"));
            MenuChoice(Private<PreferencesMenu>(fixture.Form, "_preferencesMenu"), "Start minimised").PerformClick();
            SettleSettings(fixture);
            Assert(!fixture.Settings.StartMinimised && fixture.Settings.LastSaveResult == SettingsSaveResult.Failed && !File.Exists(fixture.SettingsPath),
                "writer exception or unexecuted success claim cannot activate a preference");
        }
    }
}
