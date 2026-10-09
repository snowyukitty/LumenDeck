using System.Reflection;
using System.Text;
using System.Text.Json;

namespace LumenDeck;

/// <summary>
/// A command line for the same engine the window drives.
///
/// A GUI-only utility cannot be scripted, scheduled, or bound to a hotkey by
/// whatever launcher someone already uses. Everything here runs without opening
/// a window, prints plain text (or JSON with --json), and exits with a code that
/// means something: 0 success, 1 invalid request or nothing matched,
/// 2 a request was refused or could not be persisted.
/// </summary>
internal static class Cli
{
    public const int ExitOk = 0;
    public const int ExitNoMatch = 1;
    public const int ExitWriteRefused = 2;

    public static bool WantsCli(string[] args) => args.Length > 0;

    /// <summary>
    /// The assembly that was actually launched.
    ///
    /// Deliberately not <c>typeof(Cli).Assembly</c>. This class lives in
    /// LumenDeck.Core, so that expression reports the *engine's* version rather
    /// than the version of the executable the person ran. Both projects happened
    /// to carry 1.0.0 while each set its own, which is precisely why a number
    /// coming from the wrong assembly went unnoticed - and
    /// <c>bug_report.yml</c> asks people to paste this into every issue.
    /// </summary>
    private static Assembly Running => Assembly.GetEntryAssembly() ?? typeof(Cli).Assembly;

    /// <summary>Three-part version, the form a person is asked to quote.</summary>
    public static string VersionText => Running.GetName().Version?.ToString(3) ?? "unknown";

    /// <summary>
    /// Version plus the commit it was built from, when the build recorded one.
    /// Worth having in --diagnose: two builds of 1.1.0 can differ, and diagnose
    /// output is what arrives attached to a bug report.
    /// </summary>
    public static string FullVersionText =>
        Running.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            is { Length: > 0 } informational
            ? informational
            : VersionText;

    public static int Run(string[] args) => Run(args, RunValidated);

    // Keep preflight ahead of every production collaborator. The executor seam
    // lets tests prove rejection without opening real display/settings handles.
    internal static int Run(string[] args, Func<string[], int> execute)
    {
        if (CliArguments.Validate(args) is { } invalid)
        {
            Console.Error.WriteLine(invalid.Message);
            return invalid.ExitCode;
        }
        return execute(args);
    }

    private static int RunValidated(string[] args)
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

        if (Flag("--support-report"))
            return RunSupportReport(args, () => MonitorService.Enumerate(resolveGamma: false));

        if (Flag("-h", "--help", "/?")) { Console.WriteLine(HelpText); return ExitOk; }
        if (Flag("-v", "--version"))
        {
            Console.WriteLine(VersionText);
            return ExitOk;
        }

        PanelDatabase.Load();
        var settings = AppSettings.Load();
        var monitors = MonitorService.Enumerate();

        // Enumerate reads the hardware; the saved warmth lives in settings and
        // has to be put back onto the monitors before anything reports it.
        // Without this --list said "warmth off" on a display that was visibly
        // tinted, because Monitor.Kelvin had never been anything but its default.
        foreach (var m in monitors) m.Kelvin = settings.KelvinFor(m.StableKey);

        try
        {
            if (monitors.Count == 0)
            {
                Console.Error.WriteLine("No monitors detected.");
                return ExitNoMatch;
            }

            // --monitor narrows to one; without it every monitor is affected,
            // which is the common case and so the default.
            //
            // Resolved here, above everything, because it used to be resolved
            // below the read-only commands - so --list, --json, --diagnose and
            // --features each ignored it in silence. `--list -m left` printed
            // every monitor and exited 0, which is an option that looks exactly
            // like it worked. The help has always described --monitor as
            // narrowing to the monitors it names, with no exception for reads.
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

            if (Flag("--diagnose"))
            {
                Console.Error.WriteLine("Detailed diagnostics include device identifiers. Review before sharing; use --support-report for the minimal report.");
                Console.WriteLine("LumenDeck diagnostics");
                Console.WriteLine($"  version: {FullVersionText}");
                Console.WriteLine($"  monitors detected: {monitors.Count}" +
                                  (targets.Count == monitors.Count ? "" : $", {targets.Count} shown (--monitor \"{filter}\")"));
                foreach (var m in targets)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {m.DeviceName}  {m.DisplayName}");
                    Console.WriteLine($"    primary    {m.IsPrimary}");
                    Console.WriteLine($"    interface  {m.DeviceInterfaceId}");
                    Console.WriteLine($"    edid       {(m.Edid == null ? "not readable" : m.Edid.IdentityKey)}");
                    Console.WriteLine($"    backend    {m.BrightnessBackend}");
                    Console.WriteLine($"    ddc        {m.Diagnostic}");
                    Console.WriteLine($"    gammaRamp  {(m.CapturedRamp == null ? "not readable" : "captured")}, " +
                                      $"baseline {(m.BaselineRamp == null ? "unknown" : "known")}, " +
                                      $"loaded ramp is {(m.GammaIsOurs ? "LumenDeck's" : "the display's own")}");
                }
                return ExitOk;
            }

            if (Flag("--features"))
            {
                foreach (var m in targets.OrderBy(x => x.Rect.Left).ThenBy(x => x.Rect.Top))
                {
                    MonitorService.LoadFeatures(m);
                    Console.WriteLine();
                    Console.WriteLine($"{m.DisplayName}  ({m.PositionLabel})");
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
                Console.WriteLine(json ? ListJson(targets) : ListText(targets));
                return ExitOk;
            }

            int exit = ExitOk;
            var actions = new List<string>();

            string presetName = Value("-p", "--preset");
            if (presetName != null)
            {
                if (Presets.IsCustom(presetName))
                {
                    int restored = 0;
                    foreach (var m in targets)
                        if (RestoreCustom(m, settings, ref exit)) restored++;

                    if (restored == 0)
                    {
                        Console.Error.WriteLine(
                            "No matching monitor has settings of its own saved yet. They are remembered " +
                            "when a slider is moved in the window, or when -b / -w is used here.");
                        return ExitNoMatch;
                    }
                    actions.Add($"preset {Presets.CustomName}");
                }
                else
                {
                    // The window's Settings > Preset targets edits these.
                    var level = settings.PresetLevel(presetName);
                    if (level == null)
                    {
                        Console.Error.WriteLine(
                            $"Unknown preset \"{presetName}\". Available: " +
                            $"{string.Join(", ", Presets.Levels.Select(l => l.Name))}, {Presets.CustomName}");
                        return ExitNoMatch;
                    }
                    foreach (var m in targets)
                    {
                        // Remember where this monitor was before the preset
                        // moves it, so --preset Custom can put it back even if
                        // this is the first LumenDeck command ever run here.
                        settings.SeedCustom(m);
                        if (!ApplyBrightness(m, Presets.BrightnessFor(m, level.Nits))) exit = ExitWriteRefused;
                        if (!ApplyWarmth(m, level.Kelvin, settings)) exit = ExitWriteRefused;
                    }
                    actions.Add($"preset {level.Name} ({level.Nits} nits, {level.Kelvin}K)");
                }
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

                    // An explicit value here is the same act as moving a slider
                    // in the window: it is what this person wants, so it becomes
                    // what --preset Custom restores.
                    settings.CaptureCustom(m);
                }
                actions.Add("brightness " + brightness);
            }

            // Contrast could be read by --list and restored by --preset Custom,
            // but there was no way to set it from here at all: a value the tool
            // remembers and can put back, and cannot be told. Same shape as -b,
            // including the relative step, because the two sit side by side on
            // every card in the window.
            string contrast = Value("-c", "--contrast");
            if (contrast != null)
            {
                foreach (var m in targets)
                {
                    int raw = ResolveContrast(m, contrast);
                    if (raw < 0)
                    {
                        Console.Error.WriteLine($"Could not read \"{contrast}\" as a contrast value.");
                        return ExitNoMatch;
                    }
                    if (!ApplyContrast(m, raw)) exit = ExitWriteRefused;
                    settings.CaptureCustom(m);
                }
                actions.Add("contrast " + contrast);
            }

            string warmth = Value("-w", "--warmth");
            if (warmth != null)
            {
                if (!int.TryParse(warmth, out int kelvin))
                {
                    if (warmth.Equals("off", StringComparison.OrdinalIgnoreCase)) kelvin = GammaControl.NeutralKelvin;
                    else { Console.Error.WriteLine($"Could not read \"{warmth}\" as kelvin."); return ExitNoMatch; }
                }
                foreach (var m in targets)
                {
                    if (!ApplyWarmth(m, kelvin, settings, rememberCustom: true)) exit = ExitWriteRefused;
                }
                actions.Add("warmth " + (kelvin >= GammaControl.NeutralKelvin ? "off" : kelvin + "K"));
            }

            if (actions.Count == 0)
            {
                Console.Error.WriteLine("Nothing to do. Try --help.");
                return ExitNoMatch;
            }

            exit = FinishAdjustment(settings, exit, actions, targets.Select(t => t.FriendlyName), Console.Out, Console.Error);
            DisplayGamma.Save();
            return exit;
        }
        finally
        {
            // Resolving baselines can learn something new about a display even
            // when the command changed nothing - a first sighting, or a ramp now
            // recognised as ours. Losing that means the next run has to work it
            // out again from shape alone.
            DisplayGamma.Save();
            foreach (var m in monitors) m.Dispose();
        }
    }

    internal static int FinishAdjustment(AppSettings settings, int hardwareExit,
        IEnumerable<string> actions, IEnumerable<string> names, TextWriter output, TextWriter errors)
    {
        bool saved = settings.Save();
        if (!saved)
            errors.WriteLine("Settings save not confirmed. Monitor requests were already sent; " +
                "Custom levels and saved warmth may not persist. Check settings storage and reopen " +
                "after another app or editor changes the file.");
        // Persistence failure is distinct from a refused monitor request. Do
        // not invent a monitor failure just because both use exit code 2.
        output.WriteLine($"{(hardwareExit == ExitWriteRefused ? "Requested" : "Applied")} " +
            $"{string.Join(", ", actions)} to {string.Join(", ", names)}");
        if (hardwareExit == ExitWriteRefused)
            errors.WriteLine("At least one monitor refused a change - it may be asleep or on another input.");
        return saved ? hardwareExit : ExitWriteRefused;
    }

    internal static int RunSupportReport(string[] args, Func<List<Monitor>> enumerate)
    {
        // A report cannot silently turn a combined invocation into a hardware
        // adjustment, nor emit a private monitor-filter argument in its output.
        if (args.Any(a => !a.Equals("--support-report", StringComparison.OrdinalIgnoreCase) &&
                          !a.Equals("--json", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine("Use --support-report with optional --json only.");
            return ExitNoMatch;
        }
        List<Monitor> monitors = null;
        bool json = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));
        try
        {
            monitors = enumerate();
            var report = SupportReport.Create(monitors, DisplayScanState.Ready, VersionText, Environment.OSVersion.Version);
            Console.WriteLine(json
                ? SupportReport.Json(report) : SupportReport.Text(report));
            return ExitOk;
        }
        catch (Exception)
        {
            var report = SupportReport.Create(Array.Empty<Monitor>(), DisplayScanState.Failed,
                VersionText, Environment.OSVersion.Version);
            Console.WriteLine(json ? SupportReport.Json(report) : SupportReport.Text(report));
            Console.Error.WriteLine("Could not scan displays for the support report. Detailed errors are omitted.");
            return ExitWriteRefused;
        }
        finally
        {
            if (monitors != null) foreach (var monitor in monitors) monitor.Dispose();
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

    /// <summary>
    /// The same, for contrast. Percentages rather than raw numbers for the same
    /// reason: MCCS lets a monitor report any range it likes, and one here
    /// reports 0-100 while the next need not.
    /// </summary>
    private static int ResolveContrast(Monitor m, string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return -1;

        if (spec[0] is '+' or '-')
        {
            if (!int.TryParse(spec, out int delta)) return -1;
            double pct = Presets.ToPercent(m.Contrast, m.ContrastMin, m.ContrastMax) + delta;
            return Presets.FromPercent(pct, m.ContrastMin, m.ContrastMax);
        }
        if (!int.TryParse(spec, out int percent)) return -1;
        return Presets.FromPercent(percent, m.ContrastMin, m.ContrastMax);
    }

    private static bool ApplyBrightness(Monitor m, int raw)
    {
        if (!m.SupportsBrightness) return true;   // nothing to refuse
        bool ok = m.IsInternalPanel
            ? WmiBrightness.Set(m.WmiInstanceName, raw)
            : m.UseDdc(h => Native.SetMonitorBrightness(h, (uint)raw), false);
        if (ok) m.Brightness = raw;
        return ok;
    }

    private static bool ApplyContrast(Monitor m, int raw)
    {
        if (!m.SupportsContrast || !m.HasPhysicalHandle) return true;   // nothing to refuse
        bool ok = m.UseDdc(h => Native.SetMonitorContrast(h, (uint)raw), false);
        if (ok) m.Contrast = raw;
        return ok;
    }

    internal static bool ApplyWarmth(Monitor m, int kelvin, AppSettings settings,
        bool rememberCustom = false, Func<Monitor, bool> apply = null)
    {
        int previous = m.Kelvin;
        bool confirmed = false;
        m.Kelvin = Math.Clamp(kelvin, GammaControl.MinKelvin, GammaControl.MaxKelvin);
        try
        {
            confirmed = (apply ?? DisplayGamma.Apply)(m);
            if (confirmed)
            {
                settings.SetKelvin(m.StableKey, m.DisplayName, m.Kelvin);
                if (rememberCustom) settings.CaptureCustom(m);
            }
            return confirmed;
        }
        finally
        {
            // A refused request must not become the current or saved warmth.
            // DisplayGamma records its ramp signature only after a confirmed write.
            if (!confirmed) m.Kelvin = previous;
        }
    }

    /// <summary>
    /// Put one monitor back to the levels its owner chose. False if none were
    /// ever saved for it, so the caller can say so instead of reporting a
    /// success that moved nothing.
    /// </summary>
    private static bool RestoreCustom(Monitor m, AppSettings settings, ref int exit)
    {
        var e = settings.Find(m.StableKey);
        if (e is not { HasCustom: true }) return false;

        if (e.CustomBrightnessPercent is double bp &&
            !ApplyBrightness(m, Presets.PercentToRaw(m, bp))) exit = ExitWriteRefused;

        if (e.CustomContrastPercent is double cp &&
            !ApplyContrast(m, Presets.FromPercent(cp, m.ContrastMin, m.ContrastMax))) exit = ExitWriteRefused;

        if (e.CustomKelvin is int k && !ApplyWarmth(m, k, settings)) exit = ExitWriteRefused;
        return true;
    }

    internal static string ListText(List<Monitor> monitors)
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
            sb.AppendLine($"{m.DisplayName}  {m.SizeLabel}".TrimEnd());
            sb.AppendLine($"    position   {m.PositionLabel}");
            sb.AppendLine($"    control    {backend}");
            if (m.SupportsBrightness)
                sb.AppendLine($"    brightness {m.Brightness}  ({Presets.RawToPercent(m, m.Brightness):0}% of " +
                              $"{m.BrightnessMin}-{m.BrightnessMax}, {Presets.EstimateFor(m, m.Brightness).DisplayText})");
            if (m.SupportsContrast)
                sb.AppendLine($"    contrast   {m.Contrast}  ({m.ContrastMin}-{m.ContrastMax})");
            sb.AppendLine($"    warmth     {(m.Kelvin >= GammaControl.NeutralKelvin ? "off" : m.Kelvin + "K")}");
            sb.AppendLine($"    device     {m.DeviceName}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    internal static string ListJson(List<Monitor> monitors) =>
        JsonSerializer.Serialize(
            monitors.OrderBy(x => x.Rect.Left).ThenBy(x => x.Rect.Top).Select(m => new
            {
                name = m.DisplayName,
                position = m.PositionLabel,
                size = m.SizeLabel,
                device = m.DeviceName,
                backend = m.BrightnessBackend.ToString(),
                internalPanel = m.IsInternalPanel,
                brightness = m.SupportsBrightness ? m.Brightness : (int?)null,
                brightnessRange = m.SupportsBrightness ? new { min = m.BrightnessMin, max = m.BrightnessMax } : null,
                brightnessPercent = m.SupportsBrightness ? Math.Round(Presets.RawToPercent(m, m.Brightness)) : (double?)null,
                estimatedNits = m.SupportsBrightness ? Presets.NitsFor(m, m.Brightness) : (int?)null,
                nitsAreEstimated = true,
                luminanceProfile = Presets.EstimateFor(m, m.Brightness).ProfileKind,
                profileEndpointsMeasured = Presets.EstimateFor(m, m.Brightness).ProfileEndpointsMeasured,
                contrast = m.SupportsContrast ? m.Contrast : (int?)null,
                kelvin = m.Kelvin,
                rect = new { m.Rect.Left, m.Rect.Top, m.Rect.Right, m.Rect.Bottom },
            }),
            new JsonSerializerOptions { WriteIndented = true });

    private const string HelpText = """
        LumenDeck - brightness, contrast and colour temperature for every monitor.

        Run lumendeck-cli with no arguments to show this help.
        Open LumenDeck.exe for the window and notification-area controls.

          --list                  Show every monitor and its current settings
          --diagnose              Why a monitor does or does not respond
          --support-report        Minimal report with names and device IDs excluded
                                  Use alone, or with --json; never adjusts a display
          --features              Extra controls each monitor advertises
          --json                  Machine-readable output (with --list or --support-report)

          -m, --monitor <text>    Only monitors whose name, position or device
                                  contains <text>. Default: all of them.

          -p, --preset <name>     Day | Evening | Night
                                  Targets estimated luminance from panel
                                  profiles; does not measure emitted light.
                                  Uses the targets saved in the window's
                                  Settings > Preset targets.

                                  Custom
                                  Back to your own levels per monitor - what
                                  they were set to before a preset moved them.

          -b, --brightness <n>    0-100 as a percentage of the panel's range,
                                  or a relative step: +10, -10

          -c, --contrast <n>      0-100 as a percentage of the panel's range,
                                  or a relative step: +10, -10

          -w, --warmth <kelvin>   3000-6500, or "off" for neutral

        -b, -c and -w set your own levels for the monitors they touch, so
        --preset Custom comes back to them.

          -h, --help              This text
          -v, --version           Version

        Examples
          lumendeck-cli --list
          lumendeck-cli --list -m "left"
          lumendeck-cli --preset Night
          lumendeck-cli --preset Custom
          lumendeck-cli --brightness -10
          lumendeck-cli -m "left" -b 55 -c 45 -w 5000
          lumendeck-cli --warmth off

        Requests are validated before settings or display access. Missing, unknown,
        duplicate or invalid options fail without applying an earlier option.
        Do not combine information commands with adjustments. Relative steps stop
        at the display's limits; absolute percentages and Kelvin must be in range.

        Exit codes: 0 done, 1 invalid request or nothing matched,
                    2 a monitor change or settings save failed, or withdrawn --power,
                    3 an unexpected command failure.
        """;
}
