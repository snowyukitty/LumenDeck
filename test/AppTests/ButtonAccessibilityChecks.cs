using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static void CheckButtonAccessibility(string directory)
    {
        using var fixture = new Fixture(directory);
        Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer").Stop();
        using var button = new FlatButton("Synthetic accessible command");
        fixture.Form.Controls.Add(button);
        var accessible = button.AccessibilityObject;
        IntPtr window = button.Handle;
        int events = 0, clicks = 0;
        button.Click += (_, _) => clicks++;
        AccessibilityEventCallback callback = (_, eventId, target, objectId, childId, _, _) =>
        {
            if (target == window && eventId == 0x800A && objectId == -4 && childId == 0) events++;
        };
        IntPtr hook = SetWinEventHook(0x800A, 0x800A, IntPtr.Zero, callback, (uint)Environment.ProcessId, 0, 0);
        Assert(hook != IntPtr.Zero, "current-process button state-change hook is available");
        bool Pressed() => (accessible.State & AccessibleStates.Pressed) != 0;
        void Transition(Action action, bool pressed, string message)
        {
            int before = events;
            action();
            PumpUntil(() => events > before, message + " notification delivered");
            Assert(Pressed() == pressed, message + " matches accessible state");
        }
        try
        {
            Assert(accessible.Role == AccessibleRole.PushButton && !Pressed() &&
                (accessible.State & (AccessibleStates.Checked | AccessibleStates.Selected)) == 0,
                "momentary command starts unpressed without a latched preset state");
            Transition(() => CommandKey(button, "OnKeyDown", Keys.Space), true, "keyboard press");
            int beforeRepeat = events;
            CommandKey(button, "OnKeyDown", Keys.Space);
            Application.DoEvents();
            Assert(events == beforeRepeat && clicks == 0 && Pressed(), "held-key repeat emits no redundant state change or command");
            Transition(() => CommandKey(button, "OnKeyUp", Keys.Space), false, "keyboard release");
            Assert(clicks == 1, "state notifications preserve one matching-release callback");
            Transition(() => CommandKey(button, "OnKeyDown", Keys.Enter), true, "cancelable keyboard press");
            Transition(() => typeof(FlatButton).GetMethod("OnLostFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(button, [EventArgs.Empty]), false, "focus-loss callback cancellation");
            CommandKey(button, "OnKeyUp", Keys.Enter);
            Assert(clicks == 1, "canceled accessible press cannot activate from an orphan release");
            Transition(() => CommandKey(button, "OnKeyDown", Keys.Space), true, "accessible-action keyboard press");
            Transition(accessible.DoDefaultAction, false, "accessible activation consumes the press");
            CommandKey(button, "OnKeyUp", Keys.Space);
            Assert(clicks == 2, "accessible activation plus release does not double-dispatch");
            void Mouse(string method, MouseButtons buttons) => typeof(FlatButton).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(button, [new MouseEventArgs(buttons, 1, 1, 1, 0)]);
            Transition(() => Mouse("OnMouseDown", MouseButtons.Left), true, "left pointer press callback");
            Transition(() => Mouse("OnMouseUp", MouseButtons.Left), false, "pointer release callback");
            int beforeRight = events;
            Mouse("OnMouseDown", MouseButtons.Right);
            Application.DoEvents();
            Assert(events == beforeRight && !Pressed(), "right pointer button does not claim a pressed command state");
            Transition(() => CommandKey(button, "OnKeyDown", Keys.Space), true, "disableable keyboard press");
            Transition(() => button.Enabled = false, false, "disabled command cancellation");
            Assert((accessible.State & AccessibleStates.Unavailable) != 0 && clicks == 2,
                "disabled command is unavailable and state notification causes no click");
            button.Enabled = true;
            Application.DoEvents();
            Transition(() => CommandKey(button, "OnKeyDown", Keys.Space), true, "disposable keyboard press");
            Transition(button.Dispose, false, "disposal clears press before native handle destruction");
            Assert((accessible.State & AccessibleStates.Unavailable) != 0 && !button.IsHandleCreated,
                "retained disposed state is unavailable without reviving its native handle");
        }
        finally
        {
            Assert(UnhookWinEvent(hook), "button state-change hook is released");
            GC.KeepAlive(callback);
        }
        Assert(fixture.Transport.Writes == 0 && fixture.Scans == 1 && fixture.StartupWrites == 0 && fixture.GammaSaves == 0 &&
            GetForegroundWindow() != fixture.Form.Handle, "button state projections/notifications perform no display write, scan or foreground activation");
    }
}
