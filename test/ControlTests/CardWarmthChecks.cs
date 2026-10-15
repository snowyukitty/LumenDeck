using System.Reflection;
using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckCardWarmth(string directory)
    {
        string path = Path.Combine(directory, "card-warmth.json");
        var settings = new AppSettings(path);
        using var monitor = new Monitor { StableKey = "card-warmth", Description = "Synthetic display", Kelvin = 5000 };
        settings.SetKelvin(monitor.StableKey, monitor.DisplayName, 5000);
        settings.CaptureCustom(monitor);
        settings.Find(monitor.StableKey).CustomKelvin = 4200;
        using var worker = new DdcWorker();
        int changes = 0, reports = 0, writes = 0;
        bool accepted = false;
        using var card = new MonitorCard(monitor, settings, worker, (_, _) =>
        {
            changes++;
            settings.SetKelvin(monitor.StableKey, monitor.DisplayName, monitor.Kelvin);
        }, _ => reports++, null, null, null, applyGamma: m =>
        {
            writes++;
            Assert(m.Kelvin == 3000, "card sends requested warmth to its writer");
            return accepted;
        });
        var slider = (Slider)typeof(MonitorCard).GetField("_warmth", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(card);
        var manual = typeof(MonitorCard).GetMethod("OnWarmth", BindingFlags.Instance | BindingFlags.NonPublic);
        slider.Value = 3500;
        manual.Invoke(card, null);
        Assert(monitor.Kelvin == 5000 && slider.Value == 1500 && changes == 0,
            "refused manual warmth restores the slider and model without announcing a saved edit");
        Assert(settings.Find(monitor.StableKey) is { Kelvin: 5000, CustomKelvin: 4200 },
            "refused manual warmth preserves saved warmth and Custom");
        card.ApplyWarmth(3000);
        settings.SetKelvin(monitor.StableKey, monitor.DisplayName, monitor.Kelvin);
        Assert(monitor.Kelvin == 5000 && slider.Value == 1500 && reports == 2 && writes == 2,
            "refused preset warmth restores the control and retains failure feedback");
        Assert(settings.Save() && AppSettings.Load(path).Find(monitor.StableKey) is { Kelvin: 5000, CustomKelvin: 4200 },
            "refused manual and preset warmth cannot persist the failed target");
        accepted = true;
        card.ApplyWarmth(3000);
        Assert(monitor.Kelvin == 3000 && slider.Value == 3500 && changes == 0 &&
            settings.Find(monitor.StableKey).CustomKelvin == 4200,
            "accepted preset warmth updates the control without replacing Custom");
        monitor.Kelvin = 5000;
        card.SyncFromMonitor();
        slider.Value = 3500;
        manual.Invoke(card, null);
        Assert(monitor.Kelvin == 3000 && changes == 1 && settings.Find(monitor.StableKey) is { Kelvin: 3000, CustomKelvin: 3000 },
            "accepted manual warmth updates current and Custom exactly once");
    }
}
