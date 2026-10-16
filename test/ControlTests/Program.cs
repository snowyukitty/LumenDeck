using System.Reflection;
using System.Text.Json;
using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    private static int _checks;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--capabilities-only"])
        {
            try
            {
                CheckCapabilities();
                Console.WriteLine($"PASS: {_checks} capability data checks; no display, settings or UI access.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args is ["--cli-arguments-only"])
        {
            try
            {
                CheckCliArguments();
                Console.WriteLine($"PASS: {_checks} CLI preflight checks; no display or settings access.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 5 && args[0] == "--settings-peer")
        {
            RunSettingsPeer(args[1], args[2], args[3], args[4]);
            return;
        }
        if (args.Length == 2 && args[0] == "--activation-client")
        {
            Environment.ExitCode = InstanceActivation.RequestAsync(args[1], 2000, _ => { }).GetAwaiter().GetResult() ? 0 : 2;
            return;
        }
        if (args.Length == 1 && args[0] == "--activation-only")
        {
            CheckInstanceActivation();
            Console.WriteLine($"PASS: {_checks} isolated activation checks; no real app or monitor access.");
            return;
        }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        if (args is ["--feature-accessibility-only"])
        {
            try
            {
                CheckFeatureAccessibility();
                Console.WriteLine($"PASS: {_checks} extra-control accessibility checks; fake transport and off-desktop UI only.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args is ["--card-links-only"])
        {
            try
            {
                CheckCardLinkAccessibility();
                Console.WriteLine($"PASS: {_checks} contextual card-link checks; fake transport and off-desktop UI only.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 1 && args[0] == "--command-help-only")
        {
            try
            {
                CheckCommandHelp();
                Console.WriteLine($"PASS: {_checks} accessible command-help checks; no hardware or user settings access.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length is 1 or 2 && args[0] == "--commands-only")
        {
            CheckCommandBar(args.Length == 2 ? Path.GetFullPath(args[1]) : null);
            Console.WriteLine($"PASS: {_checks} isolated command layout checks; no monitor or settings access.");
            return;
        }
        if (args.Length is 1 or 2 && args[0] == "--buttons-only")
        {
            CheckFlatButton();
            CheckButtonAppearance(args.Length == 2 ? Path.GetFullPath(args[1]) : null);
            Console.WriteLine($"PASS: {_checks} isolated command activation checks; no monitor or settings access.");
            return;
        }
        string directory = Path.Combine(Path.GetTempPath(), "LumenDeck-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            if (args is ["--cli-warmth-only"])
            {
                try
                {
                    CheckCliWarmth(directory);
                    Console.WriteLine($"PASS: {_checks} CLI warmth checks; fake writer and temporary settings only.");
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
                return;
            }
            if (args.Length == 1 && args[0] == "--blackout-rebind-only")
            {
                try
                {
                    CheckBlackoutRebind(directory);
                    Console.WriteLine($"PASS: {_checks} blackout rebind checks; fake transport and off-desktop non-activating overlays only.");
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
                return;
            }
            if (args is ["--night-only"])
            {
                try
                {
                    CheckNightSaver(directory);
                    CheckNightDim(directory);
                    Console.WriteLine($"PASS: {_checks} night saver checks; fake transport, temporary settings and off-desktop overlays only.");
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
                return;
            }
            if (args.Length is 1 or 2 && args[0] == "--schedule-only")
            {
                try
                {
                    CheckSchedule(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                    Console.WriteLine($"PASS: {_checks} preset schedule checks; fixed clock values, temporary settings and off-desktop UI only.");
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
                return;
            }
            if (args.Length is 1 or 2 && args[0] == "--presets-only")
            {
                try
                {
                    CheckPresetTargets(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                    Console.WriteLine($"PASS: {_checks} preset target checks; temporary settings and off-desktop UI only.");
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
                return;
            }
            if (args.Length is 1 or 2 && args[0] == "--profile-only")
            {
                CheckProfileEditor(directory, args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                CheckAsyncProfileSave(directory);
                Console.WriteLine($"PASS: {_checks} isolated profile editor checks; temporary storage only.");
                return;
            }
            CheckSettingsStorage(directory);
            CheckCliArguments();
            CheckCliWarmth(directory);
            CheckCliAdjustments(directory);
            CheckCardWarmth(directory);
            if (args.Length == 1 && args[0] == "--settings-only")
            {
                Console.WriteLine($"PASS: {_checks} isolated settings checks; no monitor, user settings or registry access.");
                return;
            }
            CheckCapabilities();
            CheckLuminanceProfiles(directory, args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckProfileEditor(directory, args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckAsyncProfileSave(directory);
            CheckPresetTargets(directory, args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckSchedule(directory, args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckFlatButton();
            CheckButtonAppearance(args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckCommandHelp();
            CheckCommandBar(args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckCardLayout(args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckAdjustmentNotice();
            CheckCardLinkAccessibility();
            CheckFeatureAccessibility();
            CheckConnectionGuide(args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckSupportReport(args.Length == 2 && args[0] == "--render-dir" ? args[1] : null);
            CheckSlider();
            CheckSliderInputOwnership();
            CheckDdcWorker();
            CheckInstanceActivation();
            CheckBlanking(directory);
            CheckBlackoutRebind(directory);
            CheckNightSaver(directory);
            CheckNightDim(directory);
            Console.WriteLine($"PASS: {_checks} control checks; no physical monitor writes or user settings changes.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        _checks++;
        Console.WriteLine("PASS: " + description);
    }

    private static void CheckCommandBar(string renderDirectory)
    {
        using (var unparented = new FlatButton("Connection help for an unavailable display")
            { MinimumSize = new Size(84, 30), AutoSize = true })
        {
            Size originalSize = unparented.Size;
            foreach (Size proposal in new[] { Size.Empty, new Size(1, 0), new Size(160, 0),
                new Size(480, 0), new Size(int.MaxValue, int.MaxValue) })
            {
                Size preferred = unparented.GetPreferredSize(proposal);
                Assert(preferred.Width >= unparented.MinimumSize.Width && preferred.Height >= unparented.MinimumSize.Height,
                    "every proposed size preserves the command's minimum footprint");
                Assert(preferred == unparented.GetPreferredSize(proposal),
                    "repeated preferred-size queries settle for bounded and unbounded proposals");
                Assert(unparented.Size == originalSize && !unparented.IsHandleCreated,
                    "preferred-size queries neither change bounds nor create a native handle");
            }
            Size natural = unparented.GetPreferredSize(Size.Empty);
            Size wrapped = unparented.GetPreferredSize(new Size(160, 0));
            Assert(wrapped.Width <= 160 && wrapped.Height > natural.Height,
                "constrained preferred size wraps the label instead of escaping the row");
        }
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var form = new PreviewForm { Font = Theme.Body, AutoScaleMode = AutoScaleMode.None,
                ClientSize = new Size(620, 600) };
            using var bar = new DeskCommandBar();
            form.Controls.Add(bar);
            form.Show();
            form.Scale(new SizeF(scale, scale));
            using var scaledBody = new Font(Theme.Body.FontFamily, Theme.Body.Size * scale);
            using var scaledSmall = new Font(Theme.Small.FontFamily, Theme.Small.Size * scale);
            foreach (var control in Descendants(bar))
                control.Font = control is Label && control.Parent is CommandSection ? scaledSmall : scaledBody;
            bar.WorkMonitor.Items.Add("Display A — Centre (primary)");
            bar.WorkMonitor.SelectedIndex = 0;
            string originalHelp = bar.HelpButton.Text;
            int originalHelpWidth = bar.HelpButton.Width;
            bar.HelpButton.Text = "Connection help and troubleshooting";
            Assert(bar.HelpButton.Width > originalHelpWidth,
                $"command label growth relayouts its native button at scale {scale}");
            Assert(bar.HelpButton.Width >= TextRenderer.MeasureText(bar.HelpButton.Text, bar.HelpButton.Font).Width,
                $"the complete extended command label fits at scale {scale}: width {bar.HelpButton.Width}, measured {TextRenderer.MeasureText(bar.HelpButton.Text, bar.HelpButton.Font).Width}, parent {bar.HelpButton.Parent.Width}");
            int compactHeight = 0;
            foreach (int width in new[] { 604, 620, 780, 1100, 620 })
            {
                form.ClientSize = new Size((int)(width * scale), (int)(600 * scale));
                form.PerformLayout();
                bar.PerformLayout();
                foreach (CommandSection section in bar.Controls)
                {
                    section.PerformLayout();
                    var commands = section.Commands;
                    commands.PerformLayout();
                    Assert(section.Bottom <= bar.ClientSize.Height && section.Top >= 0,
                        $"command section is visible at {width}px, scale {scale}");
                    foreach (Control command in commands.Controls)
                    {
                        Assert(commands.ClientRectangle.Contains(command.Bounds),
                            $"{command.Text} fits its command group at {width}px, scale {scale}");
                        foreach (Control child in command.Controls)
                            Assert(command.ClientRectangle.Contains(child.Bounds),
                                $"selector child fits at {width}px, scale {scale}");
                    }
                }
                if (width == 620) compactHeight = bar.Height;
                if (width == 1100) Assert(bar.Height < compactHeight,
                    $"wide layout reclaims wrapped vertical space at scale {scale}");
                if (renderDirectory != null && width != 1100)
                {
                    Directory.CreateDirectory(renderDirectory);
                    using var image = new Bitmap(bar.Width, bar.Height);
                    bar.DrawToBitmap(image, bar.ClientRectangle);
                    image.Save(Path.Combine(renderDirectory, $"commands-{width}-{scale}.png"));
                }
            }
            bar.HelpButton.Text = originalHelp;
            Assert(bar.HelpButton.Width == originalHelpWidth,
                $"short command label restores its original footprint at scale {scale}");
            int singleLineHeight = bar.HelpButton.Height;
            bar.HelpButton.Text = string.Join(" ", Enumerable.Repeat("Connection help for displays that cannot be adjusted through the current cable, adapter or graphics driver", 3));
            Assert(bar.HelpButton.Parent.ClientRectangle.Contains(bar.HelpButton.Bounds),
                $"a label wider than the command row stays within its viewport at scale {scale}");
            Assert(bar.HelpButton.Height > singleLineHeight,
                $"an oversized command label grows vertically at scale {scale}");
            if (renderDirectory != null)
            {
                using var image = new Bitmap(bar.Width, bar.Height);
                bar.DrawToBitmap(image, bar.ClientRectangle);
                image.Save(Path.Combine(renderDirectory, $"commands-long-{scale}.png"));
            }
            bar.HelpButton.Text = originalHelp;
            Size beforeFontChange = bar.HelpButton.Size;
            using (var largerFont = new Font(Theme.Body.FontFamily, Theme.Body.Size * scale * 1.5f))
            {
                bar.HelpButton.Font = largerFont;
                Assert(bar.HelpButton.Height > beforeFontChange.Height,
                    $"late font changes resize command text vertically at scale {scale}");
                Assert(bar.HelpButton.Parent.ClientRectangle.Contains(bar.HelpButton.Bounds),
                    $"late font growth remains within its command row at scale {scale}");
                bar.HelpButton.Font = scaledBody;
            }
            Assert(bar.HelpButton.Size == beforeFontChange,
                $"restoring the font restores command bounds at scale {scale}");
            bar.HelpButton.Padding = new Padding((int)(12 * scale));
            Assert(bar.HelpButton.Height > beforeFontChange.Height,
                $"late padding changes resize the command at scale {scale}");
            bar.HelpButton.Padding = Padding.Empty;
            Assert(bar.HelpButton.Size == beforeFontChange,
                $"restoring padding restores command bounds at scale {scale}");
            int textEvents = 0, fontEvents = 0;
            EventHandler countText = (_, _) => textEvents++;
            EventHandler countFont = (_, _) => fontEvents++;
            bar.HelpButton.TextChanged += countText;
            bar.HelpButton.FontChanged += countFont;
            bar.HelpButton.Text = "Connection support";
            bar.HelpButton.Parent.PerformLayout(bar.HelpButton, nameof(Control.Text));
            Assert(textEvents == 1 && fontEvents == 0,
                "text-triggered parent layout raises no additional text or font changes");
            bar.HelpButton.TextChanged -= countText;
            bar.HelpButton.FontChanged -= countFont;
            Assert(bar.HelpButton.AccessibilityObject.Name == "Connection help",
                "the compact Help label retains its explicit connection-help name");
            bar.HelpButton.AccessibleName = null;
            bar.HelpButton.Text = "Connection & support";
            Assert(bar.HelpButton.AccessibilityObject.Name == bar.HelpButton.Text,
                "literal command punctuation remains in its accessible name");
            Assert(bar.HelpButton.AccessibilityObject.KeyboardShortcut == null,
                "literal punctuation advertises no unimplemented mnemonic shortcut");
            if (renderDirectory != null)
            {
                using var image = new Bitmap(bar.Width, bar.Height);
                bar.DrawToBitmap(image, bar.ClientRectangle);
                image.Save(Path.Combine(renderDirectory, $"commands-literal-{scale}.png"));
            }
            bar.HelpButton.AccessibleName = "Explicit help command";
            Assert(bar.HelpButton.AccessibilityObject.Name == "Explicit help command",
                "explicit command accessible names retain precedence");
            bar.HelpButton.AccessibleName = "Connection help";
            bar.HelpButton.Text = originalHelp;
            using (var manual = new FlatButton("Manual command") { Size = new Size(140, 30) })
            {
                form.Controls.Add(manual);
                Size fixedSize = manual.Size;
                manual.Text = "An extended command in an explicitly sized host";
                manual.Font = scaledBody;
                Assert(manual.Size == fixedSize && !manual.AutoSize,
                    "explicitly sized hosts retain ownership of their button bounds");
                manual.Text = "A";
                using var beforePadding = new Bitmap(manual.Width, manual.Height);
                manual.DrawToBitmap(beforePadding, manual.ClientRectangle);
                manual.Padding = new Padding(40, 0, 0, 0);
                using var afterPadding = new Bitmap(manual.Width, manual.Height);
                manual.DrawToBitmap(afterPadding, manual.ClientRectangle);
                double LabelCenter(Bitmap bitmap)
                {
                    long sum = 0;
                    int count = 0;
                    // Rounded corners can expose the light owner background;
                    // sample only the dark button interior to locate its ink.
                    for (int y = 5; y < bitmap.Height - 5; y++)
                        for (int x = 5; x < bitmap.Width - 5; x++)
                        {
                            Color pixel = bitmap.GetPixel(x, y);
                            if (pixel.R < 200 || pixel.G < 200 || pixel.B < 200) continue;
                            sum += x;
                            count++;
                        }
                    Assert(count > 0, "native manual-button label remains visible with explicit padding");
                    return (double)sum / count;
                }
                if (renderDirectory != null)
                {
                    beforePadding.Save(Path.Combine(renderDirectory, $"manual-padding-before-{scale}.png"));
                    afterPadding.Save(Path.Combine(renderDirectory, $"manual-padding-after-{scale}.png"));
                }
                double paddedCenter = LabelCenter(afterPadding), plainCenter = LabelCenter(beforePadding);
                Assert(paddedCenter > plainCenter + 15,
                    $"explicit left padding moves the native label within fixed button bounds: {plainCenter:F1} to {paddedCenter:F1}, scale {scale}");
                Assert(manual.Size == fixedSize, "manual padding changes leave outer button dimensions unchanged");
            }
            var commandSection = (CommandSection)bar.NightButton.Parent.Parent;
            int previousHeight = bar.Height;
            commandSection.Commands.Controls.Add(new FlatButton("Future command")
            {
                Width = commandSection.Commands.ClientSize.Width - 6,
            });
            Assert(bar.Height > previousHeight, $"new commands grow their section at scale {scale}");
            int picked = 0;
            bar.LevelPicked += _ => picked++;
            bar.ModeButtons["Day"].PerformClick();
            Assert(picked == 1, "preset presentation emits one action without hardware I/O");
            Assert(bar.WorkMonitor.AccessibleName == "Keep working on", "work screen selector has an accessible name");
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void CheckCardLayout(string renderDirectory, bool withBrightness = false)
    {
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var monitor = Display("layout-only");
            monitor.Description = "Example UltraWide Professional Display With A Long Model Name";
            if (withBrightness)
            {
                monitor.SupportsBrightness = true;
                monitor.BrightnessMin = 0;
                monitor.BrightnessMax = 255;
                monitor.Brightness = 128;
            }
            monitor.PositionLabel = "Centre (primary)";
            monitor.Edid = new EdidInfo { WidthCm = 75, HeightCm = 30 };
            using var worker = new DdcWorker();
            using var form = new PreviewForm { ClientSize = new Size(1600, 2000), Font = Theme.Body, AutoScaleMode = AutoScaleMode.None };
            using var card = new MonitorCard(monitor, null, worker, null, null, null, null, null, () => { }, _ => { });
            card.SetScreenBlankShortcutText("Ctrl+Alt+Shift+F12");
            form.Controls.Add(card);
            form.Show();
            card.ShowWriteFailure(DdcWorker.Feature.Brightness);
            card.ShowWriteFailure(DdcWorker.Feature.Contrast);
            form.Scale(new SizeF(scale, scale));
            var originalFonts = Descendants(card).ToDictionary(control => control, control => control.Font);
            var fonts = originalFonts.Values.Distinct().ToDictionary(
                font => font, font => new Font(font.FontFamily, font.Size * scale, font.Style));
            foreach (var pair in originalFonts) pair.Key.Font = fonts[pair.Value];
            int narrowHeight = 0;
            int firstWideHeight = 0;
            try
            {
                foreach (int width in new[] { 740, 570, 400, 740 })
                {
                    card.SetWidth((int)(width * scale));
                    card.PerformLayout();
                    foreach (var control in Descendants(card).Where(c => c.Visible))
                    {
                        var bounds = new Rectangle(card.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                        Assert(bounds.Left >= 0 && bounds.Right <= card.Width && bounds.Bottom <= card.Height,
                            $"card content {control.GetType().Name} stays within {width}px width, scale {scale}");
                        Assert(control.Parent.ClientRectangle.Contains(control.Bounds),
                            $"card content {control.GetType().Name} stays within its parent, scale {scale}");
                        if (control.Parent is FlowLayoutPanel flow)
                            foreach (Control sibling in flow.Controls)
                                if (sibling != control && sibling.Visible)
                                    Assert(!control.Bounds.IntersectsWith(sibling.Bounds), "wrapped card controls do not overlap");
                    }
                    if (width == 400) narrowHeight = card.Height;
                    if (width == 740 && narrowHeight == 0) firstWideHeight = card.Height;
                    if (width == 740 && narrowHeight > 0)
                    {
                        Assert(card.Height < narrowHeight, "wide card reclaims wrapped vertical space");
                        Assert(card.Height == firstWideHeight, "wide card returns to its original height");
                    }
                    if (renderDirectory != null)
                    {
                        Directory.CreateDirectory(renderDirectory);
                        using var image = new Bitmap(card.Width, card.Height);
                        card.DrawToBitmap(image, card.ClientRectangle);
                        image.Save(Path.Combine(renderDirectory, $"card-layout-{width}-{scale}.png"));
                    }
                }
            }
            finally { foreach (var font in fonts.Values) font.Dispose(); }
        }
    }

    private static void CheckAdjustmentNotice()
    {
        using var monitor = Display("adjustment-feedback");
        monitor.SupportsBrightness = monitor.SupportsContrast = true;
        monitor.BrightnessMin = monitor.ContrastMin = 0;
        monitor.BrightnessMax = monitor.ContrastMax = 100;
        monitor.Brightness = 42;
        monitor.Contrast = 51;
        using var worker = new DdcWorker();
        using var form = new PreviewForm { ClientSize = new Size(600, 900) };
        int helpCalls = 0;
        using var card = new MonitorCard(monitor, new AppSettings(), worker, null, null,
            null, null, null, () => helpCalls++);
        card.SetWidth(570);
        form.Controls.Add(card);
        form.Show();
        var notice = Descendants(card).OfType<AdjustmentNotice>().Single();
        Assert(!notice.Visible, "new card has no adjustment failure notice");
        int initialHeight = card.Height;
        var brightness = Descendants(card).OfType<Slider>().Single(s => s.AccessibleName.EndsWith(": Brightness (hardware units)"));
        brightness.SetValueSilently(80);
        card.ShowWriteFailure(DdcWorker.Feature.Brightness);
        Assert(notice.Visible && brightness.Value == 42, "failure notice accompanies observed brightness synchronization");
        Assert(card.Height > initialHeight, "visible failure notice expands the card without overlaying its controls");
        string first = notice.Message;
        card.ShowWriteFailure(DdcWorker.Feature.Brightness);
        Assert(notice.Message == first, "repeated failures do not duplicate notice content");
        card.ShowWriteFailure(DdcWorker.Feature.Contrast);
        Assert(notice.Message.Contains("brightness, contrast"), "a second failed control retains the first failure");
        card.SyncFromMonitor();
        card.ApplyContrast(52);
        Assert(notice.Visible && notice.Message.Contains("brightness, contrast"), "synchronization and retry do not claim prior failures are resolved");
        card.ShowWriteFailure(DdcWorker.Feature.Vcp);
        card.ShowWarmthFailure();
        Assert(notice.Message.Contains("brightness, contrast, warmth, extra controls"), "all adjustment paths share a stable readable notice");
        notice.RecordFailure((AdjustmentControl)99);
        Assert(!notice.Message.Contains("99"), "invalid adjustment kinds do not enter user-facing content");
        Assert(notice.AccessibilityObject.Name.EndsWith(": " + notice.Message), "accessible failure summary contains display context and visible message");
        var help = notice.Controls.OfType<LinkLabel>().Single(c => c.Text == "Connection help");
        InvokeLink(help);
        Assert(helpCalls == 1 && notice.Visible, "failure help navigates without dismissing the notice");
        var dismiss = notice.Controls.OfType<LinkLabel>().Single(c => c.Text == "Dismiss notice");
        int brightnessBefore = monitor.Brightness;
        int contrastBefore = monitor.Contrast;
        InvokeLink(dismiss);
        Assert(!notice.Visible && notice.Message == "", "explicit dismissal hides and resets the failure summary");
        Assert(monitor.Brightness == brightnessBefore && monitor.Contrast == contrastBefore,
            "dismissing failure feedback does not adjust a display");
        card.ShowWriteFailure(DdcWorker.Feature.Contrast);
        Assert(!notice.Message.Contains("brightness"), "new failures after dismissal start a fresh summary");
        using var withoutHelp = new AdjustmentNotice("Fictitious display", false);
        Assert(!withoutHelp.Controls.OfType<LinkLabel>().Single(c => c.Text == "Connection help").Visible,
            "notice does not offer unbound navigation");
    }

    private static void InvokeLink(LinkLabel link) =>
        typeof(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(link, [new LinkLabelLinkClickedEventArgs(link.Links[0])]);

    private static void CheckConnectionGuide(string renderDirectory)
    {
        using var external = Display("external");
        external.Description = "Display A";
        external.PositionLabel = "Left";
        external.SupportsBrightness = true;
        external.BrightnessBackend = Monitor.Backend.Ddc;
        var support = DisplaySupport.Describe(external);
        Assert(support.NeedsHelp && support.Controls.Contains("brightness") && !support.Controls.Contains("brightness and contrast are unavailable"),
            "missing contrast does not imply missing brightness or DDC");
        Assert(support.Guidance.Contains("contrast did not"), "partial availability names the affected control");
        external.SupportsBrightness = false;
        external.SupportsContrast = true;
        Assert(DisplaySupport.Describe(external).Controls == "Available hardware controls: contrast.",
            "contrast remains available when brightness does not respond");
        using var laptop = Display("laptop");
        laptop.Description = "Display B";
        laptop.PositionLabel = "Centre";
        laptop.IsInternalPanel = true;
        Assert(DisplaySupport.Describe(laptop).Guidance.Contains("not DDC/CI"),
            "unavailable built-in brightness routes to the Windows interface");
        laptop.SupportsBrightness = true;
        laptop.BrightnessBackend = Monitor.Backend.Wmi;
        Assert(DisplaySupport.Inventory(new[] { laptop }).Contains("brightness is available on all"),
            "WMI brightness is counted without claiming DDC support");
        Assert(DisplaySupport.Inventory(Array.Empty<Monitor>()).Contains("Windows display settings"),
            "empty enumeration offers a concrete next step");

        using var form = new PreviewForm { Font = Theme.Body, ClientSize = new Size(460, 560) };
        using var guide = new ConnectionGuide { Dock = DockStyle.Fill };
        form.Controls.Add(guide);
        form.Show();
        guide.SetMonitors(new[] { external, laptop });
        form.PerformLayout();
        guide.PerformLayout();
        string text = string.Join("\n", Descendants(guide).Select(c => c.Text));
        Assert(text.Contains("External monitor") && text.Contains("Built-in laptop display") && text.Contains("No displays found?"),
            "connection instructions are bundled locally for every connection path");
        Assert(text.Contains("Review before sharing"), "support guidance explains diagnostic identifiers before sharing");
        Assert(guide.HorizontalScroll.Maximum <= guide.ClientSize.Width,
            "compact connection guide wraps without horizontal scrolling");
        Assert(guide.AutoScrollMinSize.Height > guide.ClientSize.Height || guide.VerticalScroll.Visible,
            "long connection guidance remains reachable through vertical scrolling");
        guide.SetMonitors(new[] { external }, DisplayScanState.Failed);
        text = string.Join("\n", Descendants(guide).Select(c => c.Text));
        Assert(text.Contains("Could not refresh") && !text.Contains("Available hardware controls: contrast."),
            "failed scan cannot present previous controls as current availability");
        guide.SetMonitors(Array.Empty<Monitor>(), DisplayScanState.NotStarted);
        Assert(Descendants(guide).Any(c => c.Text == "Displays have not been checked yet."),
            "unchecked displays do not become a missing-display diagnosis");
        guide.SetMonitors(new[] { external, laptop });
        int returns = 0;
        guide.ReturnRequested += () => returns++;
        Descendants(guide).OfType<FlatButton>().Single(c => c.Text == "Back to controls").PerformClick();
        Assert(returns == 1, "help returns to controls without a monitor scan or write");
        if (renderDirectory != null)
        {
            Directory.CreateDirectory(renderDirectory);
            using var image = new Bitmap(guide.Width, guide.Height);
            guide.DrawToBitmap(image, guide.ClientRectangle);
            image.Save(Path.Combine(renderDirectory, "connection-guide.png"));
        }
        var back = Descendants(guide).OfType<FlatButton>().Single(c => c.Text == "Back to controls");
        guide.ScrollControlIntoView(back);
        Assert(guide.ClientRectangle.Contains(guide.PointToClient(back.PointToScreen(Point.Empty))),
            "last help action remains reachable by scrolling");
        if (renderDirectory != null)
        {
            using var image = new Bitmap(guide.Width, guide.Height);
            guide.DrawToBitmap(image, guide.ClientRectangle);
            image.Save(Path.Combine(renderDirectory, "connection-guide-end.png"));
        }

        using var worker = new DdcWorker();
        using var card = new MonitorCard(external, null, worker, null, null, null, null, null, () => returns++);
        card.SetWidth(570);
        form.Controls.Remove(guide);
        form.ClientSize = new Size(600, 600);
        form.Controls.Add(card);
        form.PerformLayout();
        Assert(Descendants(card).OfType<LinkLabel>().Any(c => c.Text == "Why are controls unavailable?"),
            "unavailable monitor controls expose connection guidance on the card");
        var helpLink = Descendants(card).OfType<LinkLabel>().Single(c => c.Text == "Why are controls unavailable?");
        typeof(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(helpLink, [new LinkLabelLinkClickedEventArgs(helpLink.Links[0])]);
        Assert(returns == 2, "card help invokes navigation without changing a display");
        Assert(!Descendants(card).Any(c => c.Text is "no DDC" or "no DDC/CI"),
            "card does not turn missing brightness into a DDC diagnosis");
        if (renderDirectory != null)
        {
            using var image = new Bitmap(card.Width, card.Height);
            card.DrawToBitmap(image, card.ClientRectangle);
            image.Save(Path.Combine(renderDirectory, "connection-card.png"));
        }
    }

    private sealed class PreviewForm : Form
    {
        public PreviewForm()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
            ShowInTaskbar = false;
        }
        protected override bool ShowWithoutActivation => true;
    }

    private static void CheckSupportReport(string renderDirectory)
    {
        using var monitor = Display("PRIVATE-STABLE-KEY");
        monitor.Description = "PRIVATE-NAME\n```injected markdown```";
        monitor.AnonymousName = "PRIVATE-ANONYMOUS-NAME";
        monitor.DeviceName = "PRIVATE-DEVICE";
        monitor.DeviceInterfaceId = "PRIVATE-INTERFACE";
        monitor.PositionLabel = "PRIVATE-POSITION";
        monitor.Diagnostic = "PRIVATE-ERROR C:\\Users\\PRIVATE-USER\\secret.txt";
        monitor.CapabilityString = "PRIVATE-CAPABILITIES";
        monitor.WmiInstanceName = "PRIVATE-WMI";
        monitor.Edid = new EdidInfo { Manufacturer = "PRIVATE-MAKER", ModelName = "PRIVATE-MODEL", TextSerial = "PRIVATE-SERIAL", BinarySerial = 123456789 };
        monitor.SupportsBrightness = true;
        monitor.BrightnessMin = 0;
        monitor.BrightnessMax = 255;
        monitor.BrightnessBackend = Monitor.Backend.Ddc;
        var document = SupportReport.Create(new[] { monitor }, DisplayScanState.Ready, "1.1.0", new Version(10, 0, 12345));
        string text = SupportReport.Text(document), json = SupportReport.Json(document);
        Assert(!text.Contains("PRIVATE") && !json.Contains("PRIVATE") && !json.Contains("123456789"),
            "support projection excludes hostile names, IDs, serials, paths and diagnostic text");
        using (var parsed = JsonDocument.Parse(json))
        {
            Assert(parsed.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(
                new[] { "schemaVersion", "appVersion", "windowsVersion", "scanState", "displays" }.Order()),
                "report root contains only the declared schema fields");
            Assert(parsed.RootElement.GetProperty("displays")[0].EnumerateObject().Select(p => p.Name).Order().SequenceEqual(
                new[] { "number", "brightnessAvailable", "brightnessRange", "contrastAvailable", "contrastRange", "brightnessBackend", "gammaRampReadable", "extraControlsScan" }.Order()),
                "display report contains only the declared diagnostic fields");
            Assert(parsed.RootElement.GetProperty("schemaVersion").GetInt32() == 1 &&
                   parsed.RootElement.GetProperty("displays")[0].GetProperty("brightnessRange").GetProperty("maximum").GetInt32() == 255,
                "support JSON preserves schema and native hardware range");
            Assert(parsed.RootElement.GetProperty("displays")[0].GetProperty("contrastRange").ValueKind == JsonValueKind.Null,
                "unavailable contrast cannot emit a fabricated range");
        }
        monitor.Description = "a different private identity";
        monitor.StableKey = "another private key";
        Assert(SupportReport.Json(SupportReport.Create(new[] { monitor }, DisplayScanState.Ready, "1.1.0", new Version(10, 0, 12345))) == json,
            "changing only identity does not change the support report");
        foreach (var state in new[] { DisplayScanState.NotStarted, DisplayScanState.Reading, DisplayScanState.Failed, (DisplayScanState)999 })
            Assert(SupportReport.Create(new[] { monitor }, state, "1.1.0", null).Displays.Count == 0,
                $"{state} report omits stale display details");
        monitor.BrightnessBackend = (Monitor.Backend)999;
        var unknown = SupportReport.Create(new[] { monitor }, DisplayScanState.Ready, "PRIVATE-VERSION\n1.0", null);
        Assert(unknown.AppVersion == "unknown" && unknown.Displays[0].BrightnessBackend == "none or unknown",
            "unknown enum and hostile version cannot inject report text");

        int scans = 0;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        int validExit, rejectedExit, failedExit;
        string validOutput, failedOutput;
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            validExit = Cli.RunSupportReport(["--support-report", "--json"], () => { scans++; return new() { monitor }; });
            validOutput = output.ToString();
            rejectedExit = Cli.RunSupportReport(["--support-report", "-b", "20"], () => { scans++; return new(); });
            output.GetStringBuilder().Clear();
            failedExit = Cli.RunSupportReport(["--support-report", "--json"], () => throw new InvalidOperationException("PRIVATE-ERROR"));
            failedOutput = output.ToString();
        }
        finally { Console.SetOut(originalOut); Console.SetError(originalError); }
        Assert(validExit == 0 && scans == 1 && !validOutput.Contains("PRIVATE"), "CLI support report uses the allowlisted projection");
        Assert(rejectedExit != 0 && scans == 1, "combined report and adjustment is rejected before hardware enumeration");
        using (var failed = JsonDocument.Parse(failedOutput))
            Assert(failedExit != 0 && failed.RootElement.GetProperty("scanState").GetString() == "failed" &&
                   failed.RootElement.GetProperty("displays").GetArrayLength() == 0 && !error.ToString().Contains("PRIVATE"),
                "failed JSON scan preserves schema and omits raw errors");

        string copied = null;
        using var form = new PreviewForm { ClientSize = new Size(640, 520), Font = Theme.Body };
        using var view = new SupportReportView(value => copied = value) { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        form.Show();
        view.SetReport(document);
        Assert(copied == null, "opening or updating a preview does not touch the clipboard");
        var preview = Descendants(view).OfType<TextBox>().Single();
        Assert(preview.ReadOnly && preview.Text == text, "read-only preview contains the exact copy payload");
        Descendants(view).OfType<FlatButton>().Single(c => c.Text == "Copy report").PerformClick();
        Assert(copied == preview.Text, "explicit Copy sends only the displayed report to the injected clipboard");
        if (renderDirectory != null)
        {
            Directory.CreateDirectory(renderDirectory);
            using var image = new Bitmap(view.Width, view.Height);
            view.DrawToBitmap(image, view.ClientRectangle);
            image.Save(Path.Combine(renderDirectory, "support-report.png"));
        }
        view.SetReport(SupportReport.Create(new[] { monitor }, DisplayScanState.Failed, "1.1.0", null));
        Descendants(view).OfType<FlatButton>().Single(c => c.Text == "Copy report").PerformClick();
        Assert(copied == preview.Text && copied.Contains("Display scan: failed") && !copied.Contains("Display 1"),
            "updating a preview cannot copy stale successful display records");
        using var failedView = new SupportReportView(_ => throw new InvalidOperationException("PRIVATE-CLIPBOARD-ERROR"));
        failedView.SetReport(document);
        Descendants(failedView).OfType<FlatButton>().Single(c => c.Text == "Copy report").PerformClick();
        Assert(Descendants(failedView).OfType<Label>().Any(c => c.Text.StartsWith("Could not copy")) &&
               !Descendants(failedView).Any(c => c.Text.Contains("PRIVATE")),
            "clipboard failure offers a retry without exposing exception text");
    }

    private static void Invoke(Slider slider, string method, object argument) =>
        typeof(Slider).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(slider, [argument]);

    private static void CheckSlider()
    {
        using var slider = new Slider { Minimum = 0, Maximum = 255, Value = 100 };
        int changes = 0;
        slider.ValueChanged += (_, _) => changes++;
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert(slider.Value == 101 && changes == 1, "one hardware unit on a 0-255 brightness range");
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right | Keys.Shift));
        Assert(slider.Value == 111, "Shift moves ten hardware units");
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.End));
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert(slider.Value == 255 && changes == 3, "clamping does not emit a redundant write");
        slider.SetValueSilently(42);
        Assert(changes == 3 && slider.Value == 42, "hardware synchronization is silent");
        Invoke(slider, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 0, 0, 60));
        Assert(slider.Value == 42, "partial wheel delta is held");
        Invoke(slider, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 0, 0, 60));
        Assert(slider.Value == 43, "precision wheel moves one hardware unit");
        slider.Enabled = false;
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Left));
        Assert(slider.Value == 43, "a blanked display cannot receive a keyboard brightness change");
        slider.Enabled = true;
        slider.Maximum = 3500;
        slider.SmallChange = 50;
        slider.Value = 1000;
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert(slider.Value == 1050, "warmth supports a precise 50 K step");
    }

    private static Monitor Display(string key) => new()
    {
        StableKey = key, Description = key,
        // Invisible test overlays: these rectangles are outside the desktop.
        Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29900, Bottom = -29900 },
    };

    private static void CheckBlanking(string directory)
    {
        string path = Path.Combine(directory, "settings.json");
        var settings = new AppSettings(path);
        using var worker = new DdcWorker();
        using var blanker = new ScreenBlanker(settings, worker, (_, _, _) => { });
        using var first = Display("first");
        Assert(blanker.Blank(first, activate: false), "overlay-only displays can enter the night blackout");
        Assert(settings.ScreenBlankActiveFor(first.StableKey) && File.Exists(path),
            "recovery marker is persisted before blanking");
        Assert(blanker.Blank(first, activate: false), "repeat blank is idempotent");
        using var rebound = Display("first");
        blanker.Rebind(rebound);
        Assert(blanker.IsBlanked("first"), "Refresh preserves a blackout by stable identity");
        Assert(blanker.Restore("first") && !settings.ScreenBlankActiveFor("first"),
            "overlay-only restore clears the recovery marker");
        settings.SetScreenBlank("first", "first", true, null);
        Assert(blanker.RecoverInterruptedBlank(first) && !settings.ScreenBlankActiveFor("first"),
            "interrupted overlay-only blackout is recovered");
        settings.SetScreenBlank("first", "first", true, 43.1372549019608, 110);
        Assert(!blanker.RecoverInterruptedBlank(first) && settings.ScreenBlankActiveFor("first"),
            "unavailable brightness backend retains the restore marker");
        Assert(settings.Save(), "isolated settings save succeeds");
        var persisted = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
        Assert(persisted.Find("first").ScreenBlankBrightnessRaw == 110,
            "exact hardware brightness survives serialization");
        first.SupportsBrightness = true;
        first.BrightnessMax = 255;
        first.Brightness = 0;
        settings.Find("first").CustomBrightnessPercent = 60;
        settings.CaptureCustom(first);
        Assert(settings.Find("first").CustomBrightnessPercent == 60,
            "editing another control during blackout preserves Custom brightness");
        first.SupportsBrightness = false;
        settings.SetScreenBlank("first", "first", false, null);
        using var second = Display("second");
        blanker.Blank(first, activate: false);
        blanker.Blank(second, activate: false);
        blanker.RestoreAll();
        Assert(!blanker.IsBlanked("first") && !blanker.IsBlanked("second"),
            "exit restores all active overlays");
        using var invalid = Display("invalid");
        invalid.Rect.Right = invalid.Rect.Left;
        Assert(!blanker.Blank(invalid), "invalid display geometry cannot create a blackout");
        string blockedPath = Path.Combine(directory, "blocked");
        File.WriteAllText(blockedPath, "a file cannot be a directory");
        var blockedSettings = new AppSettings(Path.Combine(blockedPath, "settings.json"));
        using var blockedBlanker = new ScreenBlanker(blockedSettings, worker, (_, _, _) => { });
        Assert(!blockedBlanker.Blank(first, activate: false) && !blockedBlanker.IsBlanked("first") &&
            !blockedSettings.ScreenBlankActiveFor("first"),
            "failed recovery persistence aborts blackout without leaving active state");
    }

    private static void CheckNightSaver(string directory)
    {
        var settings = new AppSettings(Path.Combine(directory, "night.json"));
        using var worker = new DdcWorker();
        using var blanker = new ScreenBlanker(settings, worker, (_, _, _) => { });
        var night = new NightSaver(blanker, settings, worker);
        using var work = Display("work");
        using var side = Display("side");
        using var independent = Display("independent");
        Monitor[] desk = [work, side, independent];
        blanker.Blank(independent, activate: false);
        Assert(night.Start(desk, "missing") == -1 && !blanker.IsBlanked("side"),
            "missing work display cannot darken the desk");
        Assert(night.Start(desk, "work") == 0 && night.Count == 1 &&
            !blanker.IsBlanked("work") && blanker.IsBlanked("side"),
            "night saver preserves the selected work display and blanks the other available display");
        Assert(night.Start(desk, "work") == 0 && night.Count == 1,
            "repeated night activation does not replace the original restore state");
        night.Restore(desk);
        Assert(night.Count == 0 && !blanker.IsBlanked("side") && blanker.IsBlanked("independent"),
            "group restore leaves independently blanked displays untouched");
        Assert(night.Start(desk, "independent") == -1,
            "a blanked work display cannot start a night session");
        night.Start(desk, "work");
        blanker.Restore("side");
        night.ForgetRestored("side");
        Assert(night.Count == 0, "individual wake updates the night session state");
        using var unavailable = Display("unavailable");
        unavailable.SupportsBrightness = true;
        unavailable.BrightnessMax = 255;
        unavailable.Brightness = 110;
        // No physical handle: writes safely fail without calling monitor APIs.
        Monitor[] failingDesk = [work, unavailable];
        night.Start(failingDesk, "work");
        Assert(settings.Find("unavailable").ScreenBlankBrightnessRaw == 110 &&
            unavailable.Brightness == 0, "night saver saves raw brightness before requesting minimum");
        night.Restore(failingDesk);
        Assert(night.Count == 1 && settings.ScreenBlankActiveFor("unavailable") &&
            !blanker.IsBlanked("unavailable"), "failed hardware restore removes the overlay but retains retry state");
        night.Restore([work]);
        Assert(night.Count == 1, "disconnected displays retain their night restore snapshot");
        night.Restore(failingDesk);
        Assert(night.Count == 1, "retry failure cannot report the night desk as restored");
    }
}
