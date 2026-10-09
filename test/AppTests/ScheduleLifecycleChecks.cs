using System.Diagnostics;
using System.Reflection;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    /// <summary>
    /// The actual window's schedule ownership, driven by a fake clock: once per
    /// period, deferred while a scan runs, manual changes kept until the next
    /// period, and an editor whose preview follows the same live rule as the
    /// window's own checks.
    /// </summary>
    private static void CheckScheduleLifecycle(string directory)
    {
        var now = new DateTime(2026, 10, 9, 19, 0, 0);
        bool failNext = false;
        using var scanGate = new ManualResetEventSlim(true);
        using var fixture = new Fixture(directory, now: () => now, settingsFactory: path =>
        {
            var settings = new AppSettings(path);
            settings.Schedule = settings.WithSchedule(true,
                PresetSchedule.Names.Select(n => (n, !Presets.IsCustom(n), PresetSchedule.DefaultStarts[n])));
            return settings;
        }, writeSettings: write =>
        {
            if (!failNext) return write();
            failNext = false;
            return SettingsSaveResult.Failed;
        }, enumerate: scan =>
        {
            scanGate.Wait();
            return [new Display
            {
                StableKey = "schedule-" + scan, Description = "Synthetic schedule model",
                Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29200, Bottom = -29400 },
                SupportsBrightness = true, Brightness = 50, BrightnessMin = 0, BrightnessMax = 100,
            }];
        });
        var bar = Private<DeskCommandBar>(fixture.Form, "_bar");
        var tray = Private<NotifyIcon>(fixture.Form, "_tray");
        var status = Private<StatusStripe>(fixture.Form, "_status");
        Display Monitor() => Controls(fixture.Form).OfType<MonitorCard>().Single().Monitor;
        void Check() => typeof(MainForm).GetMethod("CheckSchedule", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, null);
        Task Refresh() => (Task)typeof(MainForm).GetMethod("RebuildAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, [null]);
        void Requested(int writes, int nits, string preset, string name)
        {
            PumpUntil(() => fixture.Transport.Writes == writes, name);
            Assert(fixture.Transport.LastValue == Presets.BrightnessFor(Monitor(), nits) && bar.ModeButtons[preset].Primary &&
                status.AccessibleName.StartsWith($"Schedule: {preset}:"), name + " requests the scheduled target and says the schedule did it");
        }

        Requested(1, 150, "Evening", "the first ready scan of a launch");
        Assert(tray.Text == $"LumenDeck {AboutForm.VersionText} - Evening until 21:30", "the notification icon names the version, the scheduled preset and its end");
        Check();
        Assert(fixture.Transport.Writes == 1, "a period is requested once, not on every check");
        Controls(fixture.Form).OfType<MonitorCard>().Single().ApplyBrightness(80);
        PumpUntil(() => fixture.Transport.Writes == 2, "manual adjustment");
        Check();
        Assert(fixture.Transport.Writes == 2 && fixture.Transport.LastValue == 80, "a manual change stays until the next period");
        var refresh = Refresh();
        PumpUntil(() => refresh.IsCompleted, "refresh");
        Assert(fixture.Transport.Writes == 2, "Refresh does not repeat a period that was already requested");

        now = new DateTime(2026, 10, 9, 21, 31, 0);
        Check();
        Requested(3, 110, "Night", "a new period");
        now = new DateTime(2026, 10, 10, 3, 0, 0);
        Check();
        Assert(fixture.Transport.Writes == 3, "the period that began yesterday evening is not repeated after midnight");

        now = new DateTime(2026, 10, 10, 7, 0, 0);
        scanGate.Reset();
        refresh = Refresh();
        Check();
        Assert(fixture.Transport.Writes == 3, "a period that begins during a scan waits instead of writing to displays being replaced");
        scanGate.Set();
        PumpUntil(() => refresh.IsCompleted, "deferred scan");
        Requested(4, 200, "Day", "the completed scan requests the deferred period once");

        var menuItem = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu").Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Preset schedule…");
        Assert(menuItem.Checked && menuItem.ToolTipText.Contains("On: Day since 07:00; Evening at 18:00"),
            "Settings shows that the schedule is on and what it does next");
        Exception failure = null;
        int stage = 0;
        var timeout = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 15 };
        PresetScheduleView View() => Controls(fixture.ScheduleEditor).OfType<PresetScheduleView>().Single();
        string Preview() => Controls(View()).OfType<Label>().Single(l => l.AccessibleName == "Schedule preview").Text;
        FlatButton Save() => Controls(View()).OfType<FlatButton>().Single(b => b.Text == "Save schedule");
        CheckBox Enabled() => Controls(View()).OfType<CheckBox>().Single(c => c.AccessibleName == "Change presets automatically");
        bool Closed() => Private<PresetScheduleForm>(fixture.Form, "_scheduleEditor") == null;
        PresetScheduleForm first = null;
        timer.Tick += (_, _) =>
        {
            try
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Schedule editor scenario did not settle");
                var editor = fixture.ScheduleEditor;
                if (stage == 0 && editor?.Visible == true)
                {
                    first = editor;
                    Assert(editor.Modal && Preview().Contains("Day was already requested for this period"),
                        "the editor previews from the window's own request state");
                    // Time passes while the dialog is open; the window's timer still checks.
                    now = new DateTime(2026, 10, 10, 18, 1, 0);
                    Check();
                    stage = 1;
                }
                else if (stage == 1 && fixture.Transport.Writes == 5)
                {
                    Assert(Preview().StartsWith("Now: Evening (since 18:00)") && Preview().Contains("Evening was already requested"),
                        "a period requested while the editor is open refreshes its preview instead of being promised twice");
                    Enabled().Checked = false;
                    failNext = true;
                    Save().PerformClick();
                    stage = 2;
                }
                else if (stage == 2 && !editor.IsSaving && Save().Enabled)
                {
                    Assert(editor.Visible && fixture.Settings.Schedule.Enabled, "an unconfirmed save leaves the schedule on");
                    Save().PerformClick();
                    stage = 3;
                }
                else if (stage == 3 && Closed())
                {
                    Assert(!fixture.Settings.Schedule.Enabled && tray.Text == $"LumenDeck {AboutForm.VersionText}" && !menuItem.Checked && fixture.Transport.Writes == 5,
                        "a confirmed save turns the schedule off without adjusting displays");
                    now = new DateTime(2026, 10, 10, 21, 40, 0);
                    Check();
                    Assert(fixture.Transport.Writes == 5, "an off schedule requests nothing when a period begins");
                    scanGate.Reset();
                    refresh = Refresh();
                    fixture.Form.BeginInvoke((Action)(() => menuItem.PerformClick()));
                    stage = 4;
                }
                else if (stage == 4 && editor?.Visible == true && !ReferenceEquals(editor, first))
                {
                    Enabled().Checked = true;
                    Assert(Preview().Contains("Displays are not ready, so saving requests Night once they are"),
                        "turning the schedule on during a scan promises a deferred request, not an immediate one");
                    Save().PerformClick();
                    stage = 5;
                }
                else if (stage == 5 && Closed())
                {
                    Assert(fixture.Settings.Schedule.Enabled && fixture.Transport.Writes == 5 &&
                        status.AccessibleName.Contains("Night is requested once displays are ready"),
                        "saving during a scan defers the request and says so");
                    scanGate.Set();
                    timer.Stop(); stage = 6;
                }
            }
            catch (Exception ex)
            {
                failure = ex; timer.Stop(); stage = 6; scanGate.Set();
                if (fixture.ScheduleEditor is { IsDisposed: false } open) { open.DialogResult = DialogResult.Cancel; open.Dispose(); }
            }
        };
        timer.Start();
        fixture.Form.BeginInvoke((Action)(() => menuItem.PerformClick()));
        PumpUntil(() => stage == 6, "schedule editor scenario", 25000);
        if (failure != null) throw new InvalidOperationException("Schedule editor assertion failed", failure);
        PumpUntil(() => refresh.IsCompleted, "scan after enabling");
        Requested(6, 110, "Night", "the scan completed after enabling");
        Assert(AppSettings.Load(fixture.SettingsPath).Schedule.Enabled, "the confirmed schedule is the one on disk");
    }
}
