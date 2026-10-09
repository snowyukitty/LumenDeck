using LumenDeck;
using System.Reflection;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckLayoutMap(string renderDirectory)
    {
        using var left = new Display { Description = "Studio & Desk", PositionLabel = "left",
            Rect = new Native.RECT { Left = -1920, Top = 0, Right = 0, Bottom = 1080 } };
        using var right = new Display { Description = "Studio & Desk", PositionLabel = "right",
            Rect = new Native.RECT { Left = 0, Top = 0, Right = 1080, Bottom = 1920 } };
        using var map = new LayoutMap { Size = new Size(604, 108) };
        using var owner = new QuietLoopForm { ClientSize = map.Size };
        owner.Controls.Add(map);
        owner.Show();
        var caption = map.Controls.OfType<Label>().Single();
        Assert(!caption.AutoSize, $"native caption AutoSize is false; actual {caption.AutoSize}");
        int picks = 0;
        Display picked = null;
        map.MonitorPicked += monitor => { picks++; picked = monitor; };
        void Mouse(string method, Point point, MouseButtons button = MouseButtons.None) =>
            typeof(LayoutMap).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(map, [new MouseEventArgs(button, 1, point.X, point.Y, 0)]);
        List<RectangleF> Boxes() => (List<RectangleF>)typeof(LayoutMap)
            .GetMethod("Project", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, null);
        Point Center(int index) { var b = Boxes()[index]; return Point.Round(new PointF(b.X + b.Width / 2, b.Y + b.Height / 2)); }

        map.SetMonitors([left, right]);
        Assert(caption.Text.Contains("Select a display") && !map.TabStop, "map gives a discoverable hint without changing Tab navigation");
        Mouse("OnMouseClick", Center(0), MouseButtons.Right);
        Assert(picks == 0 && caption.Text.Contains("Select a display"), "right click does not navigate or select a display");
        Mouse("OnMouseClick", Center(0), MouseButtons.Middle);
        Assert(picks == 0, "middle click does not navigate or select a display");
        Mouse("OnMouseClick", Center(0), MouseButtons.Left);
        Assert(picks == 1 && ReferenceEquals(picked, left) && caption.Text == "Studio & Desk (left)", "left click identifies and routes the exact display once");
        Mouse("OnMouseMove", Center(1));
        Assert(caption.Text == "Studio & Desk (right)" && picks == 1, "hover previews the other display without activating it");
        typeof(LayoutMap).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, [EventArgs.Empty]);
        Assert(caption.Text == "Studio & Desk (left)", "leaving the map restores persistent selected identity");
        map.Refresh(right);
        Assert(caption.Text == "Studio & Desk (right)", "programmatic card navigation updates the same identity caption");
        right.Description = "Studio & Desk " + new string('W', 100);
        right.PositionLabel = " ";
        map.Refresh(right);
        Assert(caption.Text == right.DisplayName && caption.AccessibilityObject.Name == right.DisplayName &&
            string.IsNullOrEmpty(caption.AccessibilityObject.KeyboardShortcut), "long literal names remain complete to accessibility clients without empty positions or phantom shortcuts");
        foreach (int width in new[] { 240, 604, 1000 })
        {
            map.Width = width;
            owner.ClientSize = map.Size;
            map.PerformLayout();
            Assert(map.ClientRectangle.Contains(caption.Bounds) && caption.AutoEllipsis, "native identity caption remains bounded at each width");
            Assert(!caption.AutoSize && caption.Height == 24, "caption retains a fixed one-line height at the tested 96 DPI");
            Assert(Boxes().All(box => box.Bottom <= caption.Top), "projected displays leave a separate caption row without overlap");
            if (renderDirectory != null)
            {
                Directory.CreateDirectory(renderDirectory);
                using var image = new Bitmap(map.Width, map.Height);
                map.DrawToBitmap(image, map.ClientRectangle);
                image.Save(Path.Combine(renderDirectory, $"map-caption-{width}.png"));
            }
        }
        Mouse("OnMouseClick", new Point(10, caption.Top + 5), MouseButtons.Left);
        Assert(picks == 1, "caption area does not activate a display rectangle");
        if (renderDirectory != null)
        {
            right.Description = "Studio & Desk";
            right.PositionLabel = "right";
            map.Width = 604;
            map.Refresh(right);
            using var image = new Bitmap(map.Width, map.Height);
            map.DrawToBitmap(image, map.ClientRectangle);
            image.Save(Path.Combine(renderDirectory, "map-caption-normal.png"));
        }
        map.SetMonitors([left]);
        Assert(caption.Text.Contains("Select a display") && map.Cursor == Cursors.Default, "replacing the desk clears stale selected and hover identity");
        map.SetMonitors([]);
        Assert(caption.Text == "" && Boxes().Count == 0 && picks == 1, "empty desk clears identity without routing another command");
    }
}
