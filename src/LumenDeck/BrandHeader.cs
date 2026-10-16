namespace LumenDeck;

/// <summary>
/// The window's name and version. Painted in one control rather than built from
/// labels: it adds no child windows, so it costs nothing when the window is
/// created, restored or resized.
/// </summary>
internal sealed class BrandHeader : Control
{
    private readonly Icon _icon;
    private readonly string _version;
    private Icon _sizedIcon;
    private Font _smallFont;

    public BrandHeader(Icon icon, string version)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        _icon = icon;
        _version = version;
        Dock = DockStyle.Top;
        Theme.Colors(this, back: () => Theme.Bar);
        Font = Theme.Brand;
        Height = 48;
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = $"LumenDeck version {version}";
    }

    internal string VersionLabel => "v" + _version;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int pad = LogicalToDeviceUnits(16);
        int iconSize = LogicalToDeviceUnits(24);
        int x = pad;
        if (_icon != null)
        {
            if (_sizedIcon?.Width != iconSize)
            {
                _sizedIcon?.Dispose();
                _sizedIcon = new Icon(_icon, iconSize, iconSize);
            }
            g.DrawIcon(_sizedIcon, new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize));
            x += iconSize + LogicalToDeviceUnits(10);
        }
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        var name = TextRenderer.MeasureText(g, "LumenDeck", Font, Size.Empty, flags);
        TextRenderer.DrawText(g, "LumenDeck", Font, new Rectangle(x, 0, name.Width, Height), Theme.Ink, flags);
        x += name.Width + LogicalToDeviceUnits(8);

        // The version sits in a quiet pill so it reads as metadata, not a title.
        var small = _smallFont ?? Theme.Small;
        var text = TextRenderer.MeasureText(g, VersionLabel, small, Size.Empty, flags);
        int chipHeight = text.Height + LogicalToDeviceUnits(4);
        var chip = new Rectangle(x, (Height - chipHeight) / 2, text.Width + LogicalToDeviceUnits(12), chipHeight);
        using (var path = Rounded(chip, chipHeight / 2))
        using (var fill = new SolidBrush(Theme.Card))
        using (var line = new Pen(Theme.Line))
        {
            g.FillPath(fill, path);
            g.DrawPath(line, path);
        }
        TextRenderer.DrawText(g, VersionLabel, small, chip, Theme.InkMuted,
            flags | TextFormatFlags.HorizontalCenter);

        if (Width > LogicalToDeviceUnits(640))
            TextRenderer.DrawText(g, "Your displays, in balance.", small,
                new Rectangle(chip.Right + pad, 0, Math.Max(0, Width - chip.Right - pad * 2), Height),
                Theme.InkMuted, flags | TextFormatFlags.Right);

        using var divider = new Pen(Theme.Line);
        g.DrawLine(divider, 0, Height - 1, Width, Height - 1);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _smallFont?.Dispose();
        _smallFont = new Font(Theme.Small.FontFamily, Theme.Small.Size * Font.Size / Theme.Brand.Size);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _sizedIcon?.Dispose(); _smallFont?.Dispose(); }
        base.Dispose(disposing);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = Math.Max(1, radius * 2);
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
