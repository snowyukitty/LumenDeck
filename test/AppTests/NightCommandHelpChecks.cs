using LumenDeck;

internal static partial class Program
{
    private static void CheckNightCommandHelp(string directory)
    {
        using var fixture = new Fixture(directory, enumerate: _ =>
        {
            var work = DiscoveryDisplay();
            var other = DiscoveryDisplay();
            var separate = DiscoveryDisplay();
            work.StableKey = "night-help-work";
            other.StableKey = "night-help-other";
            separate.StableKey = "night-help-separate";
            work.SupportsBrightness = other.SupportsBrightness = separate.SupportsBrightness = false;
            return [work, other, separate];
        });
        var bar = fixture.Form.Controls.OfType<DeskCommandBar>().Single();
        var tray = Private<ToolStripMenuItem>(fixture.Form, "_nightTrayItem");
        var night = Private<NightSaver>(fixture.Form, "_nightSaver");
        var blanker = Private<ScreenBlanker>(fixture.Form, "_blanker");
        Assert(blanker.Blank(fixture.LastScan.Single(m => m.StableKey == "night-help-separate"), activate: false),
            "fixture creates an independently owned overlay before starting the night desk");
        string initialHelp = bar.NightButton.AccessibilityObject.Description;
        Assert(bar.NightButton.Enabled && tray.Enabled && night.Count == 0,
            "synthetic displays offer the night command before a session");
        bar.NightButton.PerformClick();
        Assert(night.Count == 1 && bar.NightButton.Text == "Restore night desk" && tray.Text == bar.NightButton.Text,
            "actual night start changes toolbar and tray to restore the owned session");
        string restoreHelp = bar.NightButton.AccessibilityObject.Description;
        Assert(restoreHelp != initialHelp && restoreHelp.StartsWith("Close", StringComparison.Ordinal)
            && restoreHelp.Contains("pending", StringComparison.Ordinal),
            "active night command describes restoration and pending recovery instead of starting another blackout");
        Assert(tray.AccessibleDescription == restoreHelp && tray.ToolTipText == restoreHelp,
            "tray restoration uses the same live explanation as the toolbar");
        tray.PerformClick();
        Assert(night.Count == 0 && bar.NightButton.Text == "Night saver" && tray.Text == bar.NightButton.Text
            && bar.NightButton.AccessibilityObject.Description == initialHelp && tray.AccessibleDescription == initialHelp,
            "actual tray restoration resets both surfaces to their start explanation");
        Assert(blanker.IsBlanked("night-help-separate") && !blanker.IsBlanked("night-help-other"),
            "restoring the night desk leaves the separately owned blackout intact as its help promises");
        Assert(blanker.Restore("night-help-separate"), "independent overlay restores through its own session");
        Assert(fixture.Transport.Writes == 0 && fixture.Transport.Restores == 0 && fixture.StartupWrites == 0,
            "overlay-only command lifecycle uses temporary settings and makes no hardware or startup writes");
    }
}
