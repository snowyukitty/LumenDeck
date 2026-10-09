using System.Diagnostics;
using System.Reflection;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    /// <summary>
    /// Minimising and restoring must not rebuild the window. Recreating the
    /// handle destroys every card control and re-registers shortcuts, which is
    /// what made restoring a populated window visibly stall.
    /// </summary>
    private static void CheckRestoreResponsiveness(string directory)
    {
        foreach (bool toTray in new[] { true, false })
        {
            using var fixture = new Fixture(directory, enumerate: scan => [.. Enumerable.Range(0, 3).Select(i => new Display
            {
                StableKey = $"restore-{scan}-{i}", Description = $"Synthetic restore model {i}",
                Rect = new Native.RECT { Left = -30000 + i * 800, Top = -30000, Right = -29200 + i * 800, Bottom = -29400 },
                SupportsBrightness = true, Brightness = 40 + i, BrightnessMin = 0, BrightnessMax = 100,
                SupportsContrast = true, Contrast = 50, ContrastMin = 0, ContrastMax = 100,
            })]);
            fixture.Settings.MinimiseToTray = toTray;
            fixture.Settings.SetScreenBlankHotkey(fixture.LastScan[0].StableKey, "Synthetic", "Ctrl+Alt+F9");
            typeof(MainForm).GetMethod("RegisterScreenBlankHotkeys", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, null);
            var form = fixture.Form;
            var cards = Controls(form).OfType<MonitorCard>().ToArray();
            int width = cards[0].Width;
            IntPtr handle = form.Handle;
            int registrations = fixture.ShortcutRegistrations;
            string mode = toTray ? "notification-area" : "taskbar";

            var timer = Stopwatch.StartNew();
            form.WindowState = FormWindowState.Minimized;
            Application.DoEvents();
            var minimise = timer.Elapsed;
            Assert(toTray ? !form.Visible : form.Visible, $"{mode} minimise reaches its expected visibility");
            Assert(cards.All(c => c.Width == width) && cards.All(c => !c.IsDisposed),
                $"{mode} minimise leaves card layout untouched instead of shrinking every card");

            timer.Restart();
            if (toTray) typeof(MainForm).GetMethod("RestoreFromTray", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
            else form.WindowState = FormWindowState.Normal;
            Application.DoEvents();
            var restore = timer.Elapsed;
            PumpUntil(() => form.Visible && form.WindowState == FormWindowState.Normal, $"{mode} restore");
            Assert(form.Handle == handle && fixture.ShortcutRegistrations == registrations,
                $"{mode} minimise and restore keep the same window and shortcut registrations");
            Assert(Controls(form).OfType<MonitorCard>().SequenceEqual(cards) && cards.All(c => c.Width == width && c.IsHandleCreated),
                $"{mode} restore shows the existing cards at their existing width");
            // Content that appears at an unchanged width must still be laid out.
            var noticeCard = Controls(form).OfType<MonitorCard>().First();
            int heightBefore = noticeCard.Height;
            noticeCard.ShowWriteFailure(DdcWorker.Feature.Brightness);
            typeof(MainForm).GetMethod("LayoutCards", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
            form.PerformLayout();
            var notice = Controls(noticeCard).OfType<AdjustmentNotice>().Single();
            var noticeBounds = new Rectangle(noticeCard.PointToClient(notice.PointToScreen(Point.Empty)), notice.Size);
            Assert(notice.Visible && noticeCard.Height > heightBefore && noticeCard.ClientRectangle.Contains(noticeBounds) &&
                Controls(notice).OfType<Label>().All(l => l.Right <= notice.ClientSize.Width),
                $"{mode} a notice added at the same width is measured into the card");
            var rebuild = Stopwatch.StartNew();
            var refresh = (Task)typeof(MainForm).GetMethod("RebuildAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, [null]);
            PumpUntil(() => refresh.IsCompleted, $"{mode} refresh");
            Console.WriteLine($"INFO: {mode} refresh with 3 cards {rebuild.ElapsedMilliseconds} ms (informational)");
            Console.WriteLine($"INFO: {mode} minimise {minimise.TotalMilliseconds:0} ms, restore {restore.TotalMilliseconds:0} ms (3 synthetic displays, informational)");
        }
    }
}
