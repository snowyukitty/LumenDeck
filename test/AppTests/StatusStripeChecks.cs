using LumenDeck;

internal static partial class Program
{
    private static void CheckStatusStripe(string directory, string renderDirectory)
    {
        using var fixture = new Fixture(directory);
        var status = Private<StatusStripe>(fixture.Form, "_status");
        var readers = Controls(status).OfType<TextBox>().ToArray();
        Assert(readers.Length == 1, "status exposes a native text reader instead of an ellipsized-only paint surface");
        var reader = readers.Single();
        Assert(reader.ReadOnly && reader.Multiline && reader.WordWrap && reader.ScrollBars == ScrollBars.Vertical
            && reader.TabStop && reader.Enabled,
            "status supports read-only keyboard selection and vertical reading without an edit action");
        Assert(status.SelectNextControl(null, true, true, true, false) && status.ActiveControl == reader,
            "native tab traversal logically selects the status reader on the private desktop");
        string warning = "Synthetic display & portrait " + new string('W', 180)
            + ": blackout removed, but brightness could not be restored yet. "
            + "Check the monitor connection, then use Restore screen or Refresh to retry.";
        status.Set(warning, StatusKind.Warn);
        Assert(status.ActiveControl == reader && reader.Text == warning && status.AccessibilityObject.Name == warning
            && reader.AccessibilityObject.Value == warning && reader.ForeColor == Theme.Warn,
            "status update preserves logical selection and exposes the complete literal warning to native readers");
        reader.SelectAll();
        SendMessage(reader.Handle, 0x0102, (IntPtr)'X', IntPtr.Zero); // WM_CHAR
        SendMessage(reader.Handle, 0x0100, (IntPtr)Keys.Delete, IntPtr.Zero); // WM_KEYDOWN
        Assert(reader.Text == warning && reader.SelectedText == warning,
            "native typing and Delete cannot replace or erase selected status text");

        // A separate instance keeps geometry/font simulations out of MainForm's
        // docking contract. The actual form's reader above covers navigation.
        using var host = new QuietLoopForm { ClientSize = new Size(1000, 160) };
        using var sample = new StatusStripe();
        host.Controls.Add(sample);
        host.Show();
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var font = new Font(Theme.Small.FontFamily, Theme.Small.Size * scale);
            sample.Font = font;
            foreach (int width in new[] { 344, 604, 1000 })
            {
                sample.Size = new Size(width, (int)(46 * scale));
                sample.Set(warning, StatusKind.Warn);
                sample.PerformLayout();
                var text = Controls(sample).OfType<TextBox>().Single();
                Assert(sample.ClientRectangle.Contains(text.Bounds) && text.Text == warning,
                    $"full status content stays inside its fixed viewport at {width}px, text scale {scale}");
                text.Select(text.TextLength, 0);
                text.ScrollToCaret();
                Application.DoEvents();
                Point tail = text.GetPositionFromCharIndex(text.TextLength - 1);
                Assert(tail.Y >= 0 && tail.Y < text.ClientSize.Height && tail.X >= 0 && tail.X < text.ClientSize.Width,
                    $"native scrolling reaches the final retry instruction at {width}px, text scale {scale}");
                RenderStatus(sample, renderDirectory, $"status-{width}-text-{scale}-end.png");
                text.Select(0, 0);
                text.ScrollToCaret();
                Assert(text.GetPositionFromCharIndex(0).Y >= 0,
                    $"native scrolling returns to the message beginning at {width}px, text scale {scale}");
                RenderStatus(sample, renderDirectory, $"status-{width}-text-{scale}-start.png");
            }
            sample.Font = Theme.Small;
        }
        var refresh = fixture.Form.Controls.OfType<DeskCommandBar>().Single().RefreshButton;
        fixture.Form.ActiveControl = refresh;
        status.Set("Ready.", StatusKind.Info);
        Assert(fixture.Form.ActiveControl == refresh && reader.Text == "Ready." && reader.ForeColor == Theme.InkMuted,
            "routine status replaces the warning without changing the selected command");
        status.Set(null, StatusKind.Info);
        Assert(reader.Text == "" && status.AccessibilityObject.Name == "" && fixture.Transport.Writes == 0
            && fixture.Transport.Restores == 0 && fixture.StartupWrites == 0,
            "clearing or reading status makes no monitor, recovery or startup requests");
    }

    private static void RenderStatus(StatusStripe status, string directory, string name)
    {
        if (directory == null) return;
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(status.Width, status.Height);
        status.DrawToBitmap(bitmap, status.ClientRectangle);
        bitmap.Save(Path.Combine(directory, name));
    }
}
