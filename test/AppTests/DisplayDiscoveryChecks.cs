using System.Reflection;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static FlatButton DiscoveryButton(DisplayScanView view, string text) =>
        Controls(view).OfType<FlatButton>().Single(b => b.Text == text);
    private static SupportReport.Document HelpReport(ConnectionHelpForm help)
    {
        var guide = (ConnectionGuide)typeof(ConnectionHelpForm).GetField("_guide", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(help);
        return (SupportReport.Document)typeof(ConnectionGuide).GetField("_report", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(guide);
    }
    private static Display DiscoveryDisplay() => new()
    {
        StableKey = "discovery-synthetic", Description = "Example discovery display", PositionLabel = "primary", IsPrimary = true,
        Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29200, Bottom = -29400 },
        SupportsBrightness = true, Brightness = 50, BrightnessMin = 0, BrightnessMax = 100,
    };

    private static void CheckDisplayDiscovery(string directory, string renderDirectory)
    {
        CheckReadingDiscovery(directory, renderDirectory);
        using var fixture = new Fixture(directory, enumerate: scan => scan switch
        {
            1 => [],
            2 => throw new IOException("synthetic private scan detail"),
            _ => [DiscoveryDisplay()],
        }, waitForCards: false);
        PumpUntil(() => Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Ready, "empty scan completes");
        var empty = Private<DisplayScanView>(fixture.Form, "_scanView");
        Assert(empty.AccessibleName == "No displays found" && DiscoveryButton(empty, "Refresh").Enabled &&
            !Private<LayoutMap>(fixture.Form, "_map").Visible && !Controls(fixture.Form).OfType<MonitorCard>().Any(),
            "completed empty scan exposes recovery actions instead of a map or monitor cards");
        CheckDiscoveryLayout(fixture, empty, renderDirectory, "empty");
        DiscoveryButton(empty, "Connection help").PerformClick();
        var help = Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp");
        Assert(HelpReport(help).ScanState == "completed" && HelpReport(help).Displays.Count == 0 && fixture.Scans == 1,
            "empty-state help receives a completed zero-display report without another scan");
        DiscoveryButton(empty, "Refresh").PerformClick();
        PumpUntil(() => Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Failed, "failed retry settles");
        var failed = Private<DisplayScanView>(fixture.Form, "_scanView");
        Assert(empty.IsDisposed && failed.AccessibleName == "Could not read your displays" &&
            !failed.AccessibleDescription.Contains("private scan detail") && DiscoveryButton(failed, "Refresh").Enabled &&
            !Private<StatusStripe>(fixture.Form, "_status").AccessibleName.Contains("private scan detail") &&
            HelpReport(help).ScanState == "failed" && HelpReport(help).Displays.Count == 0,
            "failed scan replaces its old view, preserves private error details and updates open help");
        CheckDiscoveryLayout(fixture, failed, renderDirectory, "failed");
        DiscoveryButton(failed, "Refresh").PerformClick();
        PumpUntil(() => Controls(fixture.Form).OfType<MonitorCard>().Any(), "discovery retry finds display");
        var commands = Private<DeskCommandBar>(fixture.Form, "_bar");
        Assert(failed.IsDisposed && !Controls(fixture.Form).OfType<DisplayScanView>().Any() &&
            commands.ModeButtons.Values.All(b => b.Enabled) && commands.IdentifyButton.Enabled && commands.WarmthOffButton.Enabled &&
            HelpReport(help).ScanState == "completed" && HelpReport(help).Displays.Count == 1,
            "successful retry returns live controls and replaces stale help inventory");
        Assert(fixture.Scans == 3 && fixture.Transport.Writes == 0 && fixture.StartupWrites == 0,
            "discovery recovery does not invent brightness or registration writes");
        help.Close();
        CheckClosedDiscovery(directory);
        CheckSupersededDiscovery(directory);
    }

    private static void CheckReadingDiscovery(string directory, string renderDirectory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, enumerate: _ =>
        {
            entered.Set();
            if (!release.Wait(10000)) throw new TimeoutException("discovery barrier");
            return [DiscoveryDisplay()];
        }, waitForCards: false);
        try
        {
            PumpUntil(() => entered.IsSet, "discovery reading starts");
            var view = Private<DisplayScanView>(fixture.Form, "_scanView");
            var commands = Private<DeskCommandBar>(fixture.Form, "_bar");
            var tray = Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip;
            Assert(view.AccessibleName == "Reading your displays" && !DiscoveryButton(view, "Refresh").Enabled &&
                commands.ModeButtons.Values.All(b => !b.Enabled) && !commands.IdentifyButton.Enabled && !commands.WarmthOffButton.Enabled &&
                !MenuChoice(tray, "Day").Enabled && !MenuChoice(tray, "Custom").Enabled && !MenuChoice(tray, "Warmth off").Enabled &&
                MenuChoice(tray, "Exit").Enabled && commands.SettingsButton.Enabled,
                "reading state disables monitor adjustments in both surfaces while preserving Settings and Exit");
            DiscoveryButton(view, "Refresh").PerformClick();
            typeof(MainForm).GetMethod("WarmthOff", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, null);
            typeof(MainForm).GetMethod("ApplyLevel", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(string)]).Invoke(fixture.Form, [Presets.Levels[0].Name]);
            Assert(fixture.Scans == 1 && fixture.Transport.Writes == 0 && !File.Exists(fixture.SettingsPath),
                "disabled refresh and guarded adjustment dispatch do not enqueue scans, writes or settings saves");
            CheckDiscoveryLayout(fixture, view, renderDirectory, "reading");
            DiscoveryButton(view, "Connection help").PerformClick();
            var help = Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp");
            Assert(HelpReport(help).ScanState == "reading" && HelpReport(help).Displays.Count == 0 && fixture.Scans == 1,
                "help opens during reading with unknown current availability and no automatic retry");
            release.Set();
            PumpUntil(() => Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Ready, "reading scan settles");
            Assert(view.IsDisposed && HelpReport(help).ScanState == "completed" && HelpReport(help).Displays.Count == 1,
                "successful reading disposes guidance and updates the already open help report");
            help.Close();
        }
        finally { release.Set(); }
    }

    private static void CheckDiscoveryLayout(Fixture fixture, DisplayScanView view, string directory, string state)
    {
        foreach (var size in new[] { new Size(604, 441), new Size(780, 820) })
        {
            fixture.Form.ClientSize = size;
            fixture.Form.PerformLayout();
            var list = Private<FlowLayoutPanel>(fixture.Form, "_list");
            Assert(!list.HorizontalScroll.Visible && view.Right <= list.ClientSize.Width - list.Padding.Right,
                $"{state} guidance has no horizontal scrolling after resize (view {view.Bounds}, viewport {list.ClientSize}, display {list.DisplayRectangle})");
            foreach (var button in Controls(view).OfType<FlatButton>())
            {
                list.ScrollControlIntoView(button);
                var bounds = new Rectangle(list.PointToClient(button.PointToScreen(Point.Empty)), button.Size);
                Assert(list.ClientRectangle.Contains(bounds), $"{state} action {button.Text} is reachable at {size.Width}x{size.Height}");
            }
            Assert(Controls(view).OfType<Label>().All(l => l.Width <= view.ClientSize.Width - view.Padding.Horizontal),
                $"{state} guidance wraps within its pinned card width");
            list.AutoScrollPosition = Point.Empty;
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new Bitmap(fixture.Form.Width, fixture.Form.Height);
                fixture.Form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(directory, $"discovery-{state}-{size.Width}-{size.Height}.png"));
            }
        }
    }

    private static void CheckDiscoveryTextLayout(string renderDirectory)
    {
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        foreach (int width in new[] { 300, 520 })
        {
            using var host = new QuietLoopForm { AutoScaleMode = AutoScaleMode.None, ClientSize = new Size(width, 260) };
            using var list = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false,
                FlowDirection = FlowDirection.TopDown, Padding = new Padding(8) };
            using var view = new DisplayScanView(DisplayScanState.Reading);
            host.Controls.Add(list);
            list.Controls.Add(view);
            view.SetWidth(width - list.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            var originals = Controls(view).Prepend(view).Distinct().ToDictionary(c => c, c => c.Font);
            var fonts = originals.Values.Distinct().ToDictionary(font => font,
                font => new Font(font.FontFamily, font.Size * scale, font.Style));
            int refreshes = 0, helps = 0;
            view.RefreshRequested += () => refreshes++;
            view.ConnectionHelpRequested += () => helps++;
            try
            {
                foreach (var pair in originals) pair.Key.Font = fonts[pair.Value];
                host.Show();
                foreach (var state in new[] { DisplayScanState.Reading, DisplayScanState.Ready, DisplayScanState.Failed })
                {
                    view.SetState(state);
                    host.PerformLayout(); list.PerformLayout(); view.PerformLayout();
                    foreach (var button in Controls(view).OfType<FlatButton>())
                    {
                        int textHeight = TextRenderer.MeasureText(button.Text, button.Font,
                            new Size(Math.Max(1, button.ClientSize.Width - 1), int.MaxValue),
                            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                        Assert(button.ClientSize.Height >= textHeight,
                            $"{state} discovery {button.Text} label fits at text scale {scale}, width {width}: button {button.Size}, text height {textHeight}");
                        list.ScrollControlIntoView(button);
                        var bounds = new Rectangle(list.PointToClient(button.PointToScreen(Point.Empty)), button.Size);
                        Assert(list.ClientRectangle.Contains(bounds) && !list.HorizontalScroll.Visible,
                            $"{state} discovery {button.Text} is fully reachable without horizontal scroll at text scale {scale}, width {width}");
                    }
                    int priorRefreshes = refreshes;
                    DiscoveryButton(view, "Refresh").PerformClick();
                    DiscoveryButton(view, "Connection help").PerformClick();
                    Assert(refreshes == priorRefreshes + (state == DisplayScanState.Reading ? 0 : 1),
                        $"{state} large-text recovery only dispatches an enabled Refresh");
                    if (renderDirectory != null && state == DisplayScanState.Ready)
                    {
                        Directory.CreateDirectory(renderDirectory);
                        list.AutoScrollPosition = Point.Empty;
                        using var bitmap = new Bitmap(view.Width, view.Height);
                        view.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(renderDirectory, $"discovery-text-{scale}-width-{width}.png"));
                    }
                }
                Assert(helps == 3 && refreshes == 2, "large-text discovery keeps help available in every scan state without automatic recovery");
            }
            finally
            {
                host.Dispose();
                foreach (var font in fonts.Values) font.Dispose();
            }
        }
    }

    private static void CheckClosedDiscovery(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var late = DiscoveryDisplay();
        using var fixture = new Fixture(directory, enumerate: scan =>
        {
            if (scan == 1) return [];
            entered.Set();
            if (!release.Wait(10000)) throw new TimeoutException("closed discovery barrier");
            return [late];
        }, waitForCards: false);
        try
        {
            PumpUntil(() => Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Ready, "closed discovery starts from an empty scan");
            var pending = (Task)typeof(MainForm).GetMethod("RebuildAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.Form, [null]);
            PumpUntil(() => entered.IsSet, "closed discovery reading starts");
            var view = Private<DisplayScanView>(fixture.Form, "_scanView");
            fixture.Form.Close();
            Assert(fixture.Form.IsDisposed && view.IsDisposed, "closing during discovery disposes the viewport state with its owner");
            release.Set();
            PumpUntil(() => pending.IsCompleted, "closed discovery settles its late result");
            Assert(!pending.IsFaulted && Private<List<Display>>(fixture.Form, "_monitors").Count == 0 &&
                Private<List<MonitorCard>>(fixture.Form, "_cards").Count == 0 && fixture.Transport.Writes == 0,
                "late discovery completion never installs models, creates controls or changes hardware");
        }
        finally { release.Set(); }
    }

    private static void CheckSupersededDiscovery(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var older = DiscoveryDisplay();
        older.StableKey = "older discovery";
        var newer = DiscoveryDisplay();
        newer.StableKey = "current discovery";
        using var fixture = new Fixture(directory, enumerate: scan =>
        {
            if (scan == 1) return [];
            if (scan == 2)
            {
                entered.Set();
                if (!release.Wait(10000)) throw new TimeoutException("superseded discovery barrier");
                return [older];
            }
            return [newer];
        }, waitForCards: false);
        try
        {
            PumpUntil(() => Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Ready, "superseded fixture starts empty");
            var rebuild = typeof(MainForm).GetMethod("RebuildAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var pending = (Task)rebuild.Invoke(fixture.Form, [null]);
            PumpUntil(() => entered.IsSet, "older discovery starts");
            var oldView = Private<DisplayScanView>(fixture.Form, "_scanView");
            // A coalesced display-configuration event can supersede a slow scan
            // even though the user-facing Refresh actions are disabled.
            var current = (Task)rebuild.Invoke(fixture.Form, ["Synthetic display configuration changed."]);
            PumpUntil(() => current.IsCompleted, "newer discovery completes");
            Assert(oldView.IsDisposed && Controls(fixture.Form).OfType<MonitorCard>().Single().Monitor == newer,
                "newer discovery replaces the old reading view with current controls");
            release.Set();
            PumpUntil(() => pending.IsCompleted, "older discovery completion is discarded");
            Assert(!pending.IsFaulted && Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Ready &&
                Controls(fixture.Form).OfType<MonitorCard>().Single().Monitor == newer &&
                !Controls(fixture.Form).OfType<DisplayScanView>().Any() && fixture.Transport.Writes == 0,
                "older scan cannot replace current cards, scan feedback or command availability");
        }
        finally { release.Set(); }
    }
}
