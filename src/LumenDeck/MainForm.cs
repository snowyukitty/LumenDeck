namespace LumenDeck;

internal sealed class MainForm : Form
{
    private const int WM_DISPLAYCHANGE = 0x007E;

    private readonly DdcWorker _worker = new();
    private readonly AppSettings _settings = AppSettings.Load();

    private List<Monitor> _monitors = new();
    private readonly List<MonitorPanel> _panels = new();

    private readonly FlowLayoutPanel _list;
    private readonly Label _status;
    private readonly NotifyIcon _tray;
    private readonly Icon _appIcon;
    private readonly System.Windows.Forms.Timer _saveTimer;
    private readonly System.Windows.Forms.Timer _displayChangeTimer;
    private readonly Button _refreshButton;

    /// <summary>Bumped per rebuild so a slow enumeration cannot overwrite a newer one.</summary>
    private int _generation;
    private bool _reallyClosing;

    public MainForm()
    {
        Text = "LumenDeck";
        BackColor = Color.FromArgb(26, 26, 29);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9f);
        MinimumSize = new Size(660, 400);
        Size = new Size(720, 780);
        StartPosition = FormStartPosition.CenterScreen;

        // One icon instance, shared by the window and the tray, disposed once.
        // WinForms does not take ownership of an icon handed to it.
        _appIcon = AppIcon.Create();
        Icon = _appIcon;

        // ---- toolbar ------------------------------------------------------
        var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(32, 32, 36) };
        int x = 12;

        foreach (var level in Presets.Levels)
        {
            var b = MakeButton(level.Name, ref x, 76);
            var captured = level;
            b.Click += (_, _) => ApplyLevel(captured);
            new ToolTip().SetToolTip(b, $"{captured.Description}\nAbout {captured.Nits} nits on every panel, {captured.Kelvin}K");
            bar.Controls.Add(b);
        }

        x += 12;
        var identify = MakeButton("Identify", ref x, 84);
        identify.Click += (_, _) => IdentifyOverlay.Show(_monitors);
        new ToolTip().SetToolTip(identify, "Show each monitor's name on its own screen");
        bar.Controls.Add(identify);

        _refreshButton = MakeButton("Refresh", ref x, 80);
        _refreshButton.Click += (_, _) => _ = RebuildAsync();
        new ToolTip().SetToolTip(_refreshButton, "Re-read every monitor and rebuild the list");
        bar.Controls.Add(_refreshButton);

        var warmOff = MakeButton("Warmth off", ref x, 100);
        warmOff.Click += (_, _) =>
        {
            foreach (var p in _panels) p.ApplyKelvin(GammaControl.NeutralKelvin);
            SaveSoon();
            SetStatus("All monitors restored to their original colour.");
        };
        new ToolTip().SetToolTip(warmOff, "Restore each display's original gamma, including any ICC or colorimeter profile");
        bar.Controls.Add(warmOff);

        // ---- list ---------------------------------------------------------
        _list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(26, 26, 29),
        };

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(14, 0, 14, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(165, 165, 175),
            BackColor = Color.FromArgb(32, 32, 36),
            Font = new Font("Segoe UI", 8.5f),
        };

        Controls.Add(_list);
        Controls.Add(_status);
        Controls.Add(bar);

        // ---- tray ---------------------------------------------------------
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        foreach (var level in Presets.Levels)
        {
            var captured = level;
            menu.Items.Add(captured.Name, null, (_, _) => ApplyLevel(captured));
        }
        menu.Items.Add(new ToolStripSeparator());

        var warmthOff = new ToolStripMenuItem("Warmth off", null, (_, _) =>
        {
            foreach (var p in _panels) p.ApplyKelvin(GammaControl.NeutralKelvin);
            SaveSoon();
            SetStatus("All monitors restored to their original colour.");
        });
        menu.Items.Add(warmthOff);

        // If the executable moved since this was switched on, the entry points
        // at a copy that may no longer exist - so fix it before showing its
        // state, rather than reporting "on" for something that cannot run.
        StartupEntry.RepairIfStale();
        var startWithWindows = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupEntry.IsEnabled,
            ToolTipText = "A gamma ramp does not survive a reboot, so warmth is reapplied at login.",
        };
        bool syncingStartup = false;
        startWithWindows.CheckedChanged += (_, _) =>
        {
            if (syncingStartup) return;

            bool want = startWithWindows.Checked;
            if (StartupEntry.Set(want))
            {
                SetStatus(want
                    ? "LumenDeck will start with Windows and reapply your saved warmth."
                    : "LumenDeck will no longer start with Windows.");
                return;
            }

            // Put the tick back. The menu must never claim a state the registry
            // did not accept - a checkbox that lies is worse than one that
            // refuses. The guard stops the correction re-entering this handler.
            syncingStartup = true;
            startWithWindows.Checked = StartupEntry.IsEnabled;
            syncingStartup = false;
            SetStatus("Could not change the startup setting - the registry write was refused.");
        };
        menu.Items.Add(startWithWindows);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _reallyClosing = true; Close(); });

        _tray = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "LumenDeck",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => RestoreFromTray();

        // Settings are written on a delay so dragging a slider does not hammer
        // the disk with a write per pixel.
        _saveTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _settings.Save(); };

        // Windows sends WM_DISPLAYCHANGE several times for one physical change,
        // and a rebuild costs a full DDC enumeration, so they are coalesced.
        _displayChangeTimer = new System.Windows.Forms.Timer { Interval = 900 };
        _displayChangeTimer.Tick += (_, _) =>
        {
            _displayChangeTimer.Stop();
            _ = RebuildAsync("Display configuration changed.");
        };

        _worker.WriteFailed += OnWriteFailed;

        SetStatus("Reading monitors over DDC/CI...");
        if (_settings.StartMinimised) WindowState = FormWindowState.Minimized;
    }

    /// <summary>
    /// Enumeration is started here rather than in the constructor so the window
    /// is on screen first. Doing it inline meant nothing appeared for nearly
    /// seven seconds after launch - measured, not guessed - which reads as a
    /// hung app. It also left a window with no handle during the slowest part of
    /// startup, so a display change in that gap was missed entirely.
    /// </summary>
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RebuildAsync();
    }

    private Button MakeButton(string text, ref int x, int width)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, 11),
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(52, 52, 58),
            ForeColor = Color.White,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(72, 72, 80);
        x += width + 8;
        return b;
    }

    // ---------------------------------------------------------------- rebuild

    private int PanelWidth => Math.Max(320, _list.ClientSize.Width - _list.Padding.Horizontal - 24);

    private async Task RebuildAsync(string reason = null)
    {
        int gen = ++_generation;
        _refreshButton.Enabled = false;
        SetStatus(reason == null ? "Reading monitors over DDC/CI..." : reason + " Re-reading monitors...");

        try
        {
            // Order matters. Drop queued writes, then take the lock the writer
            // holds around every native call, so no handle is freed while the
            // background thread is inside a write to it.
            _worker.ClearPending();
            lock (_worker.HandleLock)
            {
                foreach (var m in _monitors) m.Dispose();
            }
            _monitors.Clear();

            // Controls.Clear() removes without disposing, so every Font, Label
            // and window handle in the old panels would be leaked - once per
            // Refresh click and once per display change.
            foreach (var p in _panels) p.Dispose();
            _panels.Clear();
            _list.Controls.Clear();

            // The slow part, off the UI thread.
            var found = await Task.Run(() => MonitorService.Enumerate());

            if (IsDisposed || gen != _generation)
            {
                // A newer rebuild started, or the app is closing. These handles
                // belong to nobody now, so release them here.
                lock (_worker.HandleLock)
                {
                    foreach (var m in found) m.Dispose();
                }
                return;
            }

            _monitors = found.OrderBy(m => m.Rect.Left).ThenBy(m => m.Rect.Top).ToList();

            foreach (var m in _monitors)
            {
                m.Kelvin = _settings.KelvinFor(m.StableKey);

                // Reapply on every rebuild, not only at startup: a display
                // change resets the GPU gamma ramp, so without this the warmth
                // silently vanishes whenever a mode changes.
                if (_settings.ReapplyColourOnStart && m.Kelvin != GammaControl.NeutralKelvin)
                    GammaControl.Apply(m.DeviceName, m.Kelvin, m.BaselineRamp);

                var panel = new MonitorPanel(m, _worker, SaveSoon, SetStatus) { Width = PanelWidth };
                _panels.Add(panel);
                _list.Controls.Add(panel);
            }

            ReportInventory(reason);

            // Capability strings are the slowest DDC request there is, so they
            // are read after the window is already usable, one monitor at a
            // time, and each panel grows its own extra controls as its answer
            // arrives. Doing this during enumeration put seconds back onto
            // startup for information most people never open.
            _ = LoadFeaturesAsync(gen);

            Diagnostics.Log(() =>
                $"rebuild {gen}  monitors={_monitors.Count}  panels={_panels.Count}  " +
                $"controls={Diagnostics.CountControls(this)}  " +
                $"liveManagedKB={Diagnostics.LiveManagedBytes() / 1024}");
        }
        catch (Exception ex)
        {
            SetStatus("Could not read the monitors: " + ex.Message);
        }
        finally
        {
            if (!IsDisposed) _refreshButton.Enabled = true;
        }
    }

    /// <summary>
    /// Probe each monitor's extra controls in the background and hand them to
    /// its panel as they arrive.
    ///
    /// Per monitor rather than all at once, so a slow or unresponsive panel
    /// delays only its own controls. The generation check drops results from a
    /// rebuild that has since been superseded - otherwise controls belonging to
    /// disposed panels get added to the window.
    /// </summary>
    private async Task LoadFeaturesAsync(int gen)
    {
        var panels = _panels.ToList();
        foreach (var panel in panels)
        {
            if (IsDisposed || gen != _generation) return;

            var monitor = panel.Monitor;
            await Task.Run(() =>
            {
                lock (_worker.HandleLock)
                {
                    if (gen == _generation) MonitorService.LoadFeatures(monitor);
                }
            });

            if (IsDisposed || gen != _generation || panel.IsDisposed) return;
            panel.PopulateFeatures(_worker);
        }
    }

    private void ReportInventory(string reason)
    {
        int total = _monitors.Count;
        int controllable = _monitors.Count(m => m.SupportsBrightness);

        string s;
        if (total == 0)
            s = "No monitors found.";
        else if (controllable == total)
            s = $"{total} monitor{(total == 1 ? "" : "s")}, all controllable over DDC/CI.";
        else
            s = $"{controllable} of {total} monitors answer DDC/CI. The rest usually need " +
                "DDC/CI enabled in their own on-screen menu; docks and KVMs often block it.";

        if (_monitors.Any(m => m.Kelvin != GammaControl.NeutralKelvin))
            s += "  Saved warmth reapplied - a gamma ramp does not survive a reboot.";

        if (reason != null) s = reason + "  " + s;
        SetStatus(s);
    }

    private void SetStatus(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => SetStatus(text))); return; }
        _status.Text = text;
    }

    /// <summary>Raised on the worker thread when a write is refused.</summary>
    private void OnWriteFailed(Monitor m, DdcWorker.Feature what)
    {
        string name = m?.FriendlyName ?? "A monitor";
        SetStatus($"{name} refused the {what.ToString().ToLowerInvariant()} change - " +
                  "it may be asleep or on another input. Press Refresh to re-read it.");
    }

    // ----------------------------------------------------------------- levels

    private void ApplyLevel(Presets.Level level)
    {
        foreach (var p in _panels)
        {
            p.ApplyBrightness(Presets.BrightnessFor(p.Monitor, level.Nits));
            p.ApplyKelvin(level.Kelvin);
        }
        SaveSoon();

        int unknown = _monitors.Count(m => !Presets.IsKnown(m));
        string caveat = unknown > 0
            ? $"  {unknown} panel{(unknown == 1 ? " is" : "s are")} not in the luminance table, so those values are generic estimates."
            : "";
        SetStatus($"{level.Name}: every panel aimed at about {level.Nits} nits, {level.Kelvin}K.{caveat}");
    }

    private void SaveSoon()
    {
        foreach (var m in _monitors) _settings.SetKelvin(m.StableKey, m.FriendlyName, m.Kelvin);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    // ------------------------------------------------------ window plumbing

    protected override void WndProc(ref Message msg)
    {
        // A monitor plugged, unplugged or re-moded invalidates every cached DDC
        // handle and wipes the gamma ramps.
        if (msg.Msg == WM_DISPLAYCHANGE && IsHandleCreated && !IsDisposed)
        {
            _displayChangeTimer.Stop();
            _displayChangeTimer.Start();
        }
        base.WndProc(ref msg);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_list != null)
            foreach (var p in _panels) p.Width = PanelWidth;

        if (_settings.MinimiseToTray && WindowState == FormWindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    private async void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();

        // Someone may have used the monitor's own buttons while the window was
        // hidden. Re-read off the UI thread - eight DDC reads is seconds of
        // freeze otherwise.
        var snapshot = _panels.ToList();
        await Task.Run(() =>
        {
            lock (_worker.HandleLock)
            {
                foreach (var p in snapshot) MonitorService.ReadCurrent(p.Monitor);
            }
        });
        if (IsDisposed) return;
        foreach (var p in snapshot) if (!p.IsDisposed) p.SyncFromMonitor();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyClosing && _settings.MinimiseToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            return;
        }

        _generation++;              // invalidate any rebuild still in flight
        _saveTimer.Stop();
        _displayChangeTimer.Stop();
        _worker.WriteFailed -= OnWriteFailed;
        _settings.Save();

        _tray.Visible = false;
        _tray.Dispose();

        // Stop the writer before freeing what it writes to.
        _worker.Dispose();
        lock (_worker.HandleLock)
        {
            foreach (var m in _monitors) m.Dispose();
        }
        _monitors.Clear();

        _appIcon.Dispose();
        base.OnFormClosing(e);
    }
}
