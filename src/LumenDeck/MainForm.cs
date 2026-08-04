namespace LumenDeck;

internal sealed class MainForm : Form
{
    private const int WM_DISPLAYCHANGE = 0x007E;

    private readonly DdcWorker _worker = new();
    private readonly AppSettings _settings = AppSettings.Load();

    private List<Monitor> _monitors = new();
    private readonly List<MonitorCard> _cards = new();

    private readonly FlowLayoutPanel _list;
    private readonly LayoutMap _map;
    private readonly StatusStripe _status;
    private readonly NotifyIcon _tray;
    private readonly Icon _appIcon;
    private readonly Icon _trayIcon;
    private readonly System.Windows.Forms.Timer _saveTimer;
    private readonly System.Windows.Forms.Timer _displayChangeTimer;
    private readonly FlatButton _refreshButton;
    private readonly ToolTip _tips = new();
    private readonly Panel _bar;
    private readonly FlowLayoutPanel _toolRow;

    /// <summary>Mode buttons by name, so the one in force can be shown as such.</summary>
    private readonly Dictionary<string, FlatButton> _modeButtons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bumped per rebuild so a slow enumeration cannot overwrite a newer one.</summary>
    private int _generation;
    private bool _reallyClosing;

    public MainForm()
    {
        Text = "LumenDeck";
        BackColor = Theme.Base;
        ForeColor = Theme.Ink;
        Font = Theme.Body;
        // AutoScaleMode defaults to None, which leaves the whole layout in raw
        // device pixels under PerMonitorV2. Dpi is what the DPI-aware path wants.
        AutoScaleMode = AutoScaleMode.Dpi;

        // A hard-coded minimum width was wrong twice: 620 clipped "Warmth off",
        // and 720 was about to clip the Custom button. The toolbar does not
        // wrap, so its minimum is a fact about its contents and the DPI it is
        // drawn at - measured in OnShown rather than guessed here. This is only
        // the floor for a window with nothing in the bar at all.
        MinimumSize = new Size(720, 480);
        Size = new Size(780, 820);
        StartPosition = FormStartPosition.CenterScreen;

        // Two instances, because they are different frames: the window wants
        // the full multi-size icon, the tray specifically wants the hinted 16px
        // one. WinForms does not take ownership of either, so both are fields
        // and both get disposed.
        _appIcon = AppIcon.Load();
        _trayIcon = AppIcon.LoadTray();
        Icon = _appIcon;

        // ---- toolbar ------------------------------------------------------
        _bar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.Bar, Padding = new Padding(14, 0, 14, 0) };

        _toolRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 14, 0, 0),
        };

        _toolRow.Controls.Add(new Label
        {
            Text = "All monitors",
            Font = Theme.Small,
            ForeColor = Theme.InkFaint,
            AutoSize = true,
            Margin = new Padding(0, 8, 10, 0),
            BackColor = Color.Transparent,
        });

        foreach (var level in Presets.Levels)
        {
            var captured = level;
            var b = new FlatButton(captured.Name) { Width = 82, Margin = new Padding(0, 0, 6, 0) };
            b.Click += (_, _) => ApplyLevel(captured);
            _tips.SetToolTip(b,
                $"{captured.Description}\nAims every panel at about {captured.Nits} nits, {captured.Kelvin}K.");
            _modeButtons[captured.Name] = b;
            _toolRow.Controls.Add(b);
        }

        // The way back. Separated from the three levels by a gap because it is
        // not one of them: it restores what each monitor was set to by hand.
        var customButton = new FlatButton(Presets.CustomName) { Width = 82, Margin = new Padding(10, 0, 0, 0) };
        customButton.Click += (_, _) => ApplyCustom();
        _tips.SetToolTip(customButton,
            "Your own brightness, contrast and warmth, per monitor.\n" +
            "Saved whenever you move a slider, and never overwritten by a preset.");
        _modeButtons[Presets.CustomName] = customButton;
        _toolRow.Controls.Add(customButton);

        var identify = new FlatButton("Identify") { Width = 84, Margin = new Padding(16, 0, 6, 0) };
        identify.Click += (_, _) => IdentifyOverlay.Show(_monitors);
        _tips.SetToolTip(identify, "Show each monitor's name on its own screen");
        _toolRow.Controls.Add(identify);

        _refreshButton = new FlatButton("Refresh") { Width = 82, Margin = new Padding(0, 0, 6, 0) };
        _refreshButton.Click += (_, _) => _ = RebuildAsync();
        _tips.SetToolTip(_refreshButton, "Re-read every monitor");
        _toolRow.Controls.Add(_refreshButton);

        var warmOff = new FlatButton("Warmth off") { Width = 100 };
        warmOff.Click += (_, _) => WarmthOff();
        _tips.SetToolTip(warmOff,
            "Restore each display's original gamma, including any ICC or colorimeter profile");
        _toolRow.Controls.Add(warmOff);

        _bar.Controls.Add(_toolRow);

        // ---- desk map ------------------------------------------------------
        _map = new LayoutMap { Dock = DockStyle.Top };
        _map.MonitorPicked += m =>
        {
            var card = _cards.FirstOrDefault(c => c.Monitor == m);
            if (card == null) return;
            _list.ScrollControlIntoView(card);
            card.Highlight();
            _map.Refresh(m);
        };

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
        _list.ClientSizeChanged += (_, _) =>
        {
            foreach (var c in _cards) c.SetWidth(CardWidth);
        };

        Controls.Add(_list);
        Controls.Add(_status);
        Controls.Add(_map);
        Controls.Add(_bar);

        // ---- tray ----------------------------------------------------------
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add("Show LumenDeck", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        foreach (var level in Presets.Levels)
        {
            var captured = level;
            menu.Items.Add(captured.Name, null, (_, _) => ApplyLevel(captured));
        }
        // The tray is where a preset is most likely to be hit by accident, so
        // the way back has to be here too.
        menu.Items.Add(Presets.CustomName, null, (_, _) => ApplyCustom());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Warmth off", null, (_, _) => WarmthOff());

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
            // Put the tick back. A checkbox that lies is worse than one that
            // refuses. The guard stops the correction re-entering this handler.
            syncingStartup = true;
            startWithWindows.Checked = StartupEntry.IsEnabled;
            syncingStartup = false;
            SetStatus("Could not change the startup setting - the registry write was refused.", StatusKind.Warn);
        };
        menu.Items.Add(startWithWindows);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _reallyClosing = true; Close(); });

        _tray = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "LumenDeck",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => RestoreFromTray();

        // Settings are written on a delay so dragging a slider does not hammer
        // the disk with a write per pixel.
        _saveTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _settings.Save();

            // The gamma baselines ride the same timer. They only have to be
            // current before the process ends - but if it ends unexpectedly, a
            // stale record is what makes the next session unable to tell its own
            // ramp from the display's.
            DisplayGamma.Save();
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

        SetStatus("Reading monitors over DDC/CI...");
        if (_settings.StartMinimised) WindowState = FormWindowState.Minimized;
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
        FitMinimumWidthToToolbar();
        await RebuildAsync();
    }

    /// <summary>
    /// Make the window's minimum width whatever the toolbar actually needs.
    ///
    /// The row does not wrap, so a button added later - or the same buttons
    /// drawn at 150% DPI - silently falls off the right-hand edge with no
    /// scrollbar and no affordance. Twice now that has been fixed by raising a
    /// constant, which fixes the instance and not the bug. Measured after the
    /// window is shown, because that is the first moment the controls have been
    /// scaled for the monitor they are on.
    /// </summary>
    private void FitMinimumWidthToToolbar()
    {
        int content = _bar.Padding.Horizontal + _toolRow.Padding.Horizontal;
        foreach (Control c in _toolRow.Controls)
            content += c.Width + c.Margin.Horizontal;

        int chrome = Width - ClientSize.Width;
        int need = content + chrome + LogicalToDeviceUnits(8);
        if (need <= MinimumSize.Width) return;

        MinimumSize = new Size(need, MinimumSize.Height);
        if (Width < need) Width = need;
    }

    // ---------------------------------------------------------------- rebuild

    private int CardWidth => Math.Max(320, _list.ClientSize.Width - _list.Padding.Horizontal - 4);

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

            // Controls.Clear() removes without disposing, so every font, label
            // and window handle in the old cards would be leaked - once per
            // Refresh click and once per display change.
            foreach (var c in _cards) c.Dispose();
            _cards.Clear();
            _list.Controls.Clear();

            var found = await Task.Run(() => MonitorService.Enumerate());

            if (IsDisposed || gen != _generation)
            {
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
                        DisplayGamma.Apply(m);
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

                var card = new MonitorCard(m, _settings, _worker, OnCardChanged, s => SetStatus(s));
                card.SetWidth(CardWidth);
                _cards.Add(card);
                _list.Controls.Add(card);
            }

            _map.SetMonitors(_monitors);

            // Seeded Custom positions and resolved baselines are both new facts
            // about this desk. Persist them rather than waiting for the next
            // slider move, or a crash before then loses the way back.
            SaveSoon();
            ReportInventory(reason);

            // Capability strings are the slowest DDC request there is, so they
            // are read after the window is already usable, one monitor at a
            // time, and each card grows its controls as its answer arrives.
            _ = LoadFeaturesAsync(gen);

            Diagnostics.Log(() =>
                $"rebuild {gen}  monitors={_monitors.Count}  cards={_cards.Count}  " +
                $"controls={CountControls(this)}  liveManagedKB={Diagnostics.LiveManagedBytes() / 1024}");
        }
        catch (Exception ex)
        {
            SetStatus("Could not read the monitors: " + ex.Message, StatusKind.Warn);
        }
        finally
        {
            if (!IsDisposed) _refreshButton.Enabled = true;
        }
    }

    private static int CountControls(Control root)
    {
        int n = 1;
        foreach (Control c in root.Controls) n += CountControls(c);
        return n;
    }

    /// <summary>
    /// Probe each monitor's extra controls in the background and hand them to
    /// its card as they arrive. Per monitor rather than all at once, so a slow
    /// panel delays only its own controls.
    /// </summary>
    private async Task LoadFeaturesAsync(int gen)
    {
        var cards = _cards.ToList();
        foreach (var card in cards)
        {
            if (IsDisposed || gen != _generation) return;

            var monitor = card.Monitor;
            await Task.Run(() =>
            {
                lock (_worker.HandleLock)
                {
                    if (gen == _generation) MonitorService.LoadFeatures(monitor);
                }
            });

            if (IsDisposed || gen != _generation || card.IsDisposed) return;
            card.PopulateFeatures();
        }
    }

    private void ReportInventory(string reason)
    {
        int total = _monitors.Count;
        int controllable = _monitors.Count(m => m.SupportsBrightness);

        string s;
        var kind = StatusKind.Info;
        if (total == 0)
        {
            s = "No monitors detected.";
            kind = StatusKind.Warn;
        }
        else if (controllable == total)
        {
            s = $"{total} monitor{(total == 1 ? "" : "s")}, all controllable.";
        }
        else
        {
            s = $"{controllable} of {total} monitors answer DDC/CI. The rest usually need " +
                "DDC/CI enabled in their own on-screen menu; docks and KVMs often block it.";
            kind = StatusKind.Warn;
        }

        // Only claim this when it actually happened. The old wording appeared
        // whenever a saved kelvin existed, including with reapply switched off,
        // where nothing had been written to any display.
        if (_settings.ReapplyColourOnStart && _monitors.Any(m => m.Kelvin != GammaControl.NeutralKelvin))
            s += "  Saved warmth reapplied.";

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
        string name = m?.FriendlyName ?? "A monitor";
        SetStatus($"{name} refused the {what.ToString().ToLowerInvariant()} change - " +
                  "it may be asleep or on another input. Press Refresh to re-read it.", StatusKind.Warn);
    }

    // ----------------------------------------------------------------- levels

    private void ApplyLevel(Presets.Level level)
    {
        foreach (var c in _cards)
        {
            c.ApplyBrightness(Presets.BrightnessFor(c.Monitor, level.Nits));
            c.ApplyWarmth(level.Kelvin);
        }
        SaveSoon();
        SetMode(level.Name);

        int unknown = _monitors.Count(m => !Presets.IsKnown(m));
        string caveat = unknown > 0
            ? $"  {unknown} panel{(unknown == 1 ? " is" : "s are")} not in the luminance table, so those values are estimates."
            : "";
        SetStatus($"{level.Name}: every panel aimed at about {level.Nits} nits, {level.Kelvin}K." +
                  $"{caveat}  Press {Presets.CustomName} to go back.");
    }

    /// <summary>
    /// Put every monitor back to the levels its owner chose. This is the way out
    /// of a preset pressed by accident, which used to be a one-way door.
    /// </summary>
    private void ApplyCustom()
    {
        int restored = _cards.Count(c => c.RestoreCustom());
        SaveSoon();
        SetMode(restored == 0 ? null : Presets.CustomName);

        SetStatus(restored == 0
            ? "No monitor has settings of its own saved yet - move a slider and they are remembered."
            : $"Your own settings restored on {restored} monitor{(restored == 1 ? "" : "s")}.");
    }

    private void WarmthOff()
    {
        foreach (var c in _cards) c.ApplyWarmth(GammaControl.NeutralKelvin);
        SaveSoon();
        SetMode(null);
        SetStatus("All monitors restored to their original colour.");
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
        _map.Refresh(null);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    // ------------------------------------------------------ window plumbing

    protected override void WndProc(ref Message msg)
    {
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
            foreach (var c in _cards) c.SetWidth(CardWidth);

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
        var snapshot = _cards.ToList();
        await Task.Run(() =>
        {
            lock (_worker.HandleLock)
            {
                foreach (var c in snapshot) MonitorService.ReadCurrent(c.Monitor);
            }
        });
        if (IsDisposed) return;
        foreach (var c in snapshot) if (!c.IsDisposed) c.SyncFromMonitor();
        _map.Refresh(null);
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
        DisplayGamma.Save();

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
        _trayIcon.Dispose();
        _tips.Dispose();
        base.OnFormClosing(e);
    }
}

internal enum StatusKind { Info, Warn }

/// <summary>
/// The status line. It was a grey sentence at the bottom of a grey window,
/// which meant a real warning - a monitor refusing a write - looked exactly
/// like a routine count. Now a warning gets a colour and a marker.
/// </summary>
internal sealed class StatusStripe : Control
{
    private string _text = "";
    private StatusKind _kind = StatusKind.Info;

    public StatusStripe()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 46;
        BackColor = Theme.Bar;
        Font = Theme.Small;
        TabStop = false;   // output only
    }

    public void Set(string text, StatusKind kind)
    {
        _text = text ?? "";
        _kind = kind;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Bar);

        using (var line = new Pen(Theme.Line))
            g.DrawLine(line, 0, 0, Width, 0);

        Color accent = _kind == StatusKind.Warn ? Theme.Warn : Theme.AmberDim;
        using (var marker = new SolidBrush(accent))
            g.FillRectangle(marker, 0, 1, 3, Height - 1);

        var text = new Rectangle(14, 0, Math.Max(10, Width - 24), Height);
        TextRenderer.DrawText(g, _text, Font, text,
            _kind == StatusKind.Warn ? Theme.Warn : Theme.InkMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis | TextFormatFlags.Left);
    }
}
