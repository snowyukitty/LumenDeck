namespace LumenDeck;

internal partial class MainForm
{
    internal void SetAllDisplays(bool show)
    {
        _showAllDisplays = show;
        _map.SetAllDisplays(show);
        ApplyDisplayView();
    }

    private void ApplyDisplayView()
    {
        _list.SuspendLayout();
        foreach (var card in _cards)
            card.Visible = _showAllDisplays || card.Monitor.StableKey == _selectedMonitorKey;
        _list.ResumeLayout(true);
        if (!_showAllDisplays)
        {
            // Hiding an autosized card can leave WS_HSCROLL set even after
            // HorizontalScroll.Visible becomes false. Reset native scroll state
            // against the selected card's settled extent, before focusing it.
            _list.AutoScroll = false;
            _list.AutoScroll = true;
            _list.PerformLayout();
        }
        _map.Refresh(_monitors.FirstOrDefault(m => m.StableKey == _selectedMonitorKey));
    }

    private static Control NamedCardControl(MonitorCard card, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var matches = FocusControls(card).Where(c => c.Name == name).ToArray();
        return matches.Length == 1 && matches[0].CanSelect ? matches[0] : null;
    }

    private void SelectDisplay(Monitor monitor)
    {
        if (_reallyClosing || IsDisposed || Disposing || !Visible || !Enabled ||
            WindowState == FormWindowState.Minimized || !CanAdjustDisplays ||
            !_cards.Any(c => !c.IsDisposed && c.Monitor == monitor)) return;
        _scanFocus?.CancelRecovery();
        _selectedMonitorKey = monitor.StableKey;
        ApplyDisplayView();
    }

    private void PickDisplay(Monitor monitor)
    {
        if (_reallyClosing || IsDisposed || Disposing || !Visible || !Enabled ||
            WindowState == FormWindowState.Minimized || !CanAdjustDisplays) return;
        _scanFocus?.CancelRecovery();
        var card = _cards.FirstOrDefault(c => !c.IsDisposed && c.Monitor == monitor);
        if (card == null) return;
        _selectedMonitorKey = monitor.StableKey;
        ApplyDisplayView();
        Control target = NamedCardControl(card, "brightness") ?? NamedCardControl(card, "warmth");
        if (target == null) return;
        int generation = _generation;
        ActiveControl = target;
        // Enter handlers can rebuild the cards. Never scroll or announce an old selection.
        if (generation != _generation || card.IsDisposed || !_cards.Contains(card) ||
            !card.Contains(target) || !target.CanSelect || ActiveControl != target) return;
        // A whole card can exceed the viewport. Align its header and adjustment bars
        // explicitly instead of letting ScrollControlIntoView align the card's bottom.
        int offset = _list.AutoScrollPosition.Y;
        int cardTop = card.Top - offset - _list.Padding.Top;
        int targetBottom = _list.PointToClient(target.PointToScreen(new Point(0, target.Height))).Y - offset;
        _list.AutoScrollPosition = new Point(0, Math.Max(0,
            Math.Max(cardTop, targetBottom - _list.ClientSize.Height + _list.Padding.Bottom)));
        _list.PerformLayout();
        card.Highlight();
        _map.Refresh(monitor);
        string position = monitor.PositionLabel;
        SetStatus($"Display {_cards.IndexOf(card) + 1} of {_cards.Count}: {monitor.DisplayName}" +
            (string.IsNullOrWhiteSpace(position) ? "." : $" ({position})."));
    }

    private void NavigateDisplays(bool previous)
    {
        if (_reallyClosing || IsDisposed || Disposing || !Visible || !Enabled ||
            WindowState == FormWindowState.Minimized || _preferencesMenu.Visible) return;
        _scanFocus?.CancelRecovery();
        if (!CanAdjustDisplays) return;
        var cards = _cards.Where(c => !c.IsDisposed).ToArray();
        if (cards.Length == 0) return;
        Control active = ActiveControl;
        while (active is ContainerControl container && container.ActiveControl != null) active = container.ActiveControl;
        MonitorCard current = null;
        for (Control parent = active; parent != null; parent = parent.Parent)
            if (parent is MonitorCard card) { current = card; break; }
        int index = Array.IndexOf(cards, current);
        int next = index < 0 ? (previous ? cards.Length - 1 : 0)
            : (index + (previous ? cards.Length - 1 : 1)) % cards.Length;
        var destination = cards[next];
        _selectedMonitorKey = destination.Monitor.StableKey;
        ApplyDisplayView();
        Control target = destination == current && active is { CanSelect: true } ? active
            : NamedCardControl(destination, active?.Name)
                ?? NamedCardControl(destination, "brightness")
                ?? NamedCardControl(destination, "warmth");
        if (target == null) return;
        int generation = _generation;
        ActiveControl = target;
        if (generation != _generation || destination.IsDisposed || !_cards.Contains(destination) ||
            !destination.Contains(target) || !target.CanSelect || ActiveControl != target) return;
        _list.ScrollControlIntoView(target);
        _map.Refresh(destination.Monitor);
        string position = destination.Monitor.PositionLabel;
        SetStatus($"Display {next + 1} of {cards.Length}: {destination.Monitor.DisplayName}" +
            (string.IsNullOrWhiteSpace(position) ? "." : $" ({position})."));
    }
}
