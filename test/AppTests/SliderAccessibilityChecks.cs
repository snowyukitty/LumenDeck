using System.Reflection;
using System.Runtime.InteropServices;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private delegate void AccessibilityEventCallback(IntPtr hook, uint eventId, IntPtr window,
        int objectId, int childId, uint eventThread, uint eventTime);
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint minimum, uint maximum, IntPtr module,
        AccessibilityEventCallback callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);

    private static void CheckSliderAccessibility(string directory)
    {
        using var fixture = new Fixture(directory, enumerate: _ =>
        [
            new Display { StableKey = "accessibility-left", Description = "Example identical model", PositionLabel = "left, primary",
                IsPrimary = true, Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29200, Bottom = -29400 },
                SupportsBrightness = true, BrightnessMin = 0, BrightnessMax = 255, Brightness = 100,
                SupportsContrast = true, ContrastMin = 10, ContrastMax = 90, Contrast = 40 },
            new Display { StableKey = "accessibility-right", Description = "Example identical model", PositionLabel = "right",
                Rect = new Native.RECT { Left = -29200, Top = -30000, Right = -28400, Bottom = -29400 },
                SupportsBrightness = false, SupportsContrast = false },
        ]);
        Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer").Stop();
        var cards = Controls(fixture.Form).OfType<MonitorCard>().ToArray();
        var sliders = Controls(cards[0]).OfType<Slider>().ToArray();
        var brightness = sliders.Single(s => s.AccessibleName.EndsWith(": Brightness (hardware units)"));
        var contrast = sliders.Single(s => s.AccessibleName.EndsWith(": Contrast (hardware units)"));
        var warmth = sliders.Single(s => s.AccessibleName.EndsWith(": Colour temperature (kelvin)"));
        var otherBrightness = Controls(cards[1]).OfType<Slider>().Single(s => s.AccessibleName.EndsWith(": Brightness (hardware units)"));
        Assert(brightness.AccessibilityObject.Name.Contains("left, primary") &&
            otherBrightness.AccessibilityObject.Name.Contains("right") &&
            brightness.AccessibilityObject.Name != otherBrightness.AccessibilityObject.Name,
            "identical monitor models have distinct accessible slider names using display position");
        Assert(brightness.AccessibilityObject.Value.Contains("100 hardware units") &&
            brightness.AccessibilityObject.Value.Contains("39%") && brightness.AccessibilityObject.Description.Contains("range 0 to 255"),
            "brightness accessibility distinguishes hardware range from mapped percent readout");
        Assert(contrast.AccessibilityObject.Value == "40 hardware units" && contrast.AccessibilityObject.Description.Contains("range 10 to 90") &&
            !contrast.AccessibilityObject.Description.Contains("percentage"),
            "contrast accessibility preserves its raw readout and native range");
        Assert(warmth.AccessibilityObject.Value.Contains("6500 kelvin") && warmth.AccessibilityObject.Description.Contains("range 3000 to 6500") &&
            warmth.AccessibilityObject.Description.Contains("Right or Up") && warmth.AccessibilityObject.Description.Contains("F2 or Enter"),
            "neutral warmth exposes kelvin range and directional keyboard guidance instead of internal warmth bounds");
        warmth.SetValueSilently(1900);
        Assert(warmth.AccessibilityObject.Value == "4600 kelvin requested" && warmth.AccessibilityObject.Description.Contains("Current value 4600"),
            "warmth synchronization exposes the converted kelvin value without adjusting gamma");
        Assert((otherBrightness.AccessibilityObject.State & AccessibleStates.Unavailable) != 0,
            "unsupported brightness remains unavailable to assistive navigation");
        int adjustments = 0, events = 0, statusEvents = 0;
        brightness.ValueChanged += (_, _) => adjustments++;
        IntPtr expectedWindow = brightness.Handle;
        var status = Private<StatusStripe>(fixture.Form, "_status");
        IntPtr expectedStatus = status.Handle;
        AccessibilityEventCallback callback = (_, eventId, window, objectId, childId, _, _) =>
        {
            if (window == expectedWindow && eventId == 0x800E && objectId == -4 && childId == 0) events++;
            if (window == expectedStatus && eventId == 0x800C && objectId == -4 && childId == 0) statusEvents++;
        };
        IntPtr hook = SetWinEventHook(0x800C, 0x800E, IntPtr.Zero, callback, (uint)Environment.ProcessId, 0, 0);
        Assert(hook != IntPtr.Zero, "scoped current-process accessibility event hook is available");
        try
        {
            brightness.SetValueSilently(101);
            PumpUntil(() => events >= 1, "hardware-sync value notification delivered");
            Assert(adjustments == 0 && fixture.Transport.Writes == 0 && brightness.AccessibilityObject.Value.Contains("101 hardware units"),
                "accessibility notification during synchronization creates no adjustment callback or transport write");
            int before = events;
            brightness.SetValueSilently(101);
            Application.DoEvents();
            Assert(events == before && adjustments == 0, "unchanged synchronized values emit no redundant notification or adjustment");
            status.Set("Synthetic accessibility status", StatusKind.Warn);
            PumpUntil(() => statusEvents > 0, "status name notification delivered");
            Assert(status.AccessibilityObject.Name == "Synthetic accessibility status",
                "status notification names the current accessible message using the runtime-compatible path");
            // A standalone slider exercises keyboard dispatch without a card
            // collaborator, avoiding a deliberately requested monitor write.
            using var keyboard = new Slider { Minimum = 0, Maximum = 255, Value = 100 };
            fixture.Form.Controls.Add(keyboard);
            expectedWindow = keyboard.Handle;
            int keyboardEvents = events;
            int requested = 0;
            keyboard.ValueChanged += (_, _) => requested++;
            typeof(Slider).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(keyboard, [new KeyEventArgs(Keys.Right)]);
            PumpUntil(() => events > keyboardEvents, "keyboard value notification delivered");
            Assert(keyboard.Value == 101 && requested == 1, "keyboard adjustment keeps its single callback after value notification refactoring");
            keyboard.Minimum = 0;
            keyboard.Maximum = 3500;
            keyboard.SmallChange = 50;
            keyboard.EditValue = v => 6500 - v;
            keyboard.SetValueSilently(0);
            var keyMethod = typeof(Slider).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
            keyMethod.Invoke(keyboard, [new KeyEventArgs(Keys.Right)]);
            Assert(keyboard.EditValue(keyboard.Value) == 6450, "warmth Right requests 50 kelvin warmer");
            keyMethod.Invoke(keyboard, [new KeyEventArgs(Keys.Left | Keys.Shift)]);
            Assert(keyboard.EditValue(keyboard.Value) == 6500, "warmth Shift Left cools toward neutral and clamps");
            keyMethod.Invoke(keyboard, [new KeyEventArgs(Keys.End)]);
            Assert(keyboard.EditValue(keyboard.Value) == 3000, "warmth End requests the documented warmest endpoint");
            keyMethod.Invoke(keyboard, [new KeyEventArgs(Keys.Home)]);
            Assert(keyboard.EditValue(keyboard.Value) == 6500, "warmth Home requests the documented neutral endpoint");
        }
        finally
        {
            Assert(UnhookWinEvent(hook), "scoped accessibility hook is released");
            GC.KeepAlive(callback);
        }
        Assert(fixture.Transport.Writes == 0 && fixture.StartupWrites == 0 && fixture.GammaSaves == 0,
            "accessibility projections and notifications perform no physical adjustment, registration or gamma checkpoint");
    }
}
