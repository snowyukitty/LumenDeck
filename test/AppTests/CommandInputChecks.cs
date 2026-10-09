using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static void CommandKey(FlatButton button, string method, Keys key) =>
        typeof(FlatButton).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, [new KeyEventArgs(key)]);

    private static void CheckCommandInputOwnership(string directory)
    {
        using var fixture = new Fixture(directory);
        var form = fixture.Form;
        var refresh = Private<DeskCommandBar>(form, "_bar").RefreshButton;
        Private<System.Windows.Forms.Timer>(form, "_saveTimer").Stop();
        form.ActiveControl = refresh;
        int generation = Private<int>(form, "_generation"), scans = fixture.Scans;
        byte[] settings = File.Exists(fixture.SettingsPath) ? File.ReadAllBytes(fixture.SettingsPath) : null;
        foreach (Keys key in new[] { Keys.Space, Keys.Enter })
            foreach (bool release in new[] { false, true })
                foreach (bool suppress in new[] { false, true })
                {
                    KeyEventHandler intercept = (_, e) => { if (suppress) e.SuppressKeyPress = true; else e.Handled = true; };
                    if (release) refresh.KeyUp += intercept;
                    else refresh.KeyDown += intercept;
                    try
                    {
                        CommandKey(refresh, "OnKeyDown", key);
                        CommandKey(refresh, "OnKeyUp", key);
                    }
                    finally
                    {
                        if (release) refresh.KeyUp -= intercept;
                        else refresh.KeyDown -= intercept;
                    }
                    CommandKey(refresh, "OnKeyUp", key);
                    Assert(Private<int>(form, "_generation") == generation && fixture.Scans == scans &&
                        Private<DisplayScanState>(form, "_scanState") == DisplayScanState.Ready && refresh.Enabled,
                        "intercepted Refresh input and orphan release cannot start a scan or disable cards");
                }
        foreach (var mouseButton in new[] { MouseButtons.Right, MouseButtons.Middle, MouseButtons.XButton1, MouseButtons.XButton2 })
        {
            typeof(FlatButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(refresh, [new MouseEventArgs(mouseButton, 1, 1, 1, 0)]);
            Assert(Private<int>(form, "_generation") == generation && fixture.Scans == scans,
                "non-primary pointer callback cannot trigger actual toolbar Refresh");
        }
        Assert(settings == null ? !File.Exists(fixture.SettingsPath) : File.ReadAllBytes(fixture.SettingsPath).SequenceEqual(settings),
            "intercepted toolbar input leaves isolated settings bytes unchanged");
        Assert(fixture.Transport.Writes == 0 && fixture.StartupWrites == 0 && GetForegroundWindow() != form.Handle,
            "intercepted toolbar input performs no display/startup write or foreground activation");
        CommandKey(refresh, "OnKeyDown", Keys.Space);
        CommandKey(refresh, "OnKeyUp", Keys.Space);
        PumpUntil(() => fixture.Scans == scans + 1 && Private<DisplayScanState>(form, "_scanState") == DisplayScanState.Ready && refresh.Enabled,
            "fresh unclaimed Refresh gesture completes one synthetic scan");
        Assert(Private<int>(form, "_generation") == generation + 1, "fresh Refresh dispatches exactly once after interception");
    }
}
