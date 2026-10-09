using System.Security.Cryptography;
using System.Text;

namespace LumenDeck;

internal enum SettingsSaveResult { Saved, Busy, SourceChanged, Unavailable, Failed }
internal enum SettingsWriteStage { Staged, Committed }

/// <summary>Validate a loaded source and commit captured bytes under a shared writer lock.</summary>
internal sealed class SettingsFileStore
{
    private readonly string _path;
    private byte[] _original;
    private readonly Action<SettingsWriteStage> _observe;

    internal SettingsFileStore(string path, byte[] original, Action<SettingsWriteStage> observe = null)
    {
        _path = Path.GetFullPath(path);
        _original = original?.ToArray();
        _observe = observe;
    }

    internal string MutexName => "Global\\LumenDeck.Settings." +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_path.ToUpperInvariant())));

    internal SettingsSaveResult Save(byte[] serialized)
    {
        // Own the payload; neither a caller nor its mutable settings object may
        // change bytes while the writer is staging them.
        byte[] payload = serialized.ToArray();
        string temporary = null;
        bool ownsMutex = false;
        Mutex mutex = null;
        try
        {
            mutex = new Mutex(false, MutexName);
            try { ownsMutex = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex) return SettingsSaveResult.Busy;
            if (!Unchanged()) return SettingsSaveResult.SourceChanged;
            string parent = Path.GetDirectoryName(_path);
            Directory.CreateDirectory(parent);
            temporary = Path.Combine(parent, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(payload);
                output.Flush(flushToDisk: true);
            }
            _observe?.Invoke(SettingsWriteStage.Staged);
            if (!Unchanged()) return SettingsSaveResult.SourceChanged;
            if (_original == null) File.Move(temporary, _path);
            else File.Replace(temporary, _path, null);
            temporary = null;
            _original = payload;
            _observe?.Invoke(SettingsWriteStage.Committed);
            return SettingsSaveResult.Saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return SettingsSaveResult.Failed;
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
