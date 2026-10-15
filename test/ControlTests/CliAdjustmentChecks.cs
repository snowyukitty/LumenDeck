using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckCliAdjustments(string directory)
    {
        foreach (bool brightness in new[] { true, false })
        {
            string path = Path.Combine(directory, $"cli-adjustment-{brightness}.json");
            var settings = new AppSettings(path);
            using var monitor = new Monitor { StableKey = "cli-adjustment", Brightness = 60, BrightnessMax = 100,
                Contrast = 40, ContrastMax = 100 };
            int writes = 0;
            bool accepted = false;
            bool Write(Monitor m, int value) { writes++; return accepted; }
            bool Apply() => brightness ? Cli.ApplyBrightness(monitor, 30, settings, Write)
                : Cli.ApplyContrast(monitor, 30, settings, Write);
            Assert(!Apply() && writes == 0 && settings.Monitors.Count == 0,
                "unsupported explicit CLI adjustment fails without transport or Custom changes");
            monitor.SupportsBrightness = monitor.SupportsContrast = true;
            settings.CaptureCustom(monitor);
            var entry = settings.Find(monitor.StableKey);
            entry.CustomBrightnessPercent = 80;
            entry.CustomContrastPercent = 70;
            Assert(!Apply() && writes == 1 && monitor.Brightness == 60 && monitor.Contrast == 40 &&
                entry.CustomBrightnessPercent == 80 && entry.CustomContrastPercent == 70,
                "refused explicit CLI adjustment preserves the current values and previous Custom");
            using var output = new StringWriter();
            using var errors = new StringWriter();
            Assert(Cli.FinishAdjustment(settings, Cli.ExitWriteRefused, ["adjustment"], ["Synthetic display"], output, errors) == 2 &&
                output.ToString().StartsWith("Requested") && errors.ToString().Contains("does not support"),
                "unsupported or refused CLI adjustment reports exit 2 without claiming Applied");
            Assert(AppSettings.Load(path).Find(monitor.StableKey) is { CustomBrightnessPercent: 80, CustomContrastPercent: 70 },
                "failed CLI adjustment preserves Custom after disk reload");
            accepted = true;
            Assert(Apply() && writes == 2 && (brightness ? monitor.Brightness : monitor.Contrast) == 30 &&
                (brightness ? entry.CustomBrightnessPercent : entry.CustomContrastPercent) == 30,
                "accepted explicit CLI adjustment updates current and Custom values");
        }
        using var ranged = new Monitor { SupportsBrightness = true, BrightnessMin = 10, BrightnessMax = 210, Brightness = 110,
            SupportsContrast = true, ContrastMin = 20, ContrastMax = 220, Contrast = 120 };
        foreach (string step in new[] { "+10", " +10 ", "-10", " -10 " })
        {
            Assert(CliArguments.Validate(["-b", step, "-c", step]) == null,
                "relative CLI percentage steps admit surrounding whitespace consistently");
            Assert(Cli.ResolveBrightness(ranged, step) == (step.Contains('+') ? 130 : 90) &&
                Cli.ResolveContrast(ranged, step) == (step.Contains('+') ? 140 : 100),
                "relative CLI percentage steps preserve their meaning and native range with whitespace");
        }
    }
}
