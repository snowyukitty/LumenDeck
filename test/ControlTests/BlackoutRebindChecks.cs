using System.Text.Json;
using LumenDeck;

internal static partial class Program
{
    private static void CheckBlackoutRebind(string directory)
    {
        foreach (bool beganWithBrightness in new[] { false, true })
        {
            string path = Path.Combine(directory, $"rebind-{beganWithBrightness}.json");
            var settings = new AppSettings(path);
            var transport = new FakeDdcTransport();
            int restores = 0;
            transport.OnRestore = (_, _) => { restores++; return true; };
            using var worker = new DdcWorker(transport);
            using var blanker = new ScreenBlanker(settings, worker, (_, _, _) => { });
            using var original = Display("rebound-display");
            original.SupportsBrightness = beganWithBrightness;
            original.BrightnessMin = 10;
            original.BrightnessMax = 255;
            original.Brightness = 137;
            Assert(blanker.Blank(original, activate: false), "controlled blackout starts without foreground activation");
            WaitIdle(worker);
            var saved = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)).Find(original.StableKey);
            Assert(saved.ScreenBlankBrightnessRaw == (beganWithBrightness ? 137 : null),
                "blackout persists the original raw snapshot only when brightness was initially available");

            using var unavailable = Display(original.StableKey);
            blanker.Rebind(unavailable);
            using var available = Display(original.StableKey);
            available.SupportsBrightness = true;
            available.BrightnessMin = 10;
            available.BrightnessMax = 255;
            available.Brightness = 173;
            blanker.Rebind(available);
            WaitIdle(worker);
            Assert(available.Brightness == (beganWithBrightness ? 10 : 173),
                "rebind only requests minimum brightness for a session with an original recovery snapshot");
            Assert(transport.Writes.Count == (beganWithBrightness ? 2 : 0),
                "an overlay-only session never starts hardware writes when brightness becomes available");
            saved = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)).Find(original.StableKey);
            Assert(saved.ScreenBlankBrightnessRaw == (beganWithBrightness ? 137 : null),
                "capability changes do not replace the persisted recovery snapshot");
            Assert(blanker.IsBlanked(original.StableKey), "capability transitions preserve the existing overlay session");
            Assert(blanker.Restore(original.StableKey) && !settings.ScreenBlankActiveFor(original.StableKey),
                "restoring the rebound session clears its recovery marker");
            Assert(restores == (beganWithBrightness ? 1 : 0)
                && available.Brightness == (beganWithBrightness ? 137 : 173),
                "restore uses the original raw snapshot or leaves an overlay-only display brightness untouched");

            Assert(blanker.Blank(available, activate: false), "a new blackout adopts the newly available brightness capability");
            WaitIdle(worker);
            Assert(settings.Find(original.StableKey).ScreenBlankBrightnessRaw == (beganWithBrightness ? 137 : 173),
                "the new session saves current brightness before requesting its minimum");
            Assert(blanker.Restore(original.StableKey), "the new brightness-capable session remains reversible");
        }
    }
}
