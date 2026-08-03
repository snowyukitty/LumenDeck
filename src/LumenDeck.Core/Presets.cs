namespace LumenDeck;

/// <summary>
/// Levels that aim every panel at the same perceived luminance, using the
/// profiles in <see cref="PanelDatabase"/>.
/// </summary>
internal static class Presets
{
    public sealed record Level(string Name, int Nits, int Kelvin, string Description);

    public static readonly Level[] Levels =
    {
        new("Day",     200, 6500, "Matches a normally lit room"),
        new("Evening", 150, 5600, "Softer, slight warm shift"),
        new("Night",   110, 4600, "Long reading after dark"),
    };

    public static Level ByName(string name) =>
        Levels.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

    // The luminance model works in PERCENT of the panel's range. The value a
    // monitor actually wants over DDC is a raw VCP number in whatever range it
    // reports, and that range is not always 0-100 - MCCS permits any, and 0-255
    // is common. Treating the percentage as the raw value would set a 0-255
    // panel to roughly a quarter of the intended brightness while the UI happily
    // reported the target luminance. Convert in both directions.

    private static double RawSpan(Monitor m)
    {
        if (!m.SupportsBrightness) return 100;
        double span = m.BrightnessMax - m.BrightnessMin;
        return span > 0 ? span : 100;
    }

    public static int PercentToRaw(Monitor m, double percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (!m.SupportsBrightness) return (int)Math.Round(percent);
        int raw = m.BrightnessMin + (int)Math.Round(RawSpan(m) * percent / 100.0);
        return Math.Clamp(raw, m.BrightnessMin, m.BrightnessMax);
    }

    public static double RawToPercent(Monitor m, int raw)
    {
        if (!m.SupportsBrightness) return Math.Clamp(raw, 0, 100);
        return Math.Clamp((raw - m.BrightnessMin) * 100.0 / RawSpan(m), 0, 100);
    }

    /// <summary>Raw brightness value that puts this panel at the requested luminance.</summary>
    public static int BrightnessFor(Monitor m, int targetNits)
    {
        var p = PanelDatabase.For(m);
        double slope = (p.PeakNits - p.FloorNits) / 100.0;
        if (slope <= 0) return m.Brightness;

        double percent = (targetNits - p.FloorNits) / slope;
        return PercentToRaw(m, percent);
    }

    /// <summary>Rough luminance a given raw brightness value produces. For display only.</summary>
    public static int NitsFor(Monitor m, int rawBrightness)
    {
        var p = PanelDatabase.For(m);
        double percent = RawToPercent(m, rawBrightness);
        return (int)Math.Round(p.FloorNits + (p.PeakNits - p.FloorNits) * (percent / 100.0));
    }

    public static bool IsKnown(Monitor m) => PanelDatabase.IsMeasured(m);
}
