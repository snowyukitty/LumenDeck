using System.Text.Json;

namespace LumenDeck;

/// <summary>
/// Luminance profiles per panel, so presets can match how bright screens
/// might look instead of setting the same number everywhere.
///
/// The point a plain slider tool misses: an identical DDC value means different
/// light on different panels. On a 400-nit panel and a 250-nit panel, 43 and 76
/// target similar estimated nits under suitable floor assumptions. Matching
/// model values does not establish equal emitted or perceived light.
///
/// Each panel is modelled as
///     nits ~= floor + (peak - floor) * percent/100
/// where peak is the panel's rated luminance and floor is what a desktop LCD
/// still emits at brightness 0. Actual floors and curves depend on the panel.
///
/// Nobody's specific hardware is baked in. Real profiles belong in the user's
/// own file at
///     %APPDATA%\LumenDeck\panels.json
/// which is loaded at startup and matched on the monitor's name. A monitor with
/// no profile gets one of the two estimates below, and the UI labels its figures
/// as estimates rather than quietly presenting a guess as a measurement.
///
/// There used to be a five-entry table of panel classes here - office IPS,
/// gaming VA, budget, HDR400, laptop - keyed as "*office-ips" and so on. Four of
/// the five could never be selected by anything: the matcher skips keys starting
/// with "*", panels.json had no syntax for naming a class, and only the laptop
/// entry was reachable, through the explicit internal-panel branch. So every
/// desktop monitor got the generic fallback while the table implied a choice was
/// being made. A table nothing can select is not a feature waiting to be
/// finished, it is a claim the code does not honour, so it is gone. What is left
/// is what the code actually does.
/// </summary>
internal static class PanelDatabase
{
    // Measured describes user-declared endpoint provenance, never live output.
    public sealed record Profile(int PeakNits, int FloorNits, bool Measured = false, string Note = "");

    /// <summary>
    /// A laptop's internal panel, which is worth separating from the desktop
    /// fallback for one reason: its backlight goes far lower. A 45-nit floor
    /// would make every low-brightness estimate on a laptop wrong by three
    /// times, and the presets solve against the floor.
    /// </summary>
    private static readonly Profile InternalPanel =
        new(300, 15, Measured: false, Note: "typical laptop panel; backlight goes far lower");

    private static readonly Profile Fallback =
        new(300, 45, Measured: false, Note: "generic - no profile for this model");

    private static Dictionary<string, Profile> _user = new(StringComparer.OrdinalIgnoreCase);

    public static string UserFilePath =>
        Path.Combine(AppSettings.Directory, "panels.json");

    /// <summary>
    /// Load the user's own profiles. Any failure leaves the estimates in place:
    /// a broken optional file must never stop the app.
    /// </summary>
    public static void Load() => Load(UserFilePath);

    internal static void Load(string filePath)
    {
        // A reload must not retain a profile removed from the file or a prior
        // successful load when the replacement is missing, null or malformed.
        try
        {
            _user = File.Exists(filePath) ? ParseProfiles(File.ReadAllText(filePath)) : new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _user = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Write a commented example file so the format is discoverable without docs.</summary>
    public static void WriteExampleIfMissing() => WriteExampleIfMissing(UserFilePath);

    internal static void WriteExampleIfMissing(string filePath)
    {
        try
        {
            if (File.Exists(filePath)) return;
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            var example = new Dictionary<string, Profile>
            {
                ["EXAMPLE - delete this entry"] =
                    new(350, 50, false, "Illustrative estimates only. Match a model-name substring. " +
                                       "Record peak/floor sources and picture mode here. Set Measured true " +
                                       "only for instrument-measured endpoints; calculated nits remain estimates."),
            };
            File.WriteAllText(filePath,
                JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Purely a convenience; never worth an error.
        }
    }

    /// <summary>
    /// A profile the user wrote if one matches this monitor's name, otherwise an
    /// estimate. Longest match wins, so a specific model beats a broad family
    /// prefix in somebody's own file.
    /// </summary>
    public static Profile For(Monitor m)
    {
        var hit = BestMatch(_user, m?.FriendlyName ?? "");
        if (hit != null) return hit;

        return FallbackFor(m is { IsInternalPanel: true });
    }

    internal static Dictionary<string, Profile> ParseProfiles(string json)
    {
        var parsed = JsonSerializer.Deserialize<Dictionary<string, Profile>>(json);
        var clean = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
        if (parsed == null) return clean;
        foreach (var (key, value) in parsed)
            if (!string.IsNullOrWhiteSpace(key) && IsValid(value)) clean[key] = value;
        return clean;
    }

    internal static void InstallProfiles(IReadOnlyDictionary<string, Profile> profiles) =>
        _user = new Dictionary<string, Profile>(profiles, StringComparer.OrdinalIgnoreCase);

    internal static Profile FallbackFor(bool internalPanel) => internalPanel ? InternalPanel : Fallback;

    internal static bool IsValid(Profile profile) => profile is { PeakNits: > 0, FloorNits: >= 0 } &&
        profile.FloorNits < profile.PeakNits;

    private static Profile BestMatch(Dictionary<string, Profile> table, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Profile best = null;
        int bestLen = 0;
        foreach (var (key, value) in table)
        {
            if (key.Length <= bestLen) continue;
            if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                best = value;
                bestLen = key.Length;
            }
        }
        return best;
    }

    public static bool HasUserProfile(Monitor m) => BestMatch(_user, m?.FriendlyName ?? "") != null;
}
