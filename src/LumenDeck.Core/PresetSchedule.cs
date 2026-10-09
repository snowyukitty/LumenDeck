using System.Globalization;

namespace LumenDeck;

/// <summary>
/// Daily times at which the window requests a preset. Pure wall-clock
/// arithmetic: the caller supplies "now", so the rules can be checked without
/// waiting for a day to pass.
/// </summary>
internal static class PresetSchedule
{
    /// <summary>Presets a schedule may request, in the editor's order.</summary>
    public static readonly string[] Names =
        [.. Presets.Levels.Select(l => l.Name), Presets.CustomName];

    public static readonly IReadOnlyDictionary<string, TimeSpan> DefaultStarts = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
    {
        ["Day"] = new(7, 0, 0),
        ["Evening"] = new(18, 0, 0),
        ["Night"] = new(21, 30, 0),
        [Presets.CustomName] = new(7, 0, 0),
    };

    /// <summary>A preset that begins at a time of day.</summary>
    public sealed record Slot(string Preset, TimeSpan Start);

    /// <summary>
    /// The slot in force at a moment. <see cref="Start"/> is the actual moment
    /// it began, possibly yesterday, so two different days of the same slot
    /// are different periods - which is what lets the schedule act once per day.
    /// </summary>
    public sealed record Period(string Preset, DateTime Start, string NextPreset, DateTime Next);

    public static string Format(TimeSpan time) => time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>24-hour "H:mm" or "HH:mm" in the range 00:00-23:59, minutes only.</summary>
    public static bool TryParse(string text, out TimeSpan time)
    {
        time = default;
        if (!DateTime.TryParseExact(text?.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)) return false;
        time = parsed.TimeOfDay;
        return true;
    }

    public static bool IsKnown(string preset) =>
        Names.Any(n => string.Equals(n, preset, StringComparison.OrdinalIgnoreCase));

    /// <summary>Null when the slots are usable; otherwise the reason they are not.</summary>
    public static string Problem(IReadOnlyCollection<Slot> slots)
    {
        if (slots.Count == 0) return "Choose at least one preset for the schedule.";
        var clash = slots.GroupBy(s => s.Start).FirstOrDefault(g => g.Count() > 1);
        return clash == null ? null
            : $"{string.Join(" and ", clash.Select(s => s.Preset))} start at the same time ({Format(clash.Key)}). Give each a different time.";
    }

    public static Period At(IReadOnlyCollection<Slot> slots, DateTime now)
    {
        if (Problem(slots) != null) return null;
        var ordered = slots.OrderBy(s => s.Start).ToArray();
        int index = Array.FindLastIndex(ordered, s => s.Start <= now.TimeOfDay);
        // Before the first start of the day, yesterday's last slot is still in force.
        var start = index < 0 ? now.Date.AddDays(-1) + ordered[^1].Start : now.Date + ordered[index].Start;
        var current = index < 0 ? ordered[^1] : ordered[index];
        var next = ordered[(index + 1) % ordered.Length];
        var nextStart = now.Date + next.Start;
        if (nextStart <= now) nextStart = nextStart.AddDays(1);
        return new Period(current.Preset, start, next.Preset, nextStart);
    }
}
