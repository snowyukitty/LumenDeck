namespace LumenDeck;

internal class ProfileEditorForm : Form
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this);
    }

    private readonly ProfileEditorView _view;
    public bool IsSaving => !IsDisposed && _view.IsSaving;

    public ProfileEditorForm(PanelProfileStore store, ProfileEditorContext context,
        Func<PanelDatabase.Profile, bool, ProfileSaveResult> write = null)
    {
        Text = "LumenDeck — Brightness estimate";
        Font = Theme.Body;
        BackColor = Theme.Base;
        ForeColor = Theme.Ink;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(460, 440);
        Size = new Size(620, 780);
        ShowInTaskbar = false;
        MinimizeBox = false;
        _view = new ProfileEditorView(store, context, write) { Dock = DockStyle.Fill };
        _view.Saved += () => { DialogResult = DialogResult.OK; Close(); };
        _view.CancelRequested += Close;
        Controls.Add(_view);
    }

    public void ShowSavePending() => _view.ShowSavePending();

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        bool waitForSave = IsSaving && e.CloseReason is CloseReason.UserClosing or CloseReason.FormOwnerClosing;
        if (waitForSave)
        {
            e.Cancel = true;
            ShowSavePending();
        }
        base.OnFormClosing(e);
        if (waitForSave) e.Cancel = true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
