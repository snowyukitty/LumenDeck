using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static ToolStripLabel SaveInfo(PreferencesMenu menu) => menu.Items.OfType<ToolStripLabel>().Single();
    private static ToolStripMenuItem SaveAction(PreferencesMenu menu) => menu.Items.OfType<ToolStripMenuItem>()
        .Single(i => i.Text.Contains("active settings"));

    private static void CheckSettingsRecovery(string directory)
    {
        int writes = 0;
        using (var fixture = new Fixture(directory, writeSettings: write =>
            Interlocked.Increment(ref writes) == 1 ? SettingsSaveResult.Failed : write()))
        {
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            MenuChoice(menu, "Start minimised").PerformClick();
            SettleSettings(fixture);
            Assert(!fixture.Settings.StartMinimised && SaveInfo(menu).Text.Contains("not confirmed") && SaveAction(menu).Enabled,
                "failed preference leaves a persistent recovery row and active-value retry action");
            Assert(Private<DeskCommandBar>(fixture.Form, "_bar").SettingsButton.Text == "Settings !" &&
                SaveAction(menu).AccessibleDescription.Contains("must be selected again"),
                "settings attention is discoverable and retry explicitly excludes failed draft choices");
            fixture.Form.ClientSize = new Size(604, 441);
            var attention = Private<DeskCommandBar>(fixture.Form, "_bar").SettingsButton;
            Assert(TextRenderer.MeasureText(attention.Text, attention.Font).Width < attention.Width &&
                fixture.Form.ClientRectangle.Contains(new Rectangle(fixture.Form.PointToClient(attention.PointToScreen(Point.Empty)), attention.Size)),
                "settings attention text and button remain fully reachable at minimum window size");
            typeof(MainForm).GetMethod("SetStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.Form, ["Unrelated monitor message", StatusKind.Info]);
            OpenPreferenceObservation(menu);
            Assert(SaveInfo(menu).Text.Contains("not confirmed") && writes == 1,
                "unrelated status and reopening Settings cannot hide or retry a failed save");
            int gamma = fixture.GammaSaves;
            int scans = fixture.Scans;
            fixture.Settings.NightWorkMonitorKey = "current retry choice";
            SaveAction(menu).PerformClick();
            SettleSettings(fixture);
            Assert(writes == 2 && !fixture.Settings.StartMinimised && !AppSettings.Load(fixture.SettingsPath).StartMinimised &&
                AppSettings.Load(fixture.SettingsPath).NightWorkMonitorKey == "current retry choice",
                "retry captures currently active values instead of replaying the rejected preference");
            Assert(SaveInfo(menu).Text == "Active settings saved" && !SaveAction(menu).Enabled &&
                Private<DeskCommandBar>(fixture.Form, "_bar").SettingsButton.Text == "Settings" &&
                fixture.GammaSaves == gamma && fixture.Transport.Writes == 0 && fixture.Scans == scans,
                "confirmed retry clears attention without hardware, gamma or discovery side effects");
        }
        CheckStaleSaveFeedback(directory);
        CheckRecoveryMarkerFeedback(directory);
        using (var fixture = new Fixture(directory, writeSettings: _ => SettingsSaveResult.Busy))
        {
            RequestSettings(fixture);
            SettleSettings(fixture);
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            Assert(SaveInfo(menu).Text == "Settings writer busy" && SaveAction(menu).Enabled,
                "completed contention permits retry rather than being mistaken for an in-flight save");
        }
        using (var fixture = new Fixture(directory, settingsFactory: path =>
        {
            File.WriteAllText(path, "{invalid recovery source");
            return AppSettings.Load(path);
        }))
        {
            RequestSettings(fixture);
            SettleSettings(fixture);
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            Assert(SaveInfo(menu).Text == "Settings file needs attention" && !SaveAction(menu).Enabled &&
                !MenuChoice(menu, "Start minimised").Enabled && SaveInfo(menu).AccessibleDescription.Contains("privately back up") &&
                File.ReadAllText(fixture.SettingsPath) == "{invalid recovery source",
                "invalid source keeps recovery disabled and offers private backup guidance without replacement");
        }
        using (var fixture = new Fixture(directory))
        {
            Assert(fixture.Settings.Save(), "blocked recovery fixture creates its source");
            File.AppendAllText(fixture.SettingsPath, " ");
            RequestSettings(fixture);
            SettleSettings(fixture);
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            Assert(SaveInfo(menu).Text.Contains("outside this window") && !SaveAction(menu).Enabled &&
                !MenuChoice(menu, "Keep running").Enabled && MenuChoice(menu, "Start with Windows").Enabled,
                "changed-source recovery blocks file writes while keeping independent registration available");
            var tray = Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip;
            Assert(!MenuChoice(tray, "Start minimised").Enabled &&
                MenuChoice(tray, "Start minimised").AccessibleDescription.Contains("reopen"),
                "tray preference shares the changed-source recovery guard and guidance");
            string source = File.ReadAllText(fixture.SettingsPath);
            SaveAction(menu).PerformClick();
            Assert(File.ReadAllText(fixture.SettingsPath) == source &&
                SaveInfo(menu).AccessibleDescription.Contains("Restore active blackouts"),
                "blocked retry preserves the changed source and explains a safe reopen path");
        }
    }

    private static void CheckRecoveryMarkerFeedback(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int writes = 0;
        using var fixture = new Fixture(directory, writeSettings: write =>
        {
            if (Interlocked.Increment(ref writes) == 1)
            {
                entered.Set();
                if (!release.Wait(10000)) throw new TimeoutException("marker feedback writer barrier");
            }
            return write();
        });
        try
        {
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            fixture.Settings.SetScreenBlank("synthetic-1", "Synthetic", true, 50);
            RequestSettings(fixture);
            PumpUntil(() => entered.IsSet, "marker snapshot captured");
            fixture.Settings.SetScreenBlank("synthetic-1", "Synthetic", false, null);
            Assert(!fixture.Settings.Save(), "synthetic restore checkpoint is refused during an active window snapshot");
            // Exercise the actual notification handler without creating an
            // overlay or changing brightness on a physical display.
            typeof(MainForm).GetMethod("OnScreenBlankStateChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.Form, ["synthetic-1", false, true]);
            release.Set();
            SettleSettings(fixture);
            Assert(writes == 2 && !AppSettings.Load(fixture.SettingsPath).ScreenBlankActiveFor("synthetic-1") &&
                SaveInfo(menu).Text == "Active settings saved" && fixture.Transport.Writes == 0,
                "restore notification advances the revision and saves the settled recovery marker after an older snapshot");
        }
        finally { release.Set(); }
    }

    private static void CheckStaleSaveFeedback(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, writeSettings: write =>
        {
            entered.Set();
            if (!release.Wait(10000)) throw new TimeoutException("feedback writer barrier");
            return write();
        });
        try
        {
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            RequestSettings(fixture);
            PumpUntil(() => entered.IsSet, "feedback snapshot captured");
            fixture.Settings.NightWorkMonitorKey = "newer live choice";
            typeof(MainForm).GetMethod("SaveSoon", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, null);
            // Hold automatic delivery so this checks the intermediate state,
            // independently of how long the test machine takes to settle.
            Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer").Stop();
            Assert(SaveInfo(menu).Text == "Saving settings..." && !SaveAction(menu).Enabled,
                "pending save disables manual retry despite a newer live edit");
            release.Set();
            SettleSettings(fixture);
            Assert(SaveInfo(menu).Text == "Changes waiting to save" && SaveAction(menu).Enabled &&
                AppSettings.Load(fixture.SettingsPath).NightWorkMonitorKey != "newer live choice",
                "older snapshot success cannot label a newer debounced edit as saved");
            SaveAction(menu).PerformClick();
            SettleSettings(fixture);
            Assert(SaveInfo(menu).Text == "Active settings saved" &&
                AppSettings.Load(fixture.SettingsPath).NightWorkMonitorKey == "newer live choice",
                "manual active-value save advances feedback to the captured current revision");
        }
        finally { release.Set(); }
    }
}
