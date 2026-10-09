using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static bool NavigationKey(Control source, Keys key)
    {
        object[] args = [Message.Create(source.Handle, 0x0100, (IntPtr)(int)(key & Keys.KeyCode), IntPtr.Zero), key];
        return (bool)typeof(Control).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(source, args);
    }

    private static void CheckDisplayNavigation(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, enumerate: scan =>
        {
            if (scan > 1)
            {
                entered.Set();
                if (!release.Wait(10000)) throw new TimeoutException("navigation scan barrier");
            }
            return [FocusDisplay("nav-right", -29000, false), FocusDisplay("nav-left", -30000)];
        });
        var form = fixture.Form;
        form.ClientSize = new Size(604, 441);
        form.PerformLayout();
        Private<System.Windows.Forms.Timer>(form, "_saveTimer").Stop();
        int scans = fixture.Scans, gamma = fixture.GammaSaves, registrations = fixture.ShortcutRegistrations;
        byte[] settings = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        var bar = Private<DeskCommandBar>(form, "_bar");
        var list = Private<FlowLayoutPanel>(form, "_list");
        const Keys next = Keys.Control | Keys.PageDown, previous = Keys.Control | Keys.PageUp;
        void Select(Control source, Keys key, Control target, string message)
        {
            form.ActiveControl = source;
            Assert(NavigationKey(source, key), "exact display-navigation chord is consumed by parent command routing");
            Assert(form.ActiveControl == target, message);
            Assert(list.ClientRectangle.Contains(new Rectangle(list.PointToClient(target.PointToScreen(Point.Empty)), target.Size)),
                "selected control fits the minimum-size viewport");
        }
        Select(bar.SettingsButton, next, FocusTarget(form, "nav-left", "brightness"), "toolbar next enters the first card in layout order");
        Select(bar.SettingsButton, previous, FocusTarget(form, "nav-right", "warmth"), "toolbar previous enters the last card with selectable fallback");
        foreach (string name in new[] { "contrast", "warmth", "brightness-profile", "screen-blank", "screen-shortcut", "preset-Day", "preset-custom" })
        {
            var left = FocusTarget(form, "nav-left", name);
            var right = FocusTarget(form, "nav-right", name);
            Select(left, next, right, $"next preserves the available {name} control");
            Select(right, next, left, $"next wraps and preserves {name}");
            Select(left, previous, right, $"previous wraps and preserves {name}");
            Select(right, previous, left, $"previous preserves {name}");
        }
        Select(FocusTarget(form, "nav-left", "brightness"), next, FocusTarget(form, "nav-right", "warmth"),
            "unavailable brightness falls back to warmth");
        var destination = Controls(form).OfType<MonitorCard>().Single(c => c.Monitor.StableKey == "nav-right");
        var map = Private<LayoutMap>(form, "_map");
        Assert((int)typeof(LayoutMap).GetField("_selectedIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map) == 1,
            "navigation highlights the destination display in layout order");
        Assert(Private<StatusStripe>(form, "_status").AccessibleName.Contains("Display 2 of 2:") &&
            Private<StatusStripe>(form, "_status").AccessibleName.Contains("right"),
            "navigation reports display index and position despite identical descriptions");
        var source = FocusTarget(form, "nav-left", "warmth");
        form.ActiveControl = source;
        Assert(!NavigationKey(source, Keys.PageDown) && !NavigationKey(source, Keys.Control | Keys.Shift | Keys.PageDown),
            "unmodified and extra-modifier keys are not claimed as navigation");
        Assert(form.ActiveControl == source, "unrecognized navigation chords do not transfer selection");
        foreach (string name in new[] { "brightness", "contrast", "warmth" })
        {
            var slider = (Slider)FocusTarget(form, "nav-left", name);
            int value = slider.Value, changes = 0, editors = 0;
            slider.ValueChanged += (_, _) => changes++;
            var factory = slider.CreateExactEditor;
            slider.CreateExactEditor = _ => { editors++; throw new InvalidOperationException("Command modifier opened an editor"); };
            try
            {
                foreach (Keys chord in new[] { Keys.Control | Keys.Shift | Keys.PageDown, Keys.Alt | Keys.Left,
                    Keys.Control | Keys.Home, Keys.Control | Keys.Enter, Keys.Alt | Keys.F2 })
                {
                    var key = new KeyEventArgs(chord);
                    typeof(Slider).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(slider, [key]);
                    Assert(slider.Value == value && changes == 0 && editors == 0 && !key.Handled && !key.SuppressKeyPress,
                        $"{name} command-modified input requests no value change or editor");
                }
            }
            finally { slider.CreateExactEditor = factory; }
        }
        var rightPreset = FocusTarget(form, "nav-right", "preset-Day");
        string presetName = rightPreset.Name;
        rightPreset.Name = "temporarily-unmatched";
        Select(FocusTarget(form, "nav-left", "preset-Day"), next, FocusTarget(form, "nav-right", "warmth"),
            "missing matching control uses the selectable fallback");
        rightPreset.Name = presetName;
        var duplicate = new Button { Name = presetName, Text = "Synthetic duplicate" };
        destination.Controls.Add(duplicate);
        Select(FocusTarget(form, "nav-left", "preset-Day"), next, FocusTarget(form, "nav-right", "warmth"),
            "ambiguous matching controls use the selectable fallback");
        destination.Controls.Remove(duplicate);
        duplicate.Dispose();
        form.ActiveControl = source;
        form.Enabled = false;
        Assert(NavigationKey(source, next) && form.ActiveControl == source, "disabled owner consumes navigation without transferring selection");
        form.Enabled = true;
        form.Hide();
        Assert(NavigationKey(source, next) && form.ActiveControl == source, "hidden owner consumes navigation without transferring selection");
        form.Show();
        Assert(fixture.Scans == scans && fixture.GammaSaves == gamma && fixture.ShortcutRegistrations == registrations &&
            fixture.Transport.Writes == 0 && Controls(form).OfType<MonitorCard>().All(c => c.Monitor.Brightness == 50 && c.Monitor.Contrast == 50),
            "navigation leaves scans, hardware/gamma, registration and monitor values unchanged");
        Assert(settings == null ? !File.Exists(fixture.SettingsPath) : File.ReadAllBytes(fixture.SettingsPath).SequenceEqual(settings),
            "navigation leaves isolated settings bytes unchanged");
        form.ActiveControl = source;
        var scanTask = FocusRefresh(form);
        try
        {
            PumpUntil(() => entered.IsSet, "navigation scan reaches its read barrier");
            Assert(form.ActiveControl == bar.HelpButton && NavigationKey(bar.HelpButton, next),
                "reading consumes navigation on the parked persistent control");
        }
        finally { release.Set(); }
        PumpUntil(() => scanTask.IsCompleted, "navigation scan reaches terminal completion");
        scanTask.GetAwaiter().GetResult();
        Assert(form.ActiveControl == bar.HelpButton, "navigation intent during reading cancels old-card recovery");
        Private<System.Windows.Forms.Timer>(form, "_saveTimer").Stop();
        entered.Reset(); release.Reset();
        Task reentrant = null;
        var reentrantTarget = FocusTarget(form, "nav-right", "warmth");
        reentrantTarget.Enter += (_, _) => reentrant ??= FocusRefresh(form);
        form.ActiveControl = FocusTarget(form, "nav-left", "warmth");
        try
        {
            Assert(NavigationKey(form.ActiveControl, next), "reentrant navigation chord is consumed");
            PumpUntil(() => reentrant != null && entered.IsSet, "target selection callback starts a replacement scan");
            Assert(!Private<StatusStripe>(form, "_status").AccessibleName.StartsWith("Display 2 of 2:"),
                "stale navigation cannot overwrite replacement-scan status");
        }
        finally { release.Set(); }
        PumpUntil(() => reentrant?.IsCompleted == true, "reentrant replacement scan terminates");
        reentrant.GetAwaiter().GetResult();
        Assert(reentrantTarget.IsDisposed && fixture.Transport.Writes == 0, "reentrant selection safely releases stale targets without adjustments");
        Assert(GetForegroundWindow() != form.Handle, "navigation does not foreground the off-desktop owner");

        using var single = new Fixture(directory, enumerate: _ => [FocusDisplay("nav-only", -30000)]);
        Private<System.Windows.Forms.Timer>(single.Form, "_saveTimer").Stop();
        var preset = FocusTarget(single.Form, "nav-only", "preset-Day");
        single.Form.ActiveControl = preset;
        Assert(NavigationKey(preset, next) && NavigationKey(preset, previous) && single.Form.ActiveControl == preset,
            "single-card navigation retains the current selectable control");
        using var empty = new Fixture(directory, enumerate: _ => [], waitForCards: false);
        var emptyBar = Private<DeskCommandBar>(empty.Form, "_bar");
        empty.Form.ActiveControl = emptyBar.HelpButton;
        Assert(NavigationKey(emptyBar.HelpButton, next) && empty.Form.ActiveControl == emptyBar.HelpButton,
            "empty display list consumes navigation without changing selection");
    }
}
