using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private static KeyEventArgs ButtonKey(FlatButton button, string method, Keys key)
    {
        var args = new KeyEventArgs(key);
        typeof(FlatButton).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, [args]);
        return args;
    }

    private static void CheckFlatButton()
    {
        using var form = new PreviewForm();
        using var parent = new Panel();
        using var button = new FlatButton("Synthetic command");
        parent.Controls.Add(button);
        form.Controls.Add(parent);
        form.Show();
        Application.DoEvents();
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        var accessible = button.AccessibilityObject;
        Assert(accessible.Role == AccessibleRole.PushButton && accessible.DefaultAction == "Press",
            "custom command retains its accessible button role and action");
        ButtonKey(button, "OnKeyUp", Keys.Space);
        ButtonKey(button, "OnKeyUp", Keys.Enter);
        Assert(clicks == 0, "orphan Space/Enter releases cannot activate a command");
        var first = ButtonKey(button, "OnKeyDown", Keys.Space);
        Assert((accessible.State & AccessibleStates.Pressed) != 0, "armed keyboard press exposes momentary accessible Pressed state");
        ButtonKey(button, "OnKeyDown", Keys.Space);
        Assert(clicks == 0 && first.Handled && first.SuppressKeyPress,
            "held Space shows a press without auto-repeat commands or character processing");
        ButtonKey(button, "OnKeyUp", Keys.Space);
        ButtonKey(button, "OnKeyUp", Keys.Space);
        Assert((accessible.State & AccessibleStates.Pressed) == 0, "keyboard release clears accessible Pressed state");
        Assert(clicks == 1, "matching Space release activates once and duplicate release is inert");
        ButtonKey(button, "OnKeyDown", Keys.Enter);
        ButtonKey(button, "OnKeyUp", Keys.Space);
        Assert(clicks == 1, "a different activation key cannot complete the armed press");
        ButtonKey(button, "OnKeyUp", Keys.Enter);
        Assert(clicks == 2, "matching Enter release completes its own press once");
        ButtonKey(button, "OnKeyDown", Keys.Space);
        typeof(FlatButton).GetMethod("OnLostFocus", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, [EventArgs.Empty]);
        Assert(ButtonPressCleared(button), "focus loss clears the armed key and painted press before any release");
        ButtonKey(button, "OnKeyUp", Keys.Space);
        Assert(clicks == 2, "focus loss cancels an incomplete command press");
        ButtonKey(button, "OnKeyDown", Keys.Enter);
        button.Enabled = false;
        Assert((accessible.State & AccessibleStates.Unavailable) != 0,
            "disabled command reports unavailable to assistive clients");
        button.PerformClick();
        accessible.DoDefaultAction();
        typeof(FlatButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, [EventArgs.Empty]);
        ButtonKey(button, "OnKeyUp", Keys.Enter);
        Assert(clicks == 2, "disabled commands reject direct, accessible, mouse-click and keyboard dispatch");
        button.Enabled = true;
        ButtonKey(button, "OnKeyUp", Keys.Enter);
        Assert(clicks == 2, "re-enabling a command cannot revive a canceled press");
        foreach (var modifier in new[] { Keys.Control, Keys.Alt })
        {
            ButtonKey(button, "OnKeyDown", modifier | Keys.Space);
            ButtonKey(button, "OnKeyUp", modifier | Keys.Space);
            ButtonKey(button, "OnKeyDown", modifier | Keys.Enter);
            ButtonKey(button, "OnKeyUp", modifier | Keys.Enter);
        }
        Assert(clicks == 2, "Ctrl/Alt activation chords are not plain command presses");
        ButtonKey(button, "OnKeyDown", Keys.Space | Keys.Shift);
        ButtonKey(button, "OnKeyUp", Keys.Space | Keys.Shift);
        Assert(clicks == 3, "Shift does not prevent ordinary command activation");
        ButtonKey(button, "OnKeyDown", Keys.Space);
        ButtonKey(button, "OnKeyUp", Keys.Space | Keys.Control);
        ButtonKey(button, "OnKeyUp", Keys.Space);
        Assert(clicks == 3, "a modified release cancels its armed command without a later orphan activation");
        ButtonKey(button, "OnKeyDown", Keys.Space);
        parent.Enabled = false;
        accessible.DoDefaultAction();
        parent.Enabled = true;
        ButtonKey(button, "OnKeyUp", Keys.Space);
        Assert(clicks == 3, "inherited disabled state cancels presses and rejects accessible activation");
        ButtonKey(button, "OnKeyDown", Keys.Space);
        accessible.DoDefaultAction();
        ButtonKey(button, "OnKeyUp", Keys.Space);
        Assert(clicks == 4, "accessible activation consumes an armed key so its release cannot double-dispatch");
        ButtonKey(button, "OnKeyDown", Keys.Enter);
        button.PerformClick();
        ButtonKey(button, "OnKeyUp", Keys.Enter);
        Assert(clicks == 5, "direct activation consumes an armed key before a matching release");
        ButtonKey(button, "OnKeyDown", Keys.Space);
        var cancel = Message.Create(button.Handle, 0x001F, IntPtr.Zero, IntPtr.Zero); // WM_CANCELMODE
        typeof(FlatButton).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, [cancel]);
        ButtonKey(button, "OnKeyUp", Keys.Space);
        Assert(clicks == 5, "native cancel-mode processing discards an armed command");
        accessible.DoDefaultAction();
        button.PerformClick();
        Assert(clicks == 7, "enabled accessible and direct actions each activate once");
        button.Dispose();
        Assert((accessible.State & AccessibleStates.Unavailable) != 0 && (accessible.State & AccessibleStates.Pressed) == 0,
            "retained accessible state reports a disposed command as unavailable and unpressed");
        accessible.DoDefaultAction();
        button.PerformClick();
        Assert(clicks == 7, "a retained accessible object cannot activate a disposed command");
        CheckButtonInputOwnership(form);
        using var dormant = new FlatButton("Dormant synthetic command");
        ButtonKey(dormant, "OnKeyDown", Keys.Space);
        ButtonKey(dormant, "OnKeyUp", Keys.Space);
        Assert(!dormant.IsHandleCreated, "pressed state changes do not create a native handle for a dormant control");
        using var reentrant = new FlatButton("Reentrant accessible command");
        int reentrantClicks = 0;
        reentrant.Click += (_, _) => { reentrantClicks++; if (reentrantClicks == 1) reentrant.PerformClick(); };
        reentrant.PerformClick();
        Assert(reentrantClicks == 1, "nested click dispatch cannot duplicate an in-flight command");
        reentrant.PerformClick();
        Assert(reentrantClicks == 2, "completed click dispatch permits the next independent command");
        using var pointer = new FlatButton("Synthetic pointer command");
        form.Controls.Add(pointer);
        int pointerClicks = 0;
        pointer.Click += (_, _) => pointerClicks++;
        foreach (var mouseButton in new[] { MouseButtons.Right, MouseButtons.Middle, MouseButtons.XButton1, MouseButtons.XButton2 })
        {
            var args = new MouseEventArgs(mouseButton, 1, 1, 1, 0);
            typeof(FlatButton).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pointer, [args]);
            typeof(FlatButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pointer, [args]);
            typeof(FlatButton).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pointer, [args]);
            Assert(pointerClicks == 0 && ButtonPressCleared(pointer), "non-primary pointer click cannot dispatch a command");
        }
        typeof(FlatButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(pointer, [new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)]);
        Assert(pointerClicks == 1, "primary pointer click still dispatches once");
    }

    private static bool ButtonPressCleared(FlatButton button) =>
        (Keys)typeof(FlatButton).GetField("_activationKey", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button) == Keys.None &&
        !(bool)typeof(FlatButton).GetField("_down", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);

    private static bool ButtonPressArmed(FlatButton button, Keys key) =>
        (Keys)typeof(FlatButton).GetField("_activationKey", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button) == key &&
        (bool)typeof(FlatButton).GetField("_down", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);

    private static void CheckButtonInputOwnership(Form form)
    {
        foreach (Keys key in new[] { Keys.Space, Keys.Enter })
            foreach (bool release in new[] { false, true })
                foreach (bool suppress in new[] { false, true })
                {
                    using var button = new FlatButton("Owned synthetic input");
                    form.Controls.Add(button);
                    int clicks = 0;
                    button.Click += (_, _) => clicks++;
                    KeyEventHandler intercept = (_, e) => { if (suppress) e.SuppressKeyPress = true; else e.Handled = true; };
                    if (release) button.KeyUp += intercept;
                    else button.KeyDown += intercept;
                    ButtonKey(button, "OnKeyDown", key);
                    Assert(clicks == 0 && (release ? ButtonPressArmed(button, key) : ButtonPressCleared(button)),
                        "intermediate press state reflects whether key-down was intercepted");
                    ButtonKey(button, "OnKeyUp", key);
                    Assert(clicks == 0 && ButtonPressCleared(button),
                        $"subscriber-owned {key} {(release ? "release" : "press")} cancels the command and its visual press");
                    if (release) button.KeyUp -= intercept;
                    else button.KeyDown -= intercept;
                    ButtonKey(button, "OnKeyUp", key);
                    Assert(clicks == 0, "an orphan release cannot revive intercepted input");
                    ButtonKey(button, "OnKeyDown", key);
                    Assert(clicks == 0 && ButtonPressArmed(button, key), "fresh input shows an armed press before dispatch");
                    ButtonKey(button, "OnKeyUp", key);
                    Assert(clicks == 1, "a fresh gesture works after the input subscriber releases ownership");
                }
        foreach (Keys key in new[] { Keys.Space, Keys.Enter })
        {
            using var button = new FlatButton("Reentrant synthetic input");
            form.Controls.Add(button);
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            ButtonKey(button, "OnKeyDown", key);
            KeyEventHandler intercept = (_, e) => e.Handled = true;
            button.KeyDown += intercept;
            ButtonKey(button, "OnKeyDown", key);
            button.KeyDown -= intercept;
            ButtonKey(button, "OnKeyUp", key);
            Assert(clicks == 0 && ButtonPressCleared(button), "intercepted repeat cancels an earlier armed press");
            button.KeyUp += (_, e) => { e.Handled = true; button.PerformClick(); };
            ButtonKey(button, "OnKeyDown", key);
            ButtonKey(button, "OnKeyUp", key);
            Assert(clicks == 1 && ButtonPressCleared(button), "subscriber-issued direct click is not duplicated by local key release");
        }
        foreach (bool release in new[] { false, true })
        {
            using var button = new FlatButton("Invalidated synthetic input");
            form.Controls.Add(button);
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            KeyEventHandler dispose = (_, _) => button.Dispose();
            if (release) button.KeyUp += dispose;
            else button.KeyDown += dispose;
            ButtonKey(button, "OnKeyDown", Keys.Space);
            ButtonKey(button, "OnKeyUp", Keys.Space);
            Assert(button.IsDisposed && clicks == 0 && ButtonPressCleared(button),
                "subscriber disposal leaves no armed or painted keyboard press");
        }
    }
}
