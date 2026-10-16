using System.Reflection;
using System.Text.Json;
using LumenDeck;

internal static partial class Program
{
    private static ToolStripMenuItem ThemeChoice(PreferencesMenu menu, string id) =>
        MenuChoice(menu, "Theme").DropDownItems.OfType<ToolStripMenuItem>()
            .Single(i => i.Text == Theme.Palettes.Single(p => p.Id == id).Name);

    private static void CheckThemes(string directory, string renderDirectory)
    {
        var contrast = Theme.ContrastMode;
        try
        {
            Theme.ContrastMode = () => false;
            CheckThemeContrast();
            CheckThemeBindings();
            using (var fixture = new Fixture(directory, enumerate: _ => DeskDisplays(3), allDisplays: false))
            {
                var form = fixture.Form;
                form.Size = new Size(918, 768);
                Application.DoEvents();
                var menu = Private<PreferencesMenu>(form, "_preferencesMenu");
                var bar = Private<DeskCommandBar>(form, "_bar");
                var map = Private<LayoutMap>(form, "_map");
                var cards = Controls(form).OfType<MonitorCard>().ToArray();
                var list = Private<FlowLayoutPanel>(form, "_list");
                var slider = Controls(cards.Single(c => c.Visible)).OfType<Slider>().Single(s => s.Principal);
                slider.Focus();
                int scans = fixture.Scans, gamma = fixture.GammaWrites, writes = fixture.Transport.Writes;
                var bounds = cards.Select(c => c.Bounds).ToArray();
                var scroll = list.AutoScrollPosition;
                using var editor = new ExactValueForm(new ExactValueContext("Synthetic display brightness", "%", 0, 100, 62))
                { StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
                editor.Show(form);
                Application.DoEvents();
                var input = Controls(editor).OfType<WholeNumberInput>().Single();
                input.Text = "draft";
                input.Focus();
                input.Select(1, 3);
                Application.DoEvents();
                Assert(input.Controls.OfType<TextBox>().Single().SelectionStart == 1, "entry selection is established before a theme request");
                bool inputFocused = input.ContainsFocus;
                string previousPixels = null;
                foreach (var palette in Theme.Palettes.Reverse().Append(Theme.Palettes[0]))
                {
                    ThemeChoice(menu, palette.Id).PerformClick();
                    SettleSettings(fixture);
                    Application.DoEvents();
                    Assert(Theme.Id == palette.Id && fixture.Settings.ThemeId == palette.Id &&
                        ThemeChoice(menu, palette.Id).Checked && MenuChoice(menu, "Theme").DropDownItems
                            .OfType<ToolStripMenuItem>().Count(i => i.Checked) == 1,
                        $"{palette.Id}: saved theme and the sole menu check agree");
                    Assert(AppSettings.Load(fixture.SettingsPath).ThemeId == palette.Id,
                        $"{palette.Id}: theme persists through a fresh settings read");
                    Assert(form.BackColor == Theme.Base && map.BackColor == Theme.Bar && bar.WorkMonitor.BackColor == Theme.Base &&
                        input.BackColor == Theme.Sunken && input.ForeColor == Theme.Ink && editor.BackColor == Theme.Base,
                        $"{palette.Id}: open windows, inputs and display selectors follow the palette");
                    Assert(input.ContainsFocus == inputFocused && input.Text == "draft" && input.Controls.OfType<TextBox>().Single().SelectionStart == 1 && input.Controls.OfType<TextBox>().Single().SelectionLength == 3,
                        $"{palette.Id}: invalid numeric draft and selection survive live switching: text={input.Text}, selection={input.Controls.OfType<TextBox>().Single().SelectionStart}/{input.Controls.OfType<TextBox>().Single().SelectionLength}");
                    Assert(Controls(form).OfType<MonitorCard>().SequenceEqual(cards) && cards.Select(c => c.Bounds).SequenceEqual(bounds) &&
                        list.AutoScrollPosition == scroll && !list.HorizontalScroll.Visible && bar.NightButton.Visible,
                        $"{palette.Id}: cards, geometry, scroll and the visible Night saver command survive; before={string.Join(";", bounds)} after={string.Join(";", cards.Select(c => c.Bounds))}, scroll={scroll}/{list.AutoScrollPosition}, night={bar.NightButton.Visible}");
                    if (Environment.OSVersion.Version.Build >= 22000)
                        Assert(WindowChrome.HasDarkFrame(form) == palette.Dark && WindowChrome.HasDarkFrame(editor) == palette.Dark,
                            $"{palette.Id}: both native frames follow light/dark intent");
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    var visibleCard = cards.Single(c => c.Visible);
                    using var cardImage = new Bitmap(visibleCard.Width, visibleCard.Height);
                    visibleCard.DrawToBitmap(cardImage, new Rectangle(Point.Empty, visibleCard.Size));
                    var sample = cardImage.GetPixel(visibleCard.Width / 2, 6).ToArgb().ToString();
                    if (previousPixels != null && palette.Id != "amber") Assert(sample != previousPixels, "live capture updates the painted surface");
                    previousPixels = sample;
                    var dropdown = MenuChoice(menu, "Theme").DropDown;
                    dropdown.PerformLayout();
                    dropdown.CreateControl();
                    Assert(dropdown.Items.Cast<ToolStripItem>().All(item => dropdown.ClientRectangle.Contains(item.Bounds) && item.ForeColor == Theme.Ink),
                        $"{palette.Id}: theme choices fit and inherit readable menu colours");
                    if (renderDirectory != null)
                    {
                        Directory.CreateDirectory(renderDirectory);
                        using var menuImage = new Bitmap(dropdown.Width, dropdown.Height);
                        dropdown.DrawToBitmap(menuImage, dropdown.ClientRectangle);
                        menuImage.Save(Path.Combine(renderDirectory, $"theme-{palette.Id}-menu.png"));
                        bitmap.Save(Path.Combine(renderDirectory, $"theme-{palette.Id}.png"));
                        using var detail = new Bitmap(editor.Width, editor.Height);
                        editor.DrawToBitmap(detail, new Rectangle(Point.Empty, editor.Size));
                        detail.Save(Path.Combine(renderDirectory, $"theme-{palette.Id}-entry.png"));
                    }
                }
                Assert(fixture.Scans == scans && fixture.GammaWrites == gamma && fixture.Transport.Writes == writes &&
                    fixture.StartupWrites == 0, "theme changes perform no discovery, gamma, brightness or startup writes");
                editor.Close();
                bar.NightButton.PerformClick();
                PumpUntil(() => fixture.Worker.Idle, "Night saver before cosmetic theme change");
                SettleSettings(fixture);
                var night = Private<NightSaver>(form, "_nightSaver");
                int count = night.Count;
                writes = fixture.Transport.Writes;
                var recovery = fixture.Settings.Find("desk-fixture-0").NightDimBrightnessRaw;
                ThemeChoice(menu, "paper").PerformClick();
                SettleSettings(fixture);
                Assert(night.Count == count && night.KeepsWorkDisplay("desk-fixture-0") &&
                    fixture.Settings.Find("desk-fixture-0").NightDimBrightnessRaw == recovery && fixture.Transport.Writes == writes &&
                    cards.Single(c => c.Monitor.IsPrimary).Monitor.Brightness == 5,
                    "theme switch preserves active Night saver, primary 5% brightness and original recovery level");
                Theme.ContrastMode = () => true;
                SendMessage(form.Handle, 0x001A, IntPtr.Zero, IntPtr.Zero);
                Application.DoEvents();
                Assert(form.BackColor == SystemColors.Window && map.BackColor == SystemColors.Window &&
                    bar.WorkMonitor.ForeColor == SystemColors.WindowText && !WindowChrome.HasDarkFrame(form),
                    "Windows high contrast overrides saved light/dark colours and native dark-frame intent");
                ThemeChoice(menu, "ocean").PerformClick();
                SettleSettings(fixture);
                Assert(Theme.Id == "ocean" && form.BackColor == SystemColors.Window, "theme choice remains saved while high contrast has priority");
                Theme.ContrastMode = () => false;
                SendMessage(form.Handle, 0x0015, IntPtr.Zero, IntPtr.Zero);
                Application.DoEvents();
                Assert(form.BackColor == Theme.Base && map.BackColor == Theme.Bar && bar.WorkMonitor.ForeColor == Theme.Ink,
                    "leaving high contrast restores distinct semantic roles and the saved palette");
                bar.NightButton.PerformClick();
            }
            using (var reopened = new Fixture(directory, settingsFactory: path =>
                new AppSettings(path) { ThemeId = "violet" }))
                Assert(Theme.Id == "violet" && reopened.Form.BackColor == Theme.Base, "startup applies a saved palette before creating its controls");
            using (var unknown = new Fixture(directory, settingsFactory: path =>
                new AppSettings(path) { ThemeId = "future-palette" }))
            {
                Assert(Theme.Id == "amber" && unknown.Settings.ThemeId == "future-palette",
                    "unknown future palette falls back without destructively rewriting the preference");
                ThemeChoice(Private<PreferencesMenu>(unknown.Form, "_preferencesMenu"), "amber").PerformClick();
                SettleSettings(unknown);
                Assert(unknown.Settings.ThemeId == "amber" && AppSettings.Load(unknown.SettingsPath).ThemeId == "amber",
                    "explicitly choosing Amber replaces an unknown stored ID even when Amber is already the fallback");
            }
            CheckThemeSaveFailure(directory);
            CheckThemeSaveBarrier(directory);
        }
        finally { Theme.ContrastMode = contrast; Theme.Select("amber"); Theme.Refresh(); }
    }

    private static void CheckThemeSaveFailure(string directory)
    {
        using var fixture = new Fixture(directory, refuseSettingsSave: true);
        var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
        ThemeChoice(menu, "paper").PerformClick();
        SettleSettings(fixture);
        Assert(Theme.Id == "amber" && fixture.Settings.ThemeId == "amber" && ThemeChoice(menu, "amber").Checked &&
            !ThemeChoice(menu, "paper").Checked, "failed settings write preserves the active palette and menu check");
        Assert(Private<StatusStripe>(fixture.Form, "_status").AccessibleName.Contains("previous theme"),
            "save refusal explains that the previous theme remains active");
    }

    private static void CheckThemeSaveBarrier(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, writeSettings: write =>
        {
            entered.Set();
            if (!release.Wait(10000)) throw new TimeoutException("theme settings barrier");
            return write();
        });
        try
        {
            var menu = Private<PreferencesMenu>(fixture.Form, "_preferencesMenu");
            ThemeChoice(menu, "violet").PerformClick();
            PumpUntil(() => entered.IsSet, "theme save is pending");
            Assert(Theme.Id == "amber" && !MenuChoice(menu, "Theme").Enabled && ThemeChoice(menu, "amber").Checked,
                "pending save keeps the original palette and disables competing theme requests");
            ThemeChoice(menu, "paper").PerformClick();
            release.Set();
            SettleSettings(fixture);
            Assert(Theme.Id == "violet" && fixture.Settings.ThemeId == "violet", "only the admitted theme activates after a confirmed save");
        }
        finally { release.Set(); }
    }

    private static void CheckThemeBindings()
    {
        Theme.ContrastMode = () => true;
        using var view = new DisplayScanView(DisplayScanState.Failed);
        using var notice = new AdjustmentNotice("Synthetic display", true);
        Theme.ContrastMode = () => false;
        Theme.Select("paper");
        Theme.Refresh();
        Assert(view.BackColor == Theme.Card && Controls(view).OfType<Label>().Single(l => l.Text == "Could not read your displays").ForeColor == Theme.Warn,
            "controls born in high contrast recover their surface and warning roles");
        int Subscriptions() => ((Delegate)typeof(Theme).GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null))?.GetInvocationList().Length ?? 0;
        int subscribed = Subscriptions();
        var label = Theme.Colors(new Label(), fore: () => Theme.Warn);
        label.Dispose();
        Assert(Subscriptions() == subscribed, "disposing a bound control removes its global subscription");
        int changes = 0;
        label.ForeColorChanged += (_, _) => changes++;
        Theme.Select("ocean");
        Assert(changes == 0, "disposed controls no longer receive palette callbacks");
        using var menu = new PreferencesMenu();
        int requests = 0;
        menu.ThemeRequested += _ => requests++;
        foreach (var state in new[] { SettingsSaveState.Saving, SettingsSaveState.SourceChanged, SettingsSaveState.Unavailable })
        {
            menu.SetSaveState(state);
            ThemeChoice(menu, "violet").PerformClick();
            Assert(!MenuChoice(menu, "Theme").Enabled && requests == 0, $"{state}: theme choice cannot bypass settings admission");
        }
        Assert(Theme.Normalize(null) == "amber" && Theme.Normalize("") == "amber", "missing theme preference has the original Amber default");
        Theme.Select("amber");
    }

    private static void CheckThemeContrast()
    {
        static double Luminance(Color c)
        {
            static double Channel(byte x) => x / 255d <= .04045 ? x / 255d / 12.92 : Math.Pow((x / 255d + .055) / 1.055, 2.4);
            return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        }
        static double Ratio(Color a, Color b)
        {
            double x = Luminance(a), y = Luminance(b);
            return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
        }
        foreach (var palette in Theme.Palettes)
        {
            Theme.Select(palette.Id);
            foreach (var ink in new[] { Theme.Ink, Theme.InkMuted, Theme.InkFaint, Theme.Info, Theme.Warn, Theme.AmberLight })
                foreach (var surface in new[] { Theme.Base, Theme.Bar, Theme.Card, Theme.CardTop, Theme.Sunken })
                    Assert(Ratio(ink, surface) >= 4.5, $"{palette.Id}: text {ink} on {surface} meets 4.5:1 ({Ratio(ink, surface):F2})");
            foreach (var ink in new[] { Theme.InkMuted, Theme.Info, Theme.Warn })
            {
                var surface = Theme.CardTop;
                var fill = Theme.ChipFill(ink);
                var tint = Color.FromArgb((fill.R * fill.A + surface.R * (255 - fill.A)) / 255,
                    (fill.G * fill.A + surface.G * (255 - fill.A)) / 255,
                    (fill.B * fill.A + surface.B * (255 - fill.A)) / 255);
                Assert(Ratio(ink, tint) >= 4.5, $"{palette.Id}: chip label retains 4.5:1 over its tinted background ({Ratio(ink, tint):F2})");
            }
            foreach (var face in new[] { Theme.Amber, Theme.AmberLight, Theme.AmberPressed })
                Assert(Ratio(Theme.OnAmber, face) >= 4.5, $"{palette.Id}: primary label contrast meets 4.5:1");
            Assert(Ratio(Theme.AmberLight, Theme.CardHover) >= 3, $"{palette.Id}: focus indicator meets 3:1");
        }
        Theme.Select("amber");
    }
}
