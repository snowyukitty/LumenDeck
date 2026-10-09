using System.Diagnostics;
using System.Reflection;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckCrossDisplayDpi(string directory, string renderDirectory = null)
    {
        using var fixture = new Fixture(directory, enumerate: scan => [.. Enumerable.Range(0, 4).Select(i => new Display
        {
            StableKey = $"dpi-{i}", Description = $"Synthetic DPI display {i}",
            Rect = new Native.RECT { Left = -30000 + i * 800, Top = -30000, Right = -29200 + i * 800, Bottom = -29400 },
            SupportsBrightness = true, Brightness = 50, BrightnessMin = 0, BrightnessMax = 100,
            SupportsContrast = true, Contrast = 50, ContrastMin = 0, ContrastMax = 100,
        })]);
        var form = fixture.Form;
        var bar = Private<DeskCommandBar>(form, "_bar");
        var list = Private<FlowLayoutPanel>(form, "_list");
        var cards = Controls(form).OfType<MonitorCard>().ToArray();
        Private<System.Windows.Forms.Timer>(form, "_saveTimer").Stop();
        int scans = fixture.Scans, gammaSaves = fixture.GammaSaves;
        IntPtr handle = form.Handle;
        // This covers the transaction even on a single-DPI CI worker. A size
        // query can be followed by many intermediate resizes, or no DPI change.
        // Both must release layout and leave one final pinned card width.
        int width = cards[0].Width;
        var originalSize = form.Size;
        var query = typeof(MainForm).GetMethod("OnGetDpiScaledSize", BindingFlags.Instance | BindingFlags.NonPublic);
        query.Invoke(form, [form.DeviceDpi, form.DeviceDpi + 24, form.Size]);
        form.Width += 120;
        form.Width += 80;
        Assert(cards.All(c => c.Width == width), "DPI size query defers card measurement through intermediate resizes");
        Application.DoEvents();
        Assert(!Private<bool>(form, "_dpiLayoutPending") && cards.All(c => c.Width > width),
            "DPI query without a subsequent change still completes final card layout");
        form.Size = originalSize;
        Application.DoEvents();
        Assert(cards.All(c => c.Width == width), "ordinary resizing works after the DPI transaction completes");
        var screens = Screen.AllScreens;
        var targets = new List<(Point Location, int Dpi)>();
        foreach (var screen in screens)
        {
            var bounds = screen.WorkingArea;
            form.Location = new Point(bounds.Left + (bounds.Width - form.Width) / 2,
                bounds.Top + (bounds.Height - form.Height) / 2);
            Application.DoEvents();
            if (!targets.Any(t => t.Dpi == form.DeviceDpi)) targets.Add((form.Location, form.DeviceDpi));
        }
        if (targets.Count < 2)
        {
            Console.WriteLine("SKIP: actual cross-display DPI transitions require monitors with different scaling.");
            return;
        }
        int layouts = 0, heights = 0, cardLayouts = 0;
        foreach (var control in cards.SelectMany(c => Controls(c).Prepend(c))) control.Layout += (_, _) => cardLayouts++;
        foreach (var control in Controls(bar).Prepend(bar)) control.Layout += (_, _) => layouts++;
        bar.SizeChanged += (_, _) => heights++;
        var settled = new Dictionary<int, (Size Window, int Bar, int CardWidth, int CardHeight, float Font)>();
        for (int round = 0; round < 6; round++)
        {
            var target = targets[round % 2];
            layouts = heights = cardLayouts = 0;
            var watch = Stopwatch.StartNew();
            form.Location = target.Location;
            long moveMilliseconds = watch.ElapsedMilliseconds;
            Application.DoEvents();
            Console.WriteLine($"INFO: DPI transition to {form.DeviceDpi}, {watch.ElapsedMilliseconds} ms (native move {moveMilliseconds} ms), command layouts {layouts}, card layouts {cardLayouts}, bar size changes {heights}, bar height {bar.Height}, window {form.Size}");
            Assert(form.DeviceDpi == target.Dpi, "native window adopts the destination monitor DPI");
            Assert(form.Handle == handle && Controls(form).OfType<MonitorCard>().SequenceEqual(cards), "DPI transition retains window and cards");
            Assert(GetForegroundWindow() != handle, "private-desktop DPI test never activates the owner window");
            Assert(layouts <= 30 && cardLayouts <= 240 && heights <= 3,
                "DPI transition bounds command/card layout work and bar height churn");
            var metrics = (form.Size, bar.Height, cards[0].Width, cards[0].Height, bar.NightButton.Font.Size);
            if (settled.TryGetValue(target.Dpi, out var previous))
                Assert(metrics == previous, "repeated DPI round trips do not drift window, card, bar or font dimensions");
            else settled[target.Dpi] = metrics;
            foreach (CommandSection section in bar.Controls)
                Assert(section.Commands.Controls.Cast<Control>().All(c => section.Commands.ClientRectangle.Contains(c.Bounds)),
                    "destination DPI command text and selectors remain inside their row");
            Assert(cards.All(c => c.MinimumSize.Width == c.Width && c.MaximumSize.Width == c.Width),
                "destination DPI cards retain their final width constraints");
            Console.WriteLine($"INFO: card viewport {list.ClientSize}, virtual {list.DisplayRectangle}, minimum {list.AutoScrollMinSize}, card bounds {cards[0].Bounds}");
            Assert(!list.HorizontalScroll.Visible, "DPI round trip leaves no stale horizontal scroll extent");
            if (renderDirectory != null && round < 2)
            {
                Directory.CreateDirectory(renderDirectory);
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                image.Save(Path.Combine(renderDirectory, $"native-dpi-{form.DeviceDpi}.png"));
            }
        }
        Assert(fixture.Scans == scans && fixture.GammaSaves == gammaSaves, "DPI movement does not rescan displays or modify gamma");
    }
}
