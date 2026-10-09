using LumenDeck;

internal static partial class Program
{
    private static void CheckSettingsPreferenceStorage(string directory)
    {
        using (var fixture = new Fixture(directory))
        {
            Assert(fixture.Settings.Save(), "preference conflict fixture establishes its own loaded source");
            var updated = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(fixture.SettingsPath));
            updated["ExternalMarker"] = "a newer writer owns this source";
            string external = updated.ToJsonString();
            File.WriteAllText(fixture.SettingsPath, external);
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            MenuChoice(menu, "Start minimised").PerformClick();
            SettleSettings(fixture);
            Assert(!fixture.Settings.StartMinimised && !MenuChoice(menu, "Start minimised").Checked &&
                fixture.Settings.LastSaveResult == SettingsSaveResult.SourceChanged && File.ReadAllText(fixture.SettingsPath) == external,
                "actual main-window preference conflict retains runtime check and newer file bytes");
            Assert(Private<StatusStripe>(fixture.Form, "_status").AccessibleName.Contains("Quit and reopen") && fixture.Transport.Writes == 0,
                "preference source conflict gives a recovery action without monitor changes");
            fixture.Form.ClientSize = new Size(604, 441);
            var status = Private<StatusStripe>(fixture.Form, "_status");
            Assert(TextRenderer.MeasureText(status.AccessibleName, status.Font).Width <= status.ClientSize.Width - 24,
                "source-conflict recovery advice fits the minimum-width status line");
            var monitor = fixture.LastScan[0];
            Assert(!Private<ScreenBlanker>(fixture.Form, "_blanker").Blank(monitor, activate: false) &&
                !fixture.Settings.ScreenBlankActiveFor(monitor.StableKey) && fixture.Transport.Writes == 0 &&
                File.ReadAllText(fixture.SettingsPath) == external,
                "actual blackout aborts before dimming when a newer source prevents its recovery checkpoint");
        }
        using (var fixture = new Fixture(directory, settingsFactory: path =>
        {
            File.WriteAllText(path, "{damaged synthetic source");
            return AppSettings.Load(path);
        }))
        {
            MenuChoice(Private<PreferencesMenu>(fixture.Form, "_preferencesMenu"), "Start minimised").PerformClick();
            SettleSettings(fixture);
            Assert(!fixture.Settings.StartMinimised && fixture.Settings.LastSaveResult == SettingsSaveResult.Unavailable &&
                File.ReadAllText(fixture.SettingsPath) == "{damaged synthetic source",
                "actual preferences never overwrite an invalid source with launch defaults");
            Assert(Private<StatusStripe>(fixture.Form, "_status").AccessibleName.Contains("preserved") && fixture.Transport.Writes == 0,
                "invalid-source preference failure explains preservation without monitor changes");
            fixture.Form.ClientSize = new Size(604, 441);
            var status = Private<StatusStripe>(fixture.Form, "_status");
            Assert(TextRenderer.MeasureText(status.AccessibleName, status.Font).Width <= status.ClientSize.Width - 24,
                "invalid-source preservation advice fits the minimum-width status line");
        }
    }
}
