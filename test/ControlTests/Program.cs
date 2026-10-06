using System.Reflection;
using System.Text.Json;
using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        string directory = Path.Combine(Path.GetTempPath(), "LumenDeck-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            CheckSlider();
            CheckBlanking(directory);
            CheckNightSaver(directory);
            Console.WriteLine($"PASS: {_checks} control checks; no physical monitor writes or user settings changes.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        _checks++;
        Console.WriteLine("PASS: " + description);
    }

    private static void Invoke(Slider slider, string method, object argument) =>
        typeof(Slider).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(slider, [argument]);

    private static void CheckSlider()
    {
        using var slider = new Slider { Minimum = 0, Maximum = 255, Value = 100 };
        int changes = 0;
        slider.ValueChanged += (_, _) => changes++;
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert(slider.Value == 101 && changes == 1, "one hardware unit on a 0-255 brightness range");
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right | Keys.Shift));
        Assert(slider.Value == 111, "Shift moves ten hardware units");
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.End));
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert(slider.Value == 255 && changes == 3, "clamping does not emit a redundant write");
        slider.SetValueSilently(42);
        Assert(changes == 3 && slider.Value == 42, "hardware synchronization is silent");
        Invoke(slider, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 0, 0, 60));
        Assert(slider.Value == 42, "partial wheel delta is held");
        Invoke(slider, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 0, 0, 60));
        Assert(slider.Value == 43, "precision wheel moves one hardware unit");
        slider.Enabled = false;
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Left));
        Assert(slider.Value == 43, "a blanked display cannot receive a keyboard brightness change");
        slider.Enabled = true;
        slider.Maximum = 3500;
        slider.SmallChange = 50;
        slider.Value = 1000;
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert(slider.Value == 1050, "warmth supports a precise 50 K step");
    }

    private static Monitor Display(string key) => new()
    {
        StableKey = key, Description = key,
        // Invisible test overlays: these rectangles are outside the desktop.
        Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29900, Bottom = -29900 },
    };

    private static void CheckBlanking(string directory)
    {
        string path = Path.Combine(directory, "settings.json");
        var settings = new AppSettings(path);
        using var worker = new DdcWorker();
        using var blanker = new ScreenBlanker(settings, worker, (_, _, _) => { });
        using var first = Display("first");
        Assert(blanker.Blank(first, activate: false), "overlay-only displays can enter the night blackout");
        Assert(settings.ScreenBlankActiveFor(first.StableKey) && File.Exists(path),
            "recovery marker is persisted before blanking");
        Assert(blanker.Blank(first, activate: false), "repeat blank is idempotent");
        using var rebound = Display("first");
        blanker.Rebind(rebound);
        Assert(blanker.IsBlanked("first"), "Refresh preserves a blackout by stable identity");
        Assert(blanker.Restore("first") && !settings.ScreenBlankActiveFor("first"),
            "overlay-only restore clears the recovery marker");
        settings.SetScreenBlank("first", "first", true, null);
        Assert(blanker.RecoverInterruptedBlank(first) && !settings.ScreenBlankActiveFor("first"),
            "interrupted overlay-only blackout is recovered");
        settings.SetScreenBlank("first", "first", true, 43.1372549019608, 110);
        Assert(!blanker.RecoverInterruptedBlank(first) && settings.ScreenBlankActiveFor("first"),
            "unavailable brightness backend retains the restore marker");
        Assert(settings.Save(), "isolated settings save succeeds");
        var persisted = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
        Assert(persisted.Find("first").ScreenBlankBrightnessRaw == 110,
            "exact hardware brightness survives serialization");
        first.SupportsBrightness = true;
        first.BrightnessMax = 255;
        first.Brightness = 0;
        settings.Find("first").CustomBrightnessPercent = 60;
        settings.CaptureCustom(first);
        Assert(settings.Find("first").CustomBrightnessPercent == 60,
            "editing another control during blackout preserves Custom brightness");
        first.SupportsBrightness = false;
        settings.SetScreenBlank("first", "first", false, null);
        using var second = Display("second");
        blanker.Blank(first, activate: false);
        blanker.Blank(second, activate: false);
        blanker.RestoreAll();
        Assert(!blanker.IsBlanked("first") && !blanker.IsBlanked("second"),
            "exit restores all active overlays");
        using var invalid = Display("invalid");
        invalid.Rect.Right = invalid.Rect.Left;
        Assert(!blanker.Blank(invalid), "invalid display geometry cannot create a blackout");
        string blockedPath = Path.Combine(directory, "blocked");
        File.WriteAllText(blockedPath, "a file cannot be a directory");
        var blockedSettings = new AppSettings(Path.Combine(blockedPath, "settings.json"));
        using var blockedBlanker = new ScreenBlanker(blockedSettings, worker, (_, _, _) => { });
        Assert(!blockedBlanker.Blank(first, activate: false) && !blockedBlanker.IsBlanked("first") &&
            !blockedSettings.ScreenBlankActiveFor("first"),
            "failed recovery persistence aborts blackout without leaving active state");
    }

    private static void CheckNightSaver(string directory)
    {
        var settings = new AppSettings(Path.Combine(directory, "night.json"));
        using var worker = new DdcWorker();
        using var blanker = new ScreenBlanker(settings, worker, (_, _, _) => { });
        var night = new NightSaver(blanker, settings);
        using var work = Display("work");
        using var side = Display("side");
        using var independent = Display("independent");
        Monitor[] desk = [work, side, independent];
        blanker.Blank(independent, activate: false);
        Assert(night.Start(desk, "missing") == -1 && !blanker.IsBlanked("side"),
            "missing work display cannot darken the desk");
        Assert(night.Start(desk, "work") == 0 && night.Count == 1 &&
            !blanker.IsBlanked("work") && blanker.IsBlanked("side"),
            "night saver preserves the selected work display and blanks the other available display");
        Assert(night.Start(desk, "work") == 0 && night.Count == 1,
            "repeated night activation does not replace the original restore state");
        night.Restore(desk);
        Assert(night.Count == 0 && !blanker.IsBlanked("side") && blanker.IsBlanked("independent"),
            "group restore leaves independently blanked displays untouched");
        Assert(night.Start(desk, "independent") == -1,
            "a blanked work display cannot start a night session");
        night.Start(desk, "work");
        blanker.Restore("side");
        night.ForgetRestored("side");
        Assert(night.Count == 0, "individual wake updates the night session state");
        using var unavailable = Display("unavailable");
        unavailable.SupportsBrightness = true;
        unavailable.BrightnessMax = 255;
        unavailable.Brightness = 110;
        // No physical handle: writes safely fail without calling monitor APIs.
        Monitor[] failingDesk = [work, unavailable];
        night.Start(failingDesk, "work");
        Assert(settings.Find("unavailable").ScreenBlankBrightnessRaw == 110 &&
            unavailable.Brightness == 0, "night saver saves raw brightness before requesting minimum");
        night.Restore(failingDesk);
        Assert(night.Count == 1 && settings.ScreenBlankActiveFor("unavailable") &&
            !blanker.IsBlanked("unavailable"), "failed hardware restore removes the overlay but retains retry state");
        night.Restore([work]);
        Assert(night.Count == 1, "disconnected displays retain their night restore snapshot");
        night.Restore(failingDesk);
        Assert(night.Count == 1, "retry failure cannot report the night desk as restored");
    }
}
