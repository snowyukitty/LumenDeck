namespace LumenDeck;

internal partial class MainForm
{
    private DisplayScanView _scanView;
    private readonly List<ToolStripItem> _displayTrayCommands = new();

    private bool CanAdjustDisplays => _scanState == DisplayScanState.Ready && _cards.Count > 0;

    private void UpdateDisplayScanPresentation()
    {
        if (IsDisposed || Disposing || _reallyClosing) return;
        bool available = CanAdjustDisplays;
        foreach (var button in _modeButtons.Values) button.Enabled = available;
        _bar.IdentifyButton.Enabled = available;
        _bar.WarmthOffButton.Enabled = available;
        foreach (var command in _displayTrayCommands) command.Enabled = available;
        _refreshButton.Enabled = _scanState != DisplayScanState.Reading;
        if (available)
        {
            _scanView?.Dispose();
            _scanView = null;
        }
        else
        {
            if (_scanView == null || _scanView.IsDisposed)
            {
                _scanView = new DisplayScanView(_scanState);
                _scanView.RefreshRequested += () => { if (_refreshButton.Enabled) _ = RebuildAsync(); };
                _scanView.ConnectionHelpRequested += ShowConnectionHelp;
            }
            else _scanView.SetState(_scanState);
            _scanView.SetWidth(CardWidth);
            if (_scanView.Parent != _list) _list.Controls.Add(_scanView);
        }
        UpdateDeskMapVisibility();
        UpdateNightSaver();
    }
}
