namespace LumenDeck;

internal enum SettingsSaveState { None, Waiting, Saving, Saved, Busy, Failed, SourceChanged, Unavailable }

/// <summary>Privacy-safe presentation of the window's settings persistence state.</summary>
internal static class SettingsSaveFeedback
{
    public static bool NeedsAttention(SettingsSaveState state) =>
        state is SettingsSaveState.Busy or SettingsSaveState.Failed or SettingsSaveState.SourceChanged or SettingsSaveState.Unavailable;
    public static bool CanRetry(SettingsSaveState state) => state is SettingsSaveState.Waiting or SettingsSaveState.Busy or SettingsSaveState.Failed;
    public static bool RequiresReopen(SettingsSaveState state) => state is SettingsSaveState.SourceChanged or SettingsSaveState.Unavailable;
    public static string Caption(SettingsSaveState state) => state switch
    {
        SettingsSaveState.Waiting => "Changes waiting to save",
        SettingsSaveState.Saving => "Saving settings...",
        SettingsSaveState.Saved => "Active settings saved",
        SettingsSaveState.Busy => "Settings writer busy",
        SettingsSaveState.Failed => "Settings save not confirmed",
        SettingsSaveState.SourceChanged => "Settings changed outside this window",
        SettingsSaveState.Unavailable => "Settings file needs attention",
        _ => "No save result this session",
    };
    public static string Explanation(SettingsSaveState state) => state switch
    {
        SettingsSaveState.Waiting => "Current changes are waiting for a save. Save active settings now or wait for the automatic save.",
        SettingsSaveState.Saving => "A captured snapshot is being saved. Checks show active preferences; wait for completion before retrying or using Exit.",
        SettingsSaveState.Saved => "The current active settings were confirmed saved by this window. This does not confirm monitor hardware changes.",
        SettingsSaveState.Busy => "Another writer was busy. Retry saving current active settings after it finishes.",
        SettingsSaveState.Failed => "A save was not confirmed. Check storage permissions, locks and available space, then retry current active settings. The file may already have changed.",
        SettingsSaveState.SourceChanged => "This window will preserve the changed source. Restore active blackouts, use tray Exit and reopen before saving again. Current adjustments may be session-only.",
        SettingsSaveState.Unavailable => "The source could not be loaded and was preserved. Restore active blackouts and exit; privately back up and repair storage before reopening. Current adjustments may be session-only.",
        _ => "This window has not confirmed a settings save yet; settings loaded from earlier sessions may already be on disk.",
    };
}
