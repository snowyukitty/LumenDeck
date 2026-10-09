using System.Diagnostics;
using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static void CheckConnectionHelpLifetime(string directory)
    {
        using var fixture = new Fixture(directory);
        Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer").Stop();
        var request = typeof(MainForm).GetMethod("ShowConnectionHelp", BindingFlags.Instance | BindingFlags.NonPublic);
        void Request() => request.Invoke(fixture.Form, null);
        int scans = fixture.Scans, registrations = fixture.ShortcutRegistrations, gamma = fixture.GammaSaves;
        byte[] before = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        fixture.Form.Hide();
        Request();
        Assert(fixture.ConnectionHelpCreations == 0,
            "hidden owners cannot open connection help through a queued/direct request");
        fixture.Form.Show();
        fixture.Form.Enabled = false; Request();
        Assert(fixture.ConnectionHelpCreations == 0, "disabled modal owners cannot open connection help");
        fixture.Form.Enabled = true;
        fixture.Form.WindowState = FormWindowState.Minimized; Request();
        Assert(fixture.ConnectionHelpCreations == 0, "minimized owners cannot open connection help");
        fixture.Form.WindowState = FormWindowState.Normal;
        var bar = Private<DeskCommandBar>(fixture.Form, "_bar");
        bar.HelpButton.PerformClick();
        var help = (QuietConnectionHelp)Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp");
        Assert(help.Visible && help.Owner == fixture.Form && fixture.ConnectionHelpCreations == 1,
            "Connection help opens one owned reference through the actual command bar");
        bar.HelpButton.PerformClick();
        Assert(fixture.ConnectionHelpCreations == 1 && help.RevealRequests == 1 &&
            Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp") == help,
            "repeated Connection help requests reveal the existing owned reference");
        help.Hide(); Request();
        Assert(help.Visible && help.RevealRequests == 2 && fixture.ConnectionHelpCreations == 1,
            "a hidden connection reference is revealed rather than duplicated");
        ConnectionKey(help, Keys.Escape); Request();
        var replacement = Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp");
        Assert(help.IsDisposed && replacement != help && replacement.Visible && fixture.ConnectionHelpCreations == 2,
            "closed connection help can be reopened");
        byte[] after = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        Assert(fixture.Scans == scans && fixture.Transport.Writes == 0 && fixture.ShortcutRegistrations == registrations &&
            fixture.GammaSaves == gamma && fixture.StartupWrites == 0 &&
            (before == null ? after == null : after != null && before.SequenceEqual(after)),
            "connection reference lifecycle does not request a scan, adjustment, registration or settings write");
        fixture.Dispose();
        Assert(replacement.IsDisposed, "owner exit disposes the owned connection reference");
        Request();
        Assert(fixture.ConnectionHelpCreations == 2, "disposed owners cannot reopen connection help");
        CheckReadingConnectionHelp(directory);
        CheckConnectionReportLifetime(directory);
    }

    private static void CheckConnectionReportLifetime(string directory)
    {
        foreach (string scenario in new[] { "close-preview", "hide-and-restore", "exit-owner" })
        {
            using var fixture = new Fixture(directory);
            Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer").Stop();
            var helpButton = Private<DeskCommandBar>(fixture.Form, "_bar").HelpButton;
            helpButton.PerformClick();
            var help = Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp");
            var reportButton = Controls(help).OfType<FlatButton>().Single(b => b.Text == "View support report");
            int scans = fixture.Scans, writes = fixture.Transport.Writes, startup = fixture.StartupWrites;
            for (int attempt = 0; attempt < (scenario == "exit-owner" ? 1 : 2); attempt++)
            {
                SupportReportForm preview = null;
                Exception failure = null;
                int stage = 0;
                bool observed = false;
                var deadline = Stopwatch.StartNew();
                using var timer = new System.Windows.Forms.Timer { Interval = 15 };
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        if (deadline.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Support preview modal scenario did not settle");
                        preview ??= Application.OpenForms.OfType<SupportReportForm>().SingleOrDefault(f => f.Owner == help);
                        if (preview?.Visible != true) return;
                        if (!observed)
                        {
                            Assert(preview.Modal && !IsWindowEnabled(help.Handle) && !IsWindowEnabled(fixture.Form.Handle),
                                "actual support preview disables both its help owner and the main window on the UI thread");
                            Assert(GetForegroundWindow() != preview.Handle,
                                "actual support preview remains off the input desktop");
                            observed = true;
                        }
                        if (scenario == "hide-and-restore" && stage == 0)
                        {
                            fixture.Form.Hide();
                            stage = 1;
                            return;
                        }
                        if (scenario == "hide-and-restore")
                        {
                            fixture.Form.Show();
                            Assert(preview.Visible && !IsWindowEnabled(fixture.Form.Handle),
                                "restoring a hidden owner preserves the active modal preview and its input boundary");
                        }
                        timer.Stop();
                        if (scenario == "exit-owner")
                        {
                            var exit = Private<NotifyIcon>(fixture.Form, "_tray").ContextMenuStrip.Items
                                .OfType<ToolStripMenuItem>().Single(i => i.Text == "Exit");
                            exit.PerformClick();
                            Assert(fixture.Form.IsDisposed && !preview.Visible,
                                "owner exit does not leave a visible modal report behind");
                        }
                        else Controls(preview).OfType<FlatButton>().Single(b => b.Text == "Close").PerformClick();
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        timer.Stop();
                        if (preview is { IsDisposed: false }) preview.Close();
                    }
                };
                timer.Start();
                reportButton.PerformClick();
                timer.Stop();
                if (failure != null) throw new InvalidOperationException($"Support preview scenario {scenario} failed", failure);
                Assert(observed && preview.IsDisposed, "actual support preview leaves its modal loop and disposes after close");
                if (scenario == "exit-owner")
                    Assert(fixture.Form.IsDisposed && help.IsDisposed,
                        "actual tray Exit closes the main window and owned help while ending the support modal loop");
                else
                {
                    Assert(!help.IsDisposed && IsWindowEnabled(help.Handle) && IsWindowEnabled(fixture.Form.Handle),
                        "closing the support preview restores help and main-window input before another preview request");
                    if (scenario == "hide-and-restore")
                        Assert(fixture.Form.Visible, "the hidden main window was restored before the preview closed");
                }
                Assert(fixture.Scans == scans && fixture.Transport.Writes == writes && fixture.StartupWrites == startup,
                    "support preview lifecycle requests no scan, display adjustment or startup write");
            }
        }
    }

    private static void CheckReadingConnectionHelp(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, enumerate: scan =>
        {
            if (scan == 1) return [];
            entered.Set();
            if (!release.Wait(10000)) throw new TimeoutException("Connection help scan barrier");
            return [];
        }, waitForCards: false);
        PumpUntil(() => Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Ready,
            "initial connection help scan completes");
        var bar = Private<DeskCommandBar>(fixture.Form, "_bar");
        fixture.Form.ActiveControl = bar.RefreshButton;
        var task = FocusRefresh(fixture.Form);
        try
        {
            PumpUntil(() => entered.IsSet, "connection help reading barrier starts");
            typeof(MainForm).GetMethod("ShowConnectionHelp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, null);
            var help = Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp");
            Assert(help.Visible && HelpReport(help).ScanState == "reading" && fixture.Scans == 2 && fixture.Transport.Writes == 0,
                "queued/direct connection help opens during reading with current unknown availability and no retry");
            ConnectionKey(help, Keys.Escape);
            release.Set();
            PumpUntil(() => task.IsCompleted, "reading settles after connection reference closes");
            task.GetAwaiter().GetResult();
            Assert(fixture.Form.ActiveControl == bar.HelpButton,
                "connection help intent cancels stale scan focus recovery even when closed before completion");
        }
        finally { release.Set(); }
    }

    private static bool ConnectionKey(ConnectionHelpForm form, Keys key)
    {
        object[] args = [Message.Create(form.Handle, 0x0100, (IntPtr)(int)key, IntPtr.Zero), key];
        return (bool)typeof(ConnectionHelpForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(form, args);
    }

    private static void CheckConnectionHelpLayout(string renderDirectory)
    {
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var help = new QuietConnectionHelp { AutoScaleMode = AutoScaleMode.None, ClientSize = new Size(444, 361) };
            var originals = Controls(help).Prepend(help).Distinct().ToDictionary(c => c, c => c.Font);
            var fonts = originals.Values.Distinct().ToDictionary(font => font,
                font => new Font(font.FontFamily, font.Size * scale, font.Style));
            try
            {
                foreach (var pair in originals) pair.Key.Font = fonts[pair.Value];
                help.SetMonitors([], DisplayScanState.NotStarted);
                help.Show();
                var guide = Controls(help).OfType<ConnectionGuide>().Single();
                var close = Controls(help).OfType<Button>().SingleOrDefault(b => b.Text == "Close");
                Assert(close != null && help.CancelButton == close,
                    $"connection help provides a pinned Close action at text scale {scale}");
                Assert(!guide.HorizontalScroll.Visible && Controls(guide).OfType<Label>().All(l => l.Width <= guide.ClientSize.Width - guide.Padding.Horizontal),
                    $"connection help wraps its offline guidance at text scale {scale}");
                foreach (Keys key in new[] { Keys.Home, Keys.PageDown, Keys.PageUp, Keys.PageDown, Keys.End, Keys.Home })
                {
                    Assert(ConnectionKey(help, key), $"connection help owns local reading key {key} at text scale {scale}");
                    Assert(help.ClientRectangle.Contains(new Rectangle(help.PointToClient(close.PointToScreen(Point.Empty)), close.Size)) && guide.Bottom <= close.Parent.Top,
                        $"connection help Close stays outside the scrolling body after {key} at text scale {scale}");
                    if (key == Keys.PageDown) Assert(guide.AutoScrollPosition.Y < 0, "Page Down advances the connection guide");
                    if (key == Keys.PageUp) Assert(guide.AutoScrollPosition.Y == 0, "Page Up returns to the guide origin after one page");
                    if (key == Keys.Home) Assert(guide.AutoScrollPosition.Y == 0, "Home returns the connection guide to the beginning");
                    if (key == Keys.End)
                    {
                        var back = Controls(guide).OfType<FlatButton>().Single(b => b.Text == "Back to controls");
                        Assert(guide.ClientRectangle.Contains(new Rectangle(guide.PointToClient(back.PointToScreen(Point.Empty)), back.Size)),
                            $"End exposes the complete Back action at text scale {scale}");
                    }
                    if (renderDirectory != null && key is Keys.Home or Keys.End)
                    {
                        Directory.CreateDirectory(renderDirectory);
                        using var bitmap = new Bitmap(help.Width, help.Height);
                        help.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(renderDirectory, $"connection-help-text-{scale}-{key}.png"));
                    }
                }
                foreach (var action in Controls(guide).OfType<FlatButton>())
                    Assert(action.Width >= TextRenderer.MeasureText(action.Text, action.Font).Width &&
                        action.Height >= TextRenderer.MeasureText(action.Text, action.Font).Height,
                        $"connection help {action.Text} label fits at text scale {scale}");
                Assert(ConnectionKey(help, Keys.Escape) && help.IsDisposed,
                    $"Escape closes connection help at text scale {scale}");
            }
            finally
            {
                help.Dispose();
                foreach (var font in fonts.Values) font.Dispose();
            }
        }
        using var closeHelp = new QuietConnectionHelp();
        closeHelp.Show();
        ((Button)closeHelp.CancelButton).PerformClick();
        Assert(closeHelp.IsDisposed, "the pinned Close button disposes connection help");
    }
}
