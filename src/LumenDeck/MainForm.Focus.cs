namespace LumenDeck;

internal partial class MainForm
{
    private ScanFocus _scanFocus;

    private static IEnumerable<Control> FocusControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in FocusControls(child)) yield return descendant;
        }
    }

    private ScanFocus BeginScanFocus()
    {
        ScanFocus previous = _scanFocus;
        _scanFocus = null;
        if (_reallyClosing || !Visible || !Enabled || WindowState == FormWindowState.Minimized ||
            OwnedForms.Any(f => f.Visible)) { previous?.Dispose(); return null; }
        ScanFocus next = null;
        if (previous is { Canceled: false, Parked: not null } &&
            previous.VisibilityGeneration == _visibilityGeneration && ActiveControl == previous.Parked)
            next = new ScanFocus(this, previous.MonitorKey, previous.ControlName, previous.PersistentTarget);
        else if (ActiveControl is { } active && Contains(active))
        {
            while (active is ContainerControl container && container.ActiveControl != null)
                active = container.ActiveControl;
            MonitorCard card = null;
            for (Control parent = active; parent != null; parent = parent.Parent)
                if (parent is MonitorCard found) { card = found; break; }
            next = card == null
                ? new ScanFocus(this, null, null, active)
                : new ScanFocus(this, card.Monitor.StableKey, active.Name, null);
        }
        previous?.Dispose();
        return _scanFocus = next;
    }

    /// <summary>One scan's logical selection ownership; never activates a window.</summary>
    private sealed class ScanFocus(MainForm owner, string monitorKey, string controlName, Control persistentTarget) : IDisposable
    {
        public string MonitorKey { get; } = monitorKey;
        public string ControlName { get; } = controlName;
        public Control PersistentTarget { get; } = persistentTarget;
        public int VisibilityGeneration { get; } = owner._visibilityGeneration;
        public Control Parked { get; private set; }
        public bool Canceled { get; private set; }
        private readonly List<Control> _watched = new();

        public void Park()
        {
            if (owner._scanFocus != this || !owner.Enabled || !owner.Visible || owner._visibilityGeneration != VisibilityGeneration) return;
            if (PersistentTarget is { IsDisposed: false, Enabled: true, Visible: true }) return;
            Parked = owner._bar.HelpButton;
            owner.ActiveControl = Parked;
            if (owner._scanFocus != this) return;
            foreach (var control in FocusControls(owner))
            {
                _watched.Add(control);
                control.Enter += Cancel;
                control.Click += Cancel;
                control.KeyDown += CancelKey;
                control.MouseDown += CancelMouse;
            }
        }

        private void Cancel(object sender, EventArgs args) => Canceled = true;
        private void CancelKey(object sender, KeyEventArgs args) => Canceled = true;
        private void CancelMouse(object sender, MouseEventArgs args) => Canceled = true;
        public void CancelRecovery() => Canceled = true;

        public void Complete(int generation)
        {
            Dispose();
            if (Canceled || Parked == null || owner.IsDisposed || owner.Disposing || owner._reallyClosing ||
                generation != owner._generation || VisibilityGeneration != owner._visibilityGeneration ||
                !owner.Visible || !owner.Enabled || owner.WindowState == FormWindowState.Minimized ||
                owner.OwnedForms.Any(f => f.Visible) || owner.ActiveControl != Parked) return;
            Control target = PersistentTarget;
            MonitorCard card = null;
            if (MonitorKey != null)
            {
                var matches = owner._cards.Where(c => c.Monitor.StableKey == MonitorKey).ToArray();
                if (matches.Length == 1)
                {
                    card = matches[0];
                    var targets = FocusControls(card).Where(c => !string.IsNullOrEmpty(ControlName) && c.Name == ControlName).ToArray();
                    target = targets.Length == 1 ? targets[0] : null;
                }
            }
            if (target is not { IsDisposed: false, Enabled: true, Visible: true, CanSelect: true })
            { target = owner._refreshButton; card = null; }
            if (!target.CanSelect) return;
            owner.ActiveControl = target;
            if (card is { IsDisposed: false } && generation == owner._generation &&
                owner._cards.Contains(card) && owner.ActiveControl == target)
                owner._list.ScrollControlIntoView(target);
        }

        public void Dispose()
        {
            foreach (var control in _watched)
            {
                control.Enter -= Cancel;
                control.Click -= Cancel;
                control.KeyDown -= CancelKey;
                control.MouseDown -= CancelMouse;
            }
            _watched.Clear();
        }
    }
}
