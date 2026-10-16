namespace LumenDeck.Material;

public sealed record DisplayBounds(int Left, int Top, int Width, int Height);
public sealed record MaterialDisplay(string Key, string Name, string Position, DisplayBounds Bounds,
    bool Primary, bool BrightnessSupported, int BrightnessMin, int BrightnessMax, int Brightness,
    string BrightnessText, string BrightnessStatus, string Estimate, bool ContrastSupported,
    int ContrastMin, int ContrastMax, int Contrast, string ContrastStatus, int Kelvin,
    bool Blanked, string Warning, string Shortcut);
public sealed record MaterialSnapshot(int Generation, string SelectedKey, IReadOnlyList<MaterialDisplay> Displays,
    bool Reading, string Status, bool Warning, bool NightActive, bool NightAvailable, string WorkKey,
    string Finish, string Backdrop, bool SavePending, bool KeepRunning, bool StartMinimised,
    bool? Startup, bool ReapplyWarmth, string SaveStatus, string ScheduleStatus)
{
    public bool PreferencesLocked { get; init; }
    public string DayHelp { get; init; } = "";
    public string EveningHelp { get; init; } = "";
    public string NightHelp { get; init; } = "";
}

public enum MaterialAction
{
    SelectDisplay, Brightness, Contrast, Warmth, PresetDay, PresetEvening, PresetNight, Custom,
    AllDay, AllEvening, AllNight, AllCustom, WarmthOff, Blank, Shortcut, Profile, Advanced,
    Refresh, Identify, NightSaver, WorkDisplay, Preferences, PresetTargets, Schedule,
    ConnectionHelp, KeyboardHelp, About, RetrySave, Startup, StartMinimised, KeepRunning,
    ReapplyWarmth, FinishSmoke, FinishFrost, FinishCeramic, BackdropMica, BackdropGlass,
    BackdropSolid, Classic, Exit, DismissWarning, ComfortGuide,
}

/// <summary>Immutable observations and UI-thread commands; this assembly owns no device or persistence.</summary>
public interface IMaterialHost
{
    MaterialSnapshot Capture();
    void Execute(MaterialAction action, string key, int generation, int value = 0);
    void CloseRequested();
    void MinimiseRequested();
    void VisibilityChanged();
}
