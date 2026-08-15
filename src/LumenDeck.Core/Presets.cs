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

    /// <summary>
    /// The fourth mode, which is not a level: it is whatever the person set
    /// themselves. Not in <see cref="Levels"/> because it has no nits and no
    /// kelvin of its own - it restores per-monitor values from settings.
    ///
    /// It exists because the three above were a one-way door. Pressing one
    /// overwrote brightness and warmth on every monitor with nothing to go back
    /// to, so a stray click cost you a desk you had spent time tuning.
    /// </summary>
    public const string CustomName = "Custom";

    public static bool IsCustom(string name) =>
        string.Equals(name, CustomName, StringComparison.OrdinalIgnoreCase);

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

    // Contrast has the same problem and no luminance model to go with it: the
    // range a monitor reports is its own business. Storing a percentage rather
    // than the raw number means a saved value still means the same thing after a
    // cable swap onto a panel that reports a different range.

    public static double ToPercent(int value, int min, int max)
    {
        double span = max - min;
        if (span <= 0) return Math.Clamp(value, 0, 100);
        return Math.Clamp((value - min) * 100.0 / span, 0, 100);
    }

    public static int FromPercent(double percent, int min, int max)
    {
        percent = Math.Clamp(percent, 0, 100);
        double span = max - min;
        if (span <= 0) return (int)Math.Round(percent);
        return Math.Clamp(min + (int)Math.Round(span * percent / 100.0), min, max);
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
