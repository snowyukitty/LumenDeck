using System.Text.Json;
using System.Text.Json.Serialization;

namespace LumenDeck;

/// <summary>
/// Settings live in %APPDATA%\LumenDeck\settings.json.
///
/// Monitor entries are keyed on EDID identity, never on \\.\DISPLAYn: the device
/// suffix is not stable across cable swaps and is not even contiguous on this
/// machine. Keying on it would silently apply one monitor's colour temperature
/// to another after a replug.
/// </summary>
internal sealed class AppSettings
{
    public sealed class MonitorSetting
    {
        public string Key { get; set; } = "";
        public string LastSeenName { get; set; } = "";
        public int Kelvin { get; set; } = GammaControl.NeutralKelvin;
    }

    public List<MonitorSetting> Monitors { get; set; } = new();

    /// <summary>
    /// Reapply saved colour temperatures at startup. On by default because a
    /// gamma ramp does not survive a reboot, so without this the blue light
    /// setting silently disappears overnight and looks broken.
    /// </summary>
    public bool ReapplyColourOnStart { get; set; } = true;

    public bool StartMinimised { get; set; }
    public bool MinimiseToTray { get; set; } = true;

    [JsonIgnore]
    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenDeck");

    [JsonIgnore]
    public static string FilePath => Path.Combine(Directory, "settings.json");

    public int KelvinFor(string key)
    {
        var e = Monitors.FirstOrDefault(m => m.Key == key);
        return e?.Kelvin ?? GammaControl.NeutralKelvin;
    }

    public void SetKelvin(string key, string name, int kelvin)
    {
        var e = Monitors.FirstOrDefault(m => m.Key == key);
        if (e == null)
        {
            e = new MonitorSetting { Key = key };
            Monitors.Add(e);
        }
        e.LastSeenName = name;
        e.Kelvin = kelvin;
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null)
                {
                    // Valid JSON is not the same as usable data. `{"Monitors":
                    // null}` deserialises without complaint and then throws a
                    // NullReferenceException on first use - inside the MainForm
                    // constructor, before the message loop exists, so the
                    // ThreadException handler cannot catch it and the app simply
                    // dies at launch. Repair the shape here instead.
                    s.Monitors ??= new List<MonitorSetting>();
                    s.Monitors.RemoveAll(m => m == null || string.IsNullOrEmpty(m.Key));
                    foreach (var m in s.Monitors)
                    {
                        m.LastSeenName ??= "";
                        m.Kelvin = Math.Clamp(m.Kelvin, GammaControl.MinKelvin, GammaControl.MaxKelvin);
                    }
                    return s;
                }
            }
        }
        catch
        {
            // A corrupt settings file must never stop the app starting. Defaults
            // are always a usable state, and the next save overwrites it.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            // Write to a temp file and move into place, so an interrupted save
            // cannot leave a half-written file that fails to parse next start.
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch
        {
            // Losing a preference is not worth an error dialog mid-session.
        }
    }
}
