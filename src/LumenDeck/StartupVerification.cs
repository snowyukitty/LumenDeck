using System.Reflection;

namespace LumenDeck;

/// <summary>Bounded GUI runtime probe for extracted packages; no real desk or user state.</summary>
internal static class StartupVerification
{
    public static int Run()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            using var iconStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("LumenDeck.assets.LumenDeck.ico");
            if (iconStream == null) return 3;
            using var icon = new Icon(iconStream);
            using var form = new VerificationForm { Icon = icon };
            using var commands = new DeskCommandBar();
            using var guide = new ConnectionGuide { Dock = DockStyle.Fill };
            guide.SetMonitors(Array.Empty<Monitor>());
            form.Controls.Add(guide);
            form.Controls.Add(commands);
            int result = 3;
            form.Shown += (_, _) =>
            {
                try
                {
                    using var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                    form.DrawToBitmap(image, form.ClientRectangle);
                    using var report = new SupportReportView { Dock = DockStyle.Fill };
                    report.SetReport(SupportReport.Create(Array.Empty<Monitor>(), DisplayScanState.Ready,
                        Cli.VersionText, Environment.OSVersion.Version));
                    form.Controls.Add(report);
                    report.BringToFront();
                    report.DrawToBitmap(image, report.ClientRectangle);
                    string probePath = Path.Combine(Path.GetTempPath(), "LumenDeck-profile-probe-" + Guid.NewGuid().ToString("N") + ".json");
                    using var editor = new ProfileEditorView(PanelProfileStore.Open(probePath, "Synthetic display", false),
                        new ProfileEditorContext("Synthetic display", false, 50)) { Dock = DockStyle.Fill };
                    form.Controls.Add(editor);
                    editor.BringToFront();
                    editor.DrawToBitmap(image, editor.ClientRectangle);
                    // Its save delegate refuses: the probe must never write settings.
                    using var presets = new PresetEditorView(new PresetEditorContext(Presets.Levels,
                        [new PresetPreviewDisplay("Synthetic display", PanelDatabase.FallbackFor(false), false)]),
                        _ => Task.FromResult(SettingsSaveResult.Unavailable)) { Dock = DockStyle.Fill };
                    form.Controls.Add(presets);
                    presets.BringToFront();
                    presets.DrawToBitmap(image, presets.ClientRectangle);
                    using var schedule = new PresetScheduleView(new ScheduleEditorContext(false,
                        [.. PresetSchedule.Names.Select(n => new ScheduleRowDraft(n, false, PresetSchedule.DefaultStarts[n]))],
                        Presets.Levels),
                        slots => new ScheduleOutlook(PresetSchedule.At(slots, new DateTime(2000, 1, 1, 12, 0, 0)), false, false),
                        _ => Task.FromResult(SettingsSaveResult.Unavailable)) { Dock = DockStyle.Fill };
                    form.Controls.Add(schedule);
                    schedule.BringToFront();
                    schedule.DrawToBitmap(image, schedule.ClientRectangle);
                    using var settings = new PreferencesMenu();
                    settings.Synchronize(new PreferenceState(null, true, true, false));
                    settings.PerformLayout();
                    settings.CreateControl();
                    using var settingsImage = new Bitmap(settings.Width, settings.Height);
                    settings.DrawToBitmap(settingsImage, settings.ClientRectangle);
                    string channel = "LumenDeck.PackageProbe." + Guid.NewGuid().ToString("N");
                    int reveals = 0;
                    using var activation = new InstanceActivation(() => { Interlocked.Increment(ref reveals); return true; }, channel);
                    if (!activation.Start() ||
                        !InstanceActivation.RequestAsync(channel, 2000, _ => { }).GetAwaiter().GetResult() ||
                        Volatile.Read(ref reveals) != 1) return;
                    result = 0;
                }
                finally { form.BeginInvoke((Action)form.Close); }
            };
            // A WinForms exception must fail this probe without opening a modal
            // dialog in a CI runner or writing a user's error log.
            System.Threading.ThreadExceptionEventHandler failure = (_, _) => { result = 3; form.Close(); };
            Application.ThreadException += failure;
            try { Application.Run(form); }
            finally { Application.ThreadException -= failure; }
            return result;
        }
        catch { return 3; }
    }

    private sealed class VerificationForm : Form
    {
        public VerificationForm()
        {
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(780, 620);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
            ShowInTaskbar = false;
        }
        protected override bool ShowWithoutActivation => true;
    }
}
