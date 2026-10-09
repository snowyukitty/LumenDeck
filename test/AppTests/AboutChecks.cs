using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static bool ReferenceKey(ReferenceForm form, Keys key)
    {
        object[] args = [Message.Create(form.Handle, 0x0100, (IntPtr)(int)key, IntPtr.Zero), key];
        return (bool)typeof(ReferenceForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(form, args);
    }

    private static void CheckAbout(string directory, string renderDirectory)
    {
        using var fixture = new Fixture(directory);
        Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer").Stop();
        int scans = fixture.Scans, registration = fixture.ShortcutRegistrations, gamma = fixture.GammaSaves;
        byte[] before = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
        var item = MenuChoice(menu, "About LumenDeck…");
        menu.SetSaveState(SettingsSaveState.Saving);
        Assert(item.Enabled && !item.CheckOnClick, "About remains available during pending settings storage");
        item.PerformClick();
        var about = (QuietAbout)Private<AboutForm>(fixture.Form, "_about");
        Assert(about.Visible && about.Owner == fixture.Form && fixture.AboutCreations == 1,
            "Settings opens one owned About reference through the actual MainForm");
        item.PerformClick();
        Assert(Private<AboutForm>(fixture.Form, "_about") == about && about.RevealRequests == 1 && fixture.AboutCreations == 1,
            "About reuses its existing window");
        string expectedVersion = typeof(MainForm).Assembly.GetName().Version.ToString(3);
        string expectedBuild = typeof(MainForm).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        Assert(typeof(Program).Assembly.GetName().Version.ToString(3) != expectedVersion &&
            typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion != expectedBuild,
            "About metadata oracle distinguishes the GUI assembly from the synthetic test host");
        string text = string.Join("\n", Controls(about).OfType<Label>().Select(label => label.Text));
        Assert(text.Contains($"Version {expectedVersion}") && text.Contains($"Build: {expectedBuild}"),
            "About identifies the GUI assembly rather than the executing test runner");
        Assert(text.Contains("F1") && text.Contains("Connection help") && text.Contains("LICENSE.txt") && text.Contains("QUICKSTART.txt"),
            "About names the real offline help and portable-package entry points");
        var guide = Controls(about).OfType<ReferenceView>().Single();
        foreach (var size in new[] { new Size(444, 341), new Size(624, 681) })
        {
            about.ClientSize = size;
            about.PerformLayout();
            ReferenceKey(about, Keys.Home);
            Assert(!guide.HorizontalScroll.Visible && Controls(guide).OfType<Label>()
                .All(label => label.Width <= guide.ClientSize.Width - guide.Padding.Horizontal),
                "About wraps its version and reference content inside the viewport");
            var close = (Button)about.CancelButton;
            Assert(about.ClientRectangle.Contains(new Rectangle(about.PointToClient(close.PointToScreen(Point.Empty)), close.Size)),
                "About keeps Close visible outside the scrolling reference");
            Assert(ReferenceKey(about, Keys.End), "About handles reference navigation locally");
            var lastSection = Controls(guide).OfType<Label>().Last();
            Assert(guide.ClientRectangle.Contains(new Rectangle(guide.PointToClient(lastSection.PointToScreen(Point.Empty)), lastSection.Size)),
                "End exposes the full final licence section");
            if (renderDirectory != null)
            {
                Directory.CreateDirectory(renderDirectory);
                using var bitmap = new Bitmap(about.Width, about.Height);
                about.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(renderDirectory, $"about-{size.Width}-end.png"));
                ReferenceKey(about, Keys.Home);
                about.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(renderDirectory, $"about-{size.Width}-start.png"));
            }
        }
        Assert(GetForegroundWindow() != about.Handle, "About tests do not activate a foreground window");
        Assert(fixture.Scans == scans && fixture.Transport.Writes == 0 && fixture.ShortcutRegistrations == registration && fixture.GammaSaves == gamma && fixture.StartupWrites == 0,
            "About requests no scan, registration or gamma persistence");
        byte[] after = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        Assert(before == null ? after == null : after != null && before.SequenceEqual(after),
            "About leaves isolated settings bytes unchanged");
        Assert(ReferenceKey(about, Keys.Escape) && about.IsDisposed, "Escape closes and disposes About");
        item.PerformClick();
        var replacement = Private<AboutForm>(fixture.Form, "_about");
        Assert(replacement != about && replacement.Visible && fixture.AboutCreations == 2,
            "a closed About reference can be reopened");
        ((Button)replacement.CancelButton).PerformClick();
        fixture.Form.Hide(); item.PerformClick();
        Assert(fixture.AboutCreations == 2, "hidden owners cannot open About");
        fixture.Form.Show(); fixture.Form.Enabled = false; item.PerformClick();
        Assert(fixture.AboutCreations == 2, "disabled modal owners cannot open About");
        fixture.Form.Enabled = true;
        item.PerformClick();
        var last = Private<AboutForm>(fixture.Form, "_about");
        fixture.Dispose();
        Assert(last.IsDisposed, "owner exit disposes the owned About reference");
        CheckReadingAbout(directory);
        CheckAboutCopy(renderDirectory);
        CheckAboutCopyLifetime();
        CheckAboutScaling(renderDirectory);
    }

    private static void CheckAboutCopyLifetime()
    {
        foreach (bool fail in new[] { false, true })
        {
            QuietAbout about = null;
            int writes = 0, feedback = 0;
            about = new QuietAbout(_ =>
            {
                writes++;
                about.Dispose();
                if (fail) throw new IOException("synthetic callback closes before failure");
            });
            using (about)
            {
                about.Show();
                var status = Controls(about).OfType<Label>().Single(l => l.Name == "copy-version-status");
                status.TextChanged += (_, _) => feedback++;
                var button = Controls(about).OfType<Button>().Single(b => b.Name == "copy-version");
                button.PerformClick();
                Assert(about.IsDisposed && writes == 1 && feedback == 0,
                    $"a clipboard callback closing About {(fail ? "with failure" : "after success")} does not update disposed feedback");
                Assert(!status.IsHandleCreated, "closed copy feedback cannot recreate a native handle");
            }
        }
    }

    private static void CheckAboutCopy(string renderDirectory)
    {
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            var copied = new List<string>();
            bool reject = true;
            using var about = new QuietAbout(text =>
            {
                copied.Add(text);
                if (reject) throw new IOException("private synthetic clipboard path must not appear");
            }) { AutoScaleMode = AutoScaleMode.None };
            var originals = Controls(about).Prepend(about).Distinct().ToDictionary(c => c, c => c.Font);
            var fonts = originals.Values.Distinct().ToDictionary(font => font,
                font => new Font(font.FontFamily, font.Size * scale, font.Style));
            try
            {
                about.Scale(new SizeF(scale, scale));
                foreach (var pair in originals) pair.Key.Font = fonts[pair.Value];
                about.ClientSize = new Size((int)(444 * scale), (int)(341 * scale));
                about.Show();
                var button = Controls(about).OfType<Button>().Single(b => b.Name == "copy-version");
                var status = Controls(about).OfType<Label>().Single(l => l.Name == "copy-version-status");
                Assert(copied.Count == 0 && !status.Visible, $"opening About does not access the clipboard at scale {scale}");
                foreach (bool failure in new[] { true, false })
                {
                    reject = failure;
                    button.PerformClick();
                    string expected = $"LumenDeck {typeof(MainForm).Assembly.GetName().Version.ToString(3)}{Environment.NewLine}Build: " +
                        typeof(MainForm).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
                    Assert(copied.Last() == expected, $"Copy version uses only GUI metadata at scale {scale}");
                    Assert(status.Visible && status.Text == (failure
                        ? "Could not copy. Version and build remain shown above." : "Version details copied."),
                        $"Copy version reports its {(failure ? "failure" : "success")} without private exception details at scale {scale}");
                    var close = (Button)about.CancelButton;
                    foreach (var action in new[] { button, close })
                        Assert(about.ClientRectangle.Contains(new Rectangle(about.PointToClient(action.PointToScreen(Point.Empty)), action.Size)),
                            $"About footer action {action.Text} remains reachable at scale {scale}");
                    Assert(status.Parent.ClientRectangle.Contains(status.Bounds), $"About copy feedback wraps within its footer at scale {scale}");
                    var guide = Controls(about).OfType<ReferenceView>().Single();
                    Assert(guide.Bottom <= button.Parent.Top, $"About feedback reserves the guide viewport at scale {scale}");
                    ReferenceKey(about, Keys.End);
                    var last = Controls(guide).OfType<Label>().Last();
                    Assert(guide.ClientRectangle.Contains(new Rectangle(guide.PointToClient(last.PointToScreen(Point.Empty)), last.Size)),
                        $"About licence remains readable with copy feedback at scale {scale}");
                    if (renderDirectory != null)
                    {
                        ReferenceKey(about, Keys.Home);
                        using var bitmap = new Bitmap(about.Width, about.Height);
                        about.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(renderDirectory, $"about-copy-{scale}-{(failure ? "failure" : "success")}.png"));
                    }
                }
                Assert(copied.Count == 2, $"About copies exactly once per explicit action at scale {scale}");
                ReferenceKey(about, Keys.Escape);
                Assert(about.IsDisposed, $"Escape still closes About after copy feedback at scale {scale}");
            }
            finally
            {
                about.Dispose();
                foreach (var font in fonts.Values) font.Dispose();
            }
        }
    }

    private static void CheckAboutScaling(string renderDirectory)
    {
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var about = new QuietAbout { AutoScaleMode = AutoScaleMode.None };
            var originalFonts = Controls(about).Prepend(about).Distinct().ToDictionary(control => control, control => control.Font);
            var fonts = originalFonts.Values.Distinct().ToDictionary(font => font,
                font => new Font(font.FontFamily, font.Size * scale, font.Style));
            try
            {
                about.Scale(new SizeF(scale, scale));
                foreach (var pair in originalFonts) pair.Key.Font = fonts[pair.Value];
                about.ClientSize = new Size((int)(444 * scale), (int)(341 * scale));
                about.Show();
                var guide = Controls(about).OfType<ReferenceView>().Single();
                Assert(!guide.HorizontalScroll.Visible, $"scaled About retains vertical-only reading at scale {scale}");
                var close = (Button)about.CancelButton;
                Assert(about.ClientRectangle.Contains(new Rectangle(about.PointToClient(close.PointToScreen(Point.Empty)), close.Size)),
                    $"scaled About keeps Close reachable at scale {scale}");
                Assert(guide.Bottom <= close.Parent.Top,
                    $"scaled About viewport ends above the footer at scale {scale}");
                ReferenceKey(about, Keys.End);
                Assert(-guide.AutoScrollPosition.Y == Math.Max(0, guide.VerticalScroll.Maximum - guide.VerticalScroll.LargeChange + 1),
                    $"scaled About End reaches the live scroll maximum at scale {scale}");
                var last = Controls(guide).OfType<Label>().Last();
                Assert(guide.ClientRectangle.Contains(new Rectangle(guide.PointToClient(last.PointToScreen(Point.Empty)), last.Size)),
                    $"scaled About exposes the complete licence at scale {scale}");
                ReferenceKey(about, Keys.Home);
                Assert(guide.AutoScrollPosition.Y == 0, $"scaled About Home returns to the origin at scale {scale}");
                ReferenceKey(about, Keys.PageDown);
                Assert(guide.AutoScrollPosition.Y < 0, $"scaled About PageDown advances at scale {scale}");
                ReferenceKey(about, Keys.PageUp);
                Assert(guide.AutoScrollPosition.Y == 0, $"scaled About PageUp clamps to the origin at scale {scale}");
                ReferenceKey(about, Keys.End);
                if (renderDirectory != null)
                {
                    using var bitmap = new Bitmap(about.Width, about.Height);
                    about.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(renderDirectory, $"about-scaled-{scale}-end.png"));
                    ReferenceKey(about, Keys.Home);
                    about.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(renderDirectory, $"about-scaled-{scale}-start.png"));
                }
            }
            finally
            {
                about.Dispose();
                foreach (var font in fonts.Values) font.Dispose();
            }
        }
    }

    private static void CheckReadingAbout(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, enumerate: scan =>
        {
            if (scan == 1) return [];
            entered.Set();
            if (!release.Wait(10000)) throw new TimeoutException("About scan barrier");
            return [];
        }, waitForCards: false);
        PumpUntil(() => Private<DisplayScanState>(fixture.Form, "_scanState") == DisplayScanState.Ready,
            "initial empty About scan completes");
        var bar = Private<DeskCommandBar>(fixture.Form, "_bar");
        fixture.Form.ActiveControl = bar.RefreshButton;
        var task = FocusRefresh(fixture.Form);
        try
        {
            PumpUntil(() => entered.IsSet, "About reading-state barrier starts");
            MenuChoice(Private<PreferencesMenu>(fixture.Form, "_preferencesMenu"), "About LumenDeck…").PerformClick();
            var about = Private<AboutForm>(fixture.Form, "_about");
            Assert(about.Visible && fixture.Scans == 2 && fixture.Transport.Writes == 0,
                "About opens during reading without requesting another scan or adjustment");
            ReferenceKey(about, Keys.Escape);
            release.Set();
            PumpUntil(() => task.IsCompleted, "About reading scan completes after closing the reference");
            task.GetAwaiter().GetResult();
            Assert(fixture.Form.ActiveControl == bar.HelpButton,
                "About intent cancels stale scan focus recovery even when closed before completion");
        }
        finally { release.Set(); }
    }
}
