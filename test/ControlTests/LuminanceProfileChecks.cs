using System.Text.Json;
using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckLuminanceProfiles(string directory, string renderDirectory)
    {
        string profiles = Path.Combine(directory, "profiles.json");
        using var monitor = Display("profile-test");
        monitor.Description = "Example Studio Panel";
        monitor.SupportsBrightness = true;
        monitor.BrightnessMin = 0;
        monitor.BrightnessMax = 255;
        monitor.Brightness = 128;
        try
        {
            File.WriteAllText(profiles, """
                {
                  "Example": { "PeakNits": 400, "FloorNits": 50, "Measured": true },
                  "Studio": { "PeakNits": 300, "FloorNits": 30, "Measured": false },
                  "Studio Panel": { "PeakNits": 250, "FloorNits": 40 },
                  "Broken": { "PeakNits": 10, "FloorNits": 20 },
                  "Null": null
                }
                """);
            PanelDatabase.Load(profiles);
            var estimate = Presets.EstimateFor(monitor, monitor.Brightness);
            Assert(estimate.UsesUserProfile && estimate.ProfileKind == "user", "matching profile is separate from measured provenance");
            Assert(!estimate.ProfileEndpointsMeasured, "missing Measured is not promoted to instrument measurement");
            Assert(estimate.Nits == 145, "longest match selects endpoints and preserves raw-range math");
            Assert(PanelDatabase.For(monitor).PeakNits == 250, "longest model match wins");
            CheckEstimateOutput(monitor, "user", false);

            monitor.Description = "Studio";
            Assert(!Presets.EstimateFor(monitor, monitor.Brightness).ProfileEndpointsMeasured,
                "explicit false survives profile loading");
            monitor.Description = "Example";
            estimate = Presets.EstimateFor(monitor, monitor.Brightness);
            Assert(estimate.ProfileEndpointsMeasured, "explicit true retains user-declared measured endpoints");
            Assert(estimate.Nits == 226, "measured endpoints still use the unchanged interpolation model");
            CheckEstimateOutput(monitor, "user", true);

            // Exercise the longest label with the established simulated card
            // width/font checks. No monitor worker requests are enqueued.
            File.WriteAllText(profiles, """
                { "Example": { "PeakNits": 400, "FloorNits": 50, "Measured": true, "Note": "PRIVATE-PROFILE-NOTE" } }
                """);
            PanelDatabase.Load(profiles);
            string layoutDirectory = renderDirectory == null ? null : Path.Combine(renderDirectory, "measured-profile");
            CheckCardLayout(layoutDirectory, withBrightness: true);
            var support = SupportReport.Create(new[] { monitor }, DisplayScanState.Ready, "1.1.0", new Version(10, 0));
            string supportJson = SupportReport.Json(support);
            Assert(!supportJson.Contains("Example") && !supportJson.Contains("PRIVATE-PROFILE-NOTE") &&
                !supportJson.Contains("luminanceProfile") && !supportJson.Contains("profileEndpointsMeasured"),
                "support projection does not inherit profile names, notes or provenance metadata");
            foreach (int raw in new[] { 0, 255 })
            {
                monitor.Brightness = raw;
                using var endpointJson = JsonDocument.Parse(Cli.ListJson(new List<Monitor> { monitor }));
                Assert(endpointJson.RootElement[0].GetProperty("nitsAreEstimated").GetBoolean(),
                    "calculated nits remain estimated even at declared measured endpoints");
                Assert(Presets.NitsFor(monitor, raw) == (raw == 0 ? 50 : 400), "profile endpoint math is retained");
            }
            monitor.Brightness = 128;
            monitor.SupportsBrightness = false;
            using (var unavailableJson = JsonDocument.Parse(Cli.ListJson(new List<Monitor> { monitor })))
                Assert(unavailableJson.RootElement[0].GetProperty("estimatedNits").ValueKind == JsonValueKind.Null,
                    "an unavailable brightness control produces no luminance estimate");
            monitor.SupportsBrightness = true;

            monitor.Description = "Unknown";
            Assert(Presets.NitsFor(monitor, 0) == 45 && Presets.NitsFor(monitor, 255) == 300,
                "desktop fallback retains its original endpoints");
            CheckEstimateOutput(monitor, "generic", false);
            monitor.IsInternalPanel = true;
            Assert(Presets.NitsFor(monitor, 0) == 15 && Presets.NitsFor(monitor, 255) == 300,
                "laptop fallback retains its original endpoints");
            monitor.IsInternalPanel = false;

            foreach (string invalid in new[] { "null", "{", "{}", "{\"Example\":{\"PeakNits\":10,\"FloorNits\":20}}" })
            {
                File.WriteAllText(profiles, "{\"Example\":{\"PeakNits\":400,\"FloorNits\":50}}");
                PanelDatabase.Load(profiles);
                monitor.Description = "Example";
                Assert(PanelDatabase.HasUserProfile(monitor), "valid reload installs a matching profile");
                File.WriteAllText(profiles, invalid);
                PanelDatabase.Load(profiles);
                Assert(!PanelDatabase.HasUserProfile(monitor), "null/malformed/empty/invalid reload clears stale profiles");
            }
            File.WriteAllText(profiles, "{\"Example\":{\"PeakNits\":400,\"FloorNits\":50}}");
            PanelDatabase.Load(profiles);
            Assert(PanelDatabase.HasUserProfile(monitor), "profile exists before missing-file reload");
            PanelDatabase.Load(Path.Combine(directory, "absent-profiles.json"));
            Assert(!PanelDatabase.HasUserProfile(monitor), "missing replacement clears stale profiles");

            string examplePath = Path.Combine(directory, "example-profiles.json");
            PanelDatabase.WriteExampleIfMissing(examplePath);
            using var example = JsonDocument.Parse(File.ReadAllText(examplePath));
            Assert(!example.RootElement.EnumerateObject().Single().Value.GetProperty("Measured").GetBoolean(),
                "new example does not declare invented measurements");
            File.WriteAllText(examplePath, "owner content");
            PanelDatabase.WriteExampleIfMissing(examplePath);
            Assert(File.ReadAllText(examplePath) == "owner content", "example writer preserves existing user content");
        }
        finally
        {
            // Restore in-memory generic state without reading user settings.
            PanelDatabase.Load(Path.Combine(directory, "absent-profiles.json"));
        }
    }

    private static void CheckEstimateOutput(Monitor monitor, string expectedProfile, bool endpointsMeasured)
    {
        using var json = JsonDocument.Parse(Cli.ListJson(new List<Monitor> { monitor }));
        var item = json.RootElement[0];
        Assert(item.GetProperty("nitsAreEstimated").GetBoolean(), "CLI model nits always remain estimates");
        Assert(item.GetProperty("luminanceProfile").GetString() == expectedProfile, "CLI exposes profile origin separately");
        Assert(item.GetProperty("profileEndpointsMeasured").GetBoolean() == endpointsMeasured,
            "CLI preserves endpoint provenance separately");
        Assert(item.GetProperty("estimatedNits").GetInt32() == Presets.NitsFor(monitor, monitor.Brightness),
            "CLI preserves numeric estimate and existing JSON field");
        Assert(Cli.ListText(new List<Monitor> { monitor }).Contains("Estimated"), "text CLI qualifies measured and generic model results");

        using var worker = new DdcWorker();
        using var card = new MonitorCard(monitor, new AppSettings(), worker, null, null, null, null, null);
        card.SetWidth(400);
        var label = Descendants(card).OfType<Label>().Single(c => c.Text.StartsWith("Estimated "));
        Assert(label.Text == Presets.EstimateFor(monitor, monitor.Brightness).DisplayText,
            "card uses estimate-qualified profile presentation");
        Assert(label.AccessibleDescription.Contains("no light sensor"), "accessible description explains measurement limit");
        Assert(!endpointsMeasured || label.AccessibleDescription.Contains("not verified"),
            "measured endpoints remain user-declared rather than app-verified");
        int originalBrightness = monitor.Brightness;
        monitor.Brightness = 64;
        card.SyncFromMonitor();
        Assert(label.Text == Presets.EstimateFor(monitor, 64).DisplayText, "card updates estimates after model sync without a write");
        monitor.Brightness = originalBrightness;
    }
}
