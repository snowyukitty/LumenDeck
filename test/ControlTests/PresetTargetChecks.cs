using System.Text.Json.Nodes;
using LumenDeck;

internal static partial class Program
{
    private static void CheckPresetTargets(string directory, string renderDirectory)
    {
        CheckPresetTargetStorage(directory);
        CheckPresetEditorView(renderDirectory);
    }

    private static void CheckPresetTargetStorage(string directory)
    {
        string path = Path.Combine(directory, "preset-targets.json");
        var settings = new AppSettings(path);
        Assert(settings.PresetLevels.SequenceEqual(Presets.Levels), "unedited settings use the built-in preset targets");
        Assert(settings.PresetLevel("Reading") == null && settings.PresetLevel(Presets.CustomName) == null,
            "only built-in level names resolve to a target");

        var night = Presets.ByName("Night") with { Nits = 90, Kelvin = 4200 };
        var draft = settings.WithPresetLevels([night]);
        Assert(settings.PresetTargets.Count == 0 && settings.PresetLevel("Night") == Presets.ByName("Night"),
            "building a target list leaves the active targets unchanged until it is installed");
        settings.PresetTargets = draft;
        Assert(settings.PresetLevel("night") is { Name: "Night", Nits: 90, Kelvin: 4200 } level &&
            level.Description == Presets.ByName("Night").Description,
            "an installed override resolves case-insensitively and keeps the built-in name and description");
        Assert(settings.PresetLevel("Day") == Presets.ByName("Day"), "an override for one preset leaves the others at their defaults");
        Assert(settings.Save(), "preset targets save with the rest of settings");
        var stored = JsonNode.Parse(File.ReadAllText(path))["PresetTargets"].AsArray();
        Assert(stored.Count == 1 && stored[0]["Name"].GetValue<string>() == "Night",
            "only targets that differ from the defaults are written");
        Assert(AppSettings.Load(path).PresetLevel("Night") is { Nits: 90, Kelvin: 4200 }, "saved targets survive reload");

        Assert(settings.WithPresetLevels([Presets.ByName("Night") with { Nits = 0 },
                Presets.ByName("Day") with { Kelvin = 9000 }, new Presets.Level("Reading", 80, 4000, "")])
            .SequenceEqual(settings.PresetTargets),
            "out-of-range values and unknown names cannot become stored targets");
        settings.PresetTargets = settings.WithPresetLevels([Presets.ByName("Night")]);
        Assert(settings.PresetTargets.Count == 0 && settings.PresetLevel("Night") == Presets.ByName("Night"),
            "returning a preset to its defaults removes the plain override");

        File.WriteAllText(path, """
            {
              "PresetTargets": [
                null,
                { "Name": "Reading", "Nits": 5000, "Kelvin": 100, "Future": 1 },
                { "Name": "night", "Nits": 5000, "Kelvin": 100, "Curve": [1, 2] },
                { "Name": "Night", "Nits": 80, "Kelvin": 4000 }
              ]
            }
            """);
        var loaded = AppSettings.Load(path);
        Assert(loaded.PresetTargets.Count == 3, "null target entries are discarded on load");
        Assert(loaded.PresetLevel("Night") is { Nits: Presets.MaxTargetNits, Kelvin: GammaControl.MinKelvin },
            "the first matching entry wins and hand-edited values are clamped to the supported range");
        Assert(loaded.PresetTargets[0] is { Nits: 5000, Kelvin: 100 }, "an unknown preset from another version is not clamped");
        loaded.PresetTargets = loaded.WithPresetLevels([Presets.ByName("Night") with { Nits = 120, Kelvin = 4500 }]);
        Assert(loaded.Save(), "edited targets save alongside unknown entries");
        var targets = JsonNode.Parse(File.ReadAllText(path))["PresetTargets"].AsArray();
        Assert(targets.Count == 2 && targets[0]["Name"].GetValue<string>() == "Reading" && targets[0]["Future"].GetValue<int>() == 1,
            "unknown preset entries and their fields round-trip unchanged");
        Assert(targets[1]["Name"].GetValue<string>() == "Night" && targets[1]["Nits"].GetValue<int>() == 120 &&
            JsonNode.DeepEquals(targets[1]["Curve"], JsonNode.Parse("[1, 2]")),
            "editing updates the matching entry in place, keeps its unknown fields and drops a shadowed plain duplicate");
        loaded.PresetTargets = loaded.WithPresetLevels([Presets.ByName("Night")]);
        Assert(loaded.PresetTargets.Count == 2 && loaded.PresetLevel("Night") == Presets.ByName("Night"),
            "a default target keeps an entry that carries another version's fields");

        File.WriteAllText(path, """
            { "PresetTargets": [ { "Name": "Night", "Nits": 80, "Kelvin": 4000 }, { "Name": "NIGHT", "Nits": 300, "Kelvin": 6000 } ] }
            """);
        var duplicated = AppSettings.Load(path);
        duplicated.PresetTargets = duplicated.WithPresetLevels([Presets.ByName("Night")]);
        Assert(duplicated.PresetTargets.Count == 0 && duplicated.PresetLevel("Night") == Presets.ByName("Night"),
            "restoring a default cannot let a hand-edited duplicate become the active target");
        duplicated = AppSettings.Load(path);
        duplicated.PresetTargets = duplicated.WithPresetLevels([Presets.ByName("Night") with { Nits = 95 }]);
        Assert(duplicated.PresetTargets.Count == 1 && duplicated.PresetLevel("Night").Nits == 95,
            "editing a duplicated preset leaves exactly the requested target");

        File.WriteAllText(path, """{ "PresetTargets": null }""");
        Assert(AppSettings.Load(path).PresetLevels.SequenceEqual(Presets.Levels), "a null target list loads as the defaults");

        var profile = new PanelDatabase.Profile(400, 50);
        Assert(Math.Abs(Presets.PercentFor(profile, 110).Value - 60 / 3.5) < 1e-9 &&
            Presets.PercentFor(new PanelDatabase.Profile(100, 100), 110) == null,
            "preset percentages use the linear model and refuse a profile with no range");
        string edited = DeskCommandBar.PresetHelp(night, "on every visible panel");
        string builtIn = DeskCommandBar.PresetHelp(Presets.ByName("Night"), "on every visible panel");
        Assert(edited.Contains("(your target)") && edited.Contains("90 nits") && edited.Contains("4200K") &&
            !builtIn.Contains("your target") && builtIn.Contains("Settings > Preset targets"),
            "preset help states the active target and where to change it");
    }

    private static void CheckPresetEditorView(string renderDirectory)
    {
        var displays = new[]
        {
            new PresetPreviewDisplay("Desk A", new PanelDatabase.Profile(400, 50), UserProfile: true),
            new PresetPreviewDisplay("Desk B", PanelDatabase.FallbackFor(false), UserProfile: false),
        };
        var current = Presets.Levels.Select(l => l.Name == "Night" ? l with { Nits = 90, Kelvin = 4200 } : l).ToArray();
        var context = new PresetEditorContext(current, displays, WithoutBrightness: 1);
        var requests = new List<IReadOnlyList<Presets.Level>>();
        var pending = new Queue<Func<Task<SettingsSaveResult>>>();
        using var form = new PreviewForm { ClientSize = new Size(600, 760), Font = Theme.Body, AutoScaleMode = AutoScaleMode.None };
        using var view = new PresetEditorView(context, levels => { requests.Add(levels); return pending.Dequeue()(); }) { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        form.Show();
        WholeNumberInput Input(string name) => Descendants(view).OfType<WholeNumberInput>().Single(i => i.AccessibleName == name);
        TextBox Edit(string name) => Descendants(Input(name)).OfType<TextBox>().Single();
        Label Preview(string name) => Descendants(view).OfType<Label>().Single(l => l.AccessibleName == name + " brightness requests");
        var save = Descendants(view).OfType<FlatButton>().Single(b => b.Text == "Save targets");
        var defaults = Descendants(view).OfType<FlatButton>().Single(b => b.Text == "Use defaults");
        var cancel = Descendants(view).OfType<FlatButton>().Single(b => b.Text == "Cancel");
        var status = Descendants(view).OfType<Label>().Single(l => l.AccessibleRole == AccessibleRole.Alert);

        Assert(Input("Night target luminance (nits)").Value == 90 && Input("Night warmth (kelvin)").Value == 4200 &&
            Input("Day target luminance (nits)").Value == 200, "editor opens with the active targets");
        Assert(Descendants(view).OfType<Label>().Any(l => l.Text.Contains("Default: 110 nits, 4600 K")),
            "each preset shows its built-in default beside the editable value");
        string night = Preview("Night").Text;
        Assert(night.Contains("4200 K") && night.Contains("Desk A: 11% brightness") && !night.Contains("Desk A: 11% brightness,") &&
            night.Contains("Desk B:") && night.Contains("generic estimate"),
            "preview converts the target through each display's own estimate and marks generic profiles");
        Assert(Preview("Night").AccessibleDescription == night, "the changing preview is exposed to accessibility clients");
        Assert(Descendants(view).OfType<Label>().Any(l => l.Text.Contains("1 display has no brightness control")),
            "displays without brightness control are counted as warmth-only");
        Input("Night target luminance (nits)").Value = 30;
        Assert(Preview("Night").Text.Contains("Desk A: 0% brightness (below floor)"), "preview identifies a target below a panel's floor");
        Input("Day target luminance (nits)").Value = 900;
        Assert(Preview("Day").Text.Contains("Desk A: 100% brightness (above peak)"), "preview identifies a target above a panel's peak");

        foreach (string text in new[] { "", "abc", "12.5", "0", "1001" })
        {
            Edit("Day target luminance (nits)").Text = text;
            Assert(!save.Enabled && status.Text.Contains("Day target") && status.Text.Contains("Nothing has been saved") &&
                Edit("Day target luminance (nits)").Text == text && Preview("Day").ForeColor == Theme.Warn,
                "an invalid target keeps its text, names the field and disables Save");
            save.PerformClick();
            Assert(requests.Count == 0, "an invalid draft cannot request a save");
        }
        Edit("Evening warmth (kelvin)").Text = "2500";
        Assert(status.Text.Contains("Day target, Evening warmth"), "every invalid field is named in the pinned correction");
        if (renderDirectory != null)
        {
            Directory.CreateDirectory(renderDirectory);
            Render(view, Path.Combine(renderDirectory, "preset-editor-invalid.png"));
        }
        defaults.PerformClick();
        Assert(save.Enabled && Edit("Day target luminance (nits)").Text == "200" && Input("Evening warmth (kelvin)").Value == 5600 &&
            Input("Night target luminance (nits)").Value == 110 && status.Text.StartsWith("Saving does not adjust"),
            "Use defaults repairs every field with the built-in targets without saving");
        Assert(requests.Count == 0, "Use defaults does not save");

        Input("Night target luminance (nits)").Value = 80;
        var gate = new TaskCompletionSource<SettingsSaveResult>();
        pending.Enqueue(() => gate.Task);
        int saved = 0;
        view.Saved += () => saved++;
        save.PerformClick();
        Assert(view.IsSaving && requests.Count == 1 && !save.Enabled && !cancel.Enabled && !defaults.Enabled &&
            !Input("Night target luminance (nits)").Enabled, "a pending save locks the draft and every action");
        Assert(requests[0].Count == 3 && requests[0].Single(l => l.Name == "Night") is { Nits: 80, Kelvin: 4600 },
            "the save receives every preset's draft target");
        view.ShowSavePending();
        Assert(status.Text.Contains("Wait for its result"), "closing during a save explains the wait");
        gate.SetResult(SettingsSaveResult.Failed);
        PumpProfileSave(view.PendingSave);
        Assert(!view.IsSaving && saved == 0 && save.Enabled && cancel.Enabled &&
            Input("Night target luminance (nits)").Value == 80 && status.Text.Contains("Previous targets remain active"),
            "an unconfirmed save keeps the draft and says the previous targets are still active");
        foreach (var (result, text) in new[] { (SettingsSaveResult.Busy, "Another settings save"),
            (SettingsSaveResult.SourceChanged, "changed outside"), (SettingsSaveResult.Unavailable, "could not be loaded") })
        {
            pending.Enqueue(() => Task.FromResult(result));
            save.PerformClick();
            PumpProfileSave(view.PendingSave);
            Assert(saved == 0 && status.Text.Contains(text) && status.ForeColor == Theme.Warn, $"{result} save outcome has specific guidance");
        }
        pending.Enqueue(() => throw new InvalidOperationException("Fictitious owner failure"));
        save.PerformClick();
        PumpProfileSave(view.PendingSave);
        Assert(!view.IsSaving && saved == 0 && status.Text.Contains("not confirmed"), "an owner exception settles as an unconfirmed save");

        foreach (int width in new[] { 600, 460 })
        {
            form.ClientSize = new Size(width, 760);
            view.PerformLayout();
            foreach (var control in Descendants(view).Where(c => c.Visible && c is Label or NumericUpDown or FlatButton))
            {
                var bounds = new Rectangle(view.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Assert(bounds.Left >= 0 && bounds.Right <= view.ClientSize.Width, $"preset editor content fits {width} pixels");
            }
            if (renderDirectory != null) Render(view, Path.Combine(renderDirectory, $"preset-editor-{width}.png"));
        }
        form.ClientSize = new Size(460, 440);
        view.PerformLayout();
        foreach (var control in new Control[] { save, defaults, cancel, status })
        {
            var bounds = new Rectangle(view.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
            Assert(view.ClientRectangle.Contains(bounds), "save feedback and actions stay pinned in a short editor");
        }
        if (renderDirectory != null) Render(view, Path.Combine(renderDirectory, "preset-editor-460-440.png"));

        pending.Enqueue(() => Task.FromResult(SettingsSaveResult.Saved));
        save.PerformClick();
        PumpProfileSave(view.PendingSave);
        Assert(saved == 1 && requests.Count == 6, "a confirmed save completes the editor once");

        int cancelled = 0;
        view.CancelRequested += () => cancelled++;
        cancel.PerformClick();
        Assert(cancelled == 1 && requests.Count == 6, "Cancel asks the owner to close without saving");

        using var empty = new PresetEditorView(new PresetEditorContext(Presets.Levels, []), _ => Task.FromResult(SettingsSaveResult.Saved));
        Assert(Descendants(empty).OfType<Label>().Single(l => l.AccessibleName == "Day brightness requests").Text == "Requests 6500 K warmth." &&
            Descendants(empty).OfType<Label>().Any(l => l.Text.Contains("No connected display reports a brightness control")),
            "without brightness-capable displays the preview promises warmth only");

        using var scaledForm = new PreviewForm { ClientSize = new Size(600, 760), Font = Theme.Body, AutoScaleMode = AutoScaleMode.None };
        using var scaled = new PresetEditorView(context, _ => Task.FromResult(SettingsSaveResult.Saved)) { Dock = DockStyle.Fill };
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
            foreach (var control in Descendants(scaled).Where(c => c.Visible && c is Label or NumericUpDown or FlatButton))
            {
                var bounds = new Rectangle(scaled.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Assert(bounds.Left >= 0 && bounds.Right <= scaled.ClientSize.Width,
                    "preset editor content fits with simulated 200% geometry and font scaling");
                if (control is FlatButton) Assert(scaled.ClientRectangle.Contains(bounds), "scaled preset editor actions remain visible");
            }
            if (renderDirectory != null) Render(scaled, Path.Combine(renderDirectory, "preset-editor-200.png"));
        }
        finally { foreach (var font in fonts.Values) font.Dispose(); }
    }

    private static void Render(Control control, string path)
    {
        using var image = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(image, control.ClientRectangle);
        image.Save(path);
    }
}
