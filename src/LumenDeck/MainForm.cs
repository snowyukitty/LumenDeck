namespace LumenDeck;

internal partial class MainForm : Form
{
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_HOTKEY = 0x0312;
    private const int WM_TIMECHANGE = 0x001E;
    private const int WM_POWERBROADCAST = 0x0218;
    private static readonly IntPtr PBT_APMRESUMEAUTOMATIC = 0x12;
    private const int FirstScreenBlankHotkeyId = 0x4C00;

    private readonly DdcWorker _worker;
    private readonly MainFormHost _host;
    private readonly AppSettings _settings;
    private readonly ScreenBlanker _blanker;

    private List<Monitor> _monitors = new();
    private readonly List<MonitorCard> _cards = new();

    private readonly FlowLayoutPanel _list;
    private readonly LayoutMap _map;
    private string _selectedMonitorKey;
    private bool _showAllDisplays;
    private readonly StatusStripe _status;
    private readonly NotifyIcon _tray;
    private readonly Icon _appIcon;
    private readonly Icon _trayIcon;
    private readonly System.Windows.Forms.Timer _saveTimer;
    private readonly System.Windows.Forms.Timer _displayChangeTimer;
    private readonly FlatButton _refreshButton;
    private readonly DeskCommandBar _bar;
    private readonly BrandHeader _header;
    private readonly FlatButton _nightButton;
    private readonly ComboBox _workMonitor;
    private readonly ToolStripMenuItem _nightTrayItem;
    private readonly NightSaver _nightSaver;
    private ConnectionHelpForm _connectionHelp;
    private KeyboardHelpForm _keyboardHelp;
    private AboutForm _about;
    private readonly PreferencesMenu _preferencesMenu;
    private readonly ToolStripMenuItem _startupPreference;
    private readonly ToolStripMenuItem _minimisedPreference;
    private ProfileEditorForm _profileEditor;
    private bool _profileEditorOpening;
    private PresetEditorForm _presetEditor;
    private DisplayScanState _scanState = DisplayScanState.NotStarted;

    /// <summary>Mode buttons by name, so the one in force can be shown as such.</summary>
    private readonly Dictionary<string, FlatButton> _modeButtons;

    /// <summary>Registered global-hotkey id to the card it controls.</summary>
    private readonly Dictionary<int, MonitorCard> _screenBlankHotkeys = new();

    /// <summary>Bumped per rebuild so a slow enumeration cannot overwrite a newer one.</summary>
    private int _generation;
    private int _visibilityGeneration;
    private bool _reallyClosing;
    private int _revealPending;
    private bool _scalingDpi;
    private bool _layingOutCards;
    private bool _dpiLayoutPending;
    private List<Control> _dpiSuspendedControls;

    /// <summary>
    /// Set once the window has actually been shown. Guards the minimise-to-tray
    /// path in <see cref="OnResize"/>, which must not run before then - see the
    /// comment on that guard.
    /// </summary>
    private bool _shown;

    public MainForm() : this(MainFormHost.CreateDefault()) { }

    internal MainForm(MainFormHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _settings = host.Settings;
        _worker = host.Worker;
        _blanker = new ScreenBlanker(_settings, _worker, OnScreenBlankStateChanged);
        _nightSaver = new NightSaver(_blanker, _settings, _worker);

        // The version a bug report asks for, visible without opening About.
        Text = $"LumenDeck {AboutForm.VersionText}";
        BackColor = Theme.Base;
        ForeColor = Theme.Ink;
        Font = Theme.Body;
        // AutoScaleMode defaults to None, which leaves the whole layout in raw
        // device pixels under PerMonitorV2. Dpi is what the DPI-aware path wants.
        AutoScaleMode = AutoScaleMode.Dpi;

        MinimumSize = new Size(620, 480);
        Size = new Size(940, 820);
        StartPosition = FormStartPosition.CenterScreen;

        // Two instances, because they are different frames: the window wants
        // the full multi-size icon, the tray specifically wants the hinted 16px
        // one. WinForms does not take ownership of either, so both are fields
        // and both get disposed.
        _appIcon = AppIcon.Load();
        _trayIcon = AppIcon.LoadTray();
        Icon = _appIcon;

        // Presentation is separate from hardware actions so command growth and
        // DPI layout can be checked without enumerating or changing a monitor.
        _bar = new DeskCommandBar();
        _modeButtons = _bar.ModeButtons;
        _refreshButton = _bar.RefreshButton;
        _nightButton = _bar.NightButton;
        _workMonitor = _bar.WorkMonitor;
        _workMonitor.DisplayMember = nameof(WorkScreenChoice.Label);
        _bar.LevelPicked += ApplyLevel;
        _bar.CustomPicked += ApplyCustom;
        _bar.IdentifyButton.Click += (_, _) => IdentifyOverlay.Show(_monitors);
        _refreshButton.Click += (_, _) => _ = RebuildAsync();
        _bar.WarmthOffButton.Click += (_, _) => WarmthOff();
        _bar.HelpButton.Click += (_, _) => ShowConnectionHelp();
        _preferencesMenu = new PreferencesMenu();
        _preferencesMenu.ChangeRequested += ChangePreference;
        _preferencesMenu.RetrySaveRequested += RetrySettingsSave;
        _preferencesMenu.KeyboardHelpRequested += ShowKeyboardHelp;
        _preferencesMenu.AboutRequested += ShowAbout;
        _preferencesMenu.PresetTargetsRequested += ShowPresetEditor;
        _preferencesMenu.ScheduleRequested += ShowScheduleEditor;
        _bar.SetPresetHelp(_settings.PresetLevels);
        _preferencesMenu.Opening += (_, _) => SynchronizePreferences();
        _bar.SettingsButton.Click += (_, _) =>
        {
            _preferencesMenu.Show(_bar.SettingsButton, new Point(0, _bar.SettingsButton.Height));
        };
        _nightButton.Click += (_, _) => ToggleNightSaver();
        _workMonitor.SelectionChangeCommitted += (_, _) =>
        {
            if (_workMonitor.SelectedItem is WorkScreenChoice choice)
            {
                _settings.NightWorkMonitorKey = choice.Key;
                RequestSettingsSave();
            }
        };

        // ---- desk map ------------------------------------------------------
        _map = new LayoutMap { Dock = DockStyle.Top };
        _map.MonitorPicked += PickDisplay;
        _map.AllDisplaysChanged += show => { _showAllDisplays = show; ApplyDisplayView(); };

        // ---- cards ---------------------------------------------------------
        _list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(14, 12, 14, 12),
            BackColor = Theme.Base,
        };

        _status = new StatusStripe { Dock = DockStyle.Bottom };

        // A vertical scrollbar appearing shrinks _list.ClientSize but does not
        // resize the form, so nothing recomputed the card width and earlier
        // cards stayed wider than later ones. Watch the panel's own client size.
        _list.ClientSizeChanged += (_, _) => LayoutCards();

        Controls.Add(_list);
        Controls.Add(_status);
        Controls.Add(_map);
        Controls.Add(_bar);
        _header = new BrandHeader(_appIcon, AboutForm.VersionText);
        Controls.Add(_header);
        _bar.SetExpanded(false);
        _bar.SizeChanged += (_, _) => UpdateDeskMapVisibility();
        UpdateDeskMapVisibility();

        // ---- tray ----------------------------------------------------------
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add("Show LumenDeck", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        foreach (var level in Presets.Levels)
        {
            string name = level.Name;
            _displayTrayCommands.Add(menu.Items.Add(name, null, (_, _) => ApplyLevel(name)));
        }
        // The tray is where a preset is most likely to be hit by accident, so
        // the way back has to be here too.
        _displayTrayCommands.Add(menu.Items.Add(Presets.CustomName, null, (_, _) => ApplyCustom()));
        _nightTrayItem = new ToolStripMenuItem("Night saver", null, (_, _) => ToggleNightSaver());
        menu.Items.Add(_nightTrayItem);
        menu.Items.Add(new ToolStripSeparator());
        _displayTrayCommands.Add(menu.Items.Add("Warmth off", null, (_, _) => WarmthOff()));

        // If the executable moved since this was switched on, the entry points
        // at a copy that may no longer exist - so fix it before showing its
        // state, rather than reporting "on" for something that cannot run.
        _host.RepairStartup();
        _startupPreference = new ToolStripMenuItem("Start with Windows") { CheckOnClick = false };
        _startupPreference.Click += (_, _) => ChangePreference(AppPreference.WindowsStartup, !_startupPreference.Checked);
        _minimisedPreference = new ToolStripMenuItem("Start minimised") { CheckOnClick = false };
        _minimisedPreference.Click += (_, _) => ChangePreference(AppPreference.StartMinimised, !_minimisedPreference.Checked);
        menu.Items.Add(_startupPreference);
        menu.Items.Add(_minimisedPreference);
        SynchronizePreferences();
        menu.Opening += (_, _) => SynchronizePreferences();

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _reallyClosing = true; Close(); });

        _tray = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = Text,
            Visible = _host.ShowTrayIcon,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => RestoreFromTray();

        // Settings are written on a delay so dragging a slider does not hammer
        // the disk with a write per pixel.
        _saveTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            RequestSettingsSave();

            // The gamma baselines ride the same timer. They only have to be
            // current before the process ends - but if it ends unexpectedly, a
            // stale record is what makes the next session unable to tell its own
            // ramp from the display's.
            _host.SaveGamma();
        };

        // Windows sends WM_DISPLAYCHANGE several times for one physical change,
        // and a rebuild costs a full DDC enumeration, so they are coalesced.
        _displayChangeTimer = new System.Windows.Forms.Timer { Interval = 900 };
        _displayChangeTimer.Tick += (_, _) =>
        {
            _displayChangeTimer.Stop();
            _ = RebuildAsync("Display configuration changed.");
        };

        _worker.WriteFailed += OnWriteFailed;
        _scheduleTimer = new System.Windows.Forms.Timer { Interval = _host.ScheduleCheckMilliseconds };
        _scheduleTimer.Tick += (_, _) => CheckSchedule();
        _scheduleTimer.Start();
        UpdateScheduleSummary();

        SetStatus("Reading monitors over DDC/CI...");
        // StartMinimised is applied in OnShown, not here. Setting WindowState in
        // the constructor is what made the option crash the process - see there.
    }

    /// <summary>
    /// Enumeration starts here rather than in the constructor so the window is
    /// on screen first. Doing it inline meant nothing appeared for nearly seven
    /// seconds after launch - measured, not guessed - which reads as a hung app.
    /// It also left a window with no handle during the slowest part of startup,
    /// so a display change in that gap was missed entirely.
    /// </summary>
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _shown = true;

        // "Start minimised" happens here rather than in the constructor, and the
        // difference is not cosmetic: it used to kill the process outright.
        //
        // Assigning WindowState before the window has ever been shown means the
        // first WM_SIZE arrives already minimised. OnResize reacted by hiding to
        // the tray, and part of that was then assigning ShowInTaskbar - which
        // forces WinForms to recreate the window handle, which resizes the form,
        // which re-enters OnResize with the state still Minimized. (The tray
        // path no longer touches ShowInTaskbar; see RevealWindow.) Round it went until
        // the stack is gone: exit code 0xC00000FD, STACK_OVERFLOW. There is no
        // window, no tray icon and no error.log, because a StackOverflowException
        // cannot be caught - the app simply is not there after login, which is
        // the one moment nobody is watching it start.
        //
        // Minimising from here takes the ordinary path, the same one a click on
        // the minimise box has always taken. The window is briefly visible; that
        // is the honest price and it beats not starting.
        if (_settings.StartMinimised) WindowState = FormWindowState.Minimized;

        await RebuildAsync();
    }

    // ---------------------------------------------------------------- rebuild

    /// <summary>
    /// Card width as if the vertical scrollbar were always shown. Otherwise the
    /// scrollbar appearing as cards are added changes the width, and every card
    /// is measured again at the new width - roughly half of a rebuild's time.
    /// </summary>
    private int CardWidth
    {
        get
        {
            int scrollbar = SystemInformation.VerticalScrollBarWidth;
            int width = _list.ClientSize.Width + (_list.VerticalScroll.Visible ? scrollbar : 0) - scrollbar;
            return Math.Max(320, width - _list.Padding.Horizontal - 4);
        }
    }

    private bool _updatingDeskLayout;
    private void UpdateDeskMapVisibility()
    {
        if (_updatingDeskLayout) return;
        if (_scalingDpi || _dpiLayoutPending || _map == null || _bar == null || _status == null || _header == null || WindowState == FormWindowState.Minimized) return;
        _updatingDeskLayout = true;
        try
        {
            bool compact = ClientSize.Height < LogicalToDeviceUnits(600);
            _bar.SetHeightLimit(compact ? LogicalToDeviceUnits(110) : 0);
            _header.Visible = !compact;
            _map.Visible = CanAdjustDisplays;
            bool stacked = _monitors.Count > 1 && _monitors.Select(m => m.Rect.Left).Distinct().Count() == 1;
            bool side = stacked && ClientSize.Width >= LogicalToDeviceUnits(900) && !compact;
            _map.Dock = side ? DockStyle.Left : DockStyle.Top;
            if (side) _map.Width = LogicalToDeviceUnits(220);
            else _map.Height = LogicalToDeviceUnits(compact ? 112 : 140);
        }
        finally { _updatingDeskLayout = false; }
    }

    private void ShowConnectionHelp()
    {
        if (_reallyClosing || IsDisposed || Disposing || !Enabled || !Visible || WindowState == FormWindowState.Minimized) return;
        _scanFocus?.CancelRecovery();
        if (_connectionHelp == null || _connectionHelp.IsDisposed)
        {
            _connectionHelp = _host.CreateConnectionHelp();
            _connectionHelp.SetMonitors(_monitors, _scanState);
            _connectionHelp.Show(this);
        }
        else _connectionHelp.Reveal();
    }

    private void SynchronizePreferences()
    {
        bool? startup = _host.ReadStartupState();
        _startupPreference.Checked = startup == true;
        _startupPreference.Enabled = startup.HasValue;
        _startupPreference.Text = startup.HasValue ? "Start with Windows" : "Start with Windows (unavailable)";
        _startupPreference.AccessibleDescription = _startupPreference.ToolTipText = startup.HasValue
            ? "Register LumenDeck to start at Windows sign-in for this account."
            : "Windows startup registration could not be read. This option is unavailable, not confirmed disabled.";
        _minimisedPreference.Checked = _settings.StartMinimised;
        _minimisedPreference.Enabled = !_settingsWriteRunning && !SettingsSaveFeedback.RequiresReopen(CurrentSettingsSaveState());
        _minimisedPreference.AccessibleDescription = _minimisedPreference.ToolTipText = _settings.MinimiseToTray
            ? "The next launch starts in the notification area."
            : "The next launch starts minimised on the taskbar.";
        if (!_minimisedPreference.Enabled)
            _minimisedPreference.AccessibleDescription = _minimisedPreference.ToolTipText =
                SettingsSaveFeedback.Explanation(CurrentSettingsSaveState());
        _preferencesMenu.Synchronize(new PreferenceState(startup, _settings.StartMinimised,
            _settings.MinimiseToTray, _settings.ReapplyColourOnStart, _settings.UseMicaTitleBar), _settingsWriteRunning);
        UpdateSettingsSaveFeedback();
    }

    private void ChangePreference(AppPreference preference, bool wanted)
    {
        if (_reallyClosing || IsDisposed || Disposing) return;
        if (preference == AppPreference.WindowsStartup)
        {
            if (!_host.ReadStartupState().HasValue)
            {
                SetStatus("Windows startup status is unavailable. No registration change was requested.", StatusKind.Warn);
                SynchronizePreferences();
                return;
            }
            bool saved = _host.SetStartup(wanted);
            bool? actual = _host.ReadStartupState();
            SetStatus(saved && actual == wanted
                ? (actual == true ? "Windows startup registration is enabled." : "Windows startup registration is disabled.")
                : "Startup change not confirmed. The check shows the current registration.",
                saved && actual == wanted ? StatusKind.Info : StatusKind.Warn);
        }
        else
        {
            if (_settingsWriteRunning || SettingsSaveFeedback.RequiresReopen(CurrentSettingsSaveState())) return;
            _ = SaveSettingsAsync(preference, wanted);
        }
        SynchronizePreferences();
    }

    private void SetPreference(AppPreference preference, bool value)
    {
        if (preference == AppPreference.StartMinimised) _settings.StartMinimised = value;
        else if (preference == AppPreference.KeepRunning) _settings.MinimiseToTray = value;
        else if (preference == AppPreference.ReapplyWarmth) _settings.ReapplyColourOnStart = value;
        else if (preference == AppPreference.MicaTitleBar) _settings.UseMicaTitleBar = value;
        else throw new ArgumentOutOfRangeException(nameof(preference));
    }

    private string SettingsFailureAdvice() => _settings.LastSaveResult switch
    {
        SettingsSaveResult.SourceChanged => "Settings changed. Quit and reopen before saving again.",
        SettingsSaveResult.Unavailable => "Settings file unavailable; it was preserved. Check storage.",
        SettingsSaveResult.Busy => "Another settings writer is busy. Retry after it finishes.",
        _ => "Check settings storage and retry.",
    };

    private bool SaveSettingsOrWarn()
    {
        bool saved = _settings.Save();
        RecordSettingsSave(_settings.LastSaveResult, _settingsSaveRevision);
        if (saved) return true;
        SetStatus(SettingsFailureAdvice(), StatusKind.Warn);
        return false;
    }

    private async void ShowProfileEditor(MonitorCard card)
    {
        if (_reallyClosing || IsDisposed || Disposing || !Visible || WindowState == FormWindowState.Minimized ||
            card.IsDisposed || !_cards.Contains(card) || _profileEditorOpening) return;
        _profileEditorOpening = true;
        int visibilityGeneration = _visibilityGeneration;
        try
        {
            var monitor = card.Monitor;
            // Capture values only: a display-change rebuild can dispose this card
            // and Monitor while the modal editor's message loop is running.
            var context = new ProfileEditorContext(monitor.FriendlyName, monitor.IsInternalPanel,
                monitor.SupportsBrightness ? Presets.RawToPercent(monitor, monitor.Brightness) : null,
                monitor.DisplayName, _settings.PresetLevels);
            PanelProfileStore store;
            SetStatus("Opening brightness estimate profile…");
            try { store = await Task.Run(() => _host.OpenProfile(context)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
            {
                if (visibilityGeneration == _visibilityGeneration && !_reallyClosing && !IsDisposed && !Disposing &&
                    Visible && WindowState != FormWindowState.Minimized)
                {
                    SetStatus("Could not open the brightness estimate profile.", StatusKind.Warn);
                    _host.ShowProfileReadError(this,
                        "Could not read the profile file safely. Review panels.json for invalid or duplicate entries, or check folder permissions. Nothing was changed.");
                }
                return;
            }
            if (visibilityGeneration != _visibilityGeneration || _reallyClosing || IsDisposed || Disposing ||
                !Visible || WindowState == FormWindowState.Minimized) return;
            using var editor = _host.CreateProfileEditor(store, context);
            _profileEditor = editor;
            if (editor.ShowDialog(this) != DialogResult.OK || _reallyClosing || IsDisposed)
            {
                if (!_reallyClosing && !IsDisposed) SetStatus("Brightness estimate editor closed.");
                return;
            }
            // Install the confirmed snapshot on the UI thread. A disk reread here
            // would block the UI again and could select an external edit instead.
            PanelDatabase.InstallProfiles(store.CommittedProfiles);
            foreach (var current in _cards.Where(c => !c.IsDisposed)) current.SyncFromMonitor();
            SetStatus("Brightness estimate profile updated. Current screen settings were not adjusted; future presets use the updated model.");
        }
        finally
        {
            _profileEditor = null;
            _profileEditorOpening = false;
        }
    }

    private async Task RebuildAsync(string reason = null)
    {
        if (_reallyClosing || IsDisposed || Disposing) return;
        var focus = BeginScanFocus();
        int gen = ++_generation;
        _refreshButton.Enabled = false;
        _scanState = DisplayScanState.Reading;
        if (_connectionHelp is { IsDisposed: false }) _connectionHelp.SetMonitors(_monitors, _scanState);
        UpdateNightSaver();
        SetStatus(reason == null ? "Reading monitors over DDC/CI..." : reason + " Re-reading monitors...");

        try
        {
            // Order matters. Drop queued writes before disposing monitor
            // handles. Monitor.Dispose shares that monitor's own I/O lock with
            // the writer, so it waits for an in-flight request without making a
            // slow display block every other display.
            _worker.ClearPending();
            UnregisterScreenBlankHotkeys();
            foreach (var m in _monitors) m.Dispose();
            _monitors.Clear();

            // Controls.Clear() removes without disposing, so every font, label
            // and window handle in the old cards would be leaked - once per
            // Refresh click and once per display change.
            foreach (var c in _cards) c.Dispose();
            _cards.Clear();
            _scanView?.Dispose();
            _scanView = null;
            _list.Controls.Clear();
            _map.SetMonitors([]);
            UpdateDisplayScanPresentation();

            focus?.Park();
            var found = await Task.Run(_host.EnumerateMonitors);

            if (IsDisposed || gen != _generation)
            {
                foreach (var m in found) m.Dispose();
                return;
            }

            _monitors = found.OrderBy(m => m.Rect.Left).ThenBy(m => m.Rect.Top).ToList();

            // One layout for the finished list instead of one per added card,
            // each of which could also toggle the scrollbar and re-measure
            // every earlier card.
            _list.SuspendLayout();
            try
            {
            foreach (var m in _monitors)
            {
                m.Kelvin = _settings.KelvinFor(m.StableKey);
                bool warmthFailed = false;

                if (_settings.ReapplyColourOnStart)
                {
                    // Reapply on every rebuild, not only at startup: a display
                    // change resets the GPU gamma ramp, so without this the
                    // warmth silently vanishes whenever a mode changes.
                    //
                    // Also written when the saved warmth is neutral but the ramp
                    // on the display is still one of ours - after a crash, say.
                    // That case needs the baseline putting back, and skipping it
                    // is how a display stays tinted with the slider reading off.
                    if (m.Kelvin != GammaControl.NeutralKelvin || m.GammaIsOurs)
                        warmthFailed = !DisplayGamma.Apply(m);
                }
                else if (!m.GammaIsOurs)
                {
                    // Not reapplying, and the ramp on the display is not ours -
                    // a reboot or a driver restart cleared it. The saved number
                    // is now a claim about nothing, so show what is true.
                    m.Kelvin = GammaControl.NeutralKelvin;
                }

                // Give this monitor a Custom position from whatever it is
                // already set to, so the very first press of a preset is
                // reversible rather than one-way.
                _settings.SeedCustom(m);

                if (_blanker.IsBlanked(m.StableKey))
                    _blanker.Rebind(m);
                else
                    _blanker.RecoverInterruptedBlank(m);
                if (!_nightSaver.Rebind(m)) _nightSaver.RecoverInterruptedDim(m);

                var card = new MonitorCard(
                    m, _settings, _worker, OnCardChanged, s => SetStatus(s),
                    ToggleScreenBlank, ConfigureScreenBlankHotkey, LoadFeaturesForCard, ShowConnectionHelp, ShowProfileEditor);
                if (warmthFailed) card.ShowWarmthFailure();
                card.BrightnessRequested += c =>
                {
                    if (_nightSaver.TakeOver(c.Monitor.StableKey)) UpdateNightSaver();
                };
                card.SetScreenBlankState(
                    _blanker.IsBlanked(m.StableKey) ||
                    _settings.ScreenBlankActiveFor(m.StableKey));
                if (!_blanker.IsBlanked(m.StableKey) && _settings.ScreenBlankActiveFor(m.StableKey))
                    card.ShowWriteFailure(DdcWorker.Feature.Brightness);
                card.SetWidth(CardWidth);
                _cards.Add(card);
                _list.Controls.Add(card);
            }
            }
            finally { _list.ResumeLayout(); }

            _map.SetMonitors(_monitors);
            if (!_monitors.Any(m => m.StableKey == _selectedMonitorKey))
                _selectedMonitorKey = _monitors.FirstOrDefault()?.StableKey;
            ApplyDisplayView();
            PopulateWorkScreens();
            UpdateNightSaver();

            // Seeded Custom positions and resolved baselines are both new facts
            // about this desk. Persist them rather than waiting for the next
            // slider move, or a crash before then loses the way back.
            SaveSoon();
            ReportInventory(reason);
            _scanState = DisplayScanState.Ready;
            UpdateDisplayScanPresentation();
            RegisterScreenBlankHotkeys();
            // A period that began while no display could be adjusted - at
            // startup, or during this scan - is requested now, once.
            CheckSchedule();

            // Capability strings are loaded only when their disclosure is
            // opened. They are the slowest DDC request available; probing all
            // monitors immediately was enough to make early slider writes wait
            // seconds behind information the person had not asked to see.

            Diagnostics.Log(() =>
                $"rebuild {gen}  monitors={_monitors.Count}  cards={_cards.Count}  " +
                $"controls={CountControls(this)}  liveManagedKB={Diagnostics.LiveManagedBytes() / 1024}");
        }
        catch (Exception ex)
        {
            if (IsDisposed || gen != _generation) return;
            _scanState = DisplayScanState.Failed;
            foreach (var c in _cards) c.Dispose();
            _cards.Clear();
            _map.SetMonitors([]);
            UpdateDisplayScanPresentation();
            SetStatus("Display scan did not finish. Use Refresh or Connection help.", StatusKind.Warn);
            Diagnostics.Log(() => $"rebuild {gen} failed  errorType={ex.GetType().Name}");
        }
        finally
        {
            try
            {
                if (!IsDisposed && gen == _generation) _refreshButton.Enabled = true;
                if (!IsDisposed && gen == _generation) UpdateNightSaver();
                if (!IsDisposed && gen == _generation && _connectionHelp is { IsDisposed: false })
                    _connectionHelp.SetMonitors(_monitors, _scanState);
                focus?.Complete(gen);
            }
            finally
            {
                if (ReferenceEquals(_scanFocus, focus)) _scanFocus = null;
                focus?.Dispose();
            }
        }
    }

    private sealed record WorkScreenChoice(string Key, string Label);

    private void PopulateWorkScreens()
    {
        string preferred = _settings.NightWorkMonitorKey;
        _workMonitor.Items.Clear();
        foreach (var m in _monitors)
            _workMonitor.Items.Add(new WorkScreenChoice(m.StableKey,
                $"{m.DisplayName} ({m.PositionLabel})"));
        var selected = _monitors.FirstOrDefault(m => m.StableKey == preferred)
            ?? _monitors.FirstOrDefault(m => m.IsPrimary) ?? _monitors.FirstOrDefault();
        if (selected != null)
            _workMonitor.SelectedIndex = _monitors.IndexOf(selected);
    }

    private void UpdateNightSaver()
    {
        bool active = _nightSaver.Count > 0;
        _bar.SetNightSaverState(active);
        _nightButton.Enabled = _refreshButton.Enabled && (active || CanAdjustDisplays && _monitors.Count > 1);
        _nightTrayItem.Text = _nightButton.Text;
        _nightTrayItem.AccessibleDescription = _nightTrayItem.ToolTipText = _nightButton.AccessibleDescription;
        _nightTrayItem.Enabled = _nightButton.Enabled;
        _workMonitor.Enabled = !active && _refreshButton.Enabled && CanAdjustDisplays;
    }

    private void ToggleNightSaver()
    {
        if (!_refreshButton.Enabled) return;
        if (_nightSaver.Count > 0)
        {
            _nightSaver.Restore(_monitors);
            UpdateNightSaver();
            SetStatus(_nightSaver.Count == 0
                ? "Night desk restored to its saved brightness."
                : "Some brightness restores are pending. Reconnect the displays and press Restore night desk again.",
                _nightSaver.Count == 0 ? StatusKind.Info : StatusKind.Warn);
            return;
        }

        if (_workMonitor.SelectedItem is not WorkScreenChoice choice) return;
        var work = _monitors.FirstOrDefault(m => m.StableKey == choice.Key);
        int failed = _nightSaver.Start(_monitors, choice.Key);
        if (failed < 0)
        {
            SetStatus("Restore your work screen before starting Night saver.", StatusKind.Warn);
            return;
        }
        UpdateNightSaver();
        int dark = _nightSaver.Count - (_nightSaver.WorkDim == NightWorkDim.Dimmed ? 1 : 0);
        var night = _settings.PresetLevel("Night");
        string workState = _nightSaver.WorkDim switch
        {
            NightWorkDim.Dimmed => $"{work.DisplayName} dimmed toward the Night target (estimated {night.Nits} nits).",
            NightWorkDim.AlreadyDim => $"{work.DisplayName} is already at or below the Night target and stays as it is.",
            NightWorkDim.NotSaved => $"{work.DisplayName} stays unchanged: its brightness could not be saved for restoring.",
            _ => $"{work.DisplayName} stays unchanged: it has no brightness control.",
        };
        SetStatus($"Night saver: {dark} screen(s) dark; {workState}" +
            (failed > 0 ? $" {failed} screen(s) could not be blanked." : " Press Restore night desk to go back."),
            failed > 0 || _nightSaver.WorkDim == NightWorkDim.NotSaved ? StatusKind.Warn : StatusKind.Info);
    }

    private static int CountControls(Control root)
    {
        int n = 1;
        foreach (Control c in root.Controls) n += CountControls(c);
        return n;
    }

    /// <summary>Load one card's optional controls on demand.</summary>
    private async void LoadFeaturesForCard(MonitorCard card)
    {
        if (card == null || card.IsDisposed || card.Monitor.FeaturesLoaded) return;
        int gen = _generation;
        var monitor = card.Monitor;
        card.BeginFeatureLoad();

        await Task.Run(() => MonitorService.LoadFeatures(monitor));

        if (IsDisposed || gen != _generation || card.IsDisposed) return;
        card.PopulateFeatures(expand: true);
    }

    private void ReportInventory(string reason)
    {
        int total = _monitors.Count;
        int controllable = _monitors.Count(m => m.SupportsBrightness);

        string s;
        var kind = StatusKind.Info;
        if (total == 0)
        {
            s = DisplaySupport.Inventory(_monitors);
            kind = StatusKind.Warn;
        }
        else if (controllable == total)
        {
            s = DisplaySupport.Inventory(_monitors);
        }
        else
        {
            s = DisplaySupport.Inventory(_monitors);
            kind = StatusKind.Warn;
        }

        // Only claim this when it actually happened. The old wording appeared
        // whenever a saved kelvin existed, including with reapply switched off,
        // where nothing had been written to any display.
        if (_settings.ReapplyColourOnStart && _monitors.Any(m => m.Kelvin != GammaControl.NeutralKelvin))
            s += "  Saved warmth requested. Check any card warnings.";

        if (reason != null) s = reason + "  " + s;
        SetStatus(s, kind);
    }

    private void SetStatus(string text, StatusKind kind = StatusKind.Info)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => SetStatus(text, kind))); return; }
        _status.Set(text, kind);
    }

    /// <summary>Raised on the worker thread when a write is refused.</summary>
    private void OnWriteFailed(Monitor m, DdcWorker.Feature what)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => OnWriteFailed(m, what))); }
            catch (InvalidOperationException) { /* The window closed during dispatch. */ }
            return;
        }

        // Refresh replaces Monitor identities. A late callback from an old
        // device must not warn about (or synchronize) its replacement card.
        var card = _cards.FirstOrDefault(c => c.Monitor == m);
        if (card == null || card.IsDisposed) return;
        card.ShowWriteFailure(what);
        string name = m.FriendlyName;
        if (what is DdcWorker.Feature.Brightness or DdcWorker.Feature.Contrast)
        {
            _map.Refresh(null);
        }

        string detail = what.ToString().ToLowerInvariant() + " change";
        SetStatus($"{name}: the {detail} could not be confirmed. " +
                  "Check the screen or open Connection help on its card.", StatusKind.Warn);
    }

    private void OnScreenBlankStateChanged(string key, bool blanked, bool brightnessRestored)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
                OnScreenBlankStateChanged(key, blanked, brightnessRestored)));
            return;
        }

        // Recovery markers are serialized settings too. A restore during a
        // background write must not let the earlier snapshot claim this state.
        MarkSettingsChanged();
        if (_settingsWriteRunning) _settingsDirty = true;
        else RecordSettingsSave(_settings.LastSaveResult, _settingsSaveRevision);

        var card = _cards.FirstOrDefault(c => c.Monitor.StableKey == key);
        if (card is { IsDisposed: false })
        {
            card.SetScreenBlankState(
                blanked || _settings.ScreenBlankActiveFor(key));
            if (!blanked && !brightnessRestored)
                card.ShowWriteFailure(DdcWorker.Feature.Brightness);
            card.SyncFromMonitor();
        }
        if (!blanked && brightnessRestored) _nightSaver.ForgetRestored(key);
        UpdateNightSaver();
        string name = card?.Monitor.DisplayName ?? "Screen";
        SetStatus(blanked
            ? $"{name}: temporarily blanked. Click that screen or use its shortcut to restore."
            : brightnessRestored
                ? $"{name}: screen and saved brightness restored."
                : $"{name}: blackout removed, but brightness could not be restored yet. " +
                  "Check the monitor connection, then use Restore screen or Refresh to retry.",
            brightnessRestored ? StatusKind.Info : StatusKind.Warn);
    }

    private void ToggleScreenBlank(MonitorCard card)
    {
        if (card == null || card.IsDisposed)
        {
            SetStatus("That display is no longer available.", StatusKind.Warn);
            return;
        }

        if (_blanker.IsBlanked(card.Monitor.StableKey))
            _blanker.Restore(card.Monitor.StableKey);
        else if (_settings.ScreenBlankActiveFor(card.Monitor.StableKey))
        {
            if (!_blanker.RecoverInterruptedBlank(card.Monitor))
            {
                card.ShowWriteFailure(DdcWorker.Feature.Brightness);
                SetStatus($"{card.Monitor.DisplayName}: saved brightness recovery is still pending. " +
                    "Check the monitor connection, then use Restore screen or Refresh to retry.", StatusKind.Warn);
            }
        }
        else if (!_blanker.Blank(card.Monitor))
            SetStatus($"{card.Monitor.DisplayName}: could not prepare the blackout window or save recovery data.", StatusKind.Warn);
    }

    private void ConfigureScreenBlankHotkey(MonitorCard card)
    {
        if (card == null || card.IsDisposed) return;
        if (_settingsWriteRunning)
        {
            SetStatus("Settings save pending. Retry the shortcut after it finishes.", StatusKind.Warn);
            return;
        }
        string key = card.Monitor.StableKey;
        string name = card.Monitor.DisplayName;
        string current = _settings.ScreenBlankHotkeyFor(key);
        using var dialog = new ScreenBlankHotkeyDialog(name, current);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string selected = dialog.SelectedShortcut ?? current;
        ScreenBlankHotkey.TryParse(selected, out var selectedHotkey);
        if (!string.IsNullOrEmpty(selected) &&
            _cards.Any(c => c.Monitor.StableKey != key &&
                ScreenBlankHotkey.TryParse(_settings.ScreenBlankHotkeyFor(c.Monitor.StableKey), out var other) &&
                other.Modifiers == selectedHotkey.Modifiers &&
                other.VirtualKey == selectedHotkey.VirtualKey))
        {
            SetStatus($"{selected} is already assigned to another monitor.", StatusKind.Warn);
            return;
        }

        RequestShortcutSave(key, name, selected);
    }

    private void RegisterScreenBlankHotkeys()
    {
        if (!IsHandleCreated || IsDisposed) return;
        UnregisterScreenBlankHotkeys();

        int id = FirstScreenBlankHotkeyId;
        foreach (var card in _cards)
        {
            string text = _settings.ScreenBlankHotkeyFor(card.Monitor.StableKey);
            if (!ScreenBlankHotkey.TryParse(text, out var hotkey)) continue;

            int candidate = id++;
            if (_host.RegisterHotKey(Handle, candidate,
                    hotkey.Modifiers | Native.MOD_NOREPEAT, hotkey.VirtualKey))
            {
                _screenBlankHotkeys[candidate] = card;
            }
            else
            {
                SetStatus($"Could not register {hotkey.Text} for {card.Monitor.DisplayName}; " +
                          "another app may already use it.", StatusKind.Warn);
            }
        }
    }

    private void UnregisterScreenBlankHotkeys()
    {
        if (IsHandleCreated)
            foreach (int id in _screenBlankHotkeys.Keys)
                _host.UnregisterHotKey(Handle, id);
        _screenBlankHotkeys.Clear();
    }

    /// <summary>
    /// WinForms recreates the window handle for some property changes, such as
    /// ShowInTaskbar or a style change. RegisterHotKey belongs to that handle,
    /// so re-register here or every shortcut would silently stop working.
    /// </summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this, _settings.UseMicaTitleBar);
        if (_cards.Count > 0) RegisterScreenBlankHotkeys();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterScreenBlankHotkeys();
        base.OnHandleDestroyed(e);
    }

    // ----------------------------------------------------------------- levels

    private void ApplyLevel(string name) => ApplyLevel(name, null);

    /// <summary><paramref name="origin"/> prefixes the status, such as "Schedule: ".</summary>
    private void ApplyLevel(string name, string origin)
    {
        if (!CanAdjustDisplays || _settings.PresetLevel(name) is not { } level) return;
        foreach (var c in _cards.Where(c => !_settings.ScreenBlankActiveFor(c.Monitor.StableKey)))
        {
            c.ApplyBrightness(Presets.BrightnessFor(c.Monitor, level.Nits));
            c.ApplyWarmth(level.Kelvin);
        }
        SaveSoon();
        SetMode(level.Name);

        int generic = _cards.Count(c => c.Monitor.SupportsBrightness &&
            !_settings.ScreenBlankActiveFor(c.Monitor.StableKey) && !PanelDatabase.HasUserProfile(c.Monitor));
        string caveat = generic > 0
            ? $"  {generic} brightness estimate{(generic == 1 ? " uses" : "s use")} a generic profile."
            : "";
        SetStatus($"{origin}{level.Name}: estimated {level.Nits}-nit target and {level.Kelvin}K tint requested." +
                  $"{caveat}  Check card warnings. Press {Presets.CustomName} to go back.");
    }

    /// <summary>
    /// Put every monitor back to the levels its owner chose. This is the way out
    /// of a preset pressed by accident, which used to be a one-way door.
    /// </summary>
    private void ApplyCustom() => ApplyCustom(null);

    private void ApplyCustom(string origin)
    {
        if (!CanAdjustDisplays) return;
        int restored = _cards.Count(c => !_settings.ScreenBlankActiveFor(c.Monitor.StableKey) && c.RestoreCustom());
        SaveSoon();
        SetMode(restored == 0 ? null : Presets.CustomName);

        SetStatus(origin + (restored == 0
            ? "No monitor has settings of its own saved yet - move a slider and they are remembered."
            : $"Your own settings requested on {restored} monitor{(restored == 1 ? "" : "s")}. Check any card warnings."));
    }

    private void WarmthOff()
    {
        if (!CanAdjustDisplays) return;
        foreach (var c in _cards) c.ApplyWarmth(GammaControl.NeutralKelvin);
        SaveSoon();
        SetMode(null);
        SetStatus("Neutral colour requested for all monitors. Check any card warnings.");
    }

    /// <summary>
    /// Show which mode is in force. Null means the desk is mixed - a per-monitor
    /// change, or warmth switched off under a preset - and no button lights up,
    /// which is more honest than picking one.
    /// </summary>
    private void SetMode(string name)
    {
        foreach (var (key, button) in _modeButtons)
        {
            bool on = name != null && string.Equals(key, name, StringComparison.OrdinalIgnoreCase);
            if (button.Primary == on) continue;
            button.Primary = on;
            button.Invalidate();
        }
    }

    /// <summary>
    /// A card changed. <paramref name="manual"/> distinguishes a slider somebody
    /// moved - which becomes their Custom position - from a preset being applied
    /// to one monitor, which must not.
    /// </summary>
    private void OnCardChanged(MonitorCard card, bool manual)
    {
        SetMode(manual ? Presets.CustomName : null);
        SaveSoon();
    }

    private void SaveSoon()
    {
        foreach (var m in _monitors) _settings.SetKelvin(m.StableKey, m.DisplayName, m.Kelvin);
        MarkSettingsChanged();
        _map.Refresh(null);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    // ------------------------------------------------------ window plumbing

    protected override void WndProc(ref Message msg)
    {
        if (msg.Msg == WM_HOTKEY &&
            _screenBlankHotkeys.TryGetValue(msg.WParam.ToInt32(), out var card))
        {
            ToggleScreenBlank(card);
            return;
        }

        // Waking or a clock change can cross a scheduled time between checks.
        if ((msg.Msg == WM_TIMECHANGE || msg.Msg == WM_POWERBROADCAST && msg.WParam == PBT_APMRESUMEAUTOMATIC) &&
            IsHandleCreated && !IsDisposed)
            BeginInvoke((Action)CheckSchedule);

        if (msg.Msg == WM_DISPLAYCHANGE && IsHandleCreated && !IsDisposed)
        {
            _displayChangeTimer.Stop();
            _displayChangeTimer.Start();
        }
        base.WndProc(ref msg);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        // Re-showing must not revive an open request from before hiding.
        if (!Visible) _visibilityGeneration++;
        base.OnVisibleChanged(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // A minimised client area is empty. Laying cards out for it shrank every
        // card and hid the map, and restoring then laid everything out again:
        // two full relayouts per round trip for a window nobody could see.
        if (WindowState == FormWindowState.Minimized)
        {
            // _shown keeps this off the pre-show resize; see OnShown.
            if (_shown && _settings.MinimiseToTray) Hide();
            return;
        }
        UpdateDeskMapVisibility();
        LayoutCards();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        // WinForms scales the entire tree and applies Windows' suggested bounds.
        // ClientSize/Font events inside that transaction describe mixed old/new
        // metrics. Pinning card widths there forces repeated table measurement.
        BeginDpiLayout();
        base.OnDpiChanged(e);
    }

    protected override bool OnGetDpiScaledSize(int deviceDpiOld, int deviceDpiNew, ref Size desiredSize)
    {
        // PMv2 sends this before the children's BEFOREPARENT notifications.
        // Waiting for Form.DpiChanged is too late: explicit child fonts and
        // AutoSize flows have already started laying out at mixed metrics.
        if (deviceDpiOld != deviceDpiNew) BeginDpiLayout();
        return base.OnGetDpiScaledSize(deviceDpiOld, deviceDpiNew, ref desiredSize);
    }

    private void BeginDpiLayout()
    {
        if (_dpiLayoutPending) return;
        _scalingDpi = true;
        _dpiLayoutPending = true;
        var controls = new List<Control>();
        void SuspendTree(Control control)
        {
            controls.Add(control);
            control.SuspendLayout();
            foreach (Control child in control.Controls) SuspendTree(child);
        }
        SuspendTree(this);
        _dpiSuspendedControls = controls;
        BeginInvoke((Action)(() =>
        {
            if (IsDisposed || Disposing) return;
            // All AFTERPARENT notifications have now completed. Release the
            // suspension without replaying every queued intermediate layout;
            // the command rows and pinned cards perform their final passes.
            try
            {
                foreach (var control in _dpiSuspendedControls)
                    if (!control.IsDisposed) control.ResumeLayout(false);
                _bar.SuspendLayout();
                try
                {
                    foreach (CommandSection section in _bar.Controls)
                    {
                        section.PerformLayout();
                        section.Commands.PerformLayout();
                    }
                }
                finally { _bar.ResumeLayout(true); }
                PerformLayout();
            }
            finally
            {
                _dpiSuspendedControls = null;
                _scalingDpi = false;
            }
            _dpiLayoutPending = false;
            UpdateDeskMapVisibility();
            LayoutCards();
        }));
    }

    private void LayoutCards()
    {
        if (_scalingDpi || _dpiLayoutPending || _layingOutCards || _list == null || WindowState == FormWindowState.Minimized) return;
        _layingOutCards = true;
        _list.SuspendLayout();
        try
        {
            int width = CardWidth;
            foreach (var c in _cards) c.SetWidth(width);
            _scanView?.SetWidth(width);
        }
        finally
        {
            _list.ResumeLayout(true);
            // The first pass can retain the previous width in FlowLayout's
            // virtual extent after a resize; settle it once with pinned cards.
            _list.PerformLayout();
            _layingOutCards = false;
        }
    }

    private async void RestoreFromTray()
    {
        RevealWindow();

        // Someone may have used the monitor's own buttons while the window was
        // hidden. Re-read off the UI thread - eight DDC reads is seconds of
        // freeze otherwise.
        var snapshot = _cards.ToList();
        await Task.Run(() =>
        {
            foreach (var c in snapshot) _worker.RefreshValues(c.Monitor);
        });
        if (IsDisposed) return;
        foreach (var c in snapshot) if (!c.IsDisposed) c.SyncFromMonitor();
        _map.Refresh(null);
    }

    /// <summary>Accept only a visibility request; no scans, settings or monitor writes.</summary>
    internal bool RequestReveal()
    {
        if (IsDisposed || Disposing || !IsHandleCreated || _reallyClosing) return false;
        if (Interlocked.Exchange(ref _revealPending, 1) == 1) return true;
        try
        {
            BeginInvoke((Action)(() =>
            {
                try
                {
                    if (!IsDisposed && !Disposing && !_reallyClosing) RevealWindow();
                }
                finally { Volatile.Write(ref _revealPending, 0); }
            }));
            return true;
        }
        catch (InvalidOperationException) { Volatile.Write(ref _revealPending, 0); return false; }
    }

    /// <summary>
    /// Never assign ShowInTaskbar here or when hiding to the tray. A hidden
    /// top-level window has no taskbar button anyway, and changing the property
    /// makes WinForms recreate the window handle: every card control is
    /// destroyed and rebuilt and every shortcut re-registered, which is what
    /// made restoring from the tray stall.
    /// </summary>
    private void RevealWindow()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        bool waitForSave = _profileEditor?.IsSaving == true || _settingsWriteRunning;
        if (waitForSave)
        {
            e.Cancel = true;
            _profileEditor?.ShowSavePending();
            _presetEditor?.ShowSavePending();
            _scheduleEditor?.ShowSavePending();
            if (_settingsWriteRunning) SetStatus("Settings save pending. Wait before using Exit.", StatusKind.Warn);
        }
        bool hideToTray = !e.Cancel && !_reallyClosing && _settings.MinimiseToTray && e.CloseReason == CloseReason.UserClosing;
        if (hideToTray)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
        }
        // Owned-form cancellation arrives before this override; subscribed
        // handlers can also cancel inside base. Neither may tear resources down.
        base.OnFormClosing(e);
        if (waitForSave || hideToTray) e.Cancel = true;
        if (e.Cancel)
        {
            _reallyClosing = false;
            return;
        }

        _generation++;              // invalidate any rebuild still in flight
        _reallyClosing = true;
        UnregisterScreenBlankHotkeys();
        _saveTimer.Stop();
        _displayChangeTimer.Stop();
        _scheduleTimer.Stop();
        _worker.WriteFailed -= OnWriteFailed;

        // Every blackout must be reversible even when Exit is used while one
        // is active. Restore synchronously before the monitor handles and DDC
        // worker disappear.
        _blanker.RestoreAll();
        _nightSaver.RestoreWorkDim(_monitors);
        SaveSettingsOrWarn();
        _host.SaveGamma();

        _tray.Visible = false;
        _tray.Dispose();
        _preferencesMenu.Dispose();

        // Stop the writer before freeing what it writes to.
        _worker.Dispose();
        _blanker.Dispose();
        foreach (var m in _monitors) m.Dispose();
        _monitors.Clear();

        _appIcon.Dispose();
        _trayIcon.Dispose();
    }
}

internal enum StatusKind { Info, Warn }
