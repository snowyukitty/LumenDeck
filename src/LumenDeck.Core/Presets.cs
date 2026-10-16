namespace LumenDeck;

/// <summary>
/// Levels that target estimated luminance, using the
/// profiles in <see cref="PanelDatabase"/>.
/// </summary>
internal static class Presets
{
    public sealed record Level(string Name, int Nits, int Kelvin, string Description);

    // Editable targets stay inside a range the luminance model can express
    // usefully. Above roughly 1000 nits every SDR profile is at its peak anyway;
    // zero would silently mean "the floor" on every panel.
    public const int MinTargetNits = 1;
    public const int MaxTargetNits = 1000;

    /// <summary>
    /// Built-in targets. A person's own targets are stored as overrides in
    /// settings (see <see cref="AppSettings.PresetLevel"/>), so changing these
    /// defaults in a later version still reaches everyone who never edited them.
    /// </summary>
    public static readonly Level[] Levels =
    {
        // Product starting points, not clinical thresholds. See docs/comfort-research.md.
        new("Day",     160, 6500, "Lit room · neutral colour"),
        new("Evening", 100, 5000, "Softer room light · gentle warmth"),
        new("Night",    60, 4000, "Dim room · lower light and warmer colour"),
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

    public static string Guidance(string name) => name switch
    {
        "Day" => "For a lit room. Lower brightness if white pages glare; raise it if text takes effort to read.",
        "Evening" => "For softer room light. Keep text clear; increase kelvin if the tint feels too yellow.",
        "Night" => "Keep some background light and raise brightness if reading feels difficult. Warmer colour does not guarantee better sleep.",
        _ => "Fine-tune brightness and warmth for your room and your eyes. Custom remembers your manual levels for each display.",
    };

    public static bool IsCustom(string name) =>
        string.Equals(name, CustomName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The built-in level with this name; use settings for the active target.</summary>
    public static Level ByName(string name) =>
        Levels.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidTarget(int nits, int kelvin) =>
        nits is >= MinTargetNits and <= MaxTargetNits &&
        kelvin >= GammaControl.MinKelvin && kelvin <= GammaControl.MaxKelvin;

    public static bool IsBuiltIn(Level level) =>
        ByName(level?.Name) is { } builtIn && builtIn.Nits == level.Nits && builtIn.Kelvin == level.Kelvin;

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

    /// <summary>Raw brightness targeting the requested luminance in the linear model.</summary>
    public static int BrightnessFor(Monitor m, int targetNits) =>
        PercentFor(PanelDatabase.For(m), targetNits) is double percent ? PercentToRaw(m, percent) : m.Brightness;

    /// <summary>
    /// Unclamped percentage of the panel's range targeting the requested
    /// luminance, so a preview can say when a target is below the floor or
    /// above the peak. Null when the profile has no usable slope.
    /// </summary>
    public static double? PercentFor(PanelDatabase.Profile p, int targetNits)
    {
        double slope = (p.PeakNits - p.FloorNits) / 100.0;
        return slope > 0 ? (targetNits - p.FloorNits) / slope : null;
    }

    /// <summary>Model-estimated luminance for a raw brightness value. Not an optical reading.</summary>
    public static int NitsFor(Monitor m, int rawBrightness)
    {
        var p = PanelDatabase.For(m);
        double percent = RawToPercent(m, rawBrightness);
        return (int)Math.Round(p.FloorNits + (p.PeakNits - p.FloorNits) * (percent / 100.0));
    }

    public static LuminanceEstimate EstimateFor(Monitor m, int rawBrightness) =>
        new(NitsFor(m, rawBrightness), PanelDatabase.HasUserProfile(m), PanelDatabase.For(m).Measured);
}
