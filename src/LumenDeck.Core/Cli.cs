using System.Text;
using System.Text.Json;

namespace LumenDeck;

/// <summary>
/// A command line for the same engine the window drives.
///
/// A GUI-only utility cannot be scripted, scheduled, or bound to a hotkey by
/// whatever launcher someone already uses. Everything here runs without opening
/// a window, prints plain text (or JSON with --json), and exits with a code that
/// means something: 0 success, 1 nothing matched, 2 a write was refused.
/// </summary>
internal static class Cli
{
    public const int ExitOk = 0;
    public const int ExitNoMatch = 1;
    public const int ExitWriteRefused = 2;

    public static bool WantsCli(string[] args) => args.Length > 0;

    public static int Run(string[] args)
    {
        bool json = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));

        string Value(params string[] names)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (names.Contains(args[i], StringComparer.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
        bool Flag(params string[] names) => args.Any(a => names.Contains(a, StringComparer.OrdinalIgnoreCase));

        if (Flag("-h", "--help", "/?")) { Console.WriteLine(HelpText); return ExitOk; }
        if (Flag("-v", "--version"))
        {
            Console.WriteLine(typeof(Cli).Assembly.GetName().Version?.ToString() ?? "unknown");
            return ExitOk;
        }

        PanelDatabase.Load();
        var settings = AppSettings.Load();
        var monitors = MonitorService.Enumerate();

        try
        {
            if (monitors.Count == 0)
            {
                Console.Error.WriteLine("No monitors detected.");
                return ExitNoMatch;
            }

            if (Flag("--diagnose"))
            {
                Console.WriteLine("LumenDeck diagnostics");
                Console.WriteLine($"  monitors detected: {monitors.Count}");
                foreach (var m in monitors)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {m.DeviceName}  {m.FriendlyName}");
                    Console.WriteLine($"    primary    {m.IsPrimary}");
                    Console.WriteLine($"    interface  {m.DeviceInterfaceId}");
                    Console.WriteLine($"    edid       {(m.Edid == null ? "not readable" : m.Edid.IdentityKey)}");
                    Console.WriteLine($"    backend    {m.BrightnessBackend}");
                    Console.WriteLine($"    ddc        {m.Diagnostic}");
                    Console.WriteLine($"    gammaRamp  {(m.BaselineRamp == null ? "not readable" : "captured")}");
                }
                return ExitOk;
            }

            if (Flag("--features"))
            {
                foreach (var m in monitors.OrderBy(x => x.Rect.Left).ThenBy(x => x.Rect.Top))
                {
                    MonitorService.LoadFeatures(m);
                    Console.WriteLine();
                    Console.WriteLine($"{m.FriendlyName}  ({m.PositionLabel})");
                    if (!m.HasPhysicalHandle) { Console.WriteLine("    no DDC handle"); continue; }
                    if (m.Features.Count == 0) { Console.WriteLine("    advertises no adjustable controls this app knows about"); continue; }
                    foreach (var f in m.Features)
                    {
                        string detail = f.Definition.Kind switch
                        {
                            VcpCatalog.Kind.Continuous => $"{f.Current} of 0-{f.Max}",
                            VcpCatalog.Kind.Select =>
                                f.LabelFor(f.Current) + "   options: " +
                                string.Join(", ", f.AllowedValues.Where(v => !(f.CurrentIsUnadvertised && v == f.Current))
                                                                 .Select(v => VcpCatalog.ValueName(f.Definition, v))),
                            _ => "action",
                        };
                        Console.WriteLine($"    0x{f.Code:X2}  {f.Name,-22} {detail}");
                    }
                }
                Console.WriteLine();
                return ExitOk;
            }

            if (Flag("--list") || args.Length == 0)
            {
                Console.WriteLine(json ? ListJson(monitors) : ListText(monitors));
                return ExitOk;
            }

            // --monitor narrows to one; without it every monitor is affected,
            // which is the common case and so the default.
            string filter = Value("-m", "--monitor");
            var targets = string.IsNullOrEmpty(filter)
                ? monitors
                : monitors.Where(x =>
                      x.FriendlyName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                      x.DeviceName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                      x.PositionLabel.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (targets.Count == 0)
            {
                Console.Error.WriteLine($"No monitor matched \"{filter}\". Try --list.");
                return ExitNoMatch;
            }

            int exit = ExitOk;
            var actions = new List<string>();

            string presetName = Value("-p", "--preset");
            if (presetName != null)
            {
                var level = Presets.ByName(presetName);
                if (level == null)
                {
                    Console.Error.WriteLine(
                        $"Unknown preset \"{presetName}\". Available: {string.Join(", ", Presets.Levels.Select(l => l.Name))}");
                    return ExitNoMatch;
                }
                foreach (var m in targets)
                {
                    if (!ApplyBrightness(m, Presets.BrightnessFor(m, level.Nits))) exit = ExitWriteRefused;
                    ApplyWarmth(m, level.Kelvin, settings);
                }
                actions.Add($"preset {level.Name} ({level.Nits} nits, {level.Kelvin}K)");
            }

            string brightness = Value("-b", "--brightness");
            if (brightness != null)
            {
                foreach (var m in targets)
                {
                    int raw = ResolveBrightness(m, brightness);
                    if (raw < 0)
                    {
                        Console.Error.WriteLine($"Could not read \"{brightness}\" as a brightness value.");
                        return ExitNoMatch;
                    }
                    if (!ApplyBrightness(m, raw)) exit = ExitWriteRefused;
                }
                actions.Add("brightness " + brightness);
            }

            string warmth = Value("-w", "--warmth");
            if (warmth != null)
            {
                if (!int.TryParse(warmth, out int kelvin))
                {
                    if (warmth.Equals("off", StringComparison.OrdinalIgnoreCase)) kelvin = GammaControl.NeutralKelvin;
                    else { Console.Error.WriteLine($"Could not read \"{warmth}\" as kelvin."); return ExitNoMatch; }
                }
                foreach (var m in targets) ApplyWarmth(m, kelvin, settings);
                actions.Add("warmth " + (kelvin >= GammaControl.NeutralKelvin ? "off" : kelvin + "K"));
            }

            if (actions.Count == 0)
            {
                Console.Error.WriteLine("Nothing to do. Try --help.");
                return ExitNoMatch;
            }

            settings.Save();

            // Writes are queued to the hardware asynchronously in the GUI; here
            // they are direct, but a monitor still needs a moment before a read
            // reflects them. Report what was asked for, and say so plainly.
            Console.WriteLine($"Applied {string.Join(", ", actions)} to " +
                              string.Join(", ", targets.Select(t => t.FriendlyName)));
            if (exit == ExitWriteRefused)
                Console.Error.WriteLine("At least one monitor refused a change - it may be asleep or on another input.");
            return exit;
        }
        finally
        {
            foreach (var m in monitors) m.Dispose();
        }
    }

    /// <summary>Accepts an absolute value, or a relative step such as +10 or -10.</summary>
    private static int ResolveBrightness(Monitor m, string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return -1;

        if (spec[0] is '+' or '-')
        {
            if (!int.TryParse(spec, out int delta)) return -1;
            double pct = Presets.RawToPercent(m, m.Brightness) + delta;
            return Presets.PercentToRaw(m, pct);
        }
        if (!int.TryParse(spec, out int percent)) return -1;
        return Presets.PercentToRaw(m, percent);
    }

    private static bool ApplyBrightness(Monitor m, int raw)
    {
        if (!m.SupportsBrightness) return true;   // nothing to refuse
        bool ok = m.IsInternalPanel
            ? WmiBrightness.Set(m.WmiInstanceName, raw)
            : m.HasPhysicalHandle && Native.SetMonitorBrightness(m.PhysicalHandle, (uint)raw);
        if (ok) m.Brightness = raw;
        Thread.Sleep(60);   // MCCS pacing, same as the GUI writer
        return ok;
    }

    private static void ApplyWarmth(Monitor m, int kelvin, AppSettings settings)
    {
        m.Kelvin = Math.Clamp(kelvin, GammaControl.MinKelvin, GammaControl.MaxKelvin);
        GammaControl.Apply(m.DeviceName, m.Kelvin, m.BaselineRamp);
        settings.SetKelvin(m.StableKey, m.FriendlyName, m.Kelvin);
    }

    private static string ListText(List<Monitor> monitors)
    {
        var sb = new StringBuilder();
        foreach (var m in monitors.OrderBy(x => x.Rect.Left).ThenBy(x => x.Rect.Top))
        {
            string backend = m.BrightnessBackend switch
            {
                Monitor.Backend.Ddc => "DDC/CI",
                Monitor.Backend.Wmi => "WMI (internal panel)",
                _ => "no brightness control",
            };
            sb.AppendLine($"{m.FriendlyName}  {m.SizeLabel}".TrimEnd());
            sb.AppendLine($"    position   {m.PositionLabel}");
            sb.AppendLine($"    control    {backend}");
            if (m.SupportsBrightness)
                sb.AppendLine($"    brightness {m.Brightness}  ({Presets.RawToPercent(m, m.Brightness):0}% of " +
                              $"{m.BrightnessMin}-{m.BrightnessMax}, about {Presets.NitsFor(m, m.Brightness)} nits" +
                              (Presets.IsKnown(m) ? ")" : ", estimated)"));
            if (m.SupportsContrast)
                sb.AppendLine($"    contrast   {m.Contrast}  ({m.ContrastMin}-{m.ContrastMax})");
            sb.AppendLine($"    warmth     {(m.Kelvin >= GammaControl.NeutralKelvin ? "off" : m.Kelvin + "K")}");
            sb.AppendLine($"    device     {m.DeviceName}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string ListJson(List<Monitor> monitors) =>
        JsonSerializer.Serialize(
            monitors.OrderBy(x => x.Rect.Left).ThenBy(x => x.Rect.Top).Select(m => new
            {
                name = m.FriendlyName,
                position = m.PositionLabel,
                size = m.SizeLabel,
                device = m.DeviceName,
                backend = m.BrightnessBackend.ToString(),
                internalPanel = m.IsInternalPanel,
                brightness = m.SupportsBrightness ? m.Brightness : (int?)null,
                brightnessRange = m.SupportsBrightness ? new { min = m.BrightnessMin, max = m.BrightnessMax } : null,
                brightnessPercent = m.SupportsBrightness ? Math.Round(Presets.RawToPercent(m, m.Brightness)) : (double?)null,
                estimatedNits = m.SupportsBrightness ? Presets.NitsFor(m, m.Brightness) : (int?)null,
                nitsAreEstimated = !Presets.IsKnown(m),
                contrast = m.SupportsContrast ? m.Contrast : (int?)null,
                kelvin = m.Kelvin,
                rect = new { m.Rect.Left, m.Rect.Top, m.Rect.Right, m.Rect.Bottom },
            }),
            new JsonSerializerOptions { WriteIndented = true });

    private const string HelpText = """
        LumenDeck - brightness, contrast and colour temperature for every monitor.

        Run with no arguments to open the window. With arguments it stays on the
        command line and exits.

          --list                  Show every monitor and its current settings
          --diagnose              Why a monitor does or does not respond
          --features              Extra controls each monitor advertises
          --json                  Machine-readable output (with --list)

          -m, --monitor <text>    Only monitors whose name, position or device
                                  contains <text>. Default: all of them.

          -p, --preset <name>     Day | Evening | Night
                                  Aims every panel at the same perceived
                                  luminance, not the same slider number.

          -b, --brightness <n>    0-100 as a percentage of the panel's range,
                                  or a relative step: +10, -10

          -w, --warmth <kelvin>   3000-6500, or "off" for neutral

          -h, --help              This text
          -v, --version           Version

        Examples
          LumenDeck --list
          LumenDeck --preset Night
          LumenDeck --brightness -10
          LumenDeck -m "left" -b 55 -w 5000
          LumenDeck --warmth off

        Exit codes: 0 done, 1 nothing matched, 2 a monitor refused the change.
        """;
}
