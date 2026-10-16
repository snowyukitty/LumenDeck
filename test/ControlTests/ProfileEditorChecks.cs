using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using LumenDeck;

internal static partial class Program
{
    private static void CheckProfileEditor(string directory, string renderDirectory)
    {
        string work = Path.Combine(directory, "profile-editor");
        Directory.CreateDirectory(work);
        string path = Path.Combine(work, "panels.json");
        const string original = """
            {
              "Example": { "PeakNits": 400, "FloorNits": 50, "Measured": false },
              "Example Studio": { "PeakNits": 350, "FloorNits": 40, "Note": "broad" },
              "example studio panel": { "PeakNits": 300, "FloorNits": 30, "Note": "日本語 sources", "Future": { "curve": [0, 0.5, 1] } },
              "Other": { "PeakNits": 200, "FloorNits": 10, "Extra": true },
              "EXAMPLE - delete this entry": { "PeakNits": 350, "FloorNits": 50 }
            }
            """;
        File.WriteAllText(path, original);
        byte[] originalBytes = File.ReadAllBytes(path);
        var store = PanelProfileStore.Open(path, "Example Studio Panel", false);
        Assert(store.HasExactEntry && store.CurrentSource == "exact", "case-only exact model entry is updated in place");
        Assert(store.CurrentProfile.PeakNits == 300, "editor uses the longest valid current match");
        Assert(store.RemovalUsesUserProfile && store.RemovalProfile.PeakNits == 350,
            "removal preview resolves the next broader profile");
        var stale = PanelProfileStore.Open(path, "Example Studio Panel", false);
        var draft = new PanelDatabase.Profile(280, 20, true, "Measured with instrument; 日本語 conditions");
        Assert(store.Save(draft) == ProfileSaveResult.Saved, "store persists a valid draft");
        string saved = File.ReadAllText(path);
        byte[] savedBytes = File.ReadAllBytes(path);
        var updated = JsonNode.Parse(saved).AsObject();
        var before = JsonNode.Parse(original).AsObject();
        foreach (string key in before.Select(entry => entry.Key).Where(key => key != "example studio panel"))
            Assert(JsonNode.DeepEquals(updated[key], before[key]), "save preserves every other profile and sample");
        Assert(JsonNode.DeepEquals(updated["example studio panel"]["Future"], before["example studio panel"]["Future"]),
            "save preserves nested unknown fields on the edited entry");
        Assert(updated.Count == before.Count && !updated.ContainsKey("Example Studio Panel"),
            "case-only identity does not add a duplicate entry");
        Assert(updated["example studio panel"]["Note"].GetValue<string>() == draft.Note,
            "Unicode source notes persist without truncation");
        Assert(store.CommittedProfiles != null, "successful save publishes its confirmed profile snapshot");
        bool receiptReadOnly = false;
        try { ((IDictionary<string, PanelDatabase.Profile>)store.CommittedProfiles).Clear(); }
        catch (NotSupportedException) { receiptReadOnly = true; }
        Assert(receiptReadOnly, "committed profile snapshot does not expose mutable dictionary operations");
        File.WriteAllText(path, "{}");
        PanelDatabase.InstallProfiles(store.CommittedProfiles);
        using (var firstModel = Display("profile-save-first"))
        using (var secondModel = Display("profile-save-second"))
        {
            firstModel.Description = secondModel.Description = "Example Studio Panel";
            Assert(PanelDatabase.For(firstModel) == draft && PanelDatabase.For(secondModel) == draft,
                "engine installs the confirmed snapshot without rereading a later external edit");
        }
        File.WriteAllBytes(path, savedBytes);
        PanelDatabase.Load(Path.Combine(work, "absent.json"));
        string[] backups = Directory.GetFiles(Path.Combine(work, "profile-backups"));
        Assert(backups.Length == 1 && File.ReadAllBytes(backups[0]).AsSpan().SequenceEqual(originalBytes), "backup preserves the actual prior bytes");
        Assert(stale.Save(new(500, 0)) == ProfileSaveResult.SourceChanged && File.ReadAllText(path) == saved,
            "an older dialog cannot overwrite a newer cooperating save");
        var remove = PanelProfileStore.Open(path, "Example Studio Panel", false);
        Assert(remove.Save(null, removeExact: true) == ProfileSaveResult.Saved, "remove deletes only the exact full-name entry");
        updated = JsonNode.Parse(File.ReadAllText(path)).AsObject();
        Assert(!updated.ContainsKey("example studio panel") && updated.ContainsKey("Example Studio"),
            "remove keeps broader profiles");
        backups = Directory.GetFiles(Path.Combine(work, "profile-backups"));
        Assert(backups.Length == 2 && backups.Any(backup => File.ReadAllBytes(backup).AsSpan().SequenceEqual(savedBytes)),
            "successive saves keep distinct predecessor backups");
        store = PanelProfileStore.Open(path, "Example Studio Panel", false);
        Assert(!store.HasExactEntry && store.CurrentSource == "broader", "inherited profiles are not removable exact entries");
        Assert(store.Save(draft, removeExact: true) == ProfileSaveResult.InvalidValues, "remove cannot delete a broader profile");
        Assert(store.Save(new(0, 0)) == ProfileSaveResult.InvalidValues &&
            store.Save(new(100, -1)) == ProfileSaveResult.InvalidValues &&
            store.Save(new(100, 100)) == ProfileSaveResult.InvalidValues, "store validates peak and floor independently of UI");
        Assert(store.Save(draft) == ProfileSaveResult.Saved, "inherited editing creates a specific full-name entry");

        foreach (string invalid in new[] { "{", "null", "[]", "{\"X\":1}",
            "{\"X\":{},\"x\":{}}", "{\"X\":{},\"X\":{}}",
            "{\"X\":{\"PeakNits\":1,\"PeakNits\":2}}",
            "{\"X\":{\"Future\":{\"a\":1,\"a\":2}}}" })
        {
            File.WriteAllText(path, invalid);
            bool rejected = false;
            try { _ = PanelProfileStore.Open(path, "X", false); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException) { rejected = true; }
            Assert(rejected && File.ReadAllText(path) == invalid, "invalid or ambiguous source is rejected without a write");
        }
        CheckProfileSourceEncodings(work);
        File.WriteAllText(path, "{}");
        var desktop = PanelProfileStore.Open(path, "No profile", false);
        var laptop = PanelProfileStore.Open(path, "No profile", true);
        Assert(desktop.CurrentProfile == PanelDatabase.FallbackFor(false) && laptop.CurrentProfile == PanelDatabase.FallbackFor(true),
            "editor previews the established desktop and laptop generic models");
        var timestamp = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "[]");
        File.SetLastWriteTimeUtc(path, timestamp);
        Assert(desktop.Save(draft) == ProfileSaveResult.SourceChanged, "snapshot comparison detects same-size edits despite unchanged timestamps");

        string absent = Path.Combine(work, "new.json");
        var first = PanelProfileStore.Open(absent, "New model", false);
        Assert(!File.Exists(absent), "opening an absent profile file performs no write");
        File.WriteAllText(absent, "{}");
        Assert(first.Save(draft) == ProfileSaveResult.SourceChanged, "file creation after opening is a conflict");
        string newPath = Path.Combine(work, "first.json");
        Assert(PanelProfileStore.Open(newPath, "New model", false).Save(draft) == ProfileSaveResult.Saved,
            "first save creates a missing file without replacing another file");

        File.WriteAllText(path, "{}");
        var blocked = PanelProfileStore.Open(path, "Blocked", false);
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert(blocked.Save(draft) == ProfileSaveResult.Failed, "locked-file failure returns a result without deleting the source");
        Assert(File.ReadAllText(path) == "{}" && Directory.GetFiles(work, ".panels-*.tmp").Length == 0,
            "failed save preserves source and cleans its own temporary file");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert(PanelProfileStore.Open(path, "Read only", false).Save(draft) == ProfileSaveResult.Failed,
                "replacement failure is reported for a read-only target");
            Assert(File.ReadAllText(path) == "{}" && Directory.GetFiles(work, ".panels-*.tmp").Length == 0,
                "read-only replacement failure preserves source and removes temporary output");
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
        string mutexName = "Global\\LumenDeck.PanelProfiles." + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())));
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            using var mutex = new Mutex(false, mutexName);
            mutex.WaitOne();
            try { acquired.Set(); release.Wait(); }
            finally { mutex.ReleaseMutex(); }
        });
        try
        {
            Assert(acquired.Wait(TimeSpan.FromSeconds(3)), "test writer acquires the profile mutex");
            Assert(PanelProfileStore.Open(path, "Busy", false).Save(draft) == ProfileSaveResult.Busy,
                "machine-wide cooperating writer contention returns promptly without a commit");
        }
        finally { release.Set(); holder.GetAwaiter().GetResult(); }

        Assert(File.ReadAllText(path) == "{}" && Directory.GetFiles(work, ".panels-*.tmp").Length == 0,
            "global mutex contention leaves source and temporary output untouched");
        using (var collision = new EventWaitHandle(false, EventResetMode.AutoReset, mutexName))
            Assert(PanelProfileStore.Open(path, "Unavailable lock", false).Save(draft) == ProfileSaveResult.Failed,
                "an unavailable global mutex reports an unconfirmed save instead of throwing");
        Assert(File.ReadAllText(path) == "{}" && Directory.GetFiles(work, ".panels-*.tmp").Length == 0,
            "global mutex creation failure leaves source and temporary output untouched");
        Assert(PanelProfileStore.Open(path, "Recovered lock", false).Save(draft) == ProfileSaveResult.Saved,
            "save can recover after the conflicting named object is disposed");

        CheckProfileEditorView(work, renderDirectory);
    }

    private static void CheckProfileSourceEncodings(string work)
    {
        (string Name, Encoding Encoding, byte[] Invalid)[] cases =
        [
            ("utf8", new UTF8Encoding(false), [0xC3, 0x28]),
            ("utf8-bom", new UTF8Encoding(true), [0xC3, 0x28]),
            ("utf16-le", new UnicodeEncoding(false, true), [0x00, 0xD8]),
            ("utf16-be", new UnicodeEncoding(true, true), [0xD8, 0x00]),
            ("utf32-le", new UTF32Encoding(false, true), [0x00, 0x00, 0x11, 0x00]),
            ("utf32-be", new UTF32Encoding(true, true), [0x00, 0x11, 0x00, 0x00]),
        ];
        foreach (var item in cases)
        {
            string directory = Path.Combine(work, item.Name);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "panels.json");
            const string prefix = "{\"Other\":{\"PeakNits\":300,\"FloorNits\":30,\"Note\":\"";
            const string suffix = "\",\"Future\":{\"curve\":[0,1]}}}";
            byte[] malformed = item.Encoding.GetPreamble().Concat(item.Encoding.GetBytes(prefix))
                .Concat(item.Invalid).Concat(item.Encoding.GetBytes(suffix)).ToArray();
            File.WriteAllBytes(path, malformed);
            bool rejected = false;
            try { _ = PanelProfileStore.Open(path, "New model", false); }
            catch (DecoderFallbackException) { rejected = true; }
            Assert(rejected && File.ReadAllBytes(path).AsSpan().SequenceEqual(malformed),
                $"malformed {item.Name} profile bytes are rejected without substitution or writes");

            byte[] valid = item.Encoding.GetPreamble().Concat(item.Encoding.GetBytes(prefix + "日本語 sources" + suffix)).ToArray();
            File.WriteAllBytes(path, valid);
            Assert(PanelProfileStore.Open(path, "New model", false).Save(new(400, 40)) == ProfileSaveResult.Saved,
                $"valid {item.Name} profiles remain editable");
            var saved = JsonNode.Parse(File.ReadAllText(path));
            Assert(saved["Other"]["Note"].GetValue<string>() == "日本語 sources" &&
                JsonNode.DeepEquals(saved["Other"]["Future"], JsonNode.Parse("{\"curve\":[0,1]}")),
                $"saving {item.Name} preserves another model's Unicode note and unknown fields");
            string backup = Directory.GetFiles(Path.Combine(directory, "profile-backups")).Single();
            Assert(File.ReadAllBytes(backup).AsSpan().SequenceEqual(valid),
                $"saving {item.Name} preserves predecessor encoding and bytes in its backup");
        }
    }

    private static void CheckProfileEditorView(string work, string renderDirectory)
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (string cultureName in new[] { "en-US", "fr-FR", "hi-IN" })
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
                System.Globalization.CultureInfo.CurrentCulture = culture;
                using var number = new WholeNumberInput { Minimum = 0, Maximum = 200000, ThousandsSeparator = true, Value = 120000 };
                Assert(number.TryGetDraft(out int grouped) && grouped == 120000,
                    $"shared integer drafts accept native thousands formatting in {cultureName}");
                var edit = Descendants(number).OfType<TextBox>().Single();
                edit.Text = "12" + culture.NumberFormat.NumberGroupSeparator + "5";
                Assert(!number.TryGetDraft(out _), $"shared integer drafts reject malformed numeric groups in {cultureName}");
                edit.Text = "120001";
                Assert(number.TryGetDraft(out int plain) && plain == 120001,
                    $"shared integer drafts accept ungrouped repair in {cultureName}");
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
        string path = Path.Combine(work, "view.json");
        var context = new ProfileEditorContext("PRIVATE MODEL NAME", false, 50, "Display A");
        var store = PanelProfileStore.Open(path, context.ModelName, false);
        using var form = new PreviewForm { ClientSize = new Size(620, 780), Font = Theme.Body, AutoScaleMode = AutoScaleMode.None };
        using var view = new ProfileEditorView(store, context) { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        form.Show();
        var inputs = Descendants(view).OfType<NumericUpDown>().ToArray();
        var peak = inputs.Single(input => input.AccessibleName == "Peak luminance");
        var floor = inputs.Single(input => input.AccessibleName == "Floor luminance");
        var save = Descendants(view).OfType<FlatButton>().Single(button => button.Text == "Save estimate");
        var remove = Descendants(view).OfType<FlatButton>().Single(button => button.Text == "Remove this profile");
        var preview = Descendants(view).OfType<Label>().Single(label => label.AccessibleName == "Draft luminance and preset estimates");
        Assert(!remove.Enabled && !File.Exists(path), "generic editor disables remove and opens without persistence");
        Assert(!Descendants(view).Any(control => control.Text.Contains(context.ModelName)), "editor respects the captured display alias");
        peak.Value = 400;
        floor.Value = 50;
        Assert(preview.Text.Contains("estimated 225 nits") && preview.Text.Contains("Day: 31%"),
            "draft preview reflects the existing linear model without applying presets");
        string previewText = preview.Text;
        Descendants(view).OfType<CheckBox>().Single().Checked = true;
        Assert(preview.Text == previewText && preview.Text.Contains("estimated"), "measurement declaration never upgrades preview to measured light");
        floor.Value = 400;
        Assert(!save.Enabled && preview.Text.Contains("floor below"), "invalid draft disables save with visible guidance");
        var status = Descendants(view).OfType<Label>().Single(label => label.AccessibleRole == AccessibleRole.Alert);
        form.ClientSize = new Size(460, 440);
        view.PerformLayout();
        var statusBounds = new Rectangle(view.PointToClient(status.PointToScreen(Point.Empty)), status.Size);
        var saveBounds = new Rectangle(view.PointToClient(save.PointToScreen(Point.Empty)), save.Size);
        Assert(status.Text.Contains("floor below") && view.ClientRectangle.Contains(statusBounds) && statusBounds.Bottom <= saveBounds.Top,
            "invalid draft reason stays visible beside the disabled Save in a short editor");
        Assert(save.AccessibleDescription.Contains("floor below") && floor.AccessibleDescription.Contains("below"),
            "invalid draft reason is available from the save action and floor field");
        if (renderDirectory != null)
        {
            Directory.CreateDirectory(renderDirectory);
            using var invalidImage = new Bitmap(view.Width, view.Height);
            view.DrawToBitmap(invalidImage, view.ClientRectangle);
            invalidImage.Save(Path.Combine(renderDirectory, "profile-editor-invalid-460-440.png"));
        }
        save.PerformClick();
        Assert(!File.Exists(path) && !view.IsSaving, "invalid draft cannot start a save or create a file");
        peak.Value = 0;
        Assert(status.Text.Contains("peak above zero") && peak.AccessibleDescription.Contains("above zero"),
            "zero peak exposes a specific correction in both pinned feedback and field description");
        peak.Value = 200;
        floor.Value = 150;
        Assert(preview.Text.Contains("Night: 0% (below floor)"), "preview identifies a target below the profile's reachable floor");
        Assert(save.Enabled && status.Text.Contains("backup") && !save.AccessibleDescription.Contains("floor below"),
            "correcting the draft restores Save and clears obsolete validation feedback");
        foreach (var input in new[] { peak, floor })
        foreach (string draft in new[] { "", "invalid", "12.5", "-1", "999999999999999999999999999999999999" })
        {
            var edit = Descendants(input).OfType<TextBox>().Single();
            edit.Text = draft;
            Assert(!save.Enabled && status.Text.Contains("whole number") && edit.Text == draft,
                "invalid profile numeric text stays visible with a pinned correction and disabled Save");
            if (input == peak && draft == "invalid")
            {
                view.PerformLayout();
                var feedbackBounds = new Rectangle(view.PointToClient(status.PointToScreen(Point.Empty)), status.Size);
                Assert(view.ClientRectangle.Contains(feedbackBounds) && feedbackBounds.Bottom <= save.Parent.Top + save.Parent.Parent.Top,
                    "malformed profile text keeps its complete correction above the pinned actions");
                if (renderDirectory != null)
                {
                    using var invalidTextImage = new Bitmap(view.Width, view.Height);
                    view.DrawToBitmap(invalidTextImage, view.ClientRectangle);
                    invalidTextImage.Save(Path.Combine(renderDirectory, "profile-editor-invalid-text.png"));
                }
            }
            save.PerformClick();
            input.UpButton(); input.DownButton();
            Assert(!view.IsSaving && !File.Exists(path) && edit.Text == draft,
                "invalid profile numeric text cannot save or silently step from a substituted value");
            edit.Text = input == peak ? "200" : "150";
            Assert(save.Enabled, "repairing profile numeric text restores a valid draft without saving");
        }
        Assert(peak.Value == 200 && floor.Value == 150,
            "native numeric access commits repaired profile text before programmatic changes");
        peak.Value = 1200;
        Assert(save.Enabled && peak.Text == 1200.ToString("N0") && preview.Text.Contains("estimated"),
            "profile fields retain valid native thousands formatting");
        peak.Value = 100;
        floor.Value = 20;
        Assert(preview.Text.Contains("Day: 100% (above peak)"), "preview identifies a target above the profile's reachable peak");
        peak.Value = 400;
        floor.Value = 50;
        foreach (int width in new[] { 620, 460, 620 })
        {
            form.ClientSize = new Size(width, 780);
            view.PerformLayout();
            foreach (var control in Descendants(view).Where(control => control.Visible && control is Label or CheckBox or NumericUpDown or FlatButton))
            {
                var bounds = new Rectangle(view.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Assert(bounds.Left >= 0 && bounds.Right <= view.ClientSize.Width, "profile editor content fits the available width");
            }
            if (renderDirectory != null && width == 460)
            {
                Directory.CreateDirectory(renderDirectory);
                using var image = new Bitmap(view.Width, view.Height);
                view.DrawToBitmap(image, view.ClientRectangle);
                image.Save(Path.Combine(renderDirectory, "profile-editor-460.png"));
            }
        }
        form.ClientSize = new Size(460, 440);
        view.PerformLayout();
        foreach (var button in Descendants(view).OfType<FlatButton>())
        {
            var bounds = new Rectangle(view.PointToClient(button.PointToScreen(Point.Empty)), button.Size);
            Assert(view.ClientRectangle.Contains(bounds), "save, remove and cancel remain visible in a short editor window");
        }
        int saved = 0;
        view.Saved += () => saved++;
        save.PerformClick();
        PumpProfileSave(view.PendingSave);
        Assert(saved == 1 && File.Exists(path), "save persists the local draft and emits one completion");

        store = PanelProfileStore.Open(path, context.ModelName, false);
        using var conflict = new ProfileEditorView(store, context);
        File.WriteAllText(path, "{}");
        Descendants(conflict).OfType<FlatButton>().Single(button => button.Text == "Save estimate").PerformClick();
        PumpProfileSave(conflict.PendingSave);
        Assert(Descendants(conflict).OfType<Label>().Any(label => label.Text.Contains("Cancel and reopen")) && File.ReadAllText(path) == "{}",
            "conflicted editor keeps the draft and explains reopening without overwriting the file");
        int cancelled = 0;
        conflict.CancelRequested += () => cancelled++;
        Descendants(conflict).OfType<FlatButton>().Single(button => button.Text == "Cancel").PerformClick();
        Assert(cancelled == 1 && File.ReadAllText(path) == "{}", "cancel emits an event without saving or restoring another snapshot");

        File.WriteAllText(path, "{\"PRIVATE MODEL NAME\":{\"PeakNits\":300,\"FloorNits\":30}}");
        using var removal = new ProfileEditorView(PanelProfileStore.Open(path, context.ModelName, false), context);
        Descendants(removal).OfType<NumericUpDown>().First().Controls.OfType<TextBox>().Single().Text = "invalid";
        Assert(!Descendants(removal).OfType<FlatButton>().Single(button => button.Text == "Save estimate").Enabled,
            "an invalid draft disables Save without disabling removal of the existing profile");
        int removed = 0;
        removal.Saved += () => removed++;
        Descendants(removal).OfType<FlatButton>().Single(button => button.Text == "Remove this profile").PerformClick();
        PumpProfileSave(removal.PendingSave);
        Assert(removed == 1 && JsonNode.Parse(File.ReadAllText(path)).AsObject().Count == 0,
            "remove UI commits exactly the reviewed full-name entry");

        using var scaledForm = new PreviewForm { ClientSize = new Size(620, 780), Font = Theme.Body, AutoScaleMode = AutoScaleMode.None };
        using var scaled = new ProfileEditorView(PanelProfileStore.Open(path, context.ModelName, false), context) { Dock = DockStyle.Fill };
        scaledForm.Controls.Add(scaled);
        scaledForm.Show();
        scaledForm.Scale(new SizeF(2, 2));
        var originals = Descendants(scaled).ToDictionary(control => control, control => control.Font);
        var fonts = originals.Values.Distinct().ToDictionary(font => font, font => new Font(font.FontFamily, font.Size * 2, font.Style));
        try
        {
            foreach (var pair in originals) pair.Key.Font = fonts[pair.Value];
            scaledForm.ClientSize = new Size(920, 1000);
            scaled.PerformLayout();
            foreach (var control in Descendants(scaled).Where(control => control.Visible && control is Label or CheckBox or NumericUpDown or FlatButton))
            {
                var bounds = new Rectangle(scaled.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Assert(bounds.Left >= 0 && bounds.Right <= scaled.ClientSize.Width,
                    "profile editor content fits with simulated 200% geometry and font scaling");
                if (control is FlatButton) Assert(scaled.ClientRectangle.Contains(bounds), "scaled editor footer actions remain visible");
            }
            if (renderDirectory != null)
            {
                using var image = new Bitmap(scaled.Width, scaled.Height);
                scaled.DrawToBitmap(image, scaled.ClientRectangle);
                image.Save(Path.Combine(renderDirectory, "profile-editor-460-200.png"));
            }
        }
        finally { foreach (var font in fonts.Values) font.Dispose(); }
    }

    private static void PumpProfileSave(Task task)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Assert(task.IsCompleted, "profile save completes while the test UI message loop is pumped");
        task.GetAwaiter().GetResult();
    }
}
