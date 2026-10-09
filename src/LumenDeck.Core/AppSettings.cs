using System.Text;
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
    private string _storagePath;
    private SettingsFileStore _store;
    private bool _sourceUnavailable;
    private int _savePending;

    [JsonIgnore]
    public bool IsSavePending => Volatile.Read(ref _savePending) != 0;

    [JsonIgnore]
    public SettingsSaveResult LastSaveResult { get; private set; } = SettingsSaveResult.Unavailable;

    // Preserve fields from a newer app version without advertising them as
    // editable preferences in this version.
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalFields { get; set; }

    public AppSettings() { }

    /// <summary>Use isolated storage in tests without touching the user's settings.</summary>
    internal AppSettings(string storagePath) => _storagePath = storagePath;
    public sealed class MonitorSetting
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalFields { get; set; }
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
        public int? ScreenBlankBrightnessRaw { get; set; }

        /// <summary>
        /// True while Night saver holds this work display dimmed. Written before
        /// lowering brightness, like the blackout marker, so a killed process
        /// still gets the original brightness back at the next launch.
        /// </summary>
        public bool NightDimActive { get; set; }
        public double? NightDimBrightnessPercent { get; set; }
        public int? NightDimBrightnessRaw { get; set; }

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
    /// A person's own Day / Evening / Night target. Only presets that differ
    /// from the built-in table are stored, so an unedited preset keeps following
    /// the defaults of whichever version is installed. Entries with a name this
    /// version does not know are kept untouched for the version that wrote them.
    /// </summary>
    public sealed class PresetTarget
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalFields { get; set; }
        public string Name { get; set; } = "";
        public int Nits { get; set; }
        public int Kelvin { get; set; }
    }

    public List<PresetTarget> PresetTargets { get; set; } = new();

    /// <summary>The active target for a built-in preset name, or null for an unknown name.</summary>
    public Presets.Level PresetLevel(string name)
    {
        var builtIn = Presets.ByName(name);
        if (builtIn == null) return null;
        var own = PresetTargets.FirstOrDefault(t => Matches(t, builtIn));
        return own == null ? builtIn : builtIn with { Nits = own.Nits, Kelvin = own.Kelvin };
    }

    [JsonIgnore]
    public IReadOnlyList<Presets.Level> PresetLevels => Presets.Levels.Select(l => PresetLevel(l.Name)).ToArray();

    /// <summary>
    /// A new target list holding <paramref name="levels"/>, without changing
    /// this object. The window installs it only after the save that contains it
    /// is confirmed, the same way preferences and shortcuts wait for theirs.
    /// </summary>
    internal List<PresetTarget> WithPresetLevels(IEnumerable<Presets.Level> levels)
    {
        var result = PresetTargets.ToList();
        foreach (var level in levels)
        {
            var builtIn = Presets.ByName(level.Name);
            if (builtIn == null || !Presets.IsValidTarget(level.Nits, level.Kelvin)) continue;
            bool asBuilt = level.Nits == builtIn.Nits && level.Kelvin == builtIn.Kelvin;
            // Every entry for this name is rewritten or removed, never only the
            // first: removing one would let a hand-edited duplicate behind it
            // become the active target. Plain entries go; one that carries a
            // newer version's fields keeps them with the requested values.
            bool kept = false;
            for (int i = result.Count - 1; i >= 0; i--)
            {
                var existing = result[i];
                if (!Matches(existing, builtIn)) continue;
                if (existing.AdditionalFields is not { Count: > 0 }) { result.RemoveAt(i); continue; }
                result[i] = new PresetTarget
                {
                    Name = builtIn.Name, Nits = level.Nits, Kelvin = level.Kelvin,
                    AdditionalFields = existing.AdditionalFields,
                };
                kept = true;
            }
            if (!kept && !asBuilt)
                result.Add(new PresetTarget { Name = builtIn.Name, Nits = level.Nits, Kelvin = level.Kelvin });
        }
        return result;
    }

    /// <summary>
    /// Daily preset changes. Off until the person turns it on: a window that
    /// starts adjusting screens at times nobody chose would look like a fault.
    /// Times are stored as "HH:mm" text so the file stays hand-editable.
    /// </summary>
    public sealed class ScheduleSetting
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalFields { get; set; }
        public bool Enabled { get; set; }
        public List<ScheduleEntry> Entries { get; set; } = DefaultEntries();
    }

    public sealed class ScheduleEntry
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalFields { get; set; }
        public string Preset { get; set; } = "";
        public string Start { get; set; } = "";
        public bool Enabled { get; set; } = true;
    }

    // Custom starts unused: a fresh schedule moves between the three levels.
    private static List<ScheduleEntry> DefaultEntries() => PresetSchedule.Names.Select(name => new ScheduleEntry
    {
        Preset = name, Start = PresetSchedule.Format(PresetSchedule.DefaultStarts[name]),
        Enabled = !Presets.IsCustom(name),
    }).ToList();

    public ScheduleSetting Schedule { get; set; } = new();

    /// <summary>
    /// Usable slots, one per known preset; the first entry for a name wins.
    /// Unknown names and unreadable times are ignored, never guessed at.
    /// </summary>
    public IReadOnlyList<PresetSchedule.Slot> ScheduleSlots(ScheduleSetting schedule = null) =>
        (schedule ?? Schedule).Entries
            .Where(e => PresetSchedule.IsKnown(e.Preset))
            .GroupBy(e => e.Preset, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(e => e.Enabled && PresetSchedule.TryParse(e.Start, out _))
            .Select(e => new PresetSchedule.Slot(PresetSchedule.Names.First(n =>
                string.Equals(n, e.Preset, StringComparison.OrdinalIgnoreCase)),
                PresetSchedule.TryParse(e.Start, out var t) ? t : default))
            .ToArray();

    /// <summary>The editable row for a known preset, as the editor should show it.</summary>
    public (bool Used, TimeSpan Start) ScheduleRow(string preset)
    {
        var entry = Schedule.Entries.FirstOrDefault(e => string.Equals(e.Preset, preset, StringComparison.OrdinalIgnoreCase));
        return entry != null && PresetSchedule.TryParse(entry.Start, out var start)
            ? (entry.Enabled, start)
            : (false, PresetSchedule.DefaultStarts[preset]);
    }

    /// <summary>
    /// A new schedule holding the requested rows, without changing this
    /// object; the window installs it only after its save is confirmed.
    /// Entries for unknown presets and fields from newer versions are kept.
    /// </summary>
    internal ScheduleSetting WithSchedule(bool enabled, IEnumerable<(string Preset, bool Used, TimeSpan Start)> rows)
    {
        var wanted = new Dictionary<string, (bool Used, TimeSpan Start)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(r => PresetSchedule.IsKnown(r.Preset))) wanted[row.Preset] = (row.Used, row.Start);
        ScheduleEntry Entry(string preset, Dictionary<string, JsonElement> fields) => new()
        {
            Preset = PresetSchedule.Names.First(n => string.Equals(n, preset, StringComparison.OrdinalIgnoreCase)),
            Start = PresetSchedule.Format(wanted[preset].Start), Enabled = wanted[preset].Used, AdditionalFields = fields,
        };
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ScheduleEntry>();
        foreach (var existing in Schedule.Entries)
        {
            if (!wanted.ContainsKey(existing.Preset)) entries.Add(existing);
            // Later duplicates would be ignored, so drop them rather than keep a
            // hand-edited row that looks active and is not.
            else if (written.Add(existing.Preset)) entries.Add(Entry(existing.Preset, existing.AdditionalFields));
        }
        foreach (string preset in wanted.Keys.Where(p => !written.Contains(p))) entries.Add(Entry(preset, null));
        return new ScheduleSetting { Enabled = enabled, Entries = entries, AdditionalFields = Schedule.AdditionalFields };
    }

    private static bool Matches(PresetTarget target, Presets.Level builtIn) =>
        string.Equals(target.Name, builtIn.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reapply saved colour temperatures at startup. On by default because a
    /// gamma ramp does not survive a reboot, so without this the blue light
    /// setting silently disappears overnight and looks broken.
    /// </summary>
    public bool ReapplyColourOnStart { get; set; } = true;

    public bool StartMinimised { get; set; }
    public bool MinimiseToTray { get; set; } = true;
    public string NightWorkMonitorKey { get; set; } = "";

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

    public void SetScreenBlank(string key, string name, bool active, double? brightnessPercent, int? brightnessRaw = null)
    {
        if (string.IsNullOrEmpty(key)) return;
        var entry = Entry(key, name);
        entry.ScreenBlankActive = active;
        entry.ScreenBlankBrightnessPercent = active ? Sane(brightnessPercent) : null;
        entry.ScreenBlankBrightnessRaw = active ? brightnessRaw : null;
    }

    public bool NightDimActiveFor(string key) => Find(key)?.NightDimActive ?? false;

    public void SetNightDim(string key, string name, bool active, double? brightnessPercent, int? brightnessRaw)
    {
        if (string.IsNullOrEmpty(key)) return;
        var entry = Entry(key, name);
        entry.NightDimActive = active;
        entry.NightDimBrightnessPercent = active ? Sane(brightnessPercent) : null;
        entry.NightDimBrightnessRaw = active ? brightnessRaw : null;
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
        // A blacked-out or night-dimmed brightness is a temporary state, not
        // the level the person chose; Custom must not remember it.
        if (m.SupportsBrightness && !ScreenBlankActiveFor(m.StableKey) && !NightDimActiveFor(m.StableKey))
            e.CustomBrightnessPercent = Presets.RawToPercent(m, m.Brightness);
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

    public static AppSettings Load() => Load(FilePath);

    internal static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                byte[] original = File.ReadAllBytes(path);
                // Keep the previous text loader's BOM compatibility while
                // validating and writing against the exact same captured bytes.
                using var input = new MemoryStream(original, writable: false);
                using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var s = JsonSerializer.Deserialize<AppSettings>(reader.ReadToEnd());
                if (s != null)
                {
                    s._storagePath = path;
                    s._store = new SettingsFileStore(path, original);
                    // Valid JSON is not the same as usable data. `{"Monitors":
                    // null}` deserialises without complaint and then throws a
                    // NullReferenceException on first use - inside the MainForm
                    // constructor, before the message loop exists, so the
                    // ThreadException handler cannot catch it and the app simply
                    // dies at launch. Repair the shape here instead.
                    s.Monitors ??= new List<MonitorSetting>();
                    s.Monitors.RemoveAll(m => m == null || string.IsNullOrEmpty(m.Key));
                    s.Schedule ??= new ScheduleSetting();
                    s.Schedule.Entries ??= new List<ScheduleEntry>();
                    s.Schedule.Entries.RemoveAll(e => e == null);
                    foreach (var e in s.Schedule.Entries) { e.Preset ??= ""; e.Start ??= ""; }
                    s.PresetTargets ??= new List<PresetTarget>();
                    s.PresetTargets.RemoveAll(t => t == null);
                    foreach (var t in s.PresetTargets)
                    {
                        t.Name ??= "";
                        // Clamp only the presets this version owns; another
                        // version's entry may use a range this one cannot judge.
                        if (Presets.ByName(t.Name) == null) continue;
                        t.Nits = Math.Clamp(t.Nits, Presets.MinTargetNits, Presets.MaxTargetNits);
                        t.Kelvin = Math.Clamp(t.Kelvin, GammaControl.MinKelvin, GammaControl.MaxKelvin);
                    }
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
                        m.NightDimBrightnessPercent = Sane(m.NightDimBrightnessPercent);
                        if (m.CustomKelvin is int k)
                            m.CustomKelvin = Math.Clamp(k, GammaControl.MinKelvin, GammaControl.MaxKelvin);
                    }
                    return s;
                }
                return new AppSettings(path) { _sourceUnavailable = true };
            }
        }
        catch
        {
            // A corrupt/unreadable file must not stop launch or be overwritten
            // by the defaults returned for this session.
            return new AppSettings(path) { _sourceUnavailable = true };
        }
        return new AppSettings(path);
    }

    private static double? Sane(double? percent) =>
        percent is double p && double.IsFinite(p) ? Math.Clamp(p, 0, 100) : null;

    internal SettingsSaveOperation PrepareSave()
    {
        if (Interlocked.CompareExchange(ref _savePending, 1, 0) != 0)
        {
            LastSaveResult = SettingsSaveResult.Busy;
            return null;
        }
        Func<SettingsSaveResult> write;
        try
        {
            string path = _storagePath ?? FilePath;
            // Capture on the owning thread. The writer never enumerates a live
            // monitor list or reads preferences after this snapshot is created.
            byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(this, new JsonSerializerOptions { WriteIndented = true });
            _store ??= new SettingsFileStore(path, null);
            write = _sourceUnavailable ? () => SettingsSaveResult.Unavailable : () => _store.Save(serialized);
        }
        catch
        {
            write = () => SettingsSaveResult.Failed;
        }
        return new SettingsSaveOperation(write, result =>
        {
            LastSaveResult = result;
            Volatile.Write(ref _savePending, 0);
        });
    }

    public bool Save()
    {
        var operation = PrepareSave();
        if (operation == null) return false;
        SettingsSaveResult result;
        try { result = operation.Write(); }
        catch { result = SettingsSaveResult.Failed; }
        operation.Complete(result);
        return result == SettingsSaveResult.Saved;
    }
}
