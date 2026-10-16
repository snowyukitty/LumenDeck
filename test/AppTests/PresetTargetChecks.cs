using System.Diagnostics;
using System.Text.Json;
using LumenDeck;

internal static partial class Program
{
    /// <summary>
    /// Actual MainForm ownership of preset targets: the modal editor, the
    /// background settings writer, activation only after a confirmed save, and
    /// presets requesting the new target afterwards.
    /// </summary>
    private static void CheckPresetTargetLifecycle(string directory)
    {
        CheckSavedPresetTargetsAtStartup(directory);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool failNext = false, gateNext = false;
        int writes = 0;
        using var fixture = new Fixture(directory, writeSettings: write =>
        {
            Interlocked.Increment(ref writes);
            if (failNext) { failNext = false; return SettingsSaveResult.Failed; }
            if (gateNext) { gateNext = false; entered.Set(); release.Wait(); }
            return write();
        });
        var bar = Private<DeskCommandBar>(fixture.Form, "_bar");
        var card = Controls(fixture.Form).OfType<MonitorCard>().Single();
        var menuItem = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu").Items
            .OfType<ToolStripMenuItem>().Single(i => i.Text == "Preset targets…");
        Assert(bar.ModeButtons["Night"].AccessibleDescription.Contains("60 nits") &&
            !bar.ModeButtons["Night"].AccessibleDescription.Contains("your target"),
            "desk preset help starts with the built-in target");
        void Open() => fixture.Form.BeginInvoke((Action)(() => menuItem.PerformClick()));
        PresetEditorView View() => Controls(fixture.PresetEditor).OfType<PresetEditorView>().Single();
        WholeNumberInput Input(string name) => Controls(View()).OfType<WholeNumberInput>().Single(i => i.AccessibleName == name);
        FlatButton Action(string text) => Controls(View()).OfType<FlatButton>().Single(b => b.Text == text);
        string Status() => Controls(View()).OfType<Label>().Single(l => l.AccessibleRole == AccessibleRole.Alert).Text;
        bool EditorClosed() => Private<PresetEditorForm>(fixture.Form, "_presetEditor") == null;

        Exception failure = null;
        int stage = 0;
        PresetEditorForm first = null;
        var timeout = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Preset target scenario did not settle");
                var editor = fixture.PresetEditor;
                if (stage == 0 && editor?.Visible == true)
                {
                    first = editor;
                    Assert(editor.Modal && !IsWindowEnabled(fixture.Form.Handle), "preset targets open as a modal editor owned by the window");
                    Assert(Controls(View()).OfType<Label>().Single(l => l.AccessibleName == "Night brightness requests").Text
                        .Contains(card.Monitor.DisplayName), "preview lists the connected display by its card name");
                    Input("Night target luminance (nits)").Value = 90;
                    Input("Night warmth (kelvin)").Value = 4300;
                    failNext = true;
                    Action("Save targets").PerformClick();
                    stage = 1;
                }
                else if (stage == 1 && writes == 1 && !editor.IsSaving)
                {
                    Assert(editor.Visible && editor.DialogResult == DialogResult.None && Status().Contains("Previous targets remain active"),
                        "an unconfirmed save keeps the editor open with its explanation");
                    Assert(fixture.Settings.PresetLevel("Night").Nits == 60 && fixture.Settings.PresetTargets.Count == 0,
                        "an unconfirmed save leaves the previous targets active");
                    gateNext = true;
                    Action("Save targets").PerformClick();
                    stage = 2;
                }
                else if (stage == 2 && entered.IsSet)
                {
                    Assert(fixture.Settings.PresetLevel("Night").Nits == 60, "targets stay inactive while their save is pending");
                    Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
                        .Single(i => i.Text == "Exit").PerformClick();
                    Assert(!fixture.Form.IsDisposed && editor.Visible && editor.IsSaving && !fixture.TrayDisposed &&
                        !Private<bool>(fixture.Form, "_reallyClosing"),
                        "tray Exit during a pending preset save is refused without leaving an exit in progress");
                    release.Set();
                    stage = 3;
                }
                else if (stage == 3 && EditorClosed())
                {
                    Assert(editor.DialogResult == DialogResult.OK && editor.IsDisposed && IsWindowEnabled(fixture.Form.Handle),
                        "a confirmed save closes the modal editor and re-enables the window");
                    Assert(fixture.Settings.PresetLevel("Night") is { Nits: 90, Kelvin: 4300 }, "confirmed targets become active");
                    using (var saved = JsonDocument.Parse(File.ReadAllText(fixture.SettingsPath)))
                        Assert(saved.RootElement.GetProperty("PresetTargets").EnumerateArray().Single() is var night &&
                            night.GetProperty("Nits").GetInt32() == 90 && night.GetProperty("Kelvin").GetInt32() == 4300,
                            "the confirmed targets are the ones on disk");
                    Assert(bar.ModeButtons["Night"].AccessibleDescription.Contains("90 nits") &&
                        bar.ModeButtons["Night"].AccessibleDescription.Contains("your target") &&
                        Controls(card).OfType<FlatButton>().Single(b => b.Name == "preset-Night").AccessibleDescription.Contains("90 nits and 4300K"),
                        "desk and card preset help describe the new active target");
                    Assert(fixture.Transport.Writes == 0, "editing and saving targets does not adjust displays");
                    bar.ModeButtons["Night"].PerformClick();
                    stage = 4;
                }
                else if (stage == 4 && fixture.Transport.Writes == 1)
                {
                    Assert(fixture.Transport.LastValue == Presets.BrightnessFor(card.Monitor, 90) && card.Monitor.Kelvin == 4300,
                        "the desk preset requests the saved target's brightness and warmth");
                    Assert(bar.ModeButtons["Night"].Primary, "the chosen preset is shown as the desk's mode");
                    Open();
                    stage = 5;
                }
                else if (stage == 5 && editor?.Visible == true && !ReferenceEquals(editor, first))
                {
                    Assert(Input("Night target luminance (nits)").Value == 90, "reopening shows the saved target");
                    Input("Night target luminance (nits)").Value = 95;
                    Action("Save targets").PerformClick();
                    stage = 6;
                }
                else if (stage == 6 && EditorClosed())
                {
                    Assert(!bar.ModeButtons["Night"].Primary && fixture.Settings.PresetLevel("Night").Nits == 95,
                        "changing the lit preset's target stops claiming the desk is at that preset");
                    Assert(fixture.Transport.Writes == 1, "saving a changed target does not reapply it");
                    Open();
                    stage = 7;
                }
                else if (stage == 7 && editor?.Visible == true && fixture.PresetEditorCreations == 3)
                {
                    Assert(Input("Night target luminance (nits)").Value == 95, "a later editor opens with the latest saved target");
                    Input("Night target luminance (nits)").Value = 20;
                    Action("Cancel").PerformClick();
                    stage = 8;
                }
                else if (stage == 8 && EditorClosed())
                {
                    Assert(fixture.Settings.PresetLevel("Night").Nits == 95 && fixture.PresetEditorCreations == 3,
                        "Cancel discards the draft and keeps the saved targets");
                    Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
                        .Single(i => i.Text == "Exit").PerformClick();
                    Assert(fixture.Form.IsDisposed && fixture.TrayDisposed, "a later tray Exit closes the window normally");
                    using (var saved = JsonDocument.Parse(File.ReadAllText(fixture.SettingsPath)))
                        Assert(saved.RootElement.GetProperty("PresetTargets").EnumerateArray().Single().GetProperty("Nits").GetInt32() == 95,
                            "the final exit save keeps the confirmed targets");
                    timer.Stop(); stage = 9;
                }
            }
            catch (Exception ex)
            {
                failure = ex; release.Set(); timer.Stop(); stage = 9;
                if (fixture.PresetEditor is { IsDisposed: false } open) { open.DialogResult = DialogResult.Cancel; open.Dispose(); }
            }
        };
        try
        {
            timer.Start(); Open();
            PumpUntil(() => stage == 9, "preset target scenario", 25000);
            if (failure != null) throw new InvalidOperationException("Preset target assertion failed", failure);
        }
        finally { release.Set(); }
    }

    private static void CheckSavedPresetTargetsAtStartup(string directory)
    {
        using var fixture = new Fixture(directory, settingsFactory: path =>
        {
            var settings = new AppSettings(path);
            settings.PresetTargets = settings.WithPresetLevels([Presets.ByName("Evening") with { Nits = 140, Kelvin = 5200 }]);
            return settings;
        });
        var bar = Private<DeskCommandBar>(fixture.Form, "_bar");
        var card = Controls(fixture.Form).OfType<MonitorCard>().Single();
        Assert(bar.ModeButtons["Evening"].AccessibleDescription.Contains("140 nits and 5200K") &&
            Controls(card).OfType<FlatButton>().Single(b => b.Name == "preset-Evening").AccessibleDescription.Contains("140 nits and 5200K") &&
            bar.ModeButtons["Day"].AccessibleDescription.Contains("160 nits"),
            "saved targets describe the desk and card presets from the first scan");
        Controls(card).OfType<FlatButton>().Single(b => b.Name == "preset-Evening").PerformClick();
        PumpUntil(() => fixture.Transport.Writes == 1, "per-monitor preset request");
        Assert(fixture.Transport.LastValue == Presets.BrightnessFor(card.Monitor, 140) && card.Monitor.Kelvin == 5200,
            "a per-monitor preset requests the saved target");
    }
}
