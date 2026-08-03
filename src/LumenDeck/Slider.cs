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

    /// <summary>Raised while dragging, and on every keyboard or click change.</summary>
    public event EventHandler ValueChanged;

    /// <summary>Formats the readout. Defaults to the raw number.</summary>
    // These controls are constructed in code, never dropped from a designer
    // toolbox, so there is nothing for the designer serializer to emit.
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, string> Format { get; set; } = v => v.ToString();

    public Slider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 30;
        TabStop = true;
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
            int v = Math.Clamp(value, _min, _max);
            if (v == _value) return;
            _value = v;
            Invalidate();
        }
    }

    /// <summary>Set without raising ValueChanged - for syncing from hardware.</summary>
    public void SetValueSilently(int value)
    {
        _value = Math.Clamp(value, _min, _max);
        Invalidate();
    }

    private const int TrackH = 8;
    private const int ThumbR = 8;
    private const int ReadoutW = 52;

    private Rectangle TrackRect
    {
        get
        {
            int y = (Height - TrackH) / 2;
            return new Rectangle(ThumbR, y, Math.Max(1, Width - ReadoutW - ThumbR * 2), TrackH);
        }
    }

    private double Fraction => (_value - _min) / (double)(_max - _min);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var track = TrackRect;
        Theme.FillRound(g, track, TrackH / 2, Theme.Sunken);

        int fillW = (int)Math.Round(track.Width * Fraction);
        if (fillW > 2)
        {
            var fill = new Rectangle(track.X, track.Y, fillW, track.Height);
            if (Enabled)
            {
                using var brush = new LinearGradientBrush(
                    new Rectangle(track.X, track.Y, Math.Max(2, track.Width), track.Height),
                    Theme.Amber, Theme.AmberLight, LinearGradientMode.Horizontal);
                using var path = Theme.Round(fill, TrackH / 2);
                g.FillPath(brush, path);
            }
            else
            {
                Theme.FillRound(g, fill, TrackH / 2, Theme.AmberDim);
            }
        }

        if (Enabled)
        {
            int cx = track.X + fillW;
            int cy = track.Y + track.Height / 2;
            int r = _dragging || _hover ? ThumbR + 1 : ThumbR;
            using var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
            g.FillEllipse(shadow, cx - r, cy - r + 1, r * 2, r * 2);
            using var thumb = new SolidBrush(Theme.AmberLight);
            g.FillEllipse(thumb, cx - r, cy - r, r * 2, r * 2);
            if (Focused)
            {
                using var ring = new Pen(Color.FromArgb(160, 255, 255, 255), 1.5f);
                g.DrawEllipse(ring, cx - r - 2, cy - r - 2, (r + 2) * 2, (r + 2) * 2);
            }
        }

        var readout = new Rectangle(track.Right + ThumbR + 6, 0, ReadoutW - 6, Height);
        TextRenderer.DrawText(g, Format(_value), Theme.Value, readout,
            Enabled ? Theme.Ink : Theme.InkFaint,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }

    private void SetFromX(int x)
    {
        var track = TrackRect;
        double f = (x - track.X) / (double)Math.Max(1, track.Width);
        int v = _min + (int)Math.Round(f * (_max - _min));
        v = Math.Clamp(v, _min, _max);
        if (v == _value) return;
        _value = v;
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        Focus();
        _dragging = true;
        SetFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) SetFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!Enabled) return;
        // A wheel over a slider is the fastest way to nudge a monitor, and it is
        // what people try first.
        int step = Math.Max(1, (_max - _min) / 50);
        int v = Math.Clamp(_value + (e.Delta > 0 ? step : -step), _min, _max);
        if (v == _value) return;
        _value = v;
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys key) =>
        key is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(key);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled) return;

        int step = Math.Max(1, (_max - _min) / 50);
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
        if (v == _value) return;
        _value = v;
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}
