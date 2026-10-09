using System.Text.Json.Nodes;
using LumenDeck;
using Slot = LumenDeck.PresetSchedule.Slot;

internal static partial class Program
{
    private static void CheckSchedule(string directory, string renderDirectory)
    {
        CheckScheduleArithmetic();
        CheckScheduleStorage(directory);
        CheckScheduleView(renderDirectory);
    }

    private static readonly DateTime Today = new(2026, 10, 9);

    private static void CheckScheduleArithmetic()
    {
        foreach (var (text, valid) in new[] { ("7:05", true), ("07:05", true), (" 21:30 ", true), ("00:00", true), ("23:59", true),
            ("24:00", false), ("7:5", false), ("21.30", false), ("", false), (null, false), ("9:30 PM", false) })
            Assert(PresetSchedule.TryParse(text, out _) == valid, $"schedule time \"{text}\" is {(valid ? "accepted" : "rejected")}");

        Slot[] desk = [new("Day", new(7, 0, 0)), new("Night", new(21, 30, 0)), new("Evening", new(18, 0, 0))];
        void Expect(TimeSpan at, string preset, DateTime start, string next, DateTime nextStart, string name)
        {
            var period = PresetSchedule.At(desk, Today + at);
            Assert(period == new PresetSchedule.Period(preset, start, next, nextStart), name);
        }
        Expect(new(12, 0, 0), "Day", Today.AddHours(7), "Evening", Today.AddHours(18), "midday is Day until Evening, whatever the slot order");
        Expect(new(3, 0, 0), "Night", Today.AddDays(-1).AddHours(21.5), "Day", Today.AddHours(7),
            "before the first start, yesterday's last slot is still in force");
        Expect(new(6, 59, 59), "Night", Today.AddDays(-1).AddHours(21.5), "Day", Today.AddHours(7), "one second before a start keeps the earlier slot");
        Expect(new(7, 0, 0), "Day", Today.AddHours(7), "Evening", Today.AddHours(18), "a slot begins exactly at its start");
        Expect(new(21, 30, 0), "Night", Today.AddHours(21.5), "Day", Today.AddDays(1).AddHours(7), "the last slot's next change is tomorrow");
        Assert(PresetSchedule.At([new("Night", new(21, 30, 0))], Today.AddHours(10)) ==
            new PresetSchedule.Period("Night", Today.AddDays(-1).AddHours(21.5), "Night", Today.AddHours(21.5)),
            "a single slot repeats daily as distinct periods");
        Assert(PresetSchedule.At([], Today) == null && PresetSchedule.Problem([]).Contains("at least one"),
            "an empty schedule has no period and says why");
        string clash = PresetSchedule.Problem([new("Day", new(7, 0, 0)), new(Presets.CustomName, new(7, 0, 0))]);
        Assert(clash.Contains("Day and Custom") && clash.Contains("07:00") && PresetSchedule.At([new("Day", new(7, 0, 0)), new("Custom", new(7, 0, 0))], Today) == null,
            "two presets at one time are refused rather than ordered arbitrarily");
    }

    private static void CheckScheduleStorage(string directory)
    {
        string path = Path.Combine(directory, "schedule.json");
        var settings = new AppSettings(path);
        Assert(!settings.Schedule.Enabled && settings.ScheduleSlots().Select(s => s.Preset).SequenceEqual(["Day", "Evening", "Night"]) &&
            settings.ScheduleRow(Presets.CustomName) == (false, new TimeSpan(7, 0, 0)),
            "a new schedule is off, with Day, Evening and Night ready and Custom unused");
        var wanted = settings.WithSchedule(true, [("Night", true, new TimeSpan(22, 15, 0)), ("custom", true, new TimeSpan(6, 45, 0))]);
        Assert(!settings.Schedule.Enabled && settings.ScheduleRow("Night").Start == new TimeSpan(21, 30, 0),
            "building a schedule leaves the active one unchanged until it is installed");
        settings.Schedule = wanted;
        Assert(settings.Save(), "the schedule saves with the rest of settings");
        var reloaded = AppSettings.Load(path);
        Assert(reloaded.Schedule.Enabled && reloaded.ScheduleRow("Night") == (true, new TimeSpan(22, 15, 0)) &&
            reloaded.ScheduleSlots().Any(s => s is { Preset: "Custom", Start.Hours: 6, Start.Minutes: 45 }),
            "saved times, uses and the canonical Custom name survive reload");
        var stored = JsonNode.Parse(File.ReadAllText(path))["Schedule"]["Entries"].AsArray();
        Assert(stored.Any(e => e["Preset"].GetValue<string>() == "Night" && e["Start"].GetValue<string>() == "22:15"),
            "times are stored as readable 24-hour text");

        File.WriteAllText(path, """
            {
              "Schedule": {
                "Enabled": true, "Future": "kept",
                "Entries": [
                  null,
                  { "Preset": "Reading", "Start": "20:00" },
                  { "Preset": "night", "Start": "22:00", "Fade": 30 },
                  { "Preset": "Night", "Start": "23:00" },
                  { "Preset": "Evening", "Start": "25:00" },
                  { "Preset": "Day", "Start": "08:00", "Enabled": false }
                ]
              }
            }
            """);
        var loaded = AppSettings.Load(path);
        Assert(loaded.Schedule.Entries.Count == 5, "null schedule entries are discarded on load");
        Assert(loaded.ScheduleSlots().SequenceEqual([new Slot("Night", new(22, 0, 0))]),
            "unknown presets, unreadable times, unused rows and later duplicates never become slots");
        Assert(loaded.ScheduleRow("Evening") == (false, new TimeSpan(18, 0, 0)) && loaded.ScheduleRow("Day") == (false, new TimeSpan(8, 0, 0)),
            "the editor shows an unreadable time as an unused default and keeps a disabled row's time");
        loaded.Schedule = loaded.WithSchedule(true, PresetSchedule.Names.Select(n => (n, n != "Custom", loaded.ScheduleRow(n).Start)));
        Assert(loaded.Save(), "an edited schedule saves alongside unknown content");
        var schedule = JsonNode.Parse(File.ReadAllText(path))["Schedule"];
        var entries = schedule["Entries"].AsArray();
        Assert(schedule["Future"].GetValue<string>() == "kept" && entries.Any(e => e["Preset"].GetValue<string>() == "Reading"),
            "unknown schedule fields and preset entries round-trip");
        var nights = entries.Where(e => e["Preset"].GetValue<string>() == "Night").ToArray();
        Assert(nights.Length == 1 && nights[0]["Start"].GetValue<string>() == "22:00" && nights[0]["Fade"].GetValue<int>() == 30,
            "editing keeps the winning entry's fields and drops a shadowed duplicate");

        File.WriteAllText(path, """{ "Schedule": null }""");
        Assert(AppSettings.Load(path).ScheduleSlots().Count == 3, "a null schedule loads as the off default");
        File.WriteAllText(path, """{ "Schedule": { "Enabled": true, "Entries": null } }""");
        var empty = AppSettings.Load(path);
        Assert(empty.ScheduleSlots().Count == 0 && empty.ScheduleRow("Day") == (false, new TimeSpan(7, 0, 0)),
            "a schedule without entries has no slots and offers default times");
    }

    private static void CheckScheduleView(string renderDirectory)
    {
        var rows = PresetSchedule.Names.Select(n => new ScheduleRowDraft(n, n != "Custom", PresetSchedule.DefaultStarts[n])).ToArray();
        var levels = Presets.Levels.Select(l => l.Name == "Night" ? l with { Nits = 90 } : l).ToArray();
        var context = new ScheduleEditorContext(true, rows, levels);
        // A stand-in for the window's outlook: the same inputs, held in locals.
        var clock = Today.AddHours(19);
        PresetSchedule.Period requested = null;
        bool ready = true;
        ScheduleOutlook Outlook(IReadOnlyCollection<Slot> slots)
        {
            var period = PresetSchedule.At(slots, clock);
            bool pending = period != requested;
            return new ScheduleOutlook(period, pending && ready, pending && !ready);
        }
        var drafts = new List<ScheduleDraft>();
        var results = new Queue<Func<Task<SettingsSaveResult>>>();
        using var form = new PreviewForm { ClientSize = new Size(560, 600), Font = Theme.Body, AutoScaleMode = AutoScaleMode.None };
        using var view = new PresetScheduleView(context, Outlook, d => { drafts.Add(d); return results.Dequeue()(); }) { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        form.Show();
        TextBox Time(string preset) => Descendants(view).OfType<TextBox>().Single(t => t.AccessibleName == preset + " start time");
        CheckBox Use(string preset) => Descendants(view).OfType<CheckBox>().Single(c => c.AccessibleName == $"Use {preset} in the schedule");
        var enabled = Descendants(view).OfType<CheckBox>().Single(c => c.AccessibleName == "Change presets automatically");
        var preview = Descendants(view).OfType<Label>().Single(l => l.AccessibleName == "Schedule preview");
        var status = Descendants(view).OfType<Label>().Single(l => l.AccessibleRole == AccessibleRole.Alert);
        var save = Descendants(view).OfType<FlatButton>().Single(b => b.Text == "Save schedule");

        Assert(preview.Text.StartsWith("Now: Evening (since 18:00). Next: Night at 21:30.") &&
            preview.Text.Contains("Saving requests Evening on every visible display now") && preview.AccessibleDescription == preview.Text,
            "preview names the current period, the next change and the request saving makes");
        requested = PresetSchedule.At([new("Day", new(7, 0, 0)), new("Evening", new(18, 0, 0)), new("Night", new(21, 30, 0))], clock);
        view.RefreshPreview();
        Assert(preview.Text.Contains("Evening was already requested for this period, so saving adjusts no display"),
            "a period the owner already requested is not promised again");
        requested = null;
        ready = false;
        view.RefreshPreview();
        Assert(preview.Text.Contains("Displays are not ready, so saving requests Evening once they are"),
            "a request that must wait for displays is described as waiting");
        ready = true;
        clock = Today.AddHours(21.75);
        view.RefreshPreview();
        Assert(preview.Text.StartsWith("Now: Night (since 21:30). Next: Day at 07:00.") && preview.Text.Contains("Saving requests Night"),
            "refreshing follows the owner's clock while the editor stays open");
        clock = Today.AddHours(19);
        Assert(Descendants(view).OfType<Label>().Any(l => l.Text == "90 nits, 4600 K") &&
            Descendants(view).OfType<Label>().Any(l => l.Text == "your remembered levels"),
            "each row shows the active target it would request");
        foreach (string text in new[] { "25:00", "9pm", "" })
        {
            Time("Night").Text = text;
            Assert(!save.Enabled && status.Text.Contains("Night start time") && Time("Night").Text == text && Time("Night").ForeColor == Theme.Warn,
                "an unreadable time keeps its text, names the row and disables Save");
        }
        Time("Night").Text = "18:00";
        Assert(!save.Enabled && status.Text.Contains("Evening and Night start at the same time"), "two presets at one time are named");
        if (renderDirectory != null) { Directory.CreateDirectory(renderDirectory); Render(view, Path.Combine(renderDirectory, "schedule-invalid.png")); }
        enabled.Checked = false;
        Assert(save.Enabled && preview.Text.Contains("off") && status.Text.Contains("for later"),
            "an off schedule can keep clashing times for later without adjusting displays");
        enabled.Checked = true;
        Time("Night").Text = "21:30";
        foreach (string preset in new[] { "Day", "Evening", "Night" }) Use(preset).Checked = false;
        Assert(!save.Enabled && status.Text.Contains("at least one"), "an enabled schedule needs a preset");
        Use("Night").Checked = true;
        Assert(save.Enabled && preview.Text.Contains("Now: Night (since 21:30)") && preview.Text.Contains("Next: Night at 21:30"),
            "a single preset repeats daily");
        Use("Day").Checked = Use("Evening").Checked = true;

        results.Enqueue(() => Task.FromResult(SettingsSaveResult.Failed));
        save.PerformClick();
        PumpProfileSave(view.PendingSave);
        Assert(drafts.Single() is { Enabled: true } d && d.Rows.Count == 4 && d.Rows.Single(r => r.Preset == "Night").Start == new TimeSpan(21, 30, 0),
            "the save receives every row of the draft");
        Assert(save.Enabled && status.Text.Contains("previous schedule remains in effect"), "an unconfirmed save keeps the draft and explains it");
        var gate = new TaskCompletionSource<SettingsSaveResult>();
        results.Enqueue(() => gate.Task);
        save.PerformClick();
        Assert(view.IsSaving && !save.Enabled && !Time("Day").Enabled && !enabled.Enabled, "a pending save locks the draft");
        view.ShowSavePending();
        Assert(status.Text.Contains("Wait for its result"), "closing during a save explains the wait");
        int saved = 0;
        view.Saved += () => saved++;
        gate.SetResult(SettingsSaveResult.Saved);
        PumpProfileSave(view.PendingSave);
        Assert(saved == 1, "a confirmed save completes the editor once");

        foreach (int width in new[] { 560, 460 })
        {
            form.ClientSize = new Size(width, 600);
            view.PerformLayout();
            foreach (var control in Descendants(view).Where(c => c.Visible && c is Label or TextBox or CheckBox or FlatButton))
            {
                var bounds = new Rectangle(view.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Assert(bounds.Left >= 0 && bounds.Right <= view.ClientSize.Width, $"schedule editor content fits {width} pixels");
            }
            if (renderDirectory != null) Render(view, Path.Combine(renderDirectory, $"schedule-{width}.png"));
        }
        form.ClientSize = new Size(460, 440);
        view.PerformLayout();
        foreach (var control in new Control[] { save, status })
            Assert(view.ClientRectangle.Contains(new Rectangle(view.PointToClient(control.PointToScreen(Point.Empty)), control.Size)),
                "schedule save and feedback stay pinned in a short editor");
        if (renderDirectory != null) Render(view, Path.Combine(renderDirectory, "schedule-460-440.png"));

        using var scaledForm = new PreviewForm { ClientSize = new Size(560, 600), Font = Theme.Body, AutoScaleMode = AutoScaleMode.None };
        using var scaled = new PresetScheduleView(context, Outlook, _ => Task.FromResult(SettingsSaveResult.Saved)) { Dock = DockStyle.Fill };
        scaledForm.Controls.Add(scaled);
        scaledForm.Show();
        scaledForm.Scale(new SizeF(2, 2));
        var originals = Descendants(scaled).ToDictionary(c => c, c => c.Font);
        var fonts = originals.Values.Distinct().ToDictionary(f => f, f => new Font(f.FontFamily, f.Size * 2, f.Style));
        try
        {
            foreach (var pair in originals) pair.Key.Font = fonts[pair.Value];
            scaledForm.ClientSize = new Size(920, 1000);
            scaled.PerformLayout();
            foreach (var control in Descendants(scaled).Where(c => c.Visible && c is Label or TextBox or CheckBox or FlatButton))
            {
                var bounds = new Rectangle(scaled.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Assert(bounds.Left >= 0 && bounds.Right <= scaled.ClientSize.Width, "schedule editor fits with simulated 200% scaling");
            }
            if (renderDirectory != null) Render(scaled, Path.Combine(renderDirectory, "schedule-200.png"));
        }
        finally { foreach (var font in fonts.Values) font.Dispose(); }
    }
}
