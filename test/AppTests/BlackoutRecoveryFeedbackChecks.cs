using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckBlackoutRecoveryFeedback(string directory)
    {
        CheckAvailableStartupRecovery(directory);
        CheckRefreshRecovery(directory);
        using var fixture = new Fixture(directory,
            settingsFactory: path =>
            {
                var settings = new AppSettings(path);
                settings.SetScreenBlank("pending-recovery", "Synthetic recovery", true, 50, 110);
                Assert(settings.Save(), "synthetic pending recovery is persisted before startup");
                return settings;
            },
            enumerate: _ => [new Display
            {
                StableKey = "pending-recovery", Description = "Synthetic recovery & portrait",
                Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29200, Bottom = -29400 },
                BrightnessMin = 0, BrightnessMax = 255,
            }]);
        var card = Controls(fixture.Form).OfType<MonitorCard>().Single();
        var notice = Controls(card).OfType<AdjustmentNotice>().Single();
        var restore = Controls(card).OfType<FlatButton>().Single(b => b.Name == "screen-blank");
        var status = Private<StatusStripe>(fixture.Form, "_status");
        Assert(notice.Visible && notice.Message.Contains("brightness"),
            "startup pending brightness recovery has a persistent per-display warning");
        Assert(restore.Text == "Restore screen" && restore.Enabled,
            "pending recovery retains an available restore command");
        status.Set("Unrelated status", StatusKind.Info);
        Assert(notice.Visible, "unrelated status cannot erase the pending recovery warning");
        notice.Dismiss();
        typeof(MainForm).GetMethod("OnScreenBlankStateChanged",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(fixture.Form, [card.Monitor.StableKey, false, false]);
        Assert(notice.Visible && fixture.Settings.ScreenBlankActiveFor(card.Monitor.StableKey)
            && status.AccessibleName.Contains("Restore screen") && status.AccessibleName.Contains("Refresh")
            && !status.AccessibleName.Contains("will retry when"),
            "a failed active-blackout recovery notification preserves its marker and explains explicit retry actions");
        SettleSettings(fixture);
        byte[] saved = File.ReadAllBytes(fixture.SettingsPath);
        notice.Dismiss();
        restore.PerformClick();
        Assert(status.AccessibleName.Contains("still pending") && status.AccessibleName.Contains("Refresh"),
            "unavailable recovery retry gives fresh actionable status rather than silently doing nothing");
        Assert(notice.Visible && fixture.Transport.Restores == 0 && fixture.Transport.Writes == 0,
            "unavailable retry reopens the warning without sending hardware requests");
        Assert(File.ReadAllBytes(fixture.SettingsPath).SequenceEqual(saved)
            && fixture.Settings.Find(card.Monitor.StableKey).ScreenBlankBrightnessRaw == 110,
            "failed retry preserves the exact saved recovery state");
        card.Monitor.SupportsBrightness = true;
        fixture.Transport.OnRestore = (_, _) => false;
        restore.PerformClick();
        Assert(fixture.Transport.Restores == 1 && notice.Visible
            && fixture.Settings.ScreenBlankActiveFor(card.Monitor.StableKey),
            "rejected transport recovery remains pending and visibly unconfirmed");
        fixture.Transport.OnRestore = (_, raw) => raw == 110;
        restore.PerformClick();
        SettleSettings(fixture);
        Assert(fixture.Transport.Restores == 2 && card.Monitor.Brightness == 110
            && !fixture.Settings.ScreenBlankActiveFor(card.Monitor.StableKey),
            "confirmed retry restores the original raw brightness and clears its marker");
        Assert(restore.Text == "Blank screen" && status.AccessibleName.Contains("restored"),
            "confirmed recovery returns the screen command to its ordinary action");
        Assert(notice.Visible, "a confirmed retry does not silently dismiss earlier unconfirmed-adjustment history");
        notice.Dismiss();
        Assert(!notice.Visible && fixture.Transport.Restores == 2,
            "dismissing the retained notice performs no further recovery");
    }

    private static AppSettings PendingRecoverySettings(string path)
    {
        var settings = new AppSettings(path);
        settings.SetScreenBlank("pending-recovery", "Synthetic recovery", true, 50, 110);
        Assert(settings.Save(), "synthetic pending recovery is persisted before startup");
        return settings;
    }

    private static Display RecoveryDisplay(bool available) => new()
    {
        StableKey = "pending-recovery", Description = "Synthetic recovery & portrait",
        Rect = new Native.RECT { Left = -30000, Top = -30000, Right = -29200, Bottom = -29400 },
        SupportsBrightness = available, Brightness = 12, BrightnessMin = 0, BrightnessMax = 255,
    };

    private static void CheckAvailableStartupRecovery(string directory)
    {
        using var fixture = new Fixture(directory, settingsFactory: PendingRecoverySettings,
            enumerate: _ => [RecoveryDisplay(true)], onRestore: (_, raw) => raw == 110);
        SettleSettings(fixture);
        var card = Controls(fixture.Form).OfType<MonitorCard>().Single();
        var command = Controls(card).OfType<FlatButton>().Single(b => b.Name == "screen-blank");
        Assert(fixture.Scans == 1 && fixture.Transport.Restores == 1 && fixture.Transport.Writes == 0
            && card.Monitor.Brightness == 110,
            "startup with working brightness restores the saved raw value exactly once without queued writes");
        Assert(!fixture.Settings.ScreenBlankActiveFor(card.Monitor.StableKey)
            && !AppSettings.Load(fixture.SettingsPath).ScreenBlankActiveFor(card.Monitor.StableKey),
            "successful startup recovery clears both the live and persisted marker");
        Assert(command.Text == "Blank screen" && !Controls(card).OfType<AdjustmentNotice>().Single().Visible,
            "successful startup recovery presents the ordinary command without an unresolved warning");
    }

    private static void CheckRefreshRecovery(string directory)
    {
        using var fixture = new Fixture(directory, settingsFactory: PendingRecoverySettings,
            enumerate: scan => [RecoveryDisplay(scan > 1)]);
        var card = Controls(fixture.Form).OfType<MonitorCard>().Single();
        Assert(Controls(card).OfType<AdjustmentNotice>().Single().Visible && fixture.Transport.Restores == 0,
            "unavailable startup warns before any Refresh recovery attempt");
        var refresh = Private<DeskCommandBar>(fixture.Form, "_bar").RefreshButton;
        foreach (bool accepted in new[] { false, true })
        {
            var previous = card;
            fixture.Transport.OnRestore = (_, raw) => accepted && raw == 110;
            refresh.PerformClick();
            PumpUntil(() => previous.IsDisposed && refresh.Enabled
                && Controls(fixture.Form).OfType<MonitorCard>().Any(c => !ReferenceEquals(c, previous)),
                "Refresh completes recovery and replaces the previous card");
            SettleSettings(fixture);
            card = Controls(fixture.Form).OfType<MonitorCard>().Single();
            var command = Controls(card).OfType<FlatButton>().Single(b => b.Name == "screen-blank");
            var notice = Controls(card).OfType<AdjustmentNotice>().Single();
            Assert(fixture.Scans == (accepted ? 3 : 2) && fixture.Transport.Restores == (accepted ? 2 : 1)
                && fixture.Transport.Writes == 0 && card.Monitor.Brightness == (accepted ? 110 : 12),
                "each actual Refresh attempts recovery once, reflecting only confirmed raw brightness");
            Assert(fixture.Settings.ScreenBlankActiveFor(card.Monitor.StableKey) == !accepted
                && AppSettings.Load(fixture.SettingsPath).ScreenBlankActiveFor(card.Monitor.StableKey) == !accepted,
                "Refresh preserves rejected recovery and persists confirmed marker clearance");
            Assert(command.Text == (accepted ? "Blank screen" : "Restore screen") && notice.Visible == !accepted,
                "rebuilt cards expose unresolved recovery only, including after a rejected Refresh retry");
            if (!accepted)
                Assert(fixture.Settings.Find(card.Monitor.StableKey).ScreenBlankBrightnessRaw == 110
                    && AppSettings.Load(fixture.SettingsPath).Find(card.Monitor.StableKey).ScreenBlankBrightnessRaw == 110,
                    "rejected Refresh preserves the original raw snapshot in memory and on disk");
        }
    }
}
