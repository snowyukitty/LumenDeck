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
/// means something: 0 success, 1 nothing matched, 2 a write was refused.
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
                    var level = Presets.ByName(presetName);
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
                        ApplyWarmth(m, level.Kelvin, settings);
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
                    ApplyWarmth(m, kelvin, settings);
                    settings.CaptureCustom(m);
                }
                actions.Add("warmth " + (kelvin >= GammaControl.NeutralKelvin ? "off" : kelvin + "K"));
            }

            string power = Value("--power");
            if (power != null)
            {
                string mode = power.Trim().ToLowerInvariant();
                if (mode is not ("on" or "off" or "toggle"))
                {
                    Console.Error.WriteLine($"Unknown power mode \"{power}\". Available: on, off, toggle.");
                    return ExitNoMatch;
                }

                foreach (var m in targets)
                {
                    bool offWasRequested = settings.PowerOffRequestedFor(m.StableKey);
                    int target = mode switch
                    {
                        "on" => MonitorPower.On,
                        "off" => MonitorPower.Off,
                        _ => offWasRequested ? MonitorPower.On : MonitorPower.ToggleTarget(m),
                    };

                    bool knownUnsafe = MonitorPower.IsKnownWakeUnsafe(m);
                    bool rememberedUnsafe = settings.PowerWakeUnsafeFor(m.StableKey);
                    if (target == MonitorPower.Off && (knownUnsafe || rememberedUnsafe))
                    {
                        exit = ExitWriteRefused;
                        Console.Error.WriteLine(
                            $"Refusing DDC power-off for {m.FriendlyName}: " +
                            (knownUnsafe
                                ? "this model is known to cut the receiver needed for software wake."
                                : "a previous wake could not be verified on this monitor."));
                        continue;
                    }

                    if (target == MonitorPower.Off)
                    {
                        // Persist intent before the fire-and-forget request, as
                        // the GUI does. A following toggle must be Wake even if
                        // the panel disappears from DDC immediately.
                        settings.SetPowerRiskAccepted(m.StableKey, m.DisplayName, true);
                        settings.SetPowerOffRequested(m.StableKey, m.DisplayName, true);
                        settings.Save();
                    }

                    if (MonitorPower.Set(m, target))
                    {
                        settings.SetPowerOffRequested(
                            m.StableKey, m.DisplayName, target == MonitorPower.Off);
                    }
                    else
                    {
                        exit = ExitWriteRefused;
                        if (target == MonitorPower.On)
                        {
                            settings.SetPowerWakeUnsafe(m.StableKey, m.DisplayName, true);
                            settings.SetPowerOffRequested(m.StableKey, m.DisplayName, true);
                        }
                        else
                        {
                            settings.SetPowerOffRequested(m.StableKey, m.DisplayName, false);
                        }
                    }
                }
                actions.Add("power " + mode);
            }

            if (actions.Count == 0)
            {
                Console.Error.WriteLine("Nothing to do. Try --help.");
                return ExitNoMatch;
            }

            settings.Save();
            DisplayGamma.Save();

            // Writes are queued to the hardware asynchronously in the GUI; here
            // they are direct, but a monitor still needs a moment before a read
            // reflects them. Report what was asked for, and say so plainly.
            Console.WriteLine($"{(exit == ExitWriteRefused ? "Requested" : "Applied")} " +
                              $"{string.Join(", ", actions)} to " +
                              string.Join(", ", targets.Select(t => t.FriendlyName)));
            if (exit == ExitWriteRefused)
                Console.Error.WriteLine("At least one monitor refused a change - it may be asleep or on another input.");
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

    private static void ApplyWarmth(Monitor m, int kelvin, AppSettings settings)
    {
        m.Kelvin = Math.Clamp(kelvin, GammaControl.MinKelvin, GammaControl.MaxKelvin);
        DisplayGamma.Apply(m);
        settings.SetKelvin(m.StableKey, m.DisplayName, m.Kelvin);
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

        if (e.CustomKelvin is int k) ApplyWarmth(m, k, settings);
        return true;
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
            sb.AppendLine($"{m.DisplayName}  {m.SizeLabel}".TrimEnd());
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

                                  Custom
                                  Back to your own levels per monitor - what
                                  they were set to before a preset moved them.

          -b, --brightness <n>    0-100 as a percentage of the panel's range,
                                  or a relative step: +10, -10

          -c, --contrast <n>      0-100 as a percentage of the panel's range,
                                  or a relative step: +10, -10

          -w, --warmth <kelvin>   3000-6500, or "off" for neutral

              --power <mode>     toggle | off | on
                                  "off" uses MCCS DPM-off (VCP D6 value 04),
                                  the monitor's lowest normal power state.
                                  Firmware may cut DDC and require a physical
                                  power button; known unsafe models are refused.
                                  "on" succeeds only after a live DDC read.

        -b, -c and -w set your own levels for the monitors they touch, so
        --preset Custom comes back to them.

          -h, --help              This text
          -v, --version           Version

        Examples
          LumenDeck --list
          LumenDeck --list -m "left"
          LumenDeck --preset Night
          LumenDeck --preset Custom
          LumenDeck --brightness -10
          LumenDeck -m "left" -b 55 -c 45 -w 5000
          LumenDeck --warmth off
          LumenDeck -m "left" --power toggle

        Exit codes: 0 done, 1 nothing matched, 2 a monitor refused the change.
        """;
}
