using System.Text.Json;

namespace LumenDeck;

/// <summary>
/// Which gamma ramp belongs to a display, and which one LumenDeck put there.
///
/// This class exists because of a bug that made the app progressively ruin its
/// own output, and it is worth stating plainly since the fix looks like
/// bookkeeping rather than a fix.
///
/// A gamma ramp is GPU state. SetDeviceGammaRamp outlives the process that
/// called it: quit LumenDeck and the warm ramp is still on the display. The
/// enumerator used to capture whatever ramp was loaded and call it "the baseline
/// this display had before the app touched it", which is only true the very
/// first time. On the next launch - and on every Refresh, and on every
/// WM_DISPLAYCHANGE, all of which re-enumerate - it captured the app's own warm
/// ramp and composed the saved warmth onto it again.
///
/// The symptoms were exactly what that predicts and nothing like a colour bug:
///
///  - every screen drifted warmer the longer the app had been in use, on any
///    make of monitor, because the compounding is in the GPU and not the panel;
///  - "Warmth off" restored the polluted capture, so it did not undo anything;
///  - so did the Day preset, whose 6500K writes the baseline back unchanged.
///
/// Two records per display fix it. The baseline is stored, so it survives the
/// process that captured it; and a signature of the last ramp LumenDeck wrote is
/// stored beside it, so a ramp read back later can be identified as the app's
/// own rather than mistaken for the display's state.
///
/// The signature can go stale - a crash, or a settings directory wiped between
/// sessions - so recognition falls back on shape: see
/// <see cref="GammaControl.LooksLikeScaledIdentity"/>, which recovers the common
/// case exactly, and declines to guess in the case where guessing would discard
/// somebody's calibration.
/// </summary>
internal static class DisplayGamma
{
    private sealed class Entry
    {
        /// <summary>Base64 of the 768-entry ramp this display had before LumenDeck touched it.</summary>
        public string Baseline { get; set; } = "";

        /// <summary>Signature of the ramp LumenDeck last wrote here.</summary>
        public string Applied { get; set; } = "";

        /// <summary>Only so the file can be read by a human.</summary>
        public string LastSeenName { get; set; } = "";
    }

    private static Dictionary<string, Entry> _map = new(StringComparer.Ordinal);
    private static bool _loaded;
    private static bool _dirty;

    /// <summary>
    /// Enumeration resolves baselines on a background thread while the UI thread
    /// can be part way through a save. One lock over the whole map; it is taken
    /// a handful of times per rebuild, never in a loop over hardware.
    /// </summary>
    private static readonly object Gate = new();

    /// <summary>
    /// Its own file rather than a corner of settings.json: a baseline is 1.5 KB
    /// of binary per display and settings.json is meant to stay readable and
    /// hand-editable.
    /// </summary>
    public static string FilePath => Path.Combine(AppSettings.Directory, "gamma-baselines.json");

    /// <summary>
    /// Work out the true baseline for each monitor and put it on the monitor.
    /// Call once per enumeration, after stable keys are assigned.
    /// </summary>
    public static void Resolve(IEnumerable<Monitor> monitors)
    {
        Load();

        lock (Gate)
        {
            foreach (var m in monitors)
            {
                if (string.IsNullOrEmpty(m.StableKey)) continue;

                _map.TryGetValue(m.StableKey, out var entry);
                ushort[] stored = Decode(entry?.Baseline);
                ushort[] current = m.CapturedRamp;

                // Nothing readable from the adapter. Fall back on what was
                // stored; Compose treats a null baseline as identity.
                if (current == null)
                {
                    m.BaselineRamp = stored;
                    m.GammaIsOurs = false;
                    continue;
                }

                string signature = GammaControl.Signature(current);

                if (stored != null && entry.Applied == signature)
                {
                    // The ramp on the GPU is one we wrote. The display's own
                    // state is the stored baseline - which is the whole point of
                    // storing it, because a calibration LUT cannot be
                    // reconstructed from the ramp that replaced it.
                    m.BaselineRamp = stored;
                    m.GammaIsOurs = true;
                    continue;
                }

                if (GammaControl.LooksLikeScaledIdentity(current))
                {
                    // Only one thing produces this shape, and its baseline was
                    // identity. Covers a crash, a lost settings directory, and an
                    // upgrade from the version that composed warmth onto warmth.
                    m.BaselineRamp = GammaControl.Identity();
                    m.GammaIsOurs = !GammaControl.IsIdentity(current);
                    Put(m, m.BaselineRamp, signature);
                    continue;
                }

                // Genuinely unrecognised: a real calibration LUT, or a ramp some
                // other tool owns. Take it as the baseline and leave it alone.
                m.BaselineRamp = current;
                m.GammaIsOurs = false;
                Put(m, current, signature);
            }
        }
    }

    /// <summary>
    /// Put this monitor's current warmth on its display, and remember what was
    /// written so the next session can recognise it.
    /// </summary>
    public static bool Apply(Monitor m)
    {
        if (m == null) return false;

        var ramp = GammaControl.Compose(m.BaselineRamp, m.Kelvin);
        if (!GammaControl.Write(m.DeviceName, ramp)) return false;

        m.GammaIsOurs = m.Kelvin < GammaControl.NeutralKelvin;
        Put(m, m.BaselineRamp, GammaControl.Signature(ramp));
        return true;
    }

    /// <summary>
    /// Forget everything about a display and treat the untouched 1:1 ramp as its
    /// baseline. For a display whose ramp some other tool has left in a state
    /// LumenDeck cannot undo.
    /// </summary>
    public static bool ResetToNeutral(Monitor m)
    {
        if (m == null) return false;

        m.BaselineRamp = GammaControl.Identity();
        m.Kelvin = GammaControl.NeutralKelvin;
        return Apply(m);
    }

    private static void Put(Monitor m, ushort[] baseline, string applied)
    {
        if (string.IsNullOrEmpty(m.StableKey)) return;

        lock (Gate)
        {
            if (!_map.TryGetValue(m.StableKey, out var e))
                _map[m.StableKey] = e = new Entry();

            e.Baseline = Encode(baseline);
            e.Applied = applied ?? "";
            e.LastSeenName = m.DisplayName ?? "";
            _dirty = true;
        }
    }

    // ------------------------------------------------------------------- file

    public static void Load()
    {
        lock (Gate)
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                var parsed = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(FilePath));
                if (parsed == null) return;

                var clean = new Dictionary<string, Entry>(StringComparer.Ordinal);
                foreach (var (key, value) in parsed)
                {
                    if (string.IsNullOrWhiteSpace(key) || value == null) continue;
                    value.Baseline ??= "";
                    value.Applied ??= "";
                    value.LastSeenName ??= "";
                    clean[key] = value;
                }
                _map = clean;
            }
            catch
            {
                // A damaged file must never stop the app. Losing it costs one
                // display's calibration LUT if warmth is on at that moment, and
                // the shape test in Resolve recovers the ordinary case anyway.
                _map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            }
        }
    }

    public static void Save()
    {
        lock (Gate)
        {
            if (!_dirty) return;
            try
            {
                System.IO.Directory.CreateDirectory(AppSettings.Directory);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, overwrite: true);
                _dirty = false;
            }
            catch
            {
                // Left dirty on purpose, so the next save tries again.
            }
        }
    }

    private static string Encode(ushort[] ramp)
    {
        if (ramp == null || ramp.Length < 768) return "";
        var bytes = new byte[768 * 2];
        for (int i = 0; i < 768; i++)
        {
            bytes[i * 2] = (byte)ramp[i];
            bytes[i * 2 + 1] = (byte)(ramp[i] >> 8);
        }
        return Convert.ToBase64String(bytes);
    }

    private static ushort[] Decode(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            var bytes = Convert.FromBase64String(text);
            if (bytes.Length != 768 * 2) return null;

            var ramp = new ushort[768];
            for (int i = 0; i < 768; i++)
                ramp[i] = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            return ramp;
        }
        catch
        {
            return null;
        }
    }
}
