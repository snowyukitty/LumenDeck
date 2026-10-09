using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static bool HelpKey(KeyboardHelpForm form, Keys key)
    {
        object[] args = [Message.Create(form.Handle, 0x0100, (IntPtr)(int)key, IntPtr.Zero), key];
        return (bool)typeof(KeyboardHelpForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(form, args);
    }

    private static void CheckKeyboardHelp(string directory, string renderDirectory)
    {
        using var fixture = new Fixture(directory);
        Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer").Stop();
        int scans = fixture.Scans, registration = fixture.ShortcutRegistrations, gamma = fixture.GammaSaves;
        byte[] before = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
        var item = MenuChoice(menu, "Keyboard controls…");
        menu.SetSaveState(SettingsSaveState.Saving);
        Assert(item.Enabled && !item.CheckOnClick, "keyboard reference remains available during pending settings storage");
        item.PerformClick();
        var help = (QuietKeyboardHelp)Private<KeyboardHelpForm>(fixture.Form, "_keyboardHelp");
        Assert(help.Visible && help.Owner == fixture.Form && fixture.KeyboardHelpCreations == 1,
            "Settings opens one owned keyboard-reference window through the actual MainForm");
        object[] f1 = [Message.Create(fixture.Form.Handle, 0x0100, (IntPtr)(int)Keys.F1, IntPtr.Zero), Keys.F1];
        Assert((bool)typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, f1),
            "F1 in the main window uses the real local command-key path");
        Assert(Private<KeyboardHelpForm>(fixture.Form, "_keyboardHelp") == help && help.RevealRequests == 1 &&
            fixture.KeyboardHelpCreations == 1, "F1 reuses the existing reference rather than creating a second window");
        var guide = Controls(help).OfType<ReferenceView>().Single();
        var mainSelection = fixture.Form.ActiveControl;
        HelpKey(help, Keys.Control | Keys.PageDown);
        Assert(fixture.Form.ActiveControl == mainSelection, "owned guide command keys cannot navigate its owner displays");
        string text = string.Join("\n", Controls(guide).OfType<Label>().Select(l => l.Text));
        Assert(text.Contains("hardware unit") && text.Contains("50 kelvin lower") && text.Contains("6500 K") &&
            text.Contains("3000 K") && text.Contains("F2 or Enter") && text.Contains("no built-in global"),
            "offline guide distinguishes hardware units, reversed Kelvin, exact entry and configured shortcuts");
        foreach (var size in new[] { new Size(444, 341), new Size(624, 681) })
        {
            help.ClientSize = size;
            help.PerformLayout();
            HelpKey(help, Keys.Home);
            Assert(!guide.HorizontalScroll.Visible && Controls(guide).OfType<Label>().All(l => l.Width <= guide.ClientSize.Width - guide.Padding.Horizontal),
                "keyboard reference wraps within its viewport without horizontal scrolling");
            var close = Controls(help).OfType<Button>().Single(b => b.Text == "Close");
            Assert(help.ClientRectangle.Contains(new Rectangle(help.PointToClient(close.PointToScreen(Point.Empty)), close.Size)),
                "keyboard reference keeps Close visible outside the scrolling text");
            Assert(HelpKey(help, Keys.PageDown) && guide.AutoScrollPosition.Y < 0,
                "Page Down scrolls the reference body through the form command-key path");
            HelpKey(help, Keys.End);
            var last = Controls(guide).OfType<Label>().Last();
            Assert(guide.ClientRectangle.Contains(new Rectangle(guide.PointToClient(last.PointToScreen(Point.Empty)), last.Size)),
                "End exposes the complete final reference section");
            if (renderDirectory != null)
            {
                Directory.CreateDirectory(renderDirectory);
                using var bitmap = new Bitmap(help.Width, help.Height);
                help.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(renderDirectory, $"keyboard-reference-{size.Width}-end.png"));
                HelpKey(help, Keys.Home);
                help.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(renderDirectory, $"keyboard-reference-{size.Width}-start.png"));
            }
            HelpKey(help, Keys.Home);
            Assert(guide.AutoScrollPosition.Y == 0, "Home returns the reference to its start");
        }
        Assert(GetForegroundWindow() != help.Handle, "reference layout/key tests keep their off-desktop owner out of the foreground");
        Assert(HelpKey(help, Keys.Escape) && help.IsDisposed, "Escape closes and disposes the modeless keyboard reference");
        item.PerformClick();
        var replacement = Private<KeyboardHelpForm>(fixture.Form, "_keyboardHelp");
        Assert(replacement.Visible && replacement != help && fixture.KeyboardHelpCreations == 2,
            "a closed reference is replaced by one fresh owned form");
        ((Button)replacement.CancelButton).PerformClick();
        fixture.Form.Hide(); item.PerformClick();
        Assert(fixture.KeyboardHelpCreations == 2, "hidden owners cannot create a keyboard reference");
        fixture.Form.Show(); fixture.Form.Enabled = false; item.PerformClick();
        Assert(fixture.KeyboardHelpCreations == 2, "disabled modal owners cannot create another reference");
        fixture.Form.Enabled = true;
        byte[] after = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        Assert(fixture.Scans == scans && fixture.Transport.Writes == 0 && fixture.StartupWrites == 0 &&
            fixture.ShortcutRegistrations == registration && fixture.GammaSaves == gamma &&
            (before == null ? after == null : after != null && before.SequenceEqual(after)),
            "reference open, scroll, close and reopen request no scan, monitor/gamma, registration or settings writes");
        CheckReadingKeyboardHelp(directory);
    }

    private static void CheckReadingKeyboardHelp(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, enumerate: scan =>
        {
            if (scan == 1) return [];
            entered.Set();
            if (!release.Wait(10000)) throw new TimeoutException("keyboard guide scan barrier");
            return [];
        }, waitForCards: false);
        PumpUntil(() => Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Ready, "initial empty reference scan completes");
        var bar = Private<DeskCommandBar>(fixture.Form, "_bar");
        fixture.Form.ActiveControl = bar.RefreshButton;
        var scanTask = FocusRefresh(fixture.Form);
        try
        {
            PumpUntil(() => entered.IsSet, "reference reading-state barrier starts");
            object[] f1 = [Message.Create(fixture.Form.Handle, 0x0100, (IntPtr)(int)Keys.F1, IntPtr.Zero), Keys.F1];
            typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Form, f1);
            var firstGuide = Private<KeyboardHelpForm>(fixture.Form, "_keyboardHelp");
            Assert(firstGuide.Visible && fixture.Scans == 2, "local F1 opens a reference while enumeration is waiting");
            HelpKey(firstGuide, Keys.Escape);
            release.Set();
            PumpUntil(() => scanTask.IsCompleted, "reference reading scan completes after closing help");
            scanTask.GetAwaiter().GetResult();
            Assert(fixture.Form.ActiveControl == bar.HelpButton, "F1 intent cancels focus recovery even if its guide closes before the scan completes");
            entered.Reset(); release.Reset();
            fixture.Form.ActiveControl = bar.RefreshButton;
            scanTask = FocusRefresh(fixture.Form);
            PumpUntil(() => entered.IsSet, "reference owner-close scan starts");
            MenuChoice(Private<PreferencesMenu>(fixture.Form, "_preferencesMenu"), "Keyboard controls…").PerformClick();
            var guide = Private<KeyboardHelpForm>(fixture.Form, "_keyboardHelp");
            Assert(guide.Visible && fixture.Scans == 3 && fixture.Transport.Writes == 0,
                "keyboard reference opens during reading without starting another scan");
            fixture.Form.Close();
            PumpUntil(() => fixture.Form.IsDisposed, "reference owner closes while scan waits");
            Assert(guide.IsDisposed, "closing MainForm disposes its owned reference");
            release.Set();
            PumpUntil(() => scanTask.IsCompleted, "closed reference owner's scan reaches terminal completion");
            scanTask.GetAwaiter().GetResult();
            Assert(fixture.Form.IsDisposed && guide.IsDisposed, "late scan completion cannot reopen an owned keyboard reference");
        }
        finally { release.Set(); }
    }
}
