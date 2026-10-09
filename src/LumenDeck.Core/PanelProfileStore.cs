using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LumenDeck;

internal enum ProfileSaveResult { Saved, InvalidValues, SourceChanged, Busy, Failed }

/// <summary>
/// Edits one full-model entry while preserving other entries and unknown fields.
/// A snapshot check protects stale dialogs; a global mutex serializes cooperating
/// writers across Windows sessions that access the same full path.
/// Noncooperating external editors can still race after the final snapshot check.
/// </summary>
internal sealed class PanelProfileStore
{
    private readonly string _path;
    private readonly byte[] _original;
    private readonly JsonObject _root;
    private readonly string _exactKey;
    private readonly bool _internalPanel;
    public string ModelName { get; }
    public bool HasExactEntry => _exactKey != null;
    public string CurrentSource => Match(removeExact: false) == null ? "generic" :
        _exactKey != null && PanelDatabase.IsValid(_root[_exactKey]?.Deserialize<PanelDatabase.Profile>()) ? "exact" : "broader";
    public PanelDatabase.Profile CurrentProfile => Select(removeExact: false);
    public PanelDatabase.Profile RemovalProfile => Select(removeExact: true);
    public bool RemovalUsesUserProfile => Match(removeExact: true) != null;
    public IReadOnlyDictionary<string, PanelDatabase.Profile> CommittedProfiles { get; private set; }

    private PanelProfileStore(string path, string modelName, bool internalPanel, byte[] original, JsonObject root)
    {
        _path = Path.GetFullPath(path);
        ModelName = modelName;
        _internalPanel = internalPanel;
        _original = original;
        _root = root;
        _exactKey = root.Select(entry => entry.Key).FirstOrDefault(key =>
            string.Equals(key, modelName, StringComparison.OrdinalIgnoreCase));
    }

    public static PanelProfileStore Open(string path, string modelName, bool internalPanel)
    {
        if (string.IsNullOrWhiteSpace(modelName)) throw new InvalidDataException("No model name.");
        byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        JsonObject root = new();
        if (original != null)
        {
            string text = DecodeSource(original);
            using (var document = JsonDocument.Parse(text)) ValidateKeys(document.RootElement);
            root = JsonNode.Parse(text) as JsonObject ??
                throw new InvalidDataException("Profile root must be an object.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in root)
            {
                if (!names.Add(entry.Key)) throw new InvalidDataException("Ambiguous model keys.");
                // Validate typed input before allowing a write. Numeric-invalid
                // profiles and null legacy entries are retained, not silently dropped.
                if (entry.Value != null)
                {
                    if (entry.Value is not JsonObject) throw new InvalidDataException("Invalid entry.");
                    _ = entry.Value.Deserialize<PanelDatabase.Profile>();
                }
            }
        }
        return new(path, modelName, internalPanel, original, root);
    }

    private static string DecodeSource(byte[] bytes)
    {
        // Keep the existing Unicode BOM formats, but never replace malformed
        // bytes while preparing a write. UTF-32 LE must precede UTF-16 LE:
        // their BOMs share the first two bytes.
        Encoding[] encodings =
        [
            new UTF32Encoding(false, true, true),
            new UTF32Encoding(true, true, true),
            new UnicodeEncoding(false, true, true),
            new UnicodeEncoding(true, true, true),
            new UTF8Encoding(true, true),
        ];
        foreach (var encoding in encodings)
        {
            byte[] preamble = encoding.GetPreamble();
            if (bytes.AsSpan().StartsWith(preamble))
                return encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
        }
        return new UTF8Encoding(false, true).GetString(bytes);
    }

    private static void ValidateKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new InvalidDataException("Duplicate property.");
                ValidateKeys(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) ValidateKeys(child);
    }

    private PanelDatabase.Profile Match(bool removeExact)
    {
        PanelDatabase.Profile match = null;
        int length = 0;
        foreach (var entry in _root)
        {
            if (removeExact && entry.Key == _exactKey) continue;
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length <= length ||
                !ModelName.Contains(entry.Key, StringComparison.OrdinalIgnoreCase)) continue;
            var profile = entry.Value?.Deserialize<PanelDatabase.Profile>();
            if (!PanelDatabase.IsValid(profile)) continue;
            length = entry.Key.Length;
            match = profile;
        }
        return match;
    }

    private PanelDatabase.Profile Select(bool removeExact) => Match(removeExact) ?? PanelDatabase.FallbackFor(_internalPanel);

    public ProfileSaveResult Save(PanelDatabase.Profile profile, bool removeExact = false)
    {
        if ((!removeExact && !PanelDatabase.IsValid(profile)) || (removeExact && !HasExactEntry))
            return ProfileSaveResult.InvalidValues;
        string temporary = null;
        bool ownsMutex = false;
        Mutex mutex = null;
        try
        {
            mutex = new Mutex(false, "Global\\LumenDeck.PanelProfiles." +
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_path.ToUpperInvariant()))));
            try { ownsMutex = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex) return ProfileSaveResult.Busy;
            if (!Unchanged()) return ProfileSaveResult.SourceChanged;
            var updated = (JsonObject)_root.DeepClone();
            string key = _exactKey ?? ModelName;
            if (removeExact) updated.Remove(key);
            else
            {
                if (updated[key] is not JsonObject entry)
                {
                    entry = new JsonObject();
                    updated[key] = entry;
                }
                entry["PeakNits"] = profile.PeakNits;
                entry["FloorNits"] = profile.FloorNits;
                entry["Measured"] = profile.Measured;
                entry["Note"] = profile.Note ?? "";
            }
            string parent = Path.GetDirectoryName(_path);
            string json = updated.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            var profiles = PanelDatabase.ParseProfiles(json);
            Directory.CreateDirectory(parent);
            temporary = Path.Combine(parent, ".panels-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            if (!Unchanged()) return ProfileSaveResult.SourceChanged;
            if (_original == null) File.Move(temporary, _path);
            else
            {
                string backups = Path.Combine(parent, "profile-backups");
                Directory.CreateDirectory(backups);
                File.Replace(temporary, _path, Path.Combine(backups, "panels-" + Guid.NewGuid().ToString("N") + ".json"));
            }
            temporary = null;
            CommittedProfiles = new System.Collections.ObjectModel.ReadOnlyDictionary<string, PanelDatabase.Profile>(profiles);
            return ProfileSaveResult.Saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException
            or WaitHandleCannotBeOpenedException)
        {
            return ProfileSaveResult.Failed;
        }
        finally
        {
            if (temporary != null)
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            if (ownsMutex) mutex.ReleaseMutex();
            mutex?.Dispose();
        }
    }

    private bool Unchanged() => _original == null ? !File.Exists(_path) :
        File.Exists(_path) && File.ReadAllBytes(_path).AsSpan().SequenceEqual(_original);
}
