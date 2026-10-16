namespace LumenDeck;

internal partial class MainForm
{
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F1) { ShowKeyboardHelp(); return true; }
        if (keyData == (Keys.Control | Keys.PageDown)) { NavigateDisplays(previous: false); return true; }
        if (keyData == (Keys.Control | Keys.PageUp)) { NavigateDisplays(previous: true); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ShowKeyboardHelp()
    {
        if (_reallyClosing || IsDisposed || Disposing || !Enabled || !UiVisible) return;
        _scanFocus?.CancelRecovery();
        if (_keyboardHelp == null || _keyboardHelp.IsDisposed)
        {
            _keyboardHelp = _host.CreateKeyboardHelp();
            _keyboardHelp.Show(DialogOwner);
        }
        else _keyboardHelp.Reveal();
    }

    private void ShowAbout()
    {
        if (_reallyClosing || IsDisposed || Disposing || !Enabled || !UiVisible) return;
        _scanFocus?.CancelRecovery();
        if (_about == null || _about.IsDisposed)
        {
            _about = _host.CreateAbout();
            _about.Show(DialogOwner);
        }
        else _about.Reveal();
    }
}
