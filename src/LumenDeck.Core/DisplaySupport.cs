namespace LumenDeck;

/// <summary>Observed control availability, without guessing a connection failure's cause.</summary>
internal static class DisplaySupport
{
    public sealed record Summary(string Name, string Controls, string Guidance, bool NeedsHelp);

    public static Summary Describe(Monitor monitor)
    {
        var controls = new List<string>();
        if (monitor.SupportsBrightness)
            controls.Add(monitor.BrightnessBackend == Monitor.Backend.Wmi ? "brightness (built-in panel)" : "brightness");
        if (monitor.SupportsContrast) controls.Add("contrast");
        bool needsHelp = !monitor.SupportsBrightness || !monitor.SupportsContrast;
        string availability = controls.Count == 0
            ? "Hardware brightness and contrast are unavailable."
            : "Available hardware controls: " + string.Join(", ", controls) + ".";
        string guidance = !needsHelp ? "Brightness and contrast responded during the last scan."
            : monitor.IsInternalPanel
                ? "Built-in panels use the laptop's Windows brightness interface, not DDC/CI. Contrast is often not exposed."
            : monitor.SupportsBrightness
                ? "Brightness responded; contrast did not. A working brightness slider does not mean the panel exposes every control."
                : "Brightness did not respond during the last scan. For an external monitor, check its menu and connection; for a built-in panel, check Windows brightness support. This does not prove DDC/CI is disabled.";
        string name = monitor.DisplayName + (string.IsNullOrWhiteSpace(monitor.PositionLabel) ? "" : $" ({monitor.PositionLabel})");
        return new(name, availability, guidance, needsHelp);
    }

    public static string Inventory(IReadOnlyCollection<Monitor> monitors, bool offerHelp = true)
    {
        if (monitors.Count == 0) return "No displays found. Check your Windows display settings, then choose Refresh." +
            (offerHelp ? " Connection help has more steps." : "");
        int brightness = monitors.Count(m => m.SupportsBrightness);
        return brightness == monitors.Count
            ? $"{monitors.Count} display{(monitors.Count == 1 ? "" : "s")}; brightness is available on all."
            : $"Brightness is available on {brightness} of {monitors.Count} displays." +
                (offerHelp ? " Open Connection help for the unavailable controls." : "");
    }
}
