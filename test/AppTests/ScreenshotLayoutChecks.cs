using LumenDeck;

internal static partial class Program
{
    private static void CheckScreenshotLayout(string directory, string renderDirectory)
    {
        using var fixture = new Fixture(directory, enumerate: _ =>
        {
            var displays = DeskDisplays(3);
            displays[0].Brightness = 96;
            displays[0].Rect = new Native.RECT { Left = -2560, Top = 1024, Right = 0, Bottom = 2464 };
            displays[1].Rect = new Native.RECT { Left = 0, Top = 0, Right = 3840, Bottom = 2160 };
            displays[2].Rect = new Native.RECT { Left = 3840, Top = -1192, Right = 6000, Bottom = 2648 };
            displays[0].IsPrimary = false;
            displays[0].PositionLabel = "left";
            displays[1].IsPrimary = true;
            displays[1].PositionLabel = "centre, primary";
            displays[1].SupportsContrast = true;
            displays[1].Brightness = 71;
            displays[1].Contrast = 61;
            displays[2].PositionLabel = "right, portrait";
            return displays;
        }, allDisplays: false);
        var form = fixture.Form;
        // Also cover controls created after the window has adopted a monitor's
        // DPI, as happens after asynchronous discovery in the real app.
        foreach (var screen in Screen.AllScreens)
        {
            form.Location = screen.WorkingArea.Location + new Size(40, 40);
            Application.DoEvents();
            var refresh = FocusRefresh(form);
            PumpUntil(() => refresh.IsCompleted, "screenshot fixture rebuild completes at destination DPI");
            refresh.GetAwaiter().GetResult();
            form.ClientSize = new Size(918, 768);
            Application.DoEvents();
            PickMapDisplay(form, 1);
            var card = Controls(form).OfType<MonitorCard>().Single(c => c.Visible);
            int beforeWrites = fixture.Transport.Writes;
            card.ApplyBrightness(71);
            card.ApplyContrast(61);
            card.ApplyWarmth(4600);
            PumpUntil(() => fixture.Worker.Idle, "synthetic screenshot adjustments settle");
            typeof(MonitorCard).GetMethod("UpdateObservation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(card, null);
            Application.DoEvents();
            var viewport = Private<FlowLayoutPanel>(form, "_list");
            Assert(!viewport.HorizontalScroll.Visible && (GetWindowLong(viewport.Handle, -16) & 0x00100000) == 0,
                $"adjustment feedback leaves no horizontal scrollbar at {form.DeviceDpi} DPI: client={viewport.ClientSize}, extent={viewport.DisplayRectangle}, card={card.Bounds}, managed={viewport.HorizontalScroll.Visible}");
            if (renderDirectory != null)
            {
                Directory.CreateDirectory(renderDirectory);
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                image.Save(Path.Combine(renderDirectory, $"screenshot-layout-{form.DeviceDpi}.png"));
            }
            var surfaces = form.Controls.Cast<Control>().Where(c => c.Visible).ToArray();
            for (int i = 0; i < surfaces.Length; i++)
                for (int j = i + 1; j < surfaces.Length; j++)
                    Assert(!surfaces[i].Bounds.IntersectsWith(surfaces[j].Bounds),
                        $"window sections do not overlap: {surfaces[i].GetType().Name} {surfaces[i].Bounds}, {surfaces[j].GetType().Name} {surfaces[j].Bounds}");
            var principal = Controls(card).OfType<Slider>().Single(s => s.Principal);
            int textHeight = TextRenderer.MeasureText(principal.Format(principal.Value), principal.Font,
                Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height;
            Assert(principal.Height - (int)Math.Round(8 * principal.DeviceDpi / 96.0) >= textHeight,
                $"principal readout has enough vertical room after discovery at {form.DeviceDpi} DPI");
            var sliders = Controls(card).OfType<Slider>().ToArray();
            Assert(sliders.All(s => s.Height <= form.LogicalToDeviceUnits(34)) && sliders.All(s => s.Font.Size == principal.Font.Size),
                "every adjustment keeps a compact, consistent text and row hierarchy");
            var custom = Controls(card).Single(c => c.Name == "preset-custom");
            Assert(viewport.ClientRectangle.Contains(new Rectangle(viewport.PointToClient(custom.PointToScreen(Point.Empty)), custom.Size)),
                "all everyday adjustments and per-display presets fit the screenshot-sized viewport");
            var label = Controls(card).OfType<Label>().Single(l => l.Text == "Brightness");
            Assert(label.Width >= TextRenderer.MeasureText(label.Text, label.Font).Width,
                $"brightness caption does not split its word at {form.DeviceDpi} DPI");
            var map = Private<LayoutMap>(form, "_map");
            int line = TextRenderer.MeasureText("1", map.Font).Height;
            for (int i = 0; i < 3; i++)
            {
                var bounds = map.AccessibilityObject.GetChild(i).Bounds;
                Assert(bounds.Height >= line + 10 && bounds.Width >= line + 16,
                    $"offset display {i + 1} has a readable selection target at {form.DeviceDpi} DPI");
            }
            Assert(fixture.Transport.Writes == beforeWrites + 2, "screenshot regression performs only its two fake adjustment requests");
        }
    }
}
