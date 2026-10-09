using LumenDeck;

internal static partial class Program
{
    private sealed class VerificationProfileForm : ProfileEditorForm
    {
        public VerificationProfileForm(PanelProfileStore store, ProfileEditorContext context,
            Func<PanelDatabase.Profile, bool, ProfileSaveResult> write) : base(store, context, write)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
        }
        protected override bool ShowWithoutActivation => true;
        public void SendEscape()
        {
            var message = Message.Create(Handle, 0x100, (IntPtr)(int)Keys.Escape, IntPtr.Zero);
            ProcessCmdKey(ref message, Keys.Escape);
        }
    }

    private static void CheckAsyncProfileSave(string directory)
    {
        string path = Path.Combine(directory, "async-profile.json");
        var context = new ProfileEditorContext("Async synthetic model", false, 50);
        var store = PanelProfileStore.Open(path, context.ModelName, false);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int uiThread = Environment.CurrentManagedThreadId;
        int writerThread = 0;
        int writes = 0;
        int callbacks = 0;
        int callbackThread = 0;
        PanelDatabase.Profile captured = null;
        bool capturedRemove = false;
        using var host = new PreviewForm { ClientSize = new Size(620, 800) };
        using var view = new ProfileEditorView(store, context, (profile, remove) =>
        {
            Interlocked.Increment(ref writes);
            writerThread = Environment.CurrentManagedThreadId;
            captured = profile;
            capturedRemove = remove;
            entered.Set();
            release.Wait();
            return ProfileSaveResult.Failed;
        }) { Dock = DockStyle.Fill };
        host.Controls.Add(view);
        host.Show();
        var save = Descendants(view).OfType<FlatButton>().Single(button => button.Text == "Save estimate");
        var cancel = Descendants(view).OfType<FlatButton>().Single(button => button.Text == "Cancel");
        int cancellations = 0;
        view.CancelRequested += () => cancellations++;
        view.Saved += () => { callbacks++; callbackThread = Environment.CurrentManagedThreadId; };
        try
        {
            save.PerformClick();
            Assert(entered.Wait(TimeSpan.FromSeconds(3)), "background writer reaches its barrier");
            Assert(view.IsSaving && !view.PendingSave.IsCompleted && writerThread != uiThread,
                "save runs off the UI thread while its outcome is pending");
            Assert(captured.PeakNits == 300 && captured.FloorNits == 45 && !capturedRemove,
                "writer receives the immutable UI draft and operation kind");
            Assert(Descendants(view).OfType<NumericUpDown>().All(input => !input.Enabled) &&
                Descendants(view).OfType<FlatButton>().All(button => !button.Enabled),
                "pending save disables edits and duplicate/cancel actions");
            var pendingInput = Descendants(view).OfType<NumericUpDown>().First();
            var pendingEdit = Descendants(pendingInput).OfType<TextBox>().Single();
            var pendingStatus = Descendants(view).OfType<Label>().Single(label => label.AccessibleRole == AccessibleRole.Alert);
            string pendingText = pendingEdit.Text, pendingFeedback = pendingStatus.Text;
            pendingEdit.Text = "invalid";
            pendingEdit.Text = pendingText;
            Assert(Descendants(view).OfType<FlatButton>().All(button => !button.Enabled) && pendingStatus.Text == pendingFeedback,
                "numeric text notifications cannot re-enable actions or replace pending-save feedback");
            save.PerformClick();
            cancel.PerformClick();
            Assert(writes == 1 && cancellations == 0, "accessible direct actions cannot start a second write or discard a pending result");
            bool responsive = false;
            view.BeginInvoke((Action)(() => responsive = true));
            Application.DoEvents();
            Assert(responsive && !view.PendingSave.IsCompleted, "UI messages are processed while writer I/O is blocked");
            view.ShowSavePending();
            Assert(Descendants(view).OfType<Label>().Any(label => label.Text.Contains("Wait for its result")),
                "close attempts have an explicit pending-result explanation");
            CheckProfileFooter(view, host, "pending");
        }
        finally { release.Set(); }
        PumpProfileSave(view.PendingSave);
        Assert(!view.IsSaving && save.Enabled && cancel.Enabled && callbacks == 0,
            "failed completion restores editing and retains the draft without Saved");
        Assert(Descendants(view).OfType<Label>().Any(label => label.Text.Contains("Save was not confirmed") && label.Text.Contains("profile-backups")),
            "failure wording does not promise the file was unchanged");
        CheckProfileFooter(view, host, "failed");
        var status = Descendants(view).OfType<Label>().Single(label => label.AccessibleRole == AccessibleRole.Alert);
        var peak = Descendants(view).OfType<NumericUpDown>().Single(input => input.AccessibleName == "Peak luminance");
        decimal priorPeak = peak.Value;
        string failedFeedback = status.Text;
        peak.Value = 0;
        Assert(!save.Enabled && status.Text.Contains("peak above zero"),
            "invalid edit temporarily prioritizes its correction after a failed save");
        peak.Value = priorPeak;
        Assert(save.Enabled && status.Text == failedFeedback && writes == 1,
            "repairing values restores the prior unconfirmed-save warning without retrying");

        using var success = new ProfileEditorView(store, context, (_, _) => ProfileSaveResult.Saved);
        success.Saved += () => { callbacks++; callbackThread = Environment.CurrentManagedThreadId; };
        Descendants(success).OfType<FlatButton>().Single(button => button.Text == "Save estimate").PerformClick();
        PumpProfileSave(success.PendingSave);
        Assert(callbacks == 1 && callbackThread == uiThread, "confirmed completion is delivered once on the UI thread");

        using var closesOnSuccess = new ProfileEditorView(store, context, (_, _) => ProfileSaveResult.Saved);
        closesOnSuccess.Saved += () => closesOnSuccess.Dispose();
        Descendants(closesOnSuccess).OfType<FlatButton>().Single(button => button.Text == "Save estimate").PerformClick();
        PumpProfileSave(closesOnSuccess.PendingSave);
        Assert(closesOnSuccess.IsDisposed, "successful callback can synchronously dispose the editor without subsequent UI access");

        using var ambiguous = new ProfileEditorView(store, context, (profile, remove) =>
        {
            if (store.Save(profile, remove) != ProfileSaveResult.Saved)
                throw new InvalidOperationException("synthetic commit did not succeed");
            throw new IOException("synthetic loss of result after a successful commit");
        });
        int ambiguousCallbacks = 0;
        ambiguous.Saved += () => ambiguousCallbacks++;
        Descendants(ambiguous).OfType<FlatButton>().Single(button => button.Text == "Save estimate").PerformClick();
        PumpProfileSave(ambiguous.PendingSave);
        Assert(File.Exists(path) && store.CommittedProfiles?.ContainsKey(context.ModelName) == true,
            "ambiguous-result fixture actually commits its temporary profile file");
        Assert(ambiguousCallbacks == 0 && !ambiguous.IsSaving &&
            Descendants(ambiguous).OfType<Label>().Any(label => label.Text.Contains("Save was not confirmed")),
            "lost success result retains the draft without claiming unchanged bytes or confirmed save");

        using var thrown = new ProfileEditorView(store, context, (_, _) => throw new InvalidOperationException("synthetic writer construction failure"));
        Descendants(thrown).OfType<FlatButton>().Single(button => button.Text == "Save estimate").PerformClick();
        PumpProfileSave(thrown.PendingSave);
        Assert(!thrown.IsSaving && Descendants(thrown).OfType<FlatButton>().Single(button => button.Text == "Cancel").Enabled,
            "unexpected writer exception cannot leave the editor permanently busy");

        CheckDisposedProfileCompletion(store, context);
        CheckProfileCloseDuringSave(store, context);
    }

    private static void CheckProfileFooter(ProfileEditorView view, Form host, string state)
    {
        host.ClientSize = new Size(460, 440);
        view.PerformLayout();
        foreach (var action in Descendants(view).OfType<FlatButton>())
        {
            var bounds = new Rectangle(view.PointToClient(action.PointToScreen(Point.Empty)), action.Size);
            Assert(view.ClientRectangle.Contains(bounds), $"{state} profile footer action fits the short narrow window");
        }
    }

    private static void CheckDisposedProfileCompletion(PanelProfileStore store, ProfileEditorContext context)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var view = new ProfileEditorView(store, context, (_, _) =>
        {
            entered.Set(); release.Wait(); return ProfileSaveResult.Saved;
        });
        int callbacks = 0;
        view.Saved += () => callbacks++;
        try
        {
            Descendants(view).OfType<FlatButton>().Single(button => button.Text == "Save estimate").PerformClick();
            Assert(entered.Wait(TimeSpan.FromSeconds(3)), "disposed-view writer reaches its barrier");
            view.Dispose();
        }
        finally { release.Set(); }
        PumpProfileSave(view.PendingSave);
        Assert(callbacks == 0 && view.IsDisposed, "late completion does not notify or touch disposed UI");
    }

    private static void CheckProfileCloseDuringSave(PanelProfileStore store, ProfileEditorContext context)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var owner = new PreviewForm { ClientSize = new Size(700, 800) };
        using var editor = new VerificationProfileForm(store, context, (_, _) =>
        {
            entered.Set(); release.Wait(); return ProfileSaveResult.Failed;
        });
        owner.Show();
        editor.Show(owner);
        var view = Descendants(editor).OfType<ProfileEditorView>().Single();
        bool ownerCancelled = false;
        owner.FormClosing += (_, e) => ownerCancelled = e.Cancel;
        editor.FormClosing += (_, e) => e.Cancel = false;
        try
        {
            Descendants(view).OfType<FlatButton>().Single(button => button.Text == "Save estimate").PerformClick();
            Assert(entered.Wait(TimeSpan.FromSeconds(3)), "closing-form writer reaches its barrier");
            editor.Close();
            Assert(editor.Visible && !editor.IsDisposed && editor.IsSaving, "user close cannot discard a pending save result");
            editor.SendEscape();
            Assert(editor.Visible && !editor.IsDisposed && editor.IsSaving,
                "Escape cannot discard a pending save result");
            owner.Close();
            Assert(ownerCancelled && !owner.IsDisposed && !editor.IsDisposed,
                "owned-form close cancellation reaches the real WinForms owner lifecycle");
        }
        finally { release.Set(); }
        PumpProfileSave(view.PendingSave);
        editor.Close();
        Assert(editor.IsDisposed, "editor can close after a failed save has settled");
        owner.Close();
        Assert(owner.IsDisposed, "owner can close after the save has settled");
    }
}
