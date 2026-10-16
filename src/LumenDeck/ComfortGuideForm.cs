namespace LumenDeck;

/// <summary>Offline guidance; opening it never requests hardware or settings changes.</summary>
internal sealed class ComfortGuideForm : ReferenceForm
{
    public ComfortGuideForm() : base("Comfort guide",
        "Start with your room, not a number. Different eyes, screens and lighting need different settings. These presets are adjustable starting points, not a treatment or a guarantee of eye comfort.", Sections()) { }

    internal static IEnumerable<ReferenceSection> Sections()
    {
        yield return new("Find a comfortable brightness",
            "Open a page you normally read. Lower brightness if the white background feels glaring; raise it if reading takes effort. Change one control at a time and reassess. Reduce reflections from windows and lamps, and keep some soft light around the screen. Day, Evening and Night change brightness and warmth; they leave hardware contrast alone.");
        foreach (var level in Presets.Levels)
            yield return new($"{level.Name} · {level.Description}",
                $"Built-in starting point: {level.Nits} estimated nits / {level.Kelvin} K tint. " + Presets.Guidance(level.Name));
        yield return new("Make it yours",
            "First adjust one display until reading feels comfortable. Open Settings > Preset targets, then choose Use this display under Day, Evening or Night. This copies the selected display's estimated brightness and current warmth into that mode's draft. Check the previews, then Save targets. Reset this mode restores just one mode's defaults in the draft. Saving does not adjust screens. Manual sliders also update Custom for that display; presets preserve those manual levels. For colour-sensitive work, use Warmth off to request the original gamma ramp and check your calibration.");
        yield return new("Night saver is a temporary very dim desk",
            "Night saver covers unused screens and caps the work screen at 5% hardware brightness, without brightening an already dim screen. This percentage is not a comfort target. If reading feels difficult, raise the work screen's brightness or end Night saver. End Night saver requests saved brightness; a manual brightness choice during the session takes precedence. Keep enough room light to move around safely.");
        yield return new("Give close focus a break",
            "Blink regularly. The American Optometric Association suggests the 20-20-20 habit: about every 20 minutes, look roughly 20 feet (6 metres) away for 20 seconds. Enlarge text instead of leaning closer. Place the screen around an arm's length away, with its top at or below eye level. Persistent discomfort deserves an eye-care assessment.");
        yield return new("Evening light and sleep",
            "For a regular daytime routine, consider softer room and screen light during the roughly three hours before your usual bedtime. Schedule your presets around your own routine, not a universal clock time. A warmer screen alone does not guarantee better sleep; time spent on the screen and the rest of the room also matter.");
        yield return new("What the numbers can tell you",
            "Nits are estimates from a panel profile, not a light sensor. A target below the panel's estimated minimum stops at minimum brightness. Kelvin is an approximate gamma tint, not a measured white point or a measure of light reaching your eyes. HDR, drivers and other colour tools can affect the result. Comfort cannot be verified from a software readback.");
        yield return new("Research behind these choices · reviewed 16 October 2026",
            "OSHA: Computer Workstations — Workstation Environment and Monitors (osha.gov/etools/computer-workstations). AOA: Computer vision syndrome (aoa.org/healthy-eyes/eye-and-vision-conditions/computer-vision-syndrome). Brown et al., PLOS Biology (2022), doi:10.1371/journal.pbio.3001571: light exposure guidance, not screen preset values. Duraccio et al., Sleep Health (2021), doi:10.1016/j.sleh.2021.03.005: no overall sleep benefit from iPhone Night Shift in that trial. None establishes a universal best nits or kelvin value.");
    }
}
