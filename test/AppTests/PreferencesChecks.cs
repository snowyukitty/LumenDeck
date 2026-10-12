using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using LumenDeck;

internal static partial class Program
{
    private static void CheckPreferences(string directory)
    {
        Assert(StartupEntry.ReadState(() => null) == false && StartupEntry.ReadState(() => "") == false &&
            StartupEntry.ReadState(() => "synthetic command") == true,
            "startup adapter distinguishes absent and present registration without native registry access");
        Assert(StartupEntry.ReadState(() => throw new IOException("synthetic registry refusal")) == null,
            "startup read failure remains unknown instead of confirmed disabled");
        using var presentation = new PreferencesMenu();
        int requests = 0;
        presentation.ChangeRequested += (_, _) => requests++;
        presentation.Synchronize(new PreferenceState(false, false, true, true));
        MenuChoice(presentation, "Start with Windows").PerformClick();
        Assert(requests == 1 && !MenuChoice(presentation, "Start with Windows").Checked,
            "menu requests one change without optimistically toggling a check");
        presentation.Synchronize(new PreferenceState(null, true, false, false));
        Assert(!MenuChoice(presentation, "Start with Windows").Enabled &&
            MenuChoice(presentation, "Start with Windows").AccessibleDescription.Contains("not confirmed disabled"),
            "unavailable startup state is explicit and cannot be toggled");
        MenuChoice(presentation, "Start with Windows").PerformClick();
        Assert(requests == 1 && MenuChoice(presentation, "Start minimised").AccessibleDescription.Contains("taskbar"),
            "unknown action is guarded and minimised explanation reflects taskbar behavior");

        using var fixture = new Fixture(directory);
        var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
        var tray = Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip;
        Assert(Controls(fixture.Form).OfType<FlatButton>().Any(b => b.Text == "Settings" && b.Enabled),
            "actual MainForm exposes a reachable Settings command");
        int repairs = fixture.StartupRepairs;
        int gamma = fixture.GammaSaves;
        OpenPreferenceObservation(menu);
        Assert(fixture.StartupWrites == 0 && fixture.StartupRepairs == repairs && fixture.Transport.Writes == 0,
            "observing settings requests no registration repair or monitor write");
        MenuChoice(menu, "Start with Windows").PerformClick();
        Assert(fixture.StartupWrites == 1 && MenuChoice(menu, "Start with Windows").Checked &&
            MenuChoice(tray, "Start with Windows").Checked,
            "accepted startup change synchronizes main and tray checks once");
        MenuChoice(tray, "Start with Windows").PerformClick();
        Assert(fixture.StartupWrites == 2 && !MenuChoice(menu, "Start with Windows").Checked,
            "tray startup action updates the main Settings surface");
        fixture.RejectStartupWrite = true;
        MenuChoice(menu, "Start with Windows").PerformClick();
        Assert(fixture.StartupWrites == 3 && !MenuChoice(menu, "Start with Windows").Checked &&
            !MenuChoice(tray, "Start with Windows").Checked,
            "refused registry write keeps both surfaces at the observed state");
        Assert(Private<StatusStripe>(fixture.Form, "_status").AccessibleName.Contains("not confirmed"),
            "startup refusal is visible in accessible status rather than an unexplained check");
        fixture.RejectStartupWrite = false;
        fixture.StartupWriteMismatch = true;
        MenuChoice(menu, "Start with Windows").PerformClick();
        Assert(fixture.StartupWrites == 4 && !MenuChoice(menu, "Start with Windows").Checked,
            "accepted API result with readback disagreement cannot claim enabled startup");
        fixture.StartupWriteMismatch = false;
        fixture.StartupReadFailsAfterWrite = true;
        MenuChoice(menu, "Start with Windows").PerformClick();
        Assert(fixture.StartupWrites == 5 && !MenuChoice(menu, "Start with Windows").Enabled,
            "lost registration readback after accepted write is unknown rather than disabled success");
        OpenPreferenceObservation(menu);
        Assert(!MenuChoice(menu, "Start with Windows").Enabled && !MenuChoice(tray, "Start with Windows").Enabled,
            "read failure disables startup on both menu surfaces");
        MenuChoice(menu, "Start with Windows").PerformClick();
        MenuChoice(tray, "Start with Windows").PerformClick();
        Assert(fixture.StartupWrites == 5, "unknown registration state cannot request a write through either surface");
        fixture.StartupReadFailsAfterWrite = false;
        fixture.StartupState = false;
        OpenPreferenceObservation(menu);

        MenuChoice(menu, "Start minimised").PerformClick();
        SettleSettings(fixture);
        Assert(fixture.Settings.StartMinimised && MenuChoice(tray, "Start minimised").Checked &&
            fixture.Form.Visible && fixture.Form.WindowState == FormWindowState.Normal,
            "start-minimised preference is saved for next launch without hiding the current window");
        MenuChoice(tray, "Start minimised").PerformClick();
        SettleSettings(fixture);
        Assert(!fixture.Settings.StartMinimised && !MenuChoice(menu, "Start minimised").Checked,
            "tray minimised action synchronizes Settings without recursive persistence");
        MenuChoice(menu, "Keep running").PerformClick();
        SettleSettings(fixture);
        Assert(fixture.Settings.MinimiseToTray && MenuChoice(menu, "Start minimised").AccessibleDescription.Contains("notification area"),
            "keep-running preference explains its interaction with minimised startup");
        MenuChoice(menu, "Reapply saved warmth").PerformClick();
        SettleSettings(fixture);
        Assert(fixture.Settings.ReapplyColourOnStart && fixture.GammaSaves == gamma && fixture.Transport.Writes == 0,
            "warmth reapplication preference does not adjust gamma or hardware now");
        using (var saved = JsonDocument.Parse(File.ReadAllText(fixture.SettingsPath)))
        {
            Assert(saved.RootElement.GetProperty("MinimiseToTray").GetBoolean() &&
                saved.RootElement.GetProperty("ReapplyColourOnStart").GetBoolean() &&
                !saved.RootElement.GetProperty("StartMinimised").GetBoolean(),
                "successful preference operations persist the current flags");
            var monitor = saved.RootElement.GetProperty("Monitors")[0];
            Assert(monitor.GetProperty("Key").GetString() == "synthetic-1" &&
                monitor.GetProperty("CustomBrightnessPercent").GetDouble() == 50,
                "preference changes preserve seeded monitor identity and Custom levels");
        }
        CheckPreferenceSaveFailure(directory);
        CheckSettingsPreferenceStorage(directory);
        CheckMicaPreference(directory);
    }

    private static void CheckMicaPreference(string directory)
    {
        Assert(!WindowChrome.SupportsMica(new Version(10, 0, 19045), false) &&
            !WindowChrome.SupportsMica(new Version(10, 0, 22000), false) &&
            WindowChrome.SupportsMica(new Version(10, 0, 22621), false) &&
            !WindowChrome.SupportsMica(new Version(10, 0, 26100), true),
            "Mica requires the documented Windows build and avoids high contrast");
        using (var fixture = new Fixture(directory))
        {
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            var choice = MenuChoice(menu, "Use Windows Mica");
            var list = Private<FlowLayoutPanel>(fixture.Form, "_list");
            // Drain the independent startup checkpoint before measuring Mica's
            // effects. Bitmap comparison can take longer than its timer on CI.
            var saveTimer = Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer");
            typeof(System.Windows.Forms.Timer).GetMethod("OnTick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(saveTimer, [EventArgs.Empty]);
            SettleSettings(fixture);
            Assert(!saveTimer.Enabled, "initial delayed checkpoint is settled before Mica side-effect observation");
            int writes = fixture.Transport.Writes, gamma = fixture.GammaSaves, startup = fixture.StartupWrites;
            using var before = new Bitmap(list.Width, list.Height);
            list.DrawToBitmap(before, list.ClientRectangle);
            Assert(!fixture.Settings.UseMicaTitleBar && !choice.Checked, "Mica title bar is opt-in for existing settings");
            choice.PerformClick();
            SettleSettings(fixture);
            Assert(fixture.Settings.UseMicaTitleBar && choice.Checked && AppSettings.Load(fixture.SettingsPath).UseMicaTitleBar,
                "Mica activates only after its preference is saved and survives reload");
            if (WindowChrome.SupportsMica(Environment.OSVersion.Version, SystemInformation.HighContrast))
                Assert(WindowChrome.BackdropType(fixture.Form) == 2, "DWM reports the requested main-window backdrop intent");
            using var after = new Bitmap(list.Width, list.Height);
            list.DrawToBitmap(after, list.ClientRectangle);
            bool same = true;
            for (int y = 0; same && y < before.Height; y++)
                for (int x = 0; same && x < before.Width; x++)
                    same = before.GetPixel(x, y) == after.GetPixel(x, y);
            Assert(same, "Mica does not alter the opaque client control rendering");
            choice.PerformClick();
            SettleSettings(fixture);
            Assert(!fixture.Settings.UseMicaTitleBar && !choice.Checked && !AppSettings.Load(fixture.SettingsPath).UseMicaTitleBar,
                "Mica can be disabled and the preference persists");
            if (Environment.OSVersion.Version.Build >= 22621)
                Assert(WindowChrome.BackdropType(fixture.Form) == 1, "disabling Mica clears DWM's backdrop request");
            Assert(fixture.Transport.Writes == writes && fixture.GammaSaves == gamma && fixture.StartupWrites == startup,
                "title-bar preferences perform no display, gamma or startup-registration writes");
        }
        using (var refused = new Fixture(directory, refuseSettingsSave: true))
        {
            var choice = MenuChoice(Private<PreferencesMenu>(refused.Form, "_preferencesMenu"), "Use Windows Mica");
            int? before = WindowChrome.BackdropType(refused.Form);
            choice.PerformClick();
            SettleSettings(refused);
            Assert(!refused.Settings.UseMicaTitleBar && !choice.Checked && WindowChrome.BackdropType(refused.Form) == before,
                "refused Mica save preserves the previous preference and native frame");
        }
    }

    private static void CheckPreferenceSaveFailure(string directory)
    {
        using var fixture = new Fixture(directory, refuseSettingsSave: true);
        var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
        var tray = Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip;
        int count = fixture.Settings.Monitors.Count;
        MenuChoice(menu, "Start minimised").PerformClick();
        SettleSettings(fixture);
        MenuChoice(menu, "Keep running").PerformClick();
        SettleSettings(fixture);
        MenuChoice(menu, "Reapply saved warmth").PerformClick();
        SettleSettings(fixture);
        Assert(!fixture.Settings.StartMinimised && !fixture.Settings.MinimiseToTray && !fixture.Settings.ReapplyColourOnStart,
            "failed JSON saves roll back only the changed runtime preference");
        Assert(Private<StatusStripe>(fixture.Form, "_status").AccessibleName.Contains("Previous preference remains active"),
            "failed preference persistence explains the retained runtime state through accessible status");
        Assert(!MenuChoice(menu, "Start minimised").Checked && !MenuChoice(tray, "Start minimised").Checked &&
            !MenuChoice(menu, "Keep running").Checked && !MenuChoice(menu, "Reapply saved warmth").Checked,
            "failed JSON saves never leave a misleading check on either menu");
        Assert(fixture.Settings.Monitors.Count == count && fixture.Settings.Monitors[0].CustomBrightnessPercent == 50 &&
            !File.Exists(fixture.SettingsPath) && fixture.Transport.Writes == 0,
            "refused settings storage preserves unrelated runtime records and issues no monitor writes");
    }

    private static ToolStripMenuItem MenuChoice(ContextMenuStrip menu, string prefix) =>
        menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text.StartsWith(prefix, StringComparison.Ordinal));
    private static void OpenPreferenceObservation(PreferencesMenu menu) =>
        typeof(ToolStripDropDown).GetMethod("OnOpening", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(menu, [new CancelEventArgs()]);

    private static void RenderPreferences(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (float scale in new[] { 1f, 2f })
        foreach (var state in new[] { SettingsSaveState.None, SettingsSaveState.Failed, SettingsSaveState.SourceChanged, SettingsSaveState.Waiting })
        {
            using var font = new Font(Theme.Body.FontFamily, Theme.Body.Size * scale);
            using var menu = new PreferencesMenu { Font = font };
            menu.Synchronize(new PreferenceState(true, false, true, true));
            menu.SetSaveState(state);
            menu.PerformLayout();
            menu.CreateControl();
            using var image = new Bitmap(menu.Width, menu.Height);
            menu.DrawToBitmap(image, menu.ClientRectangle);
            image.Save(Path.Combine(directory, $"settings-menu-{state}-font-{scale:0}.png"));
        }
    }
}
