using System.Text;
using System.Text.Json;

namespace LumenDeck;

internal enum DisplayScanState { NotStarted, Reading, Ready, Failed }

/// <summary>
/// A support projection with an explicit field allowlist. Never serialize a Monitor
/// or redact its diagnostic strings: new source fields must stay private by default.
/// </summary>
internal static class SupportReport
{
    public sealed record Range(int Minimum, int Maximum);
    public sealed record Display(int Number, bool BrightnessAvailable, Range BrightnessRange,
        bool ContrastAvailable, Range ContrastRange, string BrightnessBackend,
        bool GammaRampReadable, string ExtraControlsScan);
    public sealed record Document(int SchemaVersion, string AppVersion, string WindowsVersion,
        string ScanState, IReadOnlyList<Display> Displays);

    public static Document Create(IEnumerable<Monitor> monitors, DisplayScanState state,
        string appVersion, Version windowsVersion)
    {
        var displays = state == DisplayScanState.Ready
            ? monitors.Select((m, i) => new Display(i + 1, m.SupportsBrightness,
                m.SupportsBrightness ? new Range(m.BrightnessMin, m.BrightnessMax) : null,
                m.SupportsContrast, m.SupportsContrast ? new Range(m.ContrastMin, m.ContrastMax) : null,
                m.BrightnessBackend switch
                {
                    Monitor.Backend.Ddc => "DDC/CI",
                    Monitor.Backend.Wmi => "WMI",
                    _ => "none or unknown",
                },
                m.CapturedRamp != null, m.FeaturesLoaded ? "completed" : "not checked")).ToArray()
            : Array.Empty<Display>();
        string scan = state switch
        {
            DisplayScanState.Ready => "completed",
            DisplayScanState.Reading => "reading",
            DisplayScanState.Failed => "failed",
            _ => "not checked",
        };
        return new(1, Version.TryParse(appVersion, out var version) ? version.ToString() : "unknown",
            windowsVersion?.ToString() ?? "unknown", scan, displays);
    }

    public static string Json(Document report) => JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
    });

    public static string Text(Document report)
    {
        var text = new StringBuilder();
        text.AppendLine("LumenDeck support report (schema 1)");
        text.AppendLine($"App version: {report.AppVersion}");
        text.AppendLine($"Windows version: {report.WindowsVersion}");
        text.AppendLine($"Display scan: {report.ScanState}");
        text.AppendLine("Names, serials, device IDs, paths and raw logs are excluded.");
        text.AppendLine("Counts and control ranges still describe your display setup. Review before sharing.");
        text.AppendLine("Availability reflects the scan, not a guarantee that future writes will succeed.");
        text.AppendLine("Display numbers belong to this report only, not Windows Identify.");
        if (report.ScanState != "completed")
            text.AppendLine("Current display details are unknown; previous results are omitted.");
        else text.AppendLine($"Displays found: {report.Displays.Count}");
        foreach (var display in report.Displays)
        {
            text.AppendLine();
            text.AppendLine($"Display {display.Number}");
            text.AppendLine($"  Brightness: {ControlText(display.BrightnessAvailable, display.BrightnessRange)}");
            text.AppendLine($"  Contrast: {ControlText(display.ContrastAvailable, display.ContrastRange)}");
            text.AppendLine($"  Brightness backend: {display.BrightnessBackend}");
            text.AppendLine($"  Gamma ramp read: {(display.GammaRampReadable ? "readable" : "unavailable during scan")}; warmth application not tested");
            text.AppendLine($"  Extra controls scan: {display.ExtraControlsScan}");
        }
        return text.ToString().TrimEnd();
    }

    private static string ControlText(bool available, Range range) => available
        ? $"available (raw range {range.Minimum}–{range.Maximum})"
        : "unavailable during scan";
}
