using System.Reflection;
using LumenDeck;

internal static partial class Program
{
    private sealed class QuietExactValueForm(ExactValueContext context) : ExactValueForm(context)
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams => NonActivating(base.CreateParams);
    }

    private static void RunExactEntry(Slider slider, Action<ExactValueForm> act)
    {
        ExactValueForm editor = null;
        slider.CreateExactEditor = context => editor = new QuietExactValueForm(context)
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000),
        };
        Exception failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) =>
        {
            if (editor is not { Visible: true }) return;
            timer.Stop();
            try { act(editor); }
            catch (Exception ex) { failure = ex; editor.DialogResult = DialogResult.Cancel; editor.Close(); }
        };
        timer.Start();
        typeof(Slider).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(slider, [new KeyEventArgs(Keys.F2)]);
        Assert(editor != null && editor.IsDisposed, "exact-entry form closes and is disposed after the modal call");
        if (failure != null) throw new InvalidOperationException("Exact-entry assertion failed", failure);
    }

    private static void CheckExactEntry(string directory, string renderDirectory)
    {
        using var fixture = new Fixture(directory);
        Private<System.Windows.Forms.Timer>(fixture.Form, "_saveTimer").Stop();
        using var slider = new Slider { Minimum = 0, Maximum = 255, Value = 100,
            AccessibleName = "Studio & Desk (left, primary): Brightness", ExactValueUnit = "hardware units" };
        fixture.Form.Controls.Add(slider);
        int changes = 0;
        slider.ValueChanged += (_, _) => changes++;
        RunExactEntry(slider, editor =>
        {
            Assert(!IsWindowEnabled(fixture.Form.Handle), "exact entry disables its owner through the actual modal loop");
            var text = string.Join("\n", Controls(editor).OfType<Label>().Select(l => l.Text));
            var input = Controls(editor).OfType<NumericUpDown>().Single();
            Assert(text.Contains("Studio & Desk (left, primary)") && text.Contains("0–255 hardware units") &&
                input.AccessibleName.Contains("hardware units"), "exact-entry body and numeric field expose target, range and unit");
            foreach (var button in Controls(editor).OfType<Button>())
                Assert(editor.ClientRectangle.Contains(new Rectangle(editor.PointToClient(button.PointToScreen(Point.Empty)), button.Size)),
                    $"exact-entry action {button.Text} fits its client area");
            if (renderDirectory != null)
            {
                Directory.CreateDirectory(renderDirectory);
                using var bitmap = new Bitmap(editor.Width, editor.Height);
                editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(renderDirectory, "exact-hardware-entry.png"));
            }
            var target = Controls(editor).OfType<Label>().Single(label => label.Text == slider.AccessibleName);
            Assert(!target.UseMnemonic, "exact-entry target preserves literal ampersands from the display name");
            input.Value = 123;
            ((Button)editor.AcceptButton).PerformClick();
        });
        Assert(slider.Value == 123 && changes == 1 && IsWindowEnabled(fixture.Form.Handle),
            "accepted exact value emits one callback and restores owner interaction");
        foreach (string draft in new[] { "999", "-1", "abc", "", "12.5", "999999999999999999999999999999999999999" })
        {
            RunExactEntry(slider, editor =>
            {
                var input = Controls(editor).OfType<NumericUpDown>().Single();
                var edit = Controls(input).OfType<TextBox>().Single();
                edit.Text = draft;
                var apply = (Button)editor.AcceptButton;
                Assert(edit.Text == draft && !apply.Enabled,
                    "invalid exact draft remains visible and disables Apply instead of substituting another value");
                ((Button)editor.CancelButton).Select();
                input.Select();
                typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(editor, [Keys.Enter]);
                Assert(edit.Text == draft && !apply.Enabled && editor.DialogResult != DialogResult.OK,
                    "blur and Enter preserve an invalid draft without accepting it");
                editor.PerformLayout();
                var feedback = Controls(editor).OfType<Label>().Single(label => label.AccessibleRole == AccessibleRole.Alert);
                var feedbackBounds = new Rectangle(editor.PointToClient(feedback.PointToScreen(Point.Empty)), feedback.Size);
                Assert(feedback.Text.Contains("whole number") && editor.ClientRectangle.Contains(feedbackBounds) &&
                    input.AccessibleDescription == feedback.Text && apply.AccessibleDescription == feedback.Text,
                    "invalid exact input exposes readable pinned guidance and accessible action/field descriptions");
                if (renderDirectory != null && draft == "999")
                {
                    using var invalidImage = new Bitmap(editor.Width, editor.Height);
                    editor.DrawToBitmap(invalidImage, new Rectangle(Point.Empty, invalidImage.Size));
                    invalidImage.Save(Path.Combine(renderDirectory, "exact-invalid-draft.png"));
                }
                apply.PerformClick();
                Assert(editor.Visible && editor.DialogResult != DialogResult.OK && slider.Value == 123 && changes == 1,
                    "invalid exact draft cannot close as accepted or dispatch an adjustment");
                input.UpButton(); input.DownButton();
                Assert(edit.Text == draft, "spinner actions cannot silently reinterpret an invalid exact draft");
                edit.Text = "124";
                Assert(apply.Enabled, "repairing exact input restores Apply without an adjustment");
                ((Button)editor.CancelButton).PerformClick();
            });
        }
        RunExactEntry(slider, editor => ((Button)editor.AcceptButton).PerformClick());
        Assert(changes == 1, "unchanged exact value emits no redundant adjustment callback");
        RunExactEntry(slider, editor =>
        {
            Controls(editor).OfType<NumericUpDown>().Single().Value = 200;
            ((Button)editor.CancelButton).PerformClick();
        });
        Assert(slider.Value == 123 && changes == 1, "Cancel never applies the numeric draft");
        RunExactEntry(slider, editor =>
        {
            Controls(editor).OfType<NumericUpDown>().Single().Value = 200;
            typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(editor, [Keys.Escape]);
        });
        Assert(slider.Value == 123 && changes == 1, "Escape closes exact entry without applying its draft");
        RunExactEntry(slider, editor =>
        {
            Controls(editor).OfType<NumericUpDown>().Single().Value = 200;
            slider.Enabled = false;
        });
        Assert(slider.Value == 123 && changes == 1, "disabling the originating slider cancels an open draft without a callback");
        slider.Enabled = true;
        RunExactEntry(slider, editor =>
        {
            Controls(editor).OfType<NumericUpDown>().Single().Value = 200;
            slider.Maximum = 240;
            ((Button)editor.AcceptButton).PerformClick();
        });
        Assert(slider.Value == 123 && changes == 1, "changed bounds reject an otherwise accepted stale numeric draft");
        RunExactEntry(slider, editor =>
        {
            slider.FromEditValue = v => v + 1;
            Controls(editor).OfType<NumericUpDown>().Single().Value = 200;
            ((Button)editor.AcceptButton).PerformClick();
        });
        Assert(slider.Value == 123 && changes == 1, "converter replacement cannot reinterpret an accepted draft");
        ExactValueForm constructed = null;
        slider.CreateExactEditor = context =>
        {
            slider.Enabled = false;
            slider.Enabled = true;
            return constructed = new QuietExactValueForm(context);
        };
        typeof(Slider).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(slider, [new KeyEventArgs(Keys.F2)]);
        Assert(constructed is { IsDisposed: true } && slider.Value == 123 && changes == 1,
            "construction-time disable remains canceled after re-enable and never enters a modal session");
        using var warmth = new Slider { Minimum = 0, Maximum = 3500, Value = 0,
            AccessibleName = "Example display: Colour temperature", ExactValueUnit = "kelvin",
            EditValue = v => 6500 - v, FromEditValue = k => 6500 - k };
        fixture.Form.Controls.Add(warmth);
        int warmthChanges = 0;
        warmth.ValueChanged += (_, _) => warmthChanges++;
        RunExactEntry(warmth, editor =>
        {
            var input = Controls(editor).OfType<NumericUpDown>().Single();
            Assert(input.Minimum == 3000 && input.Maximum == 6500 && input.Value == 6500,
                "exact warmth entry uses sorted Kelvin bounds and neutral current value");
            input.Value = 4600;
            ((Button)editor.AcceptButton).PerformClick();
        });
        Assert(warmth.Value == 1900 && warmthChanges == 1, "accepted Kelvin entry converts once to the internal warmth scale");
        using var typed = new Slider { Minimum = 0, Maximum = 255, Value = 100, AccessibleName = "Typed exact draft" };
        fixture.Form.Controls.Add(typed);
        int typedChanges = 0;
        typed.ValueChanged += (_, _) => typedChanges++;
        RunExactEntry(typed, editor =>
        {
            var input = Controls(editor).OfType<NumericUpDown>().Single();
            var edit = Controls(input).OfType<TextBox>().Single();
            edit.Text = "invalid";
            edit.Text = "124";
            input.UpButton();
            Assert(edit.Text == "125" && typedChanges == 0, "valid repaired draft retains numeric stepping without early adjustment");
            ((Button)editor.AcceptButton).PerformClick();
        });
        Assert(typed.Value == 125 && typedChanges == 1, "a repaired typed exact draft applies its displayed whole number exactly once");
        var card = Controls(fixture.Form).OfType<MonitorCard>().Single();
        var brightness = Controls(card).OfType<Slider>().Single(s => s.AccessibleName.EndsWith(": Brightness (hardware units)"));
        Task refresh = null;
        RunExactEntry(brightness, editor =>
        {
            Controls(editor).OfType<NumericUpDown>().Single().Value = 75;
            refresh = (Task)typeof(MainForm).GetMethod("RebuildAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.Form, ["Synthetic exact-entry replacement."]);
        });
        PumpUntil(() => refresh.IsCompleted, "exact-entry replacement scan settles");
        Assert(card.IsDisposed && brightness.IsDisposed && fixture.Transport.Writes == 0,
            "display refresh cancels the actual card's modal draft before sending a stale adjustment");
        Assert(fixture.StartupWrites == 0, "exact-entry tests do not change startup registration");
        using var longEntry = new QuietExactValueForm(new ExactValueContext(
            "Example display with a deliberately long descriptive name (upper left, portrait, primary): Brightness", "hardware units", 0, 255, 100))
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ClientSize = new Size(384, 261),
        };
        longEntry.Show(fixture.Form);
        longEntry.PerformLayout();
        var body = Controls(longEntry).OfType<Panel>().Single(p => p.GetType() == typeof(Panel));
        Assert(!body.HorizontalScroll.Visible && Controls(longEntry).OfType<Label>().All(l => l.Width <= body.ClientSize.Width - body.Padding.Horizontal),
            "long display context wraps without horizontal scrolling at minimum dialog size");
        foreach (var button in Controls(longEntry).OfType<Button>())
            Assert(longEntry.ClientRectangle.Contains(new Rectangle(longEntry.PointToClient(button.PointToScreen(Point.Empty)), button.Size)),
                $"long exact-entry context keeps {button.Text} reachable");
        if (renderDirectory != null)
        {
            using var bitmap = new Bitmap(longEntry.Width, longEntry.Height);
            longEntry.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(renderDirectory, "exact-long-target.png"));
        }
        longEntry.Close();
    }
}
