using System.Reflection;
using System.Runtime.InteropServices;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static Display FocusDisplay(string key, int left, bool brightness = true) => new()
    {
        StableKey = key, Description = "Identical example display", PositionLabel = left == -30000 ? "left" : "right",
        Rect = new Native.RECT { Left = left, Top = -30000, Right = left + 800, Bottom = -29400 },
        SupportsBrightness = brightness, Brightness = 50, BrightnessMin = 0, BrightnessMax = 100,
        SupportsContrast = true, Contrast = 50, ContrastMin = 0, ContrastMax = 100,
    };

    private static Task FocusRefresh(MainForm form) => (Task)typeof(MainForm)
        .GetMethod("RebuildAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, ["Synthetic focus rebuild."]);

    private static Control FocusTarget(MainForm form, string key, string name) => Controls(form)
        .OfType<MonitorCard>().Where(c => c.Monitor.StableKey == key).SelectMany(Controls).Single(c => c.Name == name);

    private static void CheckRebuildFocus(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool missing = false, unsupported = false, failure = false;
        using var fixture = new Fixture(directory, enumerate: scan =>
        {
            if (scan > 1)
            {
                entered.Set();
                if (!release.Wait(10000)) throw new TimeoutException("focus barrier");
            }
            if (failure) throw new IOException("synthetic focus scan failure");
            return missing ? [] : [FocusDisplay("focus-right", -29000, !unsupported), FocusDisplay("focus-left", -30000)];
        });
        var form = fixture.Form;
        form.ClientSize = new Size(604, 441);
        form.PerformLayout();
        var bar = Private<DeskCommandBar>(form, "_bar");
        Private<System.Windows.Forms.Timer>(form, "_saveTimer").Stop();
        void Run(Control source, Action during, Func<bool> expected, string message)
        {
            entered.Reset(); release.Reset();
            Assert(GetForegroundWindow() != form.Handle, "focus fixture starts without foreground ownership");
            form.ActiveControl = source;
            Assert(form.ActiveControl == source, "focus fixture selects the intended logical source");
            Assert(GetForegroundWindow() != form.Handle, "logical source selection does not foreground the off-desktop owner");
            var scan = FocusRefresh(form);
            try
            {
                PumpUntil(() => entered.IsSet, "focus scan reaches its enumeration barrier");
                during?.Invoke();
            }
            finally { release.Set(); }
            PumpUntil(() => scan.IsCompleted, "focus scan completes");
            scan.GetAwaiter().GetResult();
            Assert(expected(), message);
            var list = Private<FlowLayoutPanel>(form, "_list");
            if (form.ActiveControl is { } selected && list.Contains(selected))
                Assert(list.ClientRectangle.Contains(new Rectangle(list.PointToClient(selected.PointToScreen(Point.Empty)), selected.Size)),
                    "restored selection is fully visible in the minimum-size card viewport");
            Assert(Private<object>(form, "_scanFocus") == null, "completed scan releases its focus ownership");
            Assert(GetForegroundWindow() != form.Handle, "logical focus recovery does not foreground the off-desktop owner");
            Private<System.Windows.Forms.Timer>(form, "_saveTimer").Stop();
        }
        foreach (string name in new[] { "brightness", "contrast", "warmth", "brightness-profile", "screen-blank", "screen-shortcut", "preset-Day", "preset-custom" })
        {
            var old = FocusTarget(form, "focus-right", name);
            Run(old, () => Assert(form.ActiveControl == bar.HelpButton, "reading parks a removed card's logical focus on persistent help"),
                () => old.IsDisposed && form.ActiveControl == FocusTarget(form, "focus-right", name),
                $"rebuild restores {name} on the correct identical-model display");
        }
        Run(bar.RefreshButton, null, () => form.ActiveControl == bar.RefreshButton,
            "toolbar Refresh regains logical focus after re-enabling");
        Run(bar.SettingsButton, () => Assert(form.ActiveControl == bar.SettingsButton, "surviving enabled selection is not parked"),
            () => form.ActiveControl == bar.SettingsButton, "surviving Settings selection remains untouched");
        Run(FocusTarget(form, "focus-right", "brightness"), () => form.ActiveControl = bar.SettingsButton,
            () => form.ActiveControl == bar.SettingsButton, "user selection during reading overrides the old bookmark");
        Run(FocusTarget(form, "focus-right", "brightness"), () =>
        {
            form.ActiveControl = bar.SettingsButton;
            form.ActiveControl = bar.HelpButton;
        }, () => form.ActiveControl == bar.HelpButton, "moving away and back cannot revive a canceled bookmark");
        Run(FocusTarget(form, "focus-right", "brightness"), () =>
            typeof(FlatButton).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bar.HelpButton, [new KeyEventArgs(Keys.Down)]),
            () => form.ActiveControl == bar.HelpButton, "keyboard intent cancels recovery even when logical selection is unchanged");
        Run(FocusTarget(form, "focus-right", "brightness"), () =>
            typeof(FlatButton).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bar.HelpButton, [new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)]),
            () => form.ActiveControl == bar.HelpButton, "pointer intent cancels recovery without changing selection");
        Run(FocusTarget(form, "focus-right", "brightness"), () => bar.HelpButton.PerformClick(),
            () => form.ActiveControl == bar.HelpButton, "opening help during reading cannot be interrupted by focus restoration");
        Private<ConnectionHelpForm>(form, "_connectionHelp").Close();
        Run(FocusTarget(form, "focus-right", "brightness"), () => { form.Hide(); form.Show(); },
            () => form.ActiveControl != FocusTarget(form, "focus-right", "brightness"), "hide and reshow invalidate the old focus bookmark");
        Run(FocusTarget(form, "focus-right", "brightness"), () => unsupported = true,
            () => form.ActiveControl == bar.RefreshButton, "an unavailable replacement control falls back to Refresh");
        unsupported = false;
        Run(FocusTarget(form, "focus-right", "warmth"), () => missing = true,
            () => form.ActiveControl == bar.RefreshButton, "removed displays fall back to a reachable Refresh command");
        missing = false;
        Run(bar.RefreshButton, null, () => form.ActiveControl == bar.RefreshButton, "retry from empty scan preserves Refresh selection");
        Run(FocusTarget(form, "focus-right", "brightness"), () => failure = true,
            () => form.ActiveControl == bar.RefreshButton, "failed scan retains a reachable Refresh selection");
        failure = false;
        Assert(fixture.Transport.Writes == 0 && fixture.StartupWrites == 0,
            "focus restoration tests send no monitor or startup-registration requests");
        CheckSupersededFocus(directory);
    }

    private static void CheckSupersededFocus(string directory)
    {
        using var first = new ManualResetEventSlim();
        using var second = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        using var fixture = new Fixture(directory, enumerate: scan =>
        {
            if (scan == 2) { first.Set(); if (!releaseFirst.Wait(10000)) throw new TimeoutException("first focus barrier"); }
            if (scan == 3) { second.Set(); if (!releaseSecond.Wait(10000)) throw new TimeoutException("second focus barrier"); }
            return [FocusDisplay("focus-right", -29000)];
        });
        fixture.Form.ActiveControl = FocusTarget(fixture.Form, "focus-right", "contrast");
        var older = FocusRefresh(fixture.Form);
        Task newer = null;
        try
        {
            PumpUntil(() => first.IsSet, "older focus scan starts");
            newer = FocusRefresh(fixture.Form);
            PumpUntil(() => second.IsSet, "newer focus scan starts");
            releaseSecond.Set();
            PumpUntil(() => newer.IsCompleted, "newest focus scan completes first");
            var target = FocusTarget(fixture.Form, "focus-right", "contrast");
            Assert(fixture.Form.ActiveControl == target, "superseding scan inherits an unclaimed parked bookmark");
            releaseFirst.Set();
            PumpUntil(() => older.IsCompleted, "obsolete focus scan completes last");
            Assert(fixture.Form.ActiveControl == target, "obsolete scan cannot replace current focus");
            Assert(fixture.Transport.Writes == 0, "overlapping focus scans send no monitor requests");
        }
        finally { releaseFirst.Set(); releaseSecond.Set(); }
    }
}
