using LumenDeck;
using System.Reflection;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckLayoutMap(string renderDirectory)
    {
        CheckProportionalLayout();
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
        Assert(caption.Text.Contains("Select a display") && map.TabStop, "map gives a discoverable hint and joins keyboard navigation");
        Mouse("OnMouseClick", Center(0), MouseButtons.Right);
        Assert(picks == 0 && caption.Text.Contains("Select a display"), "right click does not navigate or select a display");
        Mouse("OnMouseClick", Center(0), MouseButtons.Middle);
        Assert(picks == 0, "middle click does not navigate or select a display");
        Mouse("OnMouseClick", Center(0), MouseButtons.Left);
        Assert(picks == 1 && ReferenceEquals(picked, left) && caption.Text == "Display 1 · Studio & Desk (left)", "left click identifies and routes the exact display once");
        Mouse("OnMouseMove", Center(1));
        Assert(caption.Text == "Display 2 · Studio & Desk (right)" && picks == 1, "hover previews the other display without activating it");
        typeof(LayoutMap).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, [EventArgs.Empty]);
        Assert(caption.Text == "Display 1 · Studio & Desk (left)", "leaving the map restores persistent selected identity");
        map.Refresh(right);
        Assert(caption.Text == "Display 2 · Studio & Desk (right)", "programmatic card navigation updates the same identity caption");
        right.Description = "Studio & Desk " + new string('W', 100);
        right.PositionLabel = " ";
        map.Refresh(right);
        Assert(caption.Text == "Display 2 · " + right.DisplayName && caption.AccessibilityObject.Name == "Display 2 · " + right.DisplayName &&
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

    private static void CheckProportionalLayout()
    {
        using var left = new Display { Rect = new Native.RECT { Left = -2560, Top = 1024, Right = 0, Bottom = 2464 } };
        using var centre = new Display { Rect = new Native.RECT { Left = 0, Top = 0, Right = 3840, Bottom = 2160 } };
        using var portrait = new Display { Rect = new Native.RECT { Left = 3840, Top = -1192, Right = 6000, Bottom = 2648 } };
        Display[] displays = [left, centre, portrait];
        using var map = new LayoutMap();
        List<RectangleF> Boxes() => (List<RectangleF>)typeof(LayoutMap)
            .GetMethod("Project", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, null);
        foreach (var size in new[] { new Size(604, 128), new Size(1000, 140), new Size(220, 500) })
        {
            map.Size = size;
            map.SetMonitors(displays);
            var boxes = Boxes();
            float scale = boxes[0].Width / left.Rect.Width;
            for (int i = 0; i < displays.Length; i++)
            {
                var rect = displays[i].Rect;
                Assert(Math.Abs(boxes[i].Width - rect.Width * scale) < .01f && Math.Abs(boxes[i].Height - rect.Height * scale) < .01f,
                    "one scale preserves every display's pixel size and aspect ratio");
                Assert(Math.Abs(boxes[i].Left - boxes[0].Left - (rect.Left - left.Rect.Left) * scale) < .01f &&
                    Math.Abs(boxes[i].Top - boxes[0].Top - (rect.Top - left.Rect.Top) * scale) < .01f,
                    "negative origins and vertical offsets retain their true desktop geometry");
            }
            Assert(Math.Abs(boxes[0].Right - boxes[1].Left) < .01f && Math.Abs(boxes[1].Right - boxes[2].Left) < .01f,
                "adjacent screens stay adjacent instead of becoming disconnected grid slots");
            Assert(boxes[0].Top < boxes[1].Bottom && boxes[1].Top < boxes[2].Bottom,
                "vertically overlapping screens do not turn into separate schematic rows");
        }
        // A tiny offset must remain tiny; ordinal slots used to exaggerate it
        // into an entire row even when the screen edges almost aligned.
        left.Rect = new Native.RECT { Left = -1920, Top = 0, Right = 0, Bottom = 1080 };
        centre.Rect = new Native.RECT { Left = 0, Top = 10, Right = 1920, Bottom = 1090 };
        portrait.Rect = new Native.RECT { Left = 1920, Top = 20, Right = 3840, Bottom = 1940 };
        map.Size = new Size(604, 128);
        map.SetMonitors(displays);
        var offset = Boxes();
        Assert(offset[1].Top - offset[0].Top < 1 && offset[2].Top - offset[0].Top < 1,
            "near-aligned screens remain near-aligned rather than occupying three rows");
    }
}
