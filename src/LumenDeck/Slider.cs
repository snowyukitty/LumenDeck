using System.Drawing.Drawing2D;

namespace LumenDeck;

/// <summary>
/// The control this app is mostly made of, so it is worth drawing properly.
///
/// WinForms' TrackBar was the single worst thing about the old window: a thick
/// grey Win95 groove with a triangular thumb, no value readout, no hover state,
/// and it ignores every colour you set. Since a monitor tool is a wall of
/// sliders, that one control set the tone for the whole app.
///
/// This one draws an amber fill on a sunken track - the same ramp as the icon,
/// so the bar literally looks like the light it controls - and carries its own
/// value label. It also supports a disabled state that reads as "this monitor
/// does not offer this", rather than as a broken control.
/// </summary>
internal sealed class Slider : Control
{
    private int _min;
    private int _max = 100;
    private int _value;
    private bool _dragging;
    private bool _hover;
    private bool _readoutHover;

    /// <summary>Raised while dragging, and on every keyboard or click change.</summary>
    public event EventHandler ValueChanged;

    /// <summary>Formats the readout. Defaults to the raw number.</summary>
    // These controls are constructed in code, never dropped from a designer
    // toolbox, so there is nothing for the designer serializer to emit.
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, string> Format { get; set; } = v => v.ToString();

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Neutral { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Principal { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, string> AccessibleValueFormat { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ExactValueUnit { get; set; } = "units";

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<ExactValueContext, ExactValueForm> CreateExactEditor { get; set; } = context => new ExactValueForm(context);

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SmallChange { get; set; } = 1;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, int> EditValue { get; set; } = v => v;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, int> FromEditValue { get; set; } = v => v;

    public Slider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Value;
        Height = 30;
        TabStop = true;
    }

    /// <summary>
    /// What a screen reader announces. Without this the whole window is a set of
    /// unnamed custom controls: a slider that arrow keys drive but Narrator
    /// cannot describe is not actually keyboard-accessible.
    /// </summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibleObject(this);

    private sealed class SliderAccessibleObject : ControlAccessibleObject
    {
        private readonly Slider _owner;
        public SliderAccessibleObject(Slider owner) : base(owner) => _owner = owner;

        public override AccessibleRole Role => AccessibleRole.Slider;
        public override string Value => (_owner.AccessibleValueFormat ?? _owner.Format)(_owner.Value);
        public override string Name => _owner.AccessibleName ?? _owner.Name;
        // This custom control has no Alt mnemonic. The native default derives
        // one from '&' in Name, including literal display and feature names.
        public override string KeyboardShortcut => null;
        public override string DefaultAction => "Enter exact value";
        public override void DoDefaultAction() { if (_owner.Enabled) _owner.EditExactValue(); }

        public override string Description
        {
            get
            {
                int a = _owner.EditValue(_owner.Minimum), b = _owner.EditValue(_owner.Maximum);
                return $"Current value {_owner.EditValue(_owner.Value)}; range {Math.Min(a, b)} to {Math.Max(a, b)}. " +
                    _owner.AccessibleDescription;
            }
        }

        public override AccessibleStates State =>
            base.State | (_owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => _min;
        set { _min = value; if (_max <= _min) _max = _min + 1; Value = _value; Invalidate(); }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _max;
        set { _max = Math.Max(value, _min + 1); Value = _value; Invalidate(); }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            UpdateValue(value);
        }
    }

    /// <summary>Set without raising ValueChanged - for syncing from hardware.</summary>
    public void SetValueSilently(int value)
    {
        UpdateValue(value);
    }

    private bool UpdateValue(int value)
    {
        int v = Math.Clamp(value, _min, _max);
        if (v == _value) return false;
        _value = v;
        Invalidate();
        // The accessible-object path also works around the reversed notification
        // guard in Control.AccessibilityNotifyClients shipped with .NET 10.0.11.
        if (IsHandleCreated) ((ControlAccessibleObject)AccessibilityObject).NotifyClients(AccessibleEvents.ValueChange, -1);
        return true;
    }

    // Logical (96-dpi) metrics. Owner-drawn constants are device pixels and
    // WinForms does not touch them, so at 150% a 52px readout stays 52px while
    // the text inside it grows - "6500K" was getting clipped. Scale them from
    // DeviceDpi and re-scale when the window moves to another monitor.
    private const int TrackHDip = 6;
    private const int ThumbRDip = 7;
    private const int ReadoutWDip = 72;

    private int _trackH = TrackHDip;
    private int _thumbR = ThumbRDip;
    private int _readoutW = ReadoutWDip;

    private void RescaleMetrics()
    {
        _trackH = LogicalToDeviceUnits(TrackHDip);
        _thumbR = LogicalToDeviceUnits(ThumbRDip);
        _readoutW = LogicalToDeviceUnits(ReadoutWDip);
        if (Format != null)
            _readoutW = Math.Max(_readoutW, Math.Max(TextRenderer.MeasureText(Format(_min), Font).Width,
                TextRenderer.MeasureText(Format(_max), Font).Width) + LogicalToDeviceUnits(12));
        int textHeight = TextRenderer.MeasureText(Format?.Invoke(_value) ?? _value.ToString(), Font,
            Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height;
        Height = Math.Max(LogicalToDeviceUnits(30), textHeight + LogicalToDeviceUnits(8));
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RescaleMetrics();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RescaleMetrics();
    }

    protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
    {
        base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
        RescaleMetrics();
        Invalidate();
    }

    private Rectangle TrackRect
    {
        get
        {
            int y = (Height - _trackH) / 2;
            return new Rectangle(_thumbR, y, Math.Max(1, Width - _readoutW - _thumbR * 2), _trackH);
        }
    }

    private double Fraction => (_value - _min) / (double)(_max - _min);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var track = TrackRect;
        Theme.FillRound(g, track, _trackH / 2, Theme.Sunken);
        Theme.StrokeRound(g, track, _trackH / 2, Theme.Line);
        Color accent = Neutral ? Theme.InkMuted : Theme.Amber;
        Color light = Neutral ? Theme.Ink : Theme.AmberLight;

        int fillW = (int)Math.Round(track.Width * Fraction);
        if (Enabled && fillW > 2)
        {
            var fill = new Rectangle(track.X, track.Y, fillW, track.Height);
            using var brush = new LinearGradientBrush(
                new Rectangle(track.X, track.Y, Math.Max(2, track.Width), track.Height),
                accent, light, LinearGradientMode.Horizontal);
            using var path = Theme.Round(fill, _trackH / 2);
            g.FillPath(brush, path);
        }

        if (Enabled)
        {
            int cx = track.X + fillW;
            int cy = track.Y + track.Height / 2;
            int r = _dragging || _hover ? _thumbR + 1 : _thumbR;
            using var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
            g.FillEllipse(shadow, cx - r, cy - r + 1, r * 2, r * 2);
            using var thumb = new SolidBrush(light);
            g.FillEllipse(thumb, cx - r, cy - r, r * 2, r * 2);
            int center = Math.Max(1, LogicalToDeviceUnits(2));
            using var grip = new SolidBrush(Color.FromArgb(150, Theme.Sunken));
            g.FillEllipse(grip, cx - center, cy - center, center * 2, center * 2);
            if (Focused)
            {
                using var ring = new Pen(Color.FromArgb(160, 255, 255, 255), 1.5f);
                g.DrawEllipse(ring, cx - r - 2, cy - r - 2, (r + 2) * 2, (r + 2) * 2);
            }
        }

        int inset = LogicalToDeviceUnits(4);
        var readout = new Rectangle(track.Right + _thumbR + inset, inset, _readoutW - inset, Math.Max(1, Height - inset * 2));
        bool readoutHover = Enabled && _readoutHover;
        Theme.FillRound(g, readout, LogicalToDeviceUnits(5), readoutHover ? Theme.CardHover : Enabled ? Theme.Sunken : Theme.Card);
        Theme.StrokeRound(g, readout, LogicalToDeviceUnits(5),
            Focused ? Theme.AmberLight : readoutHover ? Theme.AmberDim : Enabled ? Theme.Line : Color.FromArgb(70, Theme.Line));
        TextRenderer.DrawText(g, Enabled ? Format(_value) : "—", Font, readout,
            Enabled ? Neutral ? Theme.Ink : Theme.AmberLight : Theme.InkFaint,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    private void SetFromX(int x)
    {
        var track = TrackRect;
        double f = (x - track.X) / (double)Math.Max(1, track.Width);
        int v = _min + (int)Math.Round(f * (_max - _min));
        v = Math.Clamp(v, _min, _max);
        if (!UpdateValue(v)) return;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        Focus();
        if (e.X > TrackRect.Right + _thumbR)
        {
            EditExactValue();
            return;
        }
        _dragging = true;
        Capture = true;
        SetFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool readoutHover = Enabled && e.X > TrackRect.Right + _thumbR;
        if (_readoutHover != readoutHover)
        {
            _readoutHover = readoutHover;
            Cursor = readoutHover ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        // Belt and braces with OnMouseCaptureChanged: if the button is not
        // actually down, this is not a drag no matter what the flag says.
        if (_dragging && (MouseButtons & MouseButtons.Left) == 0) { _dragging = false; Invalidate(); return; }
        if (_dragging) SetFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    /// <summary>
    /// Losing capture ends the drag.
    ///
    /// Clearing _dragging only in OnMouseUp leaves a ghost drag whenever
    /// something else takes capture mid-gesture - an Alt-Tab, a modal, a
    /// display-change rebuild. After that, merely moving the cursor across the
    /// slider with no button held would move the value and fire DDC writes.
    /// </summary>
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture && _dragging)
        {
            _dragging = false;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = _readoutHover = false; Cursor = Cursors.Default; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled) { _readoutHover = false; Cursor = Cursors.Default; }
        base.OnEnabledChanged(e);
        Invalidate();
    }

    private int _wheelResidue;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!Enabled) return;

        // Scale by the real delta rather than its sign. A precision touchpad
        // sends many small deltas and a fast wheel sends coalesced multiples of
        // 120; treating both as "one notch" makes the first hypersensitive and
        // the second sluggish. Residue carries sub-notch movement forward so
        // slow scrolling is not silently discarded.
        _wheelResidue += e.Delta;
        int notches = _wheelResidue / SystemInformation.MouseWheelScrollDelta;
        if (notches == 0) return;
        _wheelResidue -= notches * SystemInformation.MouseWheelScrollDelta;

        int step = Math.Max(1, SmallChange) * ((ModifierKeys & Keys.Shift) != 0 ? 10 : 1);
        int v = Math.Clamp(_value + notches * step, _min, _max);
        if (!UpdateValue(v)) return;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys key) =>
        (key & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or
            Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(key);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.SuppressKeyPress || IsDisposed || Disposing || !Enabled || e.Control || e.Alt) return;

        if (e.KeyCode is Keys.Enter or Keys.F2)
        {
            EditExactValue();
            e.Handled = e.SuppressKeyPress = true;
            return;
        }

        int step = Math.Max(1, SmallChange) * (e.Shift ? 10 : 1);
        int big = Math.Max(step, (_max - _min) / 10);
        int v = e.KeyCode switch
        {
            Keys.Left or Keys.Down => _value - step,
            Keys.Right or Keys.Up => _value + step,
            Keys.PageDown => _value - big,
            Keys.PageUp => _value + big,
            Keys.Home => _min,
            Keys.End => _max,
            _ => _value,
        };
        v = Math.Clamp(v, _min, _max);
        if (!UpdateValue(v)) return;
        ValueChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void EditExactValue()
    {
        if (!Enabled || IsDisposed || Disposing) return;
        int minimum = _min, maximum = _max;
        var toEdit = EditValue;
        var fromEdit = FromEditValue;
        bool TargetValid() => !IsDisposed && !Disposing && Enabled && _min == minimum && _max == maximum &&
            ReferenceEquals(EditValue, toEdit) && ReferenceEquals(FromEditValue, fromEdit);
        int a = toEdit(minimum), b = toEdit(maximum);
        ExactValueForm dialog = null;
        bool invalidated = false;
        EventHandler invalidate = (_, _) =>
        {
            if (!TargetValid())
            {
                invalidated = true;
                if (dialog != null) { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); }
            }
        };
        Disposed += invalidate;
        EnabledChanged += invalidate;
        try
        {
            using var created = CreateExactEditor(new ExactValueContext(AccessibleName ?? "Exact value", ExactValueUnit,
                Math.Min(a, b), Math.Max(a, b), toEdit(_value)));
            dialog = created;
            if (invalidated || !TargetValid()) return;
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || invalidated || !TargetValid()) return;
            int next = Math.Clamp(fromEdit(dialog.SelectedValue), _min, _max);
            if (next == _value) return;
            Value = next;
            if (invalidated || !TargetValid()) return;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        finally { Disposed -= invalidate; EnabledChanged -= invalidate; }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
