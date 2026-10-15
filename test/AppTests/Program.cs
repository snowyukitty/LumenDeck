using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static int _checks;
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args is ["--desktop-runner-checks"]) return CheckDesktopRunner();
            if (!IsolatedDesktopRunner.EnterWorker(ref args))
            {
                var run = IsolatedDesktopRunner.Run(args, TimeSpan.FromMinutes(3));
                Console.Write(run.Output);
                if (run.TimedOut) Console.Error.WriteLine("FAIL: isolated test worker exceeded its three-minute deadline and was stopped.");
                return run.ExitCode;
            }
            if (args.Length >= 2 && args[0] == "--runner-probe") return RunDesktopProbe(args);
        }
        catch (IsolatedDesktopRunner.CleanupException ex)
        {
            Console.Write(ex.WorkerResult.Output);
            Console.Error.WriteLine($"FAIL: worker exited {ex.WorkerResult.ExitCode}, but private test logs could not be removed.");
            Console.Error.WriteLine(ex.InnerException);
            return 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        // Rebuild diagnostics otherwise use the real app-data directory.
        Environment.SetEnvironmentVariable("LUMENDECK_DIAG", null);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Control.CheckForIllegalCrossThreadCalls = true;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string directory = Path.Combine(Path.GetTempPath(), "LumenDeck-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        int result = 1;
        try
        {
            using var loop = new QuietLoopForm();
            loop.Shown += (_, _) => loop.BeginInvoke((Action)(() =>
            {
                try
                {
                    if (args.Length is 1 or 2 && args[0] == "--screenshot-layout-only")
                    {
                        CheckScreenshotLayout(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} screenshot layout checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--desk-remote-only")
                    {
                        CheckDeskRemote(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} desk-remote checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--map-caption-only")
                    {
                        CheckLayoutMap(args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated desk-map checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args is ["--scenario-cleanup-only"])
                    {
                        CheckScenarioCleanup(directory);
                        Console.WriteLine($"PASS: {_checks} isolated scenario cleanup checks; temporary files only.");
                        result = 0;
                        return;
                    }
                    if (args is ["--night-command-help-only"])
                    {
                        CheckNightCommandHelp(directory);
                        Console.WriteLine($"PASS: {_checks} isolated night-command help checks; synthetic overlay-only displays.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--status-only")
                    {
                        CheckStatusStripe(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated status reading checks; no clipboard or hardware requests.");
                        result = 0;
                        return;
                    }
                    if (args is ["--blackout-feedback-only"])
                    {
                        CheckBlackoutRecoveryFeedback(directory);
                        Console.WriteLine($"PASS: {_checks} isolated blackout recovery feedback checks; fake transport and temporary settings only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--keyboard-help-only")
                    {
                        CheckKeyboardHelp(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated MainForm keyboard-help checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--about-only")
                    {
                        CheckAbout(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated MainForm About checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--support-report-only")
                    {
                        CheckSupportReportLayout(args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated support-report checks; fake clipboard only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--exact-entry-only")
                    {
                        CheckExactEntry(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated exact-entry checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--discovery-only")
                    {
                        CheckDisplayDiscovery(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        CheckDiscoveryTextLayout(args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated display-discovery checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--connection-help-only")
                    {
                        CheckConnectionHelpLifetime(directory);
                        CheckConnectionHelpLayout(args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated connection-help checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--main-window-only")
                    {
                        CheckMainWindowLayout(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated main-window layout checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args is ["--navigation-only"])
                    {
                        CheckMapNavigation(directory);
                        CheckDisplayNavigation(directory);
                        Console.WriteLine($"PASS: {_checks} isolated MainForm display-navigation checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args is ["--night-dim-only"])
                    {
                        CheckNightDimLifecycle(directory);
                        Console.WriteLine($"PASS: {_checks} isolated MainForm night dimming checks; fake transport and synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args.Length is 1 or 2 && args[0] == "--cross-display-dpi-only")
                    {
                        CheckCrossDisplayDpi(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        Console.WriteLine($"PASS: {_checks} isolated cross-display DPI checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args is ["--restore-only"])
                    {
                        CheckRestoreResponsiveness(directory);
                        Console.WriteLine($"PASS: {_checks} isolated MainForm minimise/restore checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    if (args is ["--schedule-only"])
                    {
                        CheckScheduleLifecycle(directory);
                        Console.WriteLine($"PASS: {_checks} isolated MainForm preset schedule checks; fake clock, synthetic displays and temporary settings only.");
                        result = 0;
                        return;
                    }
                    if (args is ["--presets-only"])
                    {
                        CheckPresetTargetLifecycle(directory);
                        Console.WriteLine($"PASS: {_checks} isolated MainForm preset target checks; synthetic displays and temporary settings only.");
                        result = 0;
                        return;
                    }
                    if (args.Length == 1 && args[0] == "--focus-only")
                    {
                        CheckRebuildFocus(directory);
                        Console.WriteLine($"PASS: {_checks} isolated MainForm focus checks; synthetic displays only.");
                        result = 0;
                        return;
                    }
                    CheckDelayedOpen(directory, closeOwner: false);
                    CheckDelayedOpen(directory, closeOwner: true);
                    CheckDelayedOpen(directory, closeOwner: false, reshowOwner: true);
                    CheckDelayedOpen(directory, closeOwner: false, reshowOwner: true, failRead: true);
                    CheckReadFailureRecovery(directory);
                    foreach (string invalidJson in new[] { "[]", "{\"Model\":null,\"Model\":null}", "{\"Model\":null,\"MODEL\":null}", "{\"Model\":42}" })
                        CheckReadFailureRecovery(directory, invalidJson);
                    CheckModalSave(directory);
                    CheckModalFailureCancelReopen(directory);
                    if (args is ["--profile-lifecycle-only"])
                    {
                        Console.WriteLine($"PASS: {_checks} isolated MainForm profile lifecycle checks; synthetic displays and temporary files only.");
                        result = 0;
                        return;
                    }
                    CheckPreferences(directory);
                    CheckBackgroundSettings(directory);
                    CheckPresetTargetLifecycle(directory);
                    CheckWarmthFailure(directory);
                    CheckScheduleLifecycle(directory);
                    CheckCrossDisplayDpi(directory);
                    CheckRestoreResponsiveness(directory);
                    CheckNightDimLifecycle(directory);
                    CheckSettingsRecovery(directory);
                    CheckBlackoutRecoveryFeedback(directory);
                    CheckDisplayDiscovery(directory, args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckDiscoveryTextLayout(args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckSliderAccessibility(directory);
                    CheckExactEntry(directory, args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckRebuildFocus(directory);
                    CheckDisplayNavigation(directory);
                    CheckMapNavigation(directory);
                    CheckLayoutMap(args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckCommandInputOwnership(directory);
                    CheckButtonAccessibility(directory);
                    CheckKeyboardHelp(directory, args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckAbout(directory, args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckSupportReportLayout(args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckConnectionHelpLayout(args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckConnectionHelpLifetime(directory);
                    CheckStatusStripe(directory, args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckNightCommandHelp(directory);
                    CheckScenarioCleanup(directory);
                    CheckMainWindowLayout(directory, args.Length == 2 && args[0] == "--render-dir" ? Path.GetFullPath(args[1]) : null);
                    CheckDeskRemote(directory, null);
                    if (args.Length == 2 && args[0] == "--render-dir") RenderPreferences(Path.GetFullPath(args[1]));
                    Console.WriteLine($"PASS: {_checks} actual MainForm lifecycle checks; synthetic displays and isolated files only.");
                    result = 0;
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); }
                finally { loop.Close(); }
            }));
            // Keep a real outer message loop alive: standalone DoEvents can
            // uninstall its synchronization context between awaited operations.
            Application.Run(loop);
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); result = 1; }
        finally { result = ScenarioCleanup.Complete(directory, result, TimeSpan.FromSeconds(10), Console.Error); }
        return result;
    }

    // Exclude test windows from automatic activation when another dialog closes.
    // Explicit activation remains forbidden; foreground assertions stay enabled.
    private static CreateParams NonActivating(CreateParams parameters)
    {
        parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
        return parameters;
    }

    private sealed class QuietLoopForm : Form
    {
        public QuietLoopForm()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
            ShowInTaskbar = false;
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
    }

    private sealed class QuietMainForm(MainFormHost host) : MainForm(host)
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
        protected override void SetClientSizeCore(int x, int y)
        {
            // Form.ClientSize can cache a request below MinimumSize even when
            // the native outer window rejects it. Test the reachable minimum,
            // not inconsistent managed/native bounds created by that setter.
            var frame = Size - ClientSize;
            base.SetClientSizeCore(Math.Max(x, MinimumSize.Width - frame.Width),
                Math.Max(y, MinimumSize.Height - frame.Height));
        }
    }
    private sealed class QuietConnectionHelp : ConnectionHelpForm
    {
        public int RevealRequests;
        public QuietConnectionHelp()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
        internal override void Reveal() { RevealRequests++; Show(); }
    }
    private sealed class QuietKeyboardHelp : KeyboardHelpForm
    {
        public int RevealRequests;
        public QuietKeyboardHelp()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
        internal override void Reveal() { RevealRequests++; Show(); }
    }
    private sealed class QuietAbout : AboutForm
    {
        public int RevealRequests;
        public QuietAbout(Action<string> copy = null) : base(copy ?? (_ => throw new InvalidOperationException("Unexpected clipboard request")))
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
        internal override void Reveal() { RevealRequests++; Show(); }
    }
    private sealed class QuietEditor : ProfileEditorForm
    {
        public QuietEditor(PanelProfileStore store, ProfileEditorContext context,
            Func<PanelDatabase.Profile, bool, ProfileSaveResult> write) : base(store, context, write)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
    }
    private sealed class QuietPresetEditor : PresetEditorForm
    {
        public QuietPresetEditor(PresetEditorContext context, Func<IReadOnlyList<Presets.Level>, Task<SettingsSaveResult>> save)
            : base(context, save)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
    }
    private sealed class QuietScheduleEditor : PresetScheduleForm
    {
        public QuietScheduleEditor(ScheduleEditorContext context, Func<IReadOnlyCollection<PresetSchedule.Slot>, ScheduleOutlook> outlook,
            Func<ScheduleDraft, Task<SettingsSaveResult>> save)
            : base(context, outlook, save)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
    }
    private sealed class FakeTransport : IDdcTransport
    {
        public int Writes;
        public int Restores;
        public Func<Display, int, bool> OnRestore;
        public Func<Display, DdcWorker.Feature, int, bool> OnWrite;
        private int _value;
        public int LastValue => _value;
        public bool CanWrite(Display monitor) => true;
        public bool Write(Display monitor, DdcWorker.Feature feature, byte code, int value)
        {
            bool written = OnWrite?.Invoke(monitor, feature, value) ?? true;
            if (written) _value = value;
            Interlocked.Increment(ref Writes);
            return written;
        }
        public bool Read(Display monitor, DdcWorker.Feature feature, out int actual)
        { actual = _value; return true; }
        public bool RestoreBrightness(Display monitor, int value)
        {
            Restores++;
            if (OnRestore == null) throw new InvalidOperationException("Unexpected restore");
            bool restored = OnRestore(monitor, value);
            if (restored) _value = value;
            return restored;
        }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly FakeTransport Transport = new();
        public readonly DdcWorker Worker;
        public readonly AppSettings Settings;
        public readonly string ProfilePath;
        public readonly string SettingsPath;
        public bool? StartupState = false;
        public bool RejectStartupWrite, StartupWriteMismatch, StartupReadFailsAfterWrite;
        public int StartupWrites;
        public int Scans, GammaSaves, StartupRepairs, EditorCreations, ProfileErrors;
        public bool RefuseGamma;
        public int GammaWrites;
        public ProfileEditorForm Editor;
        public PresetEditorForm PresetEditor;
        public PresetScheduleForm ScheduleEditor;
        public int PresetEditorCreations;
        public List<Display> LastScan;
        public readonly QuietMainForm Form;
        public int TrayDisposals;
        public bool TrayDisposed => TrayDisposals > 0;
        public int ShortcutRegistrations;
        public int ShortcutRemovals;
        public int KeyboardHelpCreations;
        public int AboutCreations;
        public int ConnectionHelpCreations;
        public bool RefuseShortcutRegistration;
        public Fixture(string directory, Func<ProfileEditorContext, PanelProfileStore> open = null,
            Func<PanelProfileStore, ProfileEditorContext, ProfileEditorForm> create = null,
            bool refuseSettingsSave = false, Func<int, List<Display>> enumerate = null,
            Func<string, AppSettings> settingsFactory = null,
            Func<Func<SettingsSaveResult>, SettingsSaveResult> writeSettings = null, bool waitForCards = true,
            Func<Display, int, bool> onRestore = null, Func<DateTime> now = null, bool allDisplays = true)
        {
            ProfilePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
            string parent = directory;
            if (refuseSettingsSave)
            {
                parent = Path.Combine(directory, "occupied-parent-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(parent, "synthetic directory obstruction");
            }
            SettingsPath = Path.Combine(parent, Guid.NewGuid().ToString("N") + "-settings.json");
            Settings = settingsFactory?.Invoke(SettingsPath) ?? new AppSettings(SettingsPath);
            Settings.MinimiseToTray = false;
            Settings.ReapplyColourOnStart = false;
            Transport.OnRestore = onRestore;
            Worker = new DdcWorker(Transport, 0);
            var host = new MainFormHost
            {
                Settings = Settings, Worker = Worker, ShowTrayIcon = false,
                ApplyGamma = _ => { GammaWrites++; return !RefuseGamma; },
                WriteSettings = writeSettings ?? (write => write()),
                CreateConnectionHelp = () => { ConnectionHelpCreations++; return new QuietConnectionHelp(); },
                CreateKeyboardHelp = () => { KeyboardHelpCreations++; return new QuietKeyboardHelp(); },
                CreateAbout = () => { AboutCreations++; return new QuietAbout(); },
                CreatePresetEditor = (context, save) => { PresetEditorCreations++; return PresetEditor = new QuietPresetEditor(context, save); },
                CreateScheduleEditor = (context, outlook, save) => ScheduleEditor = new QuietScheduleEditor(context, outlook, save),
                // Tests check the schedule explicitly; a real clock would make
                // every other scenario depend on the time it happens to run.
                Now = now ?? (() => new DateTime(2026, 10, 9, 12, 0, 0)), ScheduleCheckMilliseconds = 3_600_000,
                RegisterHotKey = (_, _, _, _) => { ShortcutRegistrations++; return !RefuseShortcutRegistration; },
                UnregisterHotKey = (_, _) => { ShortcutRemovals++; return true; },
                EnumerateMonitors = () =>
                {
                    int scan = Interlocked.Increment(ref Scans);
                    if (enumerate != null) return LastScan = enumerate(scan);
                    return LastScan = [new Display
                    {
                        StableKey = "synthetic-" + scan, Description = "Synthetic lifecycle model",
                        Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29200, Bottom = -29400 },
                        SupportsBrightness = true, Brightness = 50, BrightnessMin = 0, BrightnessMax = 100,
                    }];
                },
                SaveGamma = () => GammaSaves++, RepairStartup = () => StartupRepairs++,
                ReadStartupState = () => StartupState,
                SetStartup = wanted =>
                {
                    StartupWrites++;
                    if (RejectStartupWrite) return false;
                    if (!StartupWriteMismatch) StartupState = wanted;
                    if (StartupReadFailsAfterWrite) StartupState = null;
                    return true;
                },
                OpenProfile = open ?? (context => PanelProfileStore.Open(ProfilePath, context.ModelName, context.InternalPanel)),
                CreateProfileEditor = (store, context) =>
                {
                    EditorCreations++;
                    return Editor = create?.Invoke(store, context) ?? new QuietEditor(store, context, (profile, remove) => store.Save(profile, remove));
                },
                ShowProfileReadError = (_, _) => ProfileErrors++,
            };
            Form = new QuietMainForm(host)
            {
                StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ShowInTaskbar = false,
            };
            Private<NotifyIcon>(Form, "_tray").Disposed += (_, _) => TrayDisposals++;
            Form.Show();
            if (waitForCards) PumpUntil(() => Controls(Form).OfType<MonitorCard>().Any() &&
                !Private<bool>(Form, "_dpiLayoutPending") && !Private<bool>(Form, "_deskLayoutPending"), "initial synthetic scan");
            // Existing lifecycle scenarios exercise the retained all-display workflow.
            // DeskRemoteChecks passes false to cover the production default explicitly.
            if (allDisplays)
            {
                Form.SetAllDisplays(true);
                Private<DeskCommandBar>(Form, "_bar").SetExpanded(true);
            }
        }
        public void Dispose()
        {
            Editor?.Dispose();
            PresetEditor?.Dispose();
            ScheduleEditor?.Dispose();
            SettleSettings(this);
            if (!Form.IsDisposed) Private<NotifyIcon>(Form, "_tray").ContextMenuStrip.Items
                .OfType<ToolStripMenuItem>().Single(i => i.Text == "Exit").PerformClick();
            Form.Dispose();
            Worker.Dispose();
        }
    }

    private static void CheckDelayedOpen(string directory, bool closeOwner, bool reshowOwner = false, bool failRead = false)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int reads = 0;
        using var fixture = new Fixture(directory, context =>
        {
            Interlocked.Increment(ref reads); entered.Set(); release.Wait();
            if (failRead) throw new IOException("synthetic obsolete read failure");
            return PanelProfileStore.Open(Path.Combine(directory, "delayed-" + closeOwner + ".json"), context.ModelName, context.InternalPanel);
        });
        // If suppression regresses, close the obsolete modal from its own
        // message loop so the failure remains observable instead of hanging.
        using var modalWatchdog = new System.Windows.Forms.Timer { Interval = 15 };
        modalWatchdog.Tick += (_, _) => { if (fixture.Editor is { Visible: true } editor) editor.Close(); };
        try
        {
            EditLink(fixture.Form);
            Assert(entered.Wait(TimeSpan.FromSeconds(3)), "delayed profile read reaches barrier");
            EditLink(fixture.Form);
            Assert(reads == 1 && fixture.EditorCreations == 0, "actual MainForm admits one pending profile open");
            bool responsive = false;
            fixture.Form.BeginInvoke((Action)(() => responsive = true));
            Application.DoEvents();
            Assert(responsive, "actual MainForm pumps messages during profile read");
            if (closeOwner) fixture.Form.Close(); else fixture.Form.Hide();
            if (reshowOwner) fixture.Form.Show();
            modalWatchdog.Start();
            release.Set();
            PumpUntil(() => !Private<bool>(fixture.Form, "_profileEditorOpening"), "delayed open completion");
            Assert(fixture.EditorCreations == 0, reshowOwner
                ? "hide/re-show invalidates the obsolete profile opening session"
                : "delayed read cannot create a modal editor after owner hide/close");
            Assert(closeOwner ? fixture.Form.IsDisposed && fixture.TrayDisposed : !fixture.Form.IsDisposed,
                "owner close cleans up, while hide preserves the window");
            Assert(fixture.Transport.Writes == 0, "delayed-open scenario makes no transport writes");
            Assert(fixture.ProfileErrors == 0, "obsolete profile read cannot present an error after its visibility session ends");
        }
        finally { release.Set(); }
    }

    private static void CheckReadFailureRecovery(string directory, string invalidJson = null)
    {
        int reads = 0;
        string path = Path.Combine(directory, "read-recovery-" + Guid.NewGuid().ToString("N") + ".json");
        if (invalidJson != null) File.WriteAllText(path, invalidJson);
        using var fixture = new Fixture(directory, context =>
        {
            if (Interlocked.Increment(ref reads) == 1 && invalidJson == null)
                throw new IOException("synthetic current read failure");
            return PanelProfileStore.Open(path, context.ModelName, context.InternalPanel);
        });
        EditLink(fixture.Form);
        PumpUntil(() => !Private<bool>(fixture.Form, "_profileEditorOpening"), "failed read completion");
        Assert(fixture.ProfileErrors == 1 && fixture.EditorCreations == 0 && IsWindowEnabled(fixture.Form.Handle),
            "current read failure reports once, releases admission and leaves owner enabled");
        if (invalidJson != null)
        {
            Assert(File.ReadAllText(path) == invalidJson, "rejected profile structure remains unchanged after the read error");
            File.WriteAllText(path, "{}"); // Simulate an explicit external repair before reopening.
        }
        using var watchdog = new System.Windows.Forms.Timer { Interval = 15 };
        watchdog.Tick += (_, _) => { if (fixture.Editor is { Visible: true } editor) editor.Close(); };
        watchdog.Start(); EditLink(fixture.Form);
        PumpUntil(() => !Private<bool>(fixture.Form, "_profileEditorOpening"), "read failure retry");
        Assert(reads == 2 && fixture.EditorCreations == 1 && fixture.Editor.IsDisposed && fixture.ProfileErrors == 1,
            "fresh request after read failure can open and cancel a real modal editor");
        Assert(fixture.Transport.Writes == 0, "read failure and retry do not write displays");
    }

    private static void CheckModalSave(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var settingsEntered = new ManualResetEventSlim();
        using var settingsRelease = new ManualResetEventSlim();
        int holdSettings = 0;
        using var fixture = new Fixture(directory, create: (store, context) => new QuietEditor(store, context, (profile, remove) =>
        {
            entered.Set(); release.Wait(); return store.Save(profile, remove);
        }), writeSettings: write =>
        {
            if (Volatile.Read(ref holdSettings) != 0) { settingsEntered.Set(); settingsRelease.Wait(); }
            return write();
        });
        Exception failure = null;
        int stage = 0;
        int gammaBeforeClose = 0;
        int writesBeforeClose = 0;
        MonitorCard originalCard = Controls(fixture.Form).OfType<MonitorCard>().Single();
        var timeout = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Modal lifecycle scenario did not settle");
                var editor = fixture.Editor;
                if (stage == 0 && editor?.Visible == true)
                {
                    Assert(editor.Modal && !IsWindowEnabled(fixture.Form.Handle), "production ShowDialog enters its actual modal loop and disables the owner");
                    SendMessage(fixture.Form.Handle, 0x007E, IntPtr.Zero, IntPtr.Zero);
                    stage = 1;
                }
                else if (stage == 1 && originalCard.IsDisposed && Controls(fixture.Form).OfType<MonitorCard>().Any())
                {
                    Assert(fixture.Scans == 2 && !editor.IsDisposed, "actual display-change rebuild replaces the original card while modal editor survives");
                    var view = Controls(editor).OfType<ProfileEditorView>().Single();
                    Controls(view).OfType<NumericUpDown>().Single(n => n.AccessibleName == "Peak luminance").Value = 255;
                    Controls(view).OfType<FlatButton>().Single(b => b.Text == "Save estimate").PerformClick();
                    stage = 2;
                }
                else if (stage == 2 && entered.IsSet)
                {
                    gammaBeforeClose = fixture.GammaSaves;
                    writesBeforeClose = fixture.Transport.Writes;
                    var exit = Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Exit");
                    exit.PerformClick();
                    Assert(!fixture.Form.IsDisposed && editor.Visible && editor.IsSaving, "actual tray Exit waits for the modal save outcome");
                    Assert(!fixture.TrayDisposed && fixture.GammaSaves == gammaBeforeClose && !Private<bool>(fixture.Form, "_reallyClosing"),
                        "canceled MainForm exit preserves tray/gamma lifecycle and resets exit intent");
                    Assert(fixture.Transport.Writes == writesBeforeClose, "pending profile save and close do not adjust synthetic displays");
                    release.Set();
                    stage = 3;
                }
                else if (stage == 3 && !Private<bool>(fixture.Form, "_profileEditorOpening"))
                {
                    Assert(editor.DialogResult == DialogResult.OK && editor.IsDisposed && IsWindowEnabled(fixture.Form.Handle),
                        "confirmed save ends the actual modal loop and restores the owner");
                    var current = Controls(fixture.Form).OfType<MonitorCard>().Single();
                    Assert(!current.IsDisposed && PanelDatabase.HasUserProfile(current.Monitor),
                        "confirmed profile installs on the rebuilt current card");
                    Assert(Controls(current).OfType<Label>().Any(l => l.Text.Contains("150") && l.Text.Contains("nits")),
                        "current card displays the committed estimate");
                    Assert(fixture.Transport.Writes == 0, "profile open, rebuild, save and install perform no display writes");
                    fixture.Worker.Set(current.Monitor, DdcWorker.Feature.Brightness, 51);
                    stage = 4;
                }
                else if (stage == 4 && fixture.Transport.Writes == 1)
                {
                    Assert(!fixture.TrayDisposed, "preserved worker accepts a subsequent fake-transport adjustment");
                    // The rebuild's delayed save can still be running here on a
                    // slow host. Force that interleaving instead of assuming the
                    // transport write also proves settings have settled.
                    Volatile.Write(ref holdSettings, 1);
                    RequestSettings(fixture);
                    stage = 5;
                }
                else if (stage == 5 && settingsEntered.IsSet)
                {
                    int savesBeforePendingClose = fixture.GammaSaves;
                    fixture.Form.Close();
                    Assert(!fixture.Form.IsDisposed && !fixture.TrayDisposed && !Private<bool>(fixture.Form, "_reallyClosing"),
                        "post-modal owner close waits for an independently pending settings save");
                    Assert(fixture.GammaSaves == savesBeforePendingClose,
                        "pending settings close preserves gamma cleanup until save completion");
                    settingsRelease.Set();
                    stage = 6;
                }
                else if (stage == 6 && !Private<bool>(fixture.Form, "_settingsWriteRunning") && !fixture.Settings.IsSavePending)
                {
                    Assert(fixture.Settings.LastSaveResult == SettingsSaveResult.Saved,
                        "post-modal settings save settles successfully before final owner close");
                    int savesBeforeFinalClose = fixture.GammaSaves;
                    fixture.Form.Close();
                    Assert(fixture.Form.IsDisposed && fixture.TrayDisposed && fixture.GammaSaves > gammaBeforeClose,
                        "settled ordinary owner close performs real MainForm cleanup");
                    var writerThread = (Thread)typeof(DdcWorker).GetField("_thread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.Worker);
                    Assert(!writerThread.IsAlive && fixture.TrayDisposals == 1 && fixture.GammaSaves == savesBeforeFinalClose + 1,
                        "accepted close terminates the owned writer and runs tray/gamma cleanup exactly once");
                    fixture.Form.Dispose();
                    Assert(fixture.TrayDisposals == 1 && fixture.GammaSaves == savesBeforeFinalClose + 1,
                        "repeated form disposal cannot repeat accepted-close cleanup");
                    timer.Stop(); stage = 7;
                }
            }
            catch (Exception ex)
            {
                failure = ex; release.Set(); settingsRelease.Set(); timer.Stop(); stage = 7;
                if (fixture.Editor is { IsDisposed: false } failedEditor)
                {
                    failedEditor.DialogResult = DialogResult.Cancel;
                    failedEditor.Dispose();
                }
            }
        };
        try
        {
            timer.Start(); EditLink(fixture.Form);
            try { PumpUntil(() => stage == 7, "modal scenario", 20000); }
            catch
            {
                Console.Error.WriteLine($"Modal state: stage={stage}, ownerVisible={fixture.Form.Visible}, opening={Private<bool>(fixture.Form, "_profileEditorOpening")}, editors={fixture.EditorCreations}, editorVisible={fixture.Editor?.Visible}, timer={timer.Enabled}");
                throw;
            }
            if (failure != null) throw new InvalidOperationException("Modal lifecycle assertion failed", failure);
        }
        finally { release.Set(); settingsRelease.Set(); }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr hwnd);

    private static void CheckModalFailureCancelReopen(string directory)
    {
        int attempts = 0;
        using var fixture = new Fixture(directory, create: (store, context) => new QuietEditor(store, context, (profile, remove) =>
            Interlocked.Increment(ref attempts) == 1 ? ProfileSaveResult.Failed : store.Save(profile, remove)));
        Exception failure = null;
        int stage = 0;
        ProfileEditorForm firstEditor = null;
        var timeout = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(12)) throw new TimeoutException("Failure/reopen modal scenario did not settle");
                var editor = fixture.Editor;
                if (stage == 0 && editor?.Visible == true)
                {
                    firstEditor = editor;
                    var view = Controls(editor).OfType<ProfileEditorView>().Single();
                    Controls(view).OfType<NumericUpDown>().Single(n => n.AccessibleName == "Peak luminance").Value = 400;
                    Controls(view).OfType<FlatButton>().Single(b => b.Text == "Save estimate").PerformClick();
                    stage = 1;
                }
                else if (stage == 1 && attempts == 1 && !editor.IsSaving)
                {
                    var view = Controls(editor).OfType<ProfileEditorView>().Single();
                    Assert(editor.Modal && editor.DialogResult == DialogResult.None && !IsWindowEnabled(fixture.Form.Handle),
                        "unconfirmed save leaves the actual modal loop open without successful completion");
                    Assert(Controls(view).OfType<NumericUpDown>().Single(n => n.AccessibleName == "Peak luminance").Value == 400 &&
                        Controls(view).OfType<Label>().Any(l => l.Text.Contains("Save was not confirmed")),
                        "actual modal failure retains the edited draft and explains the outcome");
                    Assert(!File.Exists(fixture.ProfilePath), "injected precommit failure creates no profile file");
                    Controls(view).OfType<FlatButton>().Single(b => b.Text == "Cancel").PerformClick();
                    stage = 2;
                }
                else if (stage == 2 && !Private<bool>(fixture.Form, "_profileEditorOpening"))
                {
                    Assert(firstEditor.IsDisposed && firstEditor.DialogResult != DialogResult.OK && IsWindowEnabled(fixture.Form.Handle),
                        "Cancel returns from the real modal loop without saving and re-enables the owner");
                    EditLink(fixture.Form); stage = 3;
                }
                else if (stage == 3 && fixture.EditorCreations == 2 && editor.Visible)
                {
                    var view = Controls(editor).OfType<ProfileEditorView>().Single();
                    Assert(!ReferenceEquals(firstEditor, editor) &&
                        Controls(view).OfType<NumericUpDown>().Single(n => n.AccessibleName == "Peak luminance").Value == 300,
                        "reopen admits a fresh editor from disk rather than the canceled draft");
                    Controls(view).OfType<FlatButton>().Single(b => b.Text == "Save estimate").PerformClick();
                    stage = 4;
                }
                else if (stage == 4 && !Private<bool>(fixture.Form, "_profileEditorOpening"))
                {
                    Assert(attempts == 2 && editor.DialogResult == DialogResult.OK && File.Exists(fixture.ProfilePath),
                        "reopened modal editor can confirm a real temporary-file save");
                    Assert(fixture.Transport.Writes == 0, "failure, cancel and reopen leave display transport untouched");
                    timer.Stop(); stage = 5;
                }
            }
            catch (Exception ex)
            {
                failure = ex; timer.Stop(); stage = 5;
                fixture.Editor?.Dispose();
            }
        };
        timer.Start(); EditLink(fixture.Form);
        PumpUntil(() => stage == 5, "failure/cancel/reopen scenario", 15000);
        if (failure != null) throw new InvalidOperationException("Failure/cancel/reopen assertion failed", failure);
    }
    private static T Private<T>(object target, string name) => (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static IEnumerable<Control> Controls(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Controls(child)) yield return nested; }
    }
    private static void EditLink(MainForm form)
    {
        var candidates = Controls(form).OfType<LinkLabel>().ToArray();
        if (!candidates.Any(l => l.Name == "brightness-profile"))
            throw new InvalidOperationException("Profile link missing; scan=" + Private<DisplayScanState>(form, "_scanState") +
                "; links=" + string.Join(" | ", candidates.Select(l => l.Text)));
        var link = candidates.Single(l => l.Name == "brightness-profile");
        typeof(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(link, [new LinkLabelLinkClickedEventArgs(link.Links[0])]);
    }
    private static void PumpUntil(Func<bool> predicate, string name, int milliseconds = 5000)
    {
        var timeout = Stopwatch.StartNew();
        while (!predicate() && timeout.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
        Assert(predicate(), name + " completes within the test deadline");
    }
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        _checks++; Console.WriteLine("PASS: " + name);
    }
}
