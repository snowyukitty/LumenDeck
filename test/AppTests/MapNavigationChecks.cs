using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static void PickMapDisplay(MainForm form, int index)
    {
        var map = Private<LayoutMap>(form, "_map");
        var boxes = (List<RectangleF>)typeof(LayoutMap)
            .GetMethod("Project", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, null);
        var box = boxes[index];
        var point = Point.Round(new PointF(box.X + box.Width / 2, box.Y + box.Height / 2));
        typeof(LayoutMap).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(map, [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);
    }

    private static void CheckMapNavigation(string directory)
    {
        using var fixture = new Fixture(directory, enumerate: _ =>
            [FocusDisplay("map-left", -30000), FocusDisplay("map-middle", -29000), FocusDisplay("map-right", -28000, false)]);
        var form = fixture.Form;
        var list = Private<FlowLayoutPanel>(form, "_list");
        var map = Private<LayoutMap>(form, "_map");
        Private<System.Windows.Forms.Timer>(form, "_saveTimer").Stop();
        byte[] settings = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        int scans = fixture.Scans;
        foreach (var size in new[] { new Size(604, 800), new Size(780, 900), new Size(1100, 700) })
        {
            form.ClientSize = size;
            form.PerformLayout();
            Assert(map.Visible, "map-navigation fixture leaves the map visible");
            foreach (int index in new[] { 2, 0, 1, 1, 0, 2 })
            {
                string key = new[] { "map-left", "map-middle", "map-right" }[index];
                var card = Controls(form).OfType<MonitorCard>().Single(c => c.Monitor.StableKey == key);
                var target = FocusTarget(form, key, index == 2 ? "warmth" : "brightness");
                form.ActiveControl = FocusTarget(form, index == 0 ? "map-middle" : "map-left", "contrast");
                list.AutoScrollPosition = new Point(0, list.DisplayRectangle.Height);
                PickMapDisplay(form, index);
                Assert(form.ActiveControl == target, "map click transfers selection to the picked display's brightness or available warmth control");
                var bounds = new Rectangle(list.PointToClient(target.PointToScreen(Point.Empty)), target.Size);
                Assert(list.ClientRectangle.Contains(bounds), "picked display's adjustment bar is fully visible");
                if (index != 2)
                    Assert(Math.Abs(card.Top - list.Padding.Top) <= 1, "map click aligns the picked card's header and brightness at the top of the adjustment viewport");
                Assert(map.Controls.OfType<Label>().Single().Text.Contains(card.Monitor.PositionLabel),
                    "map caption agrees with the selected adjustment display");
                list.AutoScrollPosition = new Point(0, list.DisplayRectangle.Height);
                PickMapDisplay(form, index);
                Assert(form.ActiveControl == target && list.ClientRectangle.Contains(new Rectangle(
                    list.PointToClient(target.PointToScreen(Point.Empty)), target.Size)),
                    "clicking the already focused display realigns its adjustment bar after scrolling away");
            }
        }
        Assert(fixture.Scans == scans && fixture.Transport.Writes == 0,
            "map navigation neither scans nor adjusts a display");
        Assert(settings == null ? !File.Exists(fixture.SettingsPath) : File.ReadAllBytes(fixture.SettingsPath).SequenceEqual(settings),
            "map navigation leaves settings unchanged");
        Assert(GetForegroundWindow() != form.Handle, "map selection does not foreground the isolated owner");

        PickMapDisplay(form, 1);
        typeof(Slider).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(form.ActiveControl, [new KeyEventArgs(Keys.Right)]);
        Assert(fixture.LastScan.Single(m => m.StableKey == "map-middle").Brightness == 51 &&
            fixture.LastScan.Where(m => m.StableKey != "map-middle").All(m => m.Brightness == 50),
            "an adjustment after map selection changes only the picked display");
        PumpUntil(() => fixture.Worker.Idle, "selected display adjustment settles");

        var reentrantTarget = FocusTarget(form, "map-left", "brightness");
        form.ActiveControl = Private<DeskCommandBar>(form, "_bar").SettingsButton;
        Task reentrant = null;
        reentrantTarget.Enter += (_, _) => reentrant ??= FocusRefresh(form);
        PickMapDisplay(form, 0);
        Assert(reentrant != null && !Private<StatusStripe>(form, "_status").AccessibleName.StartsWith("Display 1 of 3:"),
            "map selection cannot overwrite scan status when its focus callback starts a replacement scan");
        PumpUntil(() => reentrant.IsCompleted, "map-selection replacement scan completes");
        reentrant.GetAwaiter().GetResult();
        Assert(reentrantTarget.IsDisposed, "map selection safely releases the replaced card");
    }
}
