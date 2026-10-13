using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetFocus")]
    private static extern IntPtr SetDeskTestFocus(IntPtr window);

    private static KeyEventArgs DeskKey(LayoutMap map, Keys key)
    {
        var args = new KeyEventArgs(key);
        typeof(LayoutMap).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, [args]);
        return args;
    }

    private static void CheckSpatialDesk(string directory)
    {
        using var fixture = new Fixture(directory, enumerate: _ => DeskDisplays(4), allDisplays: false);
        var form = fixture.Form;
        var map = Private<LayoutMap>(form, "_map");
        var cards = Controls(form).OfType<MonitorCard>().ToArray();
        var ordered = fixture.LastScan.OrderBy(m => m.Rect.Left).ThenBy(m => m.Rect.Top).ToArray();
        int sliderEntries = 0;
        foreach (var slider in cards.SelectMany(Controls).OfType<Slider>()) slider.Enter += (_, _) => sliderEntries++;
        form.ActiveControl = map;
        SetDeskTestFocus(map.Handle);
        Assert(map.Focused, "private-desktop native focus is on the selector");
        sliderEntries = 0;
        string Selected() => cards.Single(c => c.Visible).Monitor.StableKey;
        foreach (var step in new[] { (Keys.Right, "desk-fixture-1"), (Keys.Down, "desk-fixture-3"),
            (Keys.Left, "desk-fixture-2"), (Keys.Up, "desk-fixture-0") })
        {
            var args = DeskKey(map, step.Item1);
            Assert(args.Handled && args.SuppressKeyPress && Selected() == step.Item2,
                $"{step.Item1} follows the schematic instead of column-major enumeration");
            Assert(form.ActiveControl == map && sliderEntries == 0, "spatial selection never transiently enters an adjustment control");
            var selected = map.AccessibilityObject.GetSelected();
            Assert(selected.State.HasFlag(AccessibleStates.Selected | AccessibleStates.Focused), "accessible selected item reports its actual keyboard focus");
        }
        foreach (Keys key in new[] { Keys.Up, Keys.Left })
        {
            DeskKey(map, key);
            Assert(Selected() == "desk-fixture-0", "spatial navigation stops at the desk edge without wrapping");
        }
        foreach (Keys key in new[] { Keys.Control | Keys.Right, Keys.Alt | Keys.Down, Keys.Shift | Keys.End })
        {
            var args = DeskKey(map, key);
            Assert(!args.Handled && !args.SuppressKeyPress && Selected() == "desk-fixture-0", "modified map keys are left to their command owner");
        }
        KeyEventHandler intercept = (_, e) => e.Handled = true;
        map.KeyDown += intercept;
        DeskKey(map, Keys.Right);
        map.KeyDown -= intercept;
        Assert(Selected() == "desk-fixture-0", "handled map input cannot perform a second selection");
        DeskKey(map, Keys.End);
        Assert(Selected() == "desk-fixture-3", "End selects the final display in layout order");
        DeskKey(map, Keys.Home);
        Assert(Selected() == "desk-fixture-0", "Home selects the first display in layout order");
        DeskKey(map, Keys.Right);
        DeskKey(map, Keys.Enter);
        Assert(form.ActiveControl == FocusTarget(form, "desk-fixture-1", "brightness") && sliderEntries == 1,
            "Enter explicitly opens the selected screen adjustment once");

        var selectedItem = map.AccessibilityObject.GetChild(Array.FindIndex(ordered, m => m.StableKey == "desk-fixture-1"));
        Assert(!selectedItem.State.HasFlag(AccessibleStates.Focused) && map.AccessibilityObject.GetFocused() == null,
            "selection remains distinct from focus after entering a slider");
        var bounds = selectedItem.Bounds;
        Assert(map.AccessibilityObject.HitTest(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)?.Name == selectedItem.Name,
            "accessible screen-coordinate hit testing returns the actual display item");
        var otherItem = map.AccessibilityObject.GetChild(0);
        otherItem.Select(AccessibleSelection.None);
        otherItem.Select(AccessibleSelection.RemoveSelection);
        Assert(Selected() == "desk-fixture-1", "unsupported accessible selection flags never activate a display");
        var settings = Private<DeskCommandBar>(form, "_bar").SettingsButton;
        form.ActiveControl = settings;
        otherItem.Select(AccessibleSelection.TakeSelection);
        Assert(Selected() == "desk-fixture-0" && form.ActiveControl == settings, "accessible selection alone does not take keyboard focus");
        selectedItem.Select(AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus);
        Assert(Selected() == "desk-fixture-1" && form.ActiveControl == map, "accessible focus selects the map item without opening a slider");
        var all = map.Controls.OfType<CheckBox>().Single();
        form.ActiveControl = all;
        SetDeskTestFocus(all.Handle);
        Assert(map.AccessibilityObject.GetFocused() == all.AccessibilityObject, "map reports the native All displays checkbox when it has focus");
        map.Enabled = false;
        otherItem.DoDefaultAction();
        Assert(otherItem.State.HasFlag(AccessibleStates.Unavailable) && Selected() == "desk-fixture-1",
            "disabled map items report unavailable and reject accessible activation");
        map.Enabled = true;
        map.Visible = false;
        otherItem.DoDefaultAction();
        Assert(otherItem.Bounds.IsEmpty && otherItem.State.HasFlag(AccessibleStates.Unavailable) && Selected() == "desk-fixture-1",
            "hidden map items have no actionable screen bounds");
        map.Visible = true;

        form.SetAllDisplays(true);
        form.ActiveControl = FocusTarget(form, "desk-fixture-2", "warmth");
        Assert(map.AccessibilityObject.GetSelected().Name.Contains("Laptop"), "entering a card in All displays synchronizes the selected screen");
        form.SetAllDisplays(false);
        Assert(Selected() == "desk-fixture-2" && form.ActiveControl == FocusTarget(form, "desk-fixture-2", "warmth"),
            "returning to single display preserves the screen and control just used");
        Assert(fixture.Transport.Writes == 0, "all spatial and accessible selection paths leave display values unchanged");
        CheckDeskSelectionEvents(fixture, map);

        // An offset neighbour and a portrait tile still follow the diagram's
        // coordinate slots. Enumeration order cannot decide the direction.
        using var diagonal = new Fixture(directory, enumerate: _ =>
        {
            var displays = DeskDisplays(4);
            displays[3].Rect.Left += 1920;
            displays[3].Rect.Right += 1920;
            return [displays[3], displays[1], displays[2], displays[0]];
        }, allDisplays: false);
        var diagonalMap = Private<LayoutMap>(diagonal.Form, "_map");
        diagonal.Form.ActiveControl = diagonalMap;
        DeskKey(diagonalMap, Keys.Right);
        Assert(Controls(diagonal.Form).OfType<MonitorCard>().Single(c => c.Visible).Monitor.StableKey == "desk-fixture-1",
            "an aligned portrait neighbour wins over a diagonal display regardless of enumeration order");
        DeskKey(diagonalMap, Keys.Down);
        Assert(Controls(diagonal.Form).OfType<MonitorCard>().Single(c => c.Visible).Monitor.StableKey == "desk-fixture-2",
            "diagonal ties resolve deterministically in displayed order");
    }

    private static void CheckDeskSelectionEvents(Fixture fixture, LayoutMap map)
    {
        var events = new List<(uint Kind, int Child)>();
        IntPtr expectedWindow = map.Handle;
        AccessibilityEventCallback callback = (_, kind, window, objectId, child, _, _) =>
        {
            if (window == expectedWindow && objectId == -4) events.Add((kind, child));
        };
        IntPtr hook = SetWinEventHook(0x8005, 0x8006, IntPtr.Zero, callback, (uint)Environment.ProcessId, 0, 0);
        Assert(hook != IntPtr.Zero, "selector focus/selection event hook is scoped to this process");
        try
        {
            map.AccessibilityObject.GetChild(0).Select(AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus);
            PumpUntil(() => events.Contains((0x8006, 1)) && events.Contains((0x8005, 1)),
                "native selection and focus events identify the first accessible child");
            Assert(events.Count(e => e.Kind == 0x8006) == 1, "one changed selection emits one native selection notification");
            map.Refresh(fixture.LastScan.Single(m => m.StableKey == "desk-fixture-0"));
            DeskKey(map, Keys.Left);
            Application.DoEvents();
            Assert(events.Count(e => e.Kind == 0x8006) == 1, "unchanged refresh and edge navigation do not repeat selection announcements");
            Task refresh = null;
            map.MonitorSelected += _ => refresh ??= FocusRefresh(fixture.Form);
            DeskKey(map, Keys.Right);
            PumpUntil(() => refresh?.IsCompleted == true, "selection callback can rebuild the desk safely");
            refresh.GetAwaiter().GetResult();
            Assert(fixture.Transport.Writes == 0, "reentrant selection creates no adjustment request");
        }
        finally
        {
            Assert(UnhookWinEvent(hook), "selector native event hook is released");
            GC.KeepAlive(callback);
        }
    }
}
