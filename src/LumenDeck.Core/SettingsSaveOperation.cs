namespace LumenDeck;

/// <summary>A captured, one-shot write whose admission ends on its owning thread.</summary>
internal sealed class SettingsSaveOperation
{
    private readonly Func<SettingsSaveResult> _write;
    private readonly Action<SettingsSaveResult> _complete;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private int _phase;
    private bool _completed;
    private SettingsSaveResult _writeResult = SettingsSaveResult.Failed;
    internal bool WasSaved => Volatile.Read(ref _phase) == 2 && _writeResult == SettingsSaveResult.Saved;

    internal SettingsSaveOperation(Func<SettingsSaveResult> write, Action<SettingsSaveResult> complete)
    { _write = write; _complete = complete; }

    internal SettingsSaveResult Write()
    {
        if (Interlocked.CompareExchange(ref _phase, 1, 0) != 0)
            throw new InvalidOperationException("A settings snapshot can only be written once.");
        try { _writeResult = _write(); return _writeResult; }
        finally { Volatile.Write(ref _phase, 2); }
    }

    internal void Complete(SettingsSaveResult result)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Complete settings saves on the capturing thread.");
        if (_completed) return;
        // Abandoning an unstarted operation permanently closes its write entry.
        // Otherwise that old snapshot could run after a newer save completed.
        if (Interlocked.CompareExchange(ref _phase, 3, 0) == 1)
            throw new InvalidOperationException("A running write cannot release its admission.");
        if (result == SettingsSaveResult.Saved && !WasSaved) result = SettingsSaveResult.Failed;
        _completed = true;
        _complete(result);
    }
}
