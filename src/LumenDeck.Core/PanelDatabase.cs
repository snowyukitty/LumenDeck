using System.Text.Json;

namespace LumenDeck;

/// <summary>
/// Luminance profiles per panel, so presets can match how bright screens
/// actually look instead of setting the same number everywhere.
///
/// The point a plain slider tool misses: an identical DDC value means different
/// light on different panels. On a 400-nit panel and a 250-nit panel, 43 and 76
/// look the same to the eye. Matching the numbers is exactly what produces a
/// desk where one screen glares and its neighbour looks dead.
///
/// Each panel is modelled as
///     nits ~= floor + (peak - floor) * percent/100
/// where peak is the panel's rated luminance and floor is what a desktop LCD
/// still emits at brightness 0 - typically 40-50 nits, never zero.
///
/// The built-in table is deliberately tiny and generic. Nobody's specific
/// hardware is baked in: real profiles belong in the user's own file at
///     %APPDATA%\LumenDeck\panels.json
/// which is merged over the built-ins at startup. A monitor with no profile
/// gets the generic fallback and the UI labels its figures as estimates rather
/// than quietly presenting a guess as a measurement.
/// </summary>
internal static class PanelDatabase
{
    public sealed record Profile(int PeakNits, int FloorNits, bool Measured = true, string Note = "");

    /// <summary>
    /// Seeded by panel class, not by model. These are the honest averages for
    /// each category and they are all flagged as estimates, because that is
    /// what they are. Add your own model in panels.json to do better.
    /// </summary>
    private static readonly Dictionary<string, Profile> BuiltIn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["*office-ips"] = new(300, 45, false, "Typical office IPS"),
        ["*gaming-va"] = new(320, 50, false, "Typical gaming VA"),
        ["*budget"] = new(250, 40, false, "Typical budget panel"),
        ["*hdr400"] = new(400, 50, false, "DisplayHDR 400 class"),
        ["*laptop"] = new(300, 15, false, "Typical laptop panel; backlight goes far lower"),
    };

    private static readonly Profile Fallback =
        new(300, 45, Measured: false, Note: "generic - no profile for this model");

    private static Dictionary<string, Profile> _user = new(StringComparer.OrdinalIgnoreCase);

    public static string UserFilePath =>
        Path.Combine(AppSettings.Directory, "panels.json");

    /// <summary>
    /// Load the user's own profiles. Any failure leaves the built-ins in place:
    /// a broken optional file must never stop the app.
    /// </summary>
    public static void Load()
    {
        try
        {
            if (!File.Exists(UserFilePath)) return;
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Profile>>(File.ReadAllText(UserFilePath));
            if (parsed == null) return;

            var clean = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in parsed)
            {
                if (string.IsNullOrWhiteSpace(key) || value == null) continue;
                if (value.PeakNits <= 0 || value.FloorNits < 0 || value.FloorNits >= value.PeakNits) continue;
                clean[key] = value with { Measured = true };
            }
            _user = clean;
        }
        catch
        {
            _user = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Write a commented example file so the format is discoverable without docs.</summary>
    public static void WriteExampleIfMissing()
    {
        try
        {
            if (File.Exists(UserFilePath)) return;
            System.IO.Directory.CreateDirectory(AppSettings.Directory);

            var example = new Dictionary<string, Profile>
            {
                ["EXAMPLE - delete this entry"] =
                    new(350, 50, true, "Match on any part of the monitor name as LumenDeck shows it. " +
                                       "PeakNits is the panel's rated brightness; FloorNits is what it " +
                                       "still emits at brightness 0, usually 40-50."),
            };
            File.WriteAllText(UserFilePath,
                JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Purely a convenience; never worth an error.
        }
    }

    /// <summary>
    /// Longest match wins, so a specific model beats a broad family prefix.
    /// User entries are checked before built-ins.
    /// </summary>
    public static Profile For(Monitor m)
    {
        string name = m?.FriendlyName ?? "";

        var hit = BestMatch(_user, name);
        if (hit != null) return hit;

        if (m is { IsInternalPanel: true } && BuiltIn.TryGetValue("*laptop", out var laptop)) return laptop;

        hit = BestMatch(BuiltIn, name);
        return hit ?? Fallback;
    }

    private static Profile BestMatch(Dictionary<string, Profile> table, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Profile best = null;
        int bestLen = 0;
        foreach (var (key, value) in table)
        {
            if (key.StartsWith('*')) continue;             // class keys are not name matches
            if (key.Length <= bestLen) continue;
            if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                best = value;
                bestLen = key.Length;
            }
        }
        return best;
    }

    public static bool IsMeasured(Monitor m) => For(m).Measured;
}
