namespace LumenDeck;

internal class PresetScheduleForm : Form
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this);
    }

    private readonly PresetScheduleView _view;
    public bool IsSaving => !IsDisposed && _view.IsSaving;

    public PresetScheduleForm(ScheduleEditorContext context,
        Func<IReadOnlyCollection<PresetSchedule.Slot>, ScheduleOutlook> outlook, Func<ScheduleDraft, Task<SettingsSaveResult>> save)
    {
        Text = "LumenDeck — Preset schedule";
        Font = Theme.Body;
        Theme.Colors(this, back: () => Theme.Base, fore: () => Theme.Ink);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(460, 440);
        Size = new Size(560, 600);
        ShowInTaskbar = false;
        MinimizeBox = false;
        _view = new PresetScheduleView(context, outlook, save) { Dock = DockStyle.Fill };
        _view.Saved += () => { DialogResult = DialogResult.OK; Close(); };
        _view.CancelRequested += Close;
        Controls.Add(_view);
    }

    public void ShowSavePending() => _view.ShowSavePending();
    public void RefreshPreview() { if (!IsDisposed) _view.RefreshPreview(); }

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
