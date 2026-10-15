using LumenDeck;

internal static partial class Program
{
    private static void CheckWarmthFailure(string directory)
    {
        using var fixture = new Fixture(directory, allDisplays: false);
        var card = Controls(fixture.Form).OfType<MonitorCard>().Single();
        var monitor = card.Monitor;
        fixture.RefuseGamma = true;
        var bar = Private<DeskCommandBar>(fixture.Form, "_bar");
        bar.SetExpanded(true);
        bar.ModeButtons["Night"].PerformClick();
        PumpUntil(() => fixture.Worker.Idle, "refused warmth preset brightness settles");
        RequestSettings(fixture);
        SettleSettings(fixture);
        Assert(fixture.GammaWrites == 1 && monitor.Kelvin == 6500,
            "desk preset reports refused warmth without changing the current tint");
        Assert(AppSettings.Load(fixture.SettingsPath).Find(monitor.StableKey) is { Kelvin: 6500, CustomKelvin: 6500 },
            "desk preset background save retains the prior warmth and Custom after gamma refusal");
        Assert(Controls(card).OfType<AdjustmentNotice>().Single().Visible,
            "refused warmth remains visible after desk preset status updates");
        fixture.RefuseGamma = false;
        bar.ModeButtons["Night"].PerformClick();
        RequestSettings(fixture);
        SettleSettings(fixture);
        Assert(monitor.Kelvin == 4600 && AppSettings.Load(fixture.SettingsPath).Find(monitor.StableKey) is
            { Kelvin: 4600, CustomKelvin: 6500 }, "accepted warmth retry persists the target while preserving Custom");
    }
}
