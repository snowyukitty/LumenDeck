using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckCliWarmth(string directory)
    {
        string path = Path.Combine(directory, "cli-warmth.json");
        var settings = new AppSettings(path);
        using var monitor = new Monitor { StableKey = "fictitious-warmth", Kelvin = 5000 };
        settings.SetKelvin(monitor.StableKey, "Fictitious display", 5000);
        settings.Find(monitor.StableKey).CustomKelvin = 4200;
        Assert(settings.Save(), "CLI warmth starts with isolated saved settings");
        int calls = 0;
        foreach (bool remember in new[] { false, true })
        {
            bool applied = Cli.ApplyWarmth(monitor, 3000, settings, remember, m =>
            {
                calls++;
                Assert(ReferenceEquals(m, monitor) && m.Kelvin == 3000,
                    "CLI warmth sends the requested value to the supplied writer");
                return false;
            });
            Assert(!applied && monitor.Kelvin == 5000 && settings.KelvinFor(monitor.StableKey) == 5000,
                "refused CLI warmth preserves current and saved temperature");
            Assert(settings.Find(monitor.StableKey).CustomKelvin == 4200,
                "refused CLI warmth preserves Custom for explicit and preset requests");
            using var output = new StringWriter();
            using var errors = new StringWriter();
            int exit = Cli.FinishAdjustment(settings, applied ? Cli.ExitOk : Cli.ExitWriteRefused,
                ["warmth 3000K"], ["Fictitious display"], output, errors);
            Assert(exit == 2 && output.ToString().StartsWith("Requested") &&
                !output.ToString().Contains("Applied") && errors.ToString().Contains("refused"),
                "refused warmth produces the CLI refusal summary and exit 2");
            var reloaded = AppSettings.Load(path);
            Assert(reloaded.KelvinFor(monitor.StableKey) == 5000 &&
                reloaded.Find(monitor.StableKey).CustomKelvin == 4200,
                "refused warmth cannot persist the requested or Custom temperature");
        }
        Assert(calls == 2, "each refused warmth request invokes its writer once");
        Assert(Cli.ApplyWarmth(monitor, 3500, settings, apply: _ => true) &&
            monitor.Kelvin == 3500 && settings.KelvinFor(monitor.StableKey) == 3500 &&
            settings.Find(monitor.StableKey).CustomKelvin == 4200,
            "confirmed preset warmth updates current state without replacing Custom");
        Assert(settings.Save() && AppSettings.Load(path).Find(monitor.StableKey) is
            { Kelvin: 3500, CustomKelvin: 4200 },
            "confirmed non-neutral preset warmth and prior Custom survive reload as an actual entry");
        Assert(Cli.ApplyWarmth(monitor, 6500, settings, rememberCustom: true, apply: _ => true) &&
            monitor.Kelvin == 6500 && settings.Find(monitor.StableKey).CustomKelvin == 6500,
            "confirmed explicit warmth off updates current and Custom temperature");
        Assert(settings.Save() && AppSettings.Load(path).Find(monitor.StableKey) is
            { Kelvin: 6500, CustomKelvin: 6500 },
            "confirmed warmth off and Custom survive reload as an actual entry");

        var empty = new AppSettings(Path.Combine(directory, "cli-warmth-empty.json"));
        Assert(!Cli.ApplyWarmth(monitor, 3000, empty, rememberCustom: true, apply: _ => false) &&
            empty.Monitors.Count == 0 && monitor.Kelvin == 6500,
            "refused first warmth request creates no settings entry");
        var failure = new InvalidOperationException("Fictitious writer failure");
        Exception caught = null;
        try { Cli.ApplyWarmth(monitor, 3000, settings, rememberCustom: true, apply: _ => throw failure); }
        catch (Exception ex) { caught = ex; }
        Assert(ReferenceEquals(caught, failure) && monitor.Kelvin == 6500 &&
            settings.KelvinFor(monitor.StableKey) == 6500 && settings.Find(monitor.StableKey).CustomKelvin == 6500,
            "unexpected writer exception preserves warmth state and propagates to the console boundary");
        foreach (int requested in new[] { 0, 10000 })
        {
            int expected = Math.Clamp(requested, GammaControl.MinKelvin, GammaControl.MaxKelvin);
            Assert(Cli.ApplyWarmth(monitor, requested, settings, apply: m => m.Kelvin == expected) &&
                monitor.Kelvin == expected && settings.KelvinFor(monitor.StableKey) == expected,
                "shared warmth helper retains defensive bounds before the writer");
        }
    }
}
