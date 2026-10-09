using LumenDeck;

internal static partial class Program
{
    private static void CheckSliderInputOwnership()
    {
        using var slider = new Slider { Minimum = 0, Maximum = 255, Value = 100 };
        int changes = 0, editors = 0;
        slider.ValueChanged += (_, _) => changes++;
        slider.CreateExactEditor = _ => { editors++; throw new InvalidOperationException("Blocked chord opened an editor"); };
        Keys[] commands = [Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End, Keys.F2, Keys.Enter];
        Keys[] modifiers = [Keys.Control, Keys.Alt, Keys.Control | Keys.Shift, Keys.Alt | Keys.Shift,
            Keys.Control | Keys.Alt, Keys.Control | Keys.Alt | Keys.Shift];
        foreach (Keys modifier in modifiers)
            foreach (Keys command in commands)
            {
                var args = new KeyEventArgs(modifier | command);
                Invoke(slider, "OnKeyDown", args);
                Assert(slider.Value == 100 && changes == 0 && editors == 0 && !args.Handled && !args.SuppressKeyPress,
                    $"{modifier}+{command} remains unclaimed and requests no slider change or editor");
            }
        foreach (bool suppress in new[] { false, true })
        {
            KeyEventHandler take = (_, e) => { if (suppress) e.SuppressKeyPress = true; else e.Handled = true; };
            slider.KeyDown += take;
            foreach (Keys command in commands)
            {
                Invoke(slider, "OnKeyDown", new KeyEventArgs(command));
                Assert(slider.Value == 100 && changes == 0 && editors == 0,
                    $"subscriber {(suppress ? "suppression" : "handling")} owns {command} before local slider actions");
            }
            slider.KeyDown -= take;
        }
        foreach (Keys command in new[] { Keys.Right, Keys.F2 })
        {
            using var invalidated = new Slider { Minimum = 0, Maximum = 255, Value = 100 };
            int invalidChanges = 0;
            invalidated.ValueChanged += (_, _) => invalidChanges++;
            invalidated.CreateExactEditor = _ => throw new InvalidOperationException("Disposed slider opened an editor");
            invalidated.KeyDown += (_, _) => invalidated.Dispose();
            Invoke(invalidated, "OnKeyDown", new KeyEventArgs(command));
            Assert(invalidated.IsDisposed && invalidated.Value == 100 && invalidChanges == 0,
                "subscriber disposal invalidates the slider before local keyboard processing");
        }
        foreach (var test in new[]
        {
            (Keys.Left, 99), (Keys.Right, 101), (Keys.Up, 101), (Keys.Down, 99),
            (Keys.PageUp, 125), (Keys.PageDown, 75), (Keys.Home, 0), (Keys.End, 255),
            (Keys.Shift | Keys.Left, 90), (Keys.Shift | Keys.Right, 110),
            (Keys.Shift | Keys.PageUp, 125), (Keys.Shift | Keys.PageDown, 75),
        })
        {
            slider.SetValueSilently(100);
            int before = changes;
            var args = new KeyEventArgs(test.Item1);
            Invoke(slider, "OnKeyDown", args);
            Assert(slider.Value == test.Item2 && changes == before + 1 && args.Handled,
                $"supported {test.Item1} retains its documented adjustment and single callback");
        }
    }
}
