namespace LumenDeck;

/// <summary>
/// Names for the MCCS codes worth putting in front of a person, and names for
/// the values inside them.
///
/// Only codes listed here are ever shown. That is deliberate on both sides:
///
///  - A monitor's capability string routinely advertises codes nobody wants a
///    slider for (horizontal frequency, firmware level, asset tags).
///  - A code NOT in a monitor's capability string must never be offered, because
///    reading an unsupported code does not fail - it answers with a plausible
///    number. One panel returned 80 for three RGB black-level codes it does not
///    implement, which reads exactly like a real setting that needs fixing.
///
/// So the UI is the intersection: codes this catalog knows how to present, and
/// codes the monitor itself claims to support.
/// </summary>
internal static class VcpCatalog
{
    /// <summary>How a feature should be presented.</summary>
    public enum Kind
    {
        /// <summary>0..max, a slider.</summary>
        Continuous,
        /// <summary>A fixed set of values, a dropdown.</summary>
        Select,
        /// <summary>A one-shot action, a button.</summary>
        Action,
    }

    public sealed record Definition(
        byte Code,
        string Name,
        Kind Kind,
        string Description = "",
        IReadOnlyDictionary<int, string> Values = null,
        bool Risky = false);

    private static readonly Dictionary<int, string> InputSources = new()
    {
        [0x01] = "VGA 1",
        [0x02] = "VGA 2",
        [0x03] = "DVI 1",
        [0x04] = "DVI 2",
        [0x05] = "Composite 1",
        [0x06] = "Composite 2",
        [0x07] = "S-Video 1",
        [0x08] = "S-Video 2",
        [0x09] = "Tuner 1",
        [0x0A] = "Tuner 2",
        [0x0B] = "Tuner 3",
        [0x0C] = "Component 1",
        [0x0D] = "Component 2",
        [0x0E] = "Component 3",
        [0x0F] = "DisplayPort 1",
        [0x10] = "DisplayPort 2",
        [0x11] = "HDMI 1",
        [0x12] = "HDMI 2",
        [0x13] = "HDMI 3",
        [0x14] = "HDMI 4",
        [0x15] = "USB-C",
    };

    private static readonly Dictionary<int, string> ColourPresets = new()
    {
        [0x01] = "sRGB",
        [0x02] = "Display native",
        [0x03] = "4000 K",
        [0x04] = "5000 K",
        [0x05] = "6500 K",
        [0x06] = "7500 K",
        [0x07] = "8200 K",
        [0x08] = "9300 K",
        [0x09] = "10000 K",
        [0x0A] = "11500 K",
        [0x0B] = "User 1",
        [0x0C] = "User 2",
        [0x0D] = "User 3",
    };

    private static readonly Dictionary<int, string> PictureModes = new()
    {
        [0x00] = "Standard",
        [0x01] = "Productivity",
        [0x02] = "Mixed",
        [0x03] = "Movie",
        [0x04] = "User defined",
        [0x05] = "Games",
        [0x06] = "Sports",
        [0x07] = "Professional",
        [0x08] = "Standard, low power",
        [0x09] = "Standard, lowest power",
        [0x0A] = "Demonstration",
        [0xF0] = "Dynamic contrast",
    };

    private static readonly Dictionary<int, string> PowerModes = new()
    {
        [0x01] = "On",
        [0x02] = "Standby",
        [0x03] = "Suspend",
        [0x04] = "Off, low power",
        [0x05] = "Off",
    };

    /// <summary>
    /// Ordered: this is the order controls appear under a monitor. Brightness,
    /// contrast and colour temperature are handled by dedicated UI and are not
    /// repeated here.
    /// </summary>
    public static readonly Definition[] All =
    {
        new(0x60, "Input source", Kind.Select,
            "Which cable the monitor is showing. Switching sends this screen to another computer.",
            InputSources, Risky: true),

        new(0x14, "Colour preset", Kind.Select,
            "The monitor's own white point. Independent of LumenDeck's warmth, which is applied by the GPU.",
            ColourPresets),

        new(0xDC, "Picture mode", Kind.Select,
            "The monitor's own picture preset.",
            PictureModes),

        new(0x62, "Speaker volume", Kind.Continuous,
            "Only meaningful on a monitor with speakers or a headphone jack."),

        new(0x87, "Sharpness", Kind.Continuous,
            "Edge enhancement. Most panels look best at the middle of the range."),

        new(0x8A, "Colour saturation", Kind.Continuous),

        new(0x16, "Red gain", Kind.Continuous,
            "Fine white-balance trim. Many monitors advertise these and ignore writes to them."),
        new(0x18, "Green gain", Kind.Continuous,
            "Fine white-balance trim. Many monitors advertise these and ignore writes to them."),
        new(0x1A, "Blue gain", Kind.Continuous,
            "Fine white-balance trim. Many monitors advertise these and ignore writes to them."),

        new(0x6C, "Red black level", Kind.Continuous),
        new(0x6E, "Green black level", Kind.Continuous),
        new(0x70, "Blue black level", Kind.Continuous),

        new(0xD6, "Power mode", Kind.Select,
            "Putting a monitor to sleep from here is recoverable, but you will need its own button or a mouse move to wake it.",
            PowerModes, Risky: true),

        new(0x04, "Restore factory defaults", Kind.Action,
            "Resets everything the monitor stores, including brightness and contrast.",
            Risky: true),

        new(0x08, "Restore factory colour", Kind.Action,
            "Resets the monitor's colour settings only.",
            Risky: true),
    };

    private static readonly Dictionary<byte, Definition> ByCode = All.ToDictionary(d => d.Code);

    public static Definition Find(byte code) => ByCode.TryGetValue(code, out var d) ? d : null;

    /// <summary>Human label for a value, falling back to the raw number.</summary>
    public static string ValueName(Definition d, int value)
    {
        if (d?.Values != null && d.Values.TryGetValue(value, out string name)) return name;
        return value.ToString();
    }
}
