namespace LumenDeck;

/// <summary>External collaborators for the desktop window; the window owns Worker.</summary>
internal sealed class MainFormHost
{
    public required AppSettings Settings { get; init; }
    public required DdcWorker Worker { get; init; }
    public required Func<List<Monitor>> EnumerateMonitors { get; init; }
    public required Action SaveGamma { get; init; }
    public Func<Monitor, bool> ApplyGamma { get; init; } = DisplayGamma.Apply;
    public required Action RepairStartup { get; init; }
    public required Func<bool?> ReadStartupState { get; init; }
    public required Func<bool, bool> SetStartup { get; init; }
    public required Func<ProfileEditorContext, PanelProfileStore> OpenProfile { get; init; }
    public required Func<PanelProfileStore, ProfileEditorContext, ProfileEditorForm> CreateProfileEditor { get; init; }
    public required Action<IWin32Window, string> ShowProfileReadError { get; init; }
    public bool ShowTrayIcon { get; init; } = true;
    public bool EnableMaterialInterface { get; init; }
    public Func<Func<SettingsSaveResult>, SettingsSaveResult> WriteSettings { get; init; } = write => write();
    public Func<IntPtr, int, uint, uint, bool> RegisterHotKey { get; init; } = Native.RegisterHotKey;
    public Func<IntPtr, int, bool> UnregisterHotKey { get; init; } = Native.UnregisterHotKey;
    public Func<ConnectionHelpForm> CreateConnectionHelp { get; init; } = () => new ConnectionHelpForm();
    public Func<KeyboardHelpForm> CreateKeyboardHelp { get; init; } = () => new KeyboardHelpForm();
    public Func<AboutForm> CreateAbout { get; init; } = () => new AboutForm();
    public Func<PresetEditorContext, Func<IReadOnlyList<Presets.Level>, Task<SettingsSaveResult>>, PresetEditorForm> CreatePresetEditor { get; init; } =
        (context, save) => new PresetEditorForm(context, save);
    public Func<ScheduleEditorContext, Func<IReadOnlyCollection<PresetSchedule.Slot>, ScheduleOutlook>,
        Func<ScheduleDraft, Task<SettingsSaveResult>>, PresetScheduleForm> CreateScheduleEditor { get; init; } =
        (context, outlook, save) => new PresetScheduleForm(context, outlook, save);
    /// <summary>Local wall-clock time for the preset schedule.</summary>
    public Func<DateTime> Now { get; init; } = () => DateTime.Now;
    /// <summary>How often the schedule is checked; tests drive it directly.</summary>
    public int ScheduleCheckMilliseconds { get; init; } = 20_000;

    public static MainFormHost CreateDefault() => new()
    {
        Settings = AppSettings.Load(), Worker = new DdcWorker(),
        EnableMaterialInterface = true,
        EnumerateMonitors = () => MonitorService.Enumerate(), SaveGamma = DisplayGamma.Save,
        RepairStartup = StartupEntry.RepairIfStale, ReadStartupState = StartupEntry.ReadState,
        SetStartup = StartupEntry.Set,
        OpenProfile = context => PanelProfileStore.Open(PanelDatabase.UserFilePath, context.ModelName, context.InternalPanel),
        CreateProfileEditor = (store, context) => new ProfileEditorForm(store, context),
        ShowProfileReadError = (owner, message) => MessageBox.Show(owner, message,
            "Brightness estimate", MessageBoxButtons.OK, MessageBoxIcon.Information),
    };
}
