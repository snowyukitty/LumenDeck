using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckMainWindowLayout(string directory, string renderDirectory)
    {
        using var fixture = new Fixture(directory, enumerate: _ =>
        [
            new Display { StableKey = "synthetic-desktop", Description = "Example desktop display", PositionLabel = "left, primary", IsPrimary = true,
                Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -28080, Bottom = -28920 },
                SupportsBrightness = true, Brightness = 62, BrightnessMin = 0, BrightnessMax = 100,
                SupportsContrast = true, Contrast = 50, ContrastMin = 0, ContrastMax = 100 },
            new Display { StableKey = "synthetic-portrait", Description = "Example portrait display", PositionLabel = "right, portrait",
                Rect = new Native.RECT { Left = -28080, Top = -30000, Right = -27000, Bottom = -28080 },
                SupportsBrightness = true, Brightness = 45, BrightnessMin = 0, BrightnessMax = 100 },
        ]);
        var form = fixture.Form;
        var list = Private<FlowLayoutPanel>(form, "_list");
        var map = Private<LayoutMap>(form, "_map");
        var commands = Private<DeskCommandBar>(form, "_bar");
        var status = Private<StatusStripe>(form, "_status");
        var header = Private<BrandHeader>(form, "_header");
        Assert(WindowChrome.HasDarkFrame(form), "Windows reports the dark window frame for the actual main window");
        foreach (var size in new[] { new Size(780, 1000), new Size(604, 441), new Size(604, 600), new Size(1000, 600), new Size(604, 441), new Size(780, 1000) })
        {
            form.ClientSize = size;
            form.PerformLayout();
            Assert(form.ClientRectangle.Contains(commands.Bounds) && form.ClientRectangle.Contains(status.Bounds),
                $"main commands and status fit at {size.Width}x{size.Height}");
            Assert(list.Top >= commands.Bottom && list.Bottom <= status.Top,
                $"actual card viewport does not overlap fixed chrome at {size.Width}x{size.Height}");
            Assert(list.ClientSize.Height >= 150, $"actual card viewport remains usable at {size.Width}x{size.Height}: list={list.Height}, bar={commands.Height}, map={map.Height}, header={header.Visible}, status={status.Height}");
            Assert(header.Visible == (size.Height >= 600) && (!header.Visible || header.Bottom <= commands.Top) &&
                form.Text == $"LumenDeck {AboutForm.VersionText}" && header.AccessibleName.Contains(AboutForm.VersionText),
                $"the version stays in the title bar and the header yields to cards in a short window at {size.Width}x{size.Height}");
            Assert(map.Visible,
                $"desk map visibility reverses with compact window height at {size.Width}x{size.Height}");
            var cards = Controls(form).OfType<MonitorCard>().ToArray();
            foreach (var card in cards)
            {
                int contentBottom = Controls(card).Where(c => c.Visible && c is Label or Slider or FlatButton or LinkLabel)
                    .Max(c => card.PointToClient(c.PointToScreen(Point.Empty)).Y + c.Height);
                Assert(card.ClientSize.Height - contentBottom <= 40,
                    "card height follows content measured at its pinned width without a blank tail");
            }
            list.ScrollControlIntoView(cards[^1]);
            Assert(cards[^1].Top < list.ClientRectangle.Bottom && cards[^1].Bottom > list.ClientRectangle.Top,
                "last monitor card is reachable through the actual scroll container");
            foreach (var control in Controls(cards[^1]).Where(c => c.Visible && c is Slider or FlatButton or LinkLabel))
            {
                list.ScrollControlIntoView(control);
                var bounds = new Rectangle(list.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Assert(list.ClientRectangle.Contains(bounds),
                    $"last card control {control.AccessibleName ?? control.Text} can be fully scrolled into view");
            }
            list.AutoScrollPosition = Point.Empty;
            if (renderDirectory != null)
            {
                Directory.CreateDirectory(renderDirectory);
                // Form.DrawToBitmap includes nonclient chrome; allocate the
                // complete outer bounds so the status/footer is not cropped.
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                image.Save(Path.Combine(renderDirectory, $"main-window-{size.Width}-{size.Height}.png"));
                var metadata = new
                {
                    fixture = "synthetic-two-displays-v1",
                    synthetic = true,
                    build = "working-tree development build; not a released-version preview",
                    assemblyInformationalVersion = typeof(MainForm).Assembly.GetCustomAttributes(false)
                        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
                    clientWidth = form.ClientSize.Width, clientHeight = form.ClientSize.Height,
                    outerWidth = form.Width, outerHeight = form.Height, deviceDpi = form.DeviceDpi,
                    method = "Actual MainForm.DrawToBitmap including nonclient bounds",
                    estimates = "Generic profiles; calculated nits, not measured light",
                };
                File.WriteAllText(Path.Combine(renderDirectory, $"main-window-{size.Width}-{size.Height}.json"),
                    System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            }
        }
        if (renderDirectory != null)
        {
            PickMapDisplay(form, 0);
            using var selected = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(selected, new Rectangle(Point.Empty, form.Size));
            selected.Save(Path.Combine(renderDirectory, "main-window-selected.png"));
        }
        // Use the hypothetical map-present viewport on both sides of the
        // threshold: hiding the map must not make the next layout show it again.
        foreach (int remainingHeight in new[] { 239, 240, 241, 239, 241 })
        {
            form.ClientSize = new Size(1000, header.Height + commands.Height + status.Height + map.Height
                + (int)Math.Round(remainingHeight * form.DeviceDpi / 96d));
            for (int pass = 0; pass < 3; pass++)
            {
                form.PerformLayout();
                Assert(list.ClientSize.Height >= 150, $"the detail viewport remains usable, pass {pass}");
                Assert(map.Visible,
                    $"selector stays available at {remainingHeight} logical pixels, pass {pass}");
            }
        }
        Assert(fixture.Transport.Writes == 0 && fixture.StartupWrites == 0,
            "resizing, scrolling and rendering the actual MainForm issue no monitor or registration writes");
    }
}
