using LumenDeck;

internal static partial class Program
{
    private static void CheckSupportReportLayout(string renderDirectory)
    {
        CheckSupportReportCopyLifetime();
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            int writes = 0, closes = 0;
            bool fail = true;
            string copied = null;
            using var host = new QuietLoopForm { AutoScaleMode = AutoScaleMode.None, ClientSize = new Size(464, 381) };
            using var view = new SupportReportView(text =>
            {
                writes++;
                copied = text;
                if (fail) throw new IOException("PRIVATE-CLIPBOARD-PATH");
            }) { Dock = DockStyle.Fill };
            view.CloseRequested += () => closes++;
            host.Controls.Add(view);
            var originals = Controls(view).Prepend(view).Distinct().ToDictionary(c => c, c => c.Font);
            var fonts = originals.Values.Distinct().ToDictionary(font => font,
                font => new Font(font.FontFamily, font.Size * scale, font.Style));
            try
            {
                foreach (var pair in originals) pair.Key.Font = fonts[pair.Value];
                var report = SupportReport.Create([], DisplayScanState.Ready, "Synthetic build", new Version(10, 0));
                view.SetReport(report);
                host.Show();
                var preview = Controls(view).OfType<TextBox>().Single();
                var status = Controls(view).OfType<Label>().Single();
                var copy = Controls(view).OfType<FlatButton>().Single(b => b.Text == "Copy report");
                var close = Controls(view).OfType<FlatButton>().Single(b => b.Text == "Close");
                Assert(writes == 0 && preview.ReadOnly && preview.Text == SupportReport.Text(report),
                    $"opening support report preserves the exact read-only preview without copying at text scale {scale}");
                foreach (int state in new[] { 0, 1, 2 })
                {
                    if (state != 0) { fail = state == 1; copy.PerformClick(); }
                    host.PerformLayout(); view.PerformLayout();
                    Assert(!status.Text.Contains("PRIVATE") && (state == 0 || status.Text.Contains(state == 1 ? "Could not copy" : "Report copied")),
                        $"support report feedback describes state {state} without exception details at text scale {scale}");
                    int requiredHeight = TextRenderer.MeasureText(status.Text, status.Font,
                        new Size(status.ClientSize.Width, int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height;
                    Assert(status.ClientSize.Height >= requiredHeight,
                        $"support report feedback remains fully readable in state {state} at text scale {scale}");
                    foreach (var action in new[] { copy, close })
                    {
                        var bounds = new Rectangle(view.PointToClient(action.PointToScreen(Point.Empty)), action.Size);
                        Assert(view.ClientRectangle.Contains(bounds) && bounds.Top >= status.Bottom &&
                            action.Width >= TextRenderer.MeasureText(action.Text, action.Font).Width &&
                            action.Height >= TextRenderer.MeasureText(action.Text, action.Font).Height,
                            $"support report {action.Text} remains readable and reachable at text scale {scale}");
                    }
                    Assert(preview.Height >= 100 && preview.Bottom <= status.Top && status.Bottom <= copy.Parent.Top,
                        $"support report keeps a usable preview above pinned feedback/actions at text scale {scale}");
                    if (renderDirectory != null)
                    {
                        Directory.CreateDirectory(renderDirectory);
                        using var bitmap = new Bitmap(view.Width, view.Height);
                        view.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(renderDirectory, $"support-report-text-{scale}-state-{state}.png"));
                    }
                }
                Assert(writes == 2 && copied == preview.Text, $"support report copies only the displayed payload on explicit actions at text scale {scale}");
                close.PerformClick();
                Assert(closes == 1, $"support report retains its Close action at text scale {scale}");
            }
            finally
            {
                host.Dispose();
                foreach (var font in fonts.Values) font.Dispose();
            }
        }
    }

    private static void CheckSupportReportCopyLifetime()
    {
        foreach (bool fail in new[] { false, true })
        {
            int writes = 0, feedback = 0;
            using var host = new QuietLoopForm();
            using var view = new SupportReportView(_ =>
            {
                writes++;
                host.Close();
                if (fail) throw new IOException("synthetic callback closes the report before failing");
            }) { Dock = DockStyle.Fill };
            host.Controls.Add(view);
            view.SetReport(SupportReport.Create([], DisplayScanState.Ready, "1.1.0", null));
            host.Show();
            var status = Controls(view).OfType<Label>().Single();
            status.TextChanged += (_, _) => feedback++;
            Controls(view).OfType<FlatButton>().Single(b => b.Text == "Copy report").PerformClick();
            Assert(host.IsDisposed && view.IsDisposed && writes == 1 && feedback == 0,
                $"a report callback closing its preview {(fail ? "with failure" : "after success")} cannot update disposed feedback");
            Assert(!status.IsHandleCreated, "closed support feedback cannot recreate a native handle");
        }
    }
}
