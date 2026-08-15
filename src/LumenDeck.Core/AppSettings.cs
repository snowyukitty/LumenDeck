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

        /// <summary>Warmth currently applied to this monitor, whatever put it there.</summary>
        public int Kelvin { get; set; } = GammaControl.NeutralKelvin;

        // The person's own levels for this monitor, which is what makes the Day
        // / Evening / Night buttons reversible. Written when a slider is moved
        // by hand and never by a preset, so a preset cannot destroy them. Null
        // means "never set", which is different from zero.
        //
        // Brightness and contrast are stored as a percentage of the monitor's
        // own range rather than as the raw DDC number: the raw number means
        // nothing on a panel that reports 0-255 instead of 0-100.

        public double? CustomBrightnessPercent { get; set; }
        public double? CustomContrastPercent { get; set; }
        public int? CustomKelvin { get; set; }

        /// <summary>Optional global shortcut for reversible per-screen blanking.</summary>
        public string ScreenBlankHotkey { get; set; } = "";

        /// <summary>
        /// True between lowering this monitor and restoring it. If the process
        /// is killed, the next launch uses this marker to restore the saved
        /// brightness rather than leaving a black screen behind.
        /// </summary>
        public bool ScreenBlankActive { get; set; }

        public double? ScreenBlankBrightnessPercent { get; set; }

        /// <summary>Migrates shortcuts written by the withdrawn DDC-off UI.</summary>
        [JsonPropertyName("PowerHotkey")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyPowerHotkey { get; set; }

        /// <summary>
        /// Derived, so it must not be written. A get-only property is still
        /// serialised by System.Text.Json, and it was landing in settings.json as
        /// a plain `"HasCustom": true` - which reads like a switch somebody could
        /// turn off, in a file this class deliberately keeps hand-editable. It is
        /// ignored on the way back in, so setting it does nothing at all: the
        /// worst kind of knob.
        /// </summary>
        [JsonIgnore]
        public bool HasCustom =>
            CustomBrightnessPercent.HasValue || CustomContrastPercent.HasValue || CustomKelvin.HasValue;
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

    public MonitorSetting Find(string key) =>
        string.IsNullOrEmpty(key) ? null : Monitors.FirstOrDefault(m => m.Key == key);

    private MonitorSetting Entry(string key, string name)
    {
        var e = Find(key);
        if (e == null)
        {
            e = new MonitorSetting { Key = key };
            Monitors.Add(e);
        }
        if (!string.IsNullOrEmpty(name)) e.LastSeenName = name;
        return e;
    }

    public int KelvinFor(string key) => Find(key)?.Kelvin ?? GammaControl.NeutralKelvin;

    public string ScreenBlankHotkeyFor(string key) => Find(key)?.ScreenBlankHotkey ?? "";

    public bool ScreenBlankActiveFor(string key) => Find(key)?.ScreenBlankActive ?? false;

    public double? ScreenBlankBrightnessFor(string key) =>
        Find(key)?.ScreenBlankBrightnessPercent;

    public void SetScreenBlankHotkey(string key, string name, string hotkey)
    {
        if (string.IsNullOrEmpty(key)) return;
        Entry(key, name).ScreenBlankHotkey = hotkey?.Trim() ?? "";
    }

    public void SetScreenBlank(string key, string name, bool active, double? brightnessPercent)
    {
        if (string.IsNullOrEmpty(key)) return;
        var entry = Entry(key, name);
        entry.ScreenBlankActive = active;
        entry.ScreenBlankBrightnessPercent = active ? Sane(brightnessPercent) : null;
    }

    public void SetKelvin(string key, string name, int kelvin)
    {
        if (string.IsNullOrEmpty(key)) return;
        Entry(key, name).Kelvin = kelvin;
    }

    /// <summary>
    /// Record this monitor's live values as the person's own. Called when a
    /// slider is moved by hand, never by a preset - that separation is the
    /// entire reason Custom can bring a desk back.
    /// </summary>
    public void CaptureCustom(Monitor m)
    {
        if (m == null || string.IsNullOrEmpty(m.StableKey)) return;

        var e = Entry(m.StableKey, m.DisplayName);
        if (m.SupportsBrightness) e.CustomBrightnessPercent = Presets.RawToPercent(m, m.Brightness);
        if (m.SupportsContrast) e.CustomContrastPercent = Presets.ToPercent(m.Contrast, m.ContrastMin, m.ContrastMax);
        e.CustomKelvin = m.Kelvin;
    }

    /// <summary>
    /// Give a monitor a Custom position the first time it is seen, taken from
    /// whatever it was already set to.
    ///
    /// Without this, Custom would be empty until somebody happened to move a
    /// slider - so the first press of Day on a fresh install would still be
    /// unrecoverable, which is the complaint this feature answers.
    /// </summary>
    public void SeedCustom(Monitor m)
    {
        if (m == null || string.IsNullOrEmpty(m.StableKey)) return;
        if (Find(m.StableKey) is { HasCustom: true }) return;
        CaptureCustom(m);
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
                        m.ScreenBlankHotkey ??= "";
                        if (m.ScreenBlankHotkey.Length == 0 &&
                            !string.IsNullOrWhiteSpace(m.LegacyPowerHotkey))
                            m.ScreenBlankHotkey = m.LegacyPowerHotkey.Trim();
                        m.LegacyPowerHotkey = null;
                        m.Kelvin = Math.Clamp(m.Kelvin, GammaControl.MinKelvin, GammaControl.MaxKelvin);

                        // A hand-edited or half-written custom value must not be
                        // able to drive a monitor somewhere it cannot go. NaN
                        // survives Math.Clamp, so it is discarded rather than
                        // clamped - it would reach the slider as a silent zero.
                        m.CustomBrightnessPercent = Sane(m.CustomBrightnessPercent);
                        m.CustomContrastPercent = Sane(m.CustomContrastPercent);
                        m.ScreenBlankBrightnessPercent = Sane(m.ScreenBlankBrightnessPercent);
                        if (m.CustomKelvin is int k)
                            m.CustomKelvin = Math.Clamp(k, GammaControl.MinKelvin, GammaControl.MaxKelvin);
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

    private static double? Sane(double? percent) =>
        percent is double p && double.IsFinite(p) ? Math.Clamp(p, 0, 100) : null;

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
