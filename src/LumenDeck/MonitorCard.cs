using System.Drawing.Drawing2D;

namespace LumenDeck;

/// <summary>
/// One monitor, as a card.
///
/// Replaces the old flat grey box. Three things drove the redesign:
///
///  - The three controls people actually use every day (brightness, contrast,
///    warmth) were buried in the same visual weight as a dozen discovered
///    extras. The extras now live behind a disclosure, so the default view is
///    calm and the card is scannable.
///  - Nothing tied a card to a physical screen. The header carries the position
///    and the card highlights when its rectangle is picked on the layout map.
///  - Every value needed a separate label to read. The slider carries its own.
/// </summary>
internal sealed class MonitorCard : Panel
{
    public Monitor Monitor { get; }

    private readonly DdcWorker _worker;
    private readonly AppSettings _settings;

    /// <summary>Raised after any change. The flag says whether a person moved a control by hand.</summary>
    private readonly Action<MonitorCard, bool> _onChanged;
    private readonly Action<MonitorCard> _onScreenBlankToggle;
    private readonly Action<MonitorCard> _onConfigureScreenBlankHotkey;
    private readonly Action<MonitorCard> _onFeaturesRequested;

    private readonly Action<string> _report;

    private readonly Slider _brightness;
    private readonly Slider _contrast;
    private readonly Slider _warmth;
    private readonly Label _nits;
    private readonly Label _featureNote;
    private readonly TableLayoutPanel _grid;
    private readonly Panel _extras;
    private readonly LinkLabel _disclosure;
    private readonly LinkLabel _screenBlankShortcut;
    private readonly FlatButton _screenBlankButton;

    private bool _suppress;
    private bool _highlighted;
    private bool _extrasBuilt;
    private bool _featuresLoading;

    // One ToolTip for the whole card, disposed with it. A `new ToolTip()` per
    // button looks harmless and is not: cards are rebuilt on every display
    // change and every Refresh, so each rebuild stranded a handful of native
    // tooltip windows that nothing ever released.
    private readonly ToolTip _tips = new();

    // One reusable timer for the highlight pulse, for the same reason - and so
    // a second click cannot leave an earlier timer running against a card that
    // is being disposed.
    private System.Windows.Forms.Timer _highlightTimer;

    public MonitorCard(Monitor m, AppSettings settings, DdcWorker worker,
                       Action<MonitorCard, bool> onChanged, Action<string> report,
                       Action<MonitorCard> onScreenBlankToggle,
                       Action<MonitorCard> onConfigureScreenBlankHotkey,
                       Action<MonitorCard> onFeaturesRequested)
    {
        Monitor = m;
        _settings = settings;
        _worker = worker;
        _onChanged = onChanged ?? ((_, _) => { });
        _report = report ?? (_ => { });
        _onScreenBlankToggle = onScreenBlankToggle ?? (_ => { });
        _onConfigureScreenBlankHotkey = onConfigureScreenBlankHotkey ?? (_ => { });
        _onFeaturesRequested = onFeaturesRequested ?? (_ => { });

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // AutoSize gives the card its height from its content. Width must be
        // pinned separately: with GrowAndShrink the card would otherwise shrink
        // to the preferred width of a TableLayoutPanel whose only sized column
        // is a percentage, which is zero - the cards collapsed into 30px strips.
        // MinimumSize is what stops that, and the owner sets it with the width.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Theme.Base;
        Padding = new Padding(Theme.CardPad, 14, Theme.CardPad, 14);
        Margin = new Padding(0, 0, 0, Theme.Gap);

        _grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            BackColor = Color.Transparent,
        };
        // Leave enough room for "Brightness" at 100%-plus Windows DPI.
        // TableLayoutPanel counts the label's horizontal margins inside this
        // column, so the previous 84px could wrap the final character.
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // ---- header ------------------------------------------------------
        var header = BuildHeader(m);
        _grid.Controls.Add(header, 0, 0);
        _grid.SetColumnSpan(header, 2);

        // ---- the three everyday controls ---------------------------------
        int row = 1;

        _brightness = MakeSlider(m.SupportsBrightness, m.BrightnessMin, m.BrightnessMax, m.Brightness);
        _brightness.Format = v => $"{Presets.RawToPercent(Monitor, v):0}%";
        _brightness.ValueChanged += (_, _) => OnBrightness();
        AddRow(row++, "Brightness", _brightness, m.SupportsBrightness ? null : "no DDC");

        _nits = new Label
        {
            Font = Theme.Small,
            ForeColor = Theme.InkFaint,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 0, 0, 6),
            BackColor = Color.Transparent,
        };
        _grid.Controls.Add(_nits, 1, row++);

        _contrast = MakeSlider(m.SupportsContrast, m.ContrastMin, m.ContrastMax, m.Contrast);
        _contrast.ValueChanged += (_, _) => OnContrast();
        AddRow(row++, "Contrast", _contrast, m.SupportsContrast ? null : "no DDC");

        // Always enabled: warmth is a GPU gamma ramp, so it works even on a
        // monitor that answers no DDC at all.
        //
        // The slider runs in WARMTH, not in kelvin. Driving it directly by
        // kelvin put "off" (6500 K) at the maximum, so a screen with no warmth
        // applied showed a completely full amber bar - it read as warmth turned
        // all the way up. Amount-of-warmth runs the right way: empty is off,
        // full is as warm as it goes. The readout still shows kelvin, because
        // that is the number people compare against f.lux and Night Light.
        _warmth = MakeSlider(true, 0, GammaControl.NeutralKelvin - GammaControl.MinKelvin,
                             WarmthFromKelvin(m.Kelvin));
        _warmth.Format = v => v <= 0 ? "off" : KelvinFromWarmth(v) + "K";
        _warmth.ValueChanged += (_, _) => OnWarmth();
        AddRow(row++, "Warmth", _warmth, null);

        // Per-monitor blacking is deliberately a Windows overlay plus the
        // lowest supported brightness. It is less power-efficient than DDC
        // hardware-off, but every recovery path remains under app control.
        var blankRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 5, 0, 3),
            BackColor = Color.Transparent,
        };
        _screenBlankButton = new FlatButton("Blank screen")
        {
            Width = 126,
            Enabled = true,
            Margin = new Padding(0, 0, 12, 0),
        };
        _screenBlankButton.Click += (_, _) => _onScreenBlankToggle(this);
        blankRow.Controls.Add(_screenBlankButton);

        _screenBlankShortcut = new LinkLabel
        {
            Font = Theme.Small,
            LinkColor = Theme.Info,
            ActiveLinkColor = Theme.AmberLight,
            VisitedLinkColor = Theme.Info,
            LinkBehavior = LinkBehavior.NeverUnderline,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Enabled = true,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent,
        };
        _screenBlankShortcut.LinkClicked += (_, _) => _onConfigureScreenBlankHotkey(this);
        SetScreenBlankShortcutText(_settings?.ScreenBlankHotkeyFor(m.StableKey));
        blankRow.Controls.Add(_screenBlankShortcut);
        AddRow(row++, "Screen", blankRow, m.SupportsBrightness ? "min brightness" : "blackout only");
        SetScreenBlankState(false);

        // ---- per-monitor presets ------------------------------------------
        var presets = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent,
        };
        foreach (var level in Presets.Levels)
        {
            var captured = level;
            var b = new FlatButton(captured.Name) { Width = 78, Margin = new Padding(0, 0, 6, 0) };
            b.Click += (_, _) =>
            {
                ApplyBrightness(Presets.BrightnessFor(Monitor, captured.Nits));
                ApplyWarmth(captured.Kelvin);
                _onChanged(this, false);
                _report($"{Monitor.DisplayName}: {captured.Name} - about {captured.Nits} nits, {captured.Kelvin}K.");
            };
            _tips.SetToolTip(b, $"{captured.Description}\nThis monitor only.");
            presets.Controls.Add(b);
        }

        // Set apart from the three above, because it is not a fourth level: it
        // is the way back from them.
        var custom = new FlatButton(Presets.CustomName) { Width = 78, Margin = new Padding(10, 0, 0, 0) };
        custom.Click += (_, _) =>
        {
            if (RestoreCustom())
            {
                _onChanged(this, false);
                _report($"{Monitor.DisplayName}: back to your own settings.");
            }
            else
            {
                _report($"{Monitor.DisplayName} has no saved settings of its own yet - " +
                        "move a slider and they are remembered.");
            }
        };
        _tips.SetToolTip(custom,
            "Your own brightness, contrast and warmth for this monitor.\n" +
            "Saved whenever you move one of its sliders, and never touched by a preset.");
        presets.Controls.Add(custom);

        _grid.Controls.Add(presets, 1, row++);

        // ---- extras, collapsed --------------------------------------------
        _disclosure = new LinkLabel
        {
            Text = m.HasPhysicalHandle ? "Show this monitor's other controls" : "",
            Font = Theme.Small,
            LinkColor = Theme.Info,
            ActiveLinkColor = Theme.AmberLight,
            VisitedLinkColor = Theme.Info,
            LinkBehavior = LinkBehavior.NeverUnderline,
            ForeColor = Theme.InkFaint,
            AutoSize = true,
            Enabled = m.HasPhysicalHandle,
            Margin = new Padding(2, 10, 0, 0),
            BackColor = Color.Transparent,
        };
        if (m.HasPhysicalHandle)
            _disclosure.LinkArea = new LinkArea(0, _disclosure.Text.Length);
        _disclosure.LinkClicked += (_, _) =>
        {
            if (Monitor.FeaturesLoaded) ToggleExtras();
            else if (!_featuresLoading) _onFeaturesRequested(this);
        };
        _grid.Controls.Add(_disclosure, 1, row++);

        _extras = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Visible = false,
            Margin = new Padding(0, 6, 0, 0),
            BackColor = Color.Transparent,
        };
        _grid.Controls.Add(_extras, 1, row++);

        _featureNote = new Label { Visible = false, AutoSize = true, BackColor = Color.Transparent };

        Controls.Add(_grid);
        RefreshLabels();
    }

    // ------------------------------------------------------------------ build

    private Control BuildHeader(Monitor m)
    {
        var host = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.Transparent,
        };

        host.Controls.Add(new Label
        {
            Text = m.DisplayName,
            Font = Theme.H2,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Margin = new Padding(0, 1, 10, 0),
            BackColor = Color.Transparent,
        });

        if (!string.IsNullOrEmpty(m.SizeLabel))
            host.Controls.Add(Chip(m.SizeLabel, Theme.InkMuted));

        host.Controls.Add(Chip(m.PositionLabel, Theme.Info));

        if (m.IsInternalPanel)
            host.Controls.Add(Chip("built-in", Theme.InkMuted));
        else if (!m.SupportsBrightness)
            host.Controls.Add(Chip("no DDC/CI", Theme.Warn));

        return host;
    }

    private static Control Chip(string text, Color color) => new ChipLabel(text, color);

    private Slider MakeSlider(bool enabled, int min, int max, int value)
    {
        if (max <= min) { min = 0; max = 100; }
        return new Slider
        {
            Minimum = min,
            Maximum = max,
            Enabled = enabled,
            Dock = DockStyle.Fill,
            Height = 30,
            Margin = new Padding(0, 1, 0, 1),
            Value = Math.Clamp(value, min, max),
        };
    }

    private void AddRow(int row, string caption, Control editor, string note)
    {
        _grid.Controls.Add(new Label
        {
            Text = caption,
            Font = Theme.Body,
            ForeColor = editor.Enabled ? Theme.InkMuted : Theme.InkFaint,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 8, 8, 0),
            BackColor = Color.Transparent,
        }, 0, row);

        if (note == null)
        {
            _grid.Controls.Add(editor, 1, row);
            return;
        }

        // A disabled slider on its own reads as a bug. Say why.
        var host = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = Color.Transparent,
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        host.Controls.Add(editor, 0, 0);
        host.Controls.Add(new Label
        {
            Text = note,
            Font = Theme.Small,
            ForeColor = Theme.Warn,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(6, 8, 0, 0),
            BackColor = Color.Transparent,
        }, 1, 0);
        _grid.Controls.Add(host, 1, row);
    }

    // ------------------------------------------------------------- behaviour

    private void OnBrightness()
    {
        if (_suppress || !Monitor.SupportsBrightness) return;
        Monitor.Brightness = _brightness.Value;
        _worker.Set(Monitor, DdcWorker.Feature.Brightness, _brightness.Value);
        RefreshLabels();
        ManualEdit();
    }

    private void OnContrast()
    {
        if (_suppress || !Monitor.SupportsContrast) return;
        Monitor.Contrast = _contrast.Value;
        _worker.Set(Monitor, DdcWorker.Feature.Contrast, _contrast.Value);
        ManualEdit();
    }

    /// <summary>
    /// A person moved a control on this card. That is the only thing that
    /// rewrites their Custom position - the presets deliberately do not, which
    /// is what makes them reversible.
    /// </summary>
    private void ManualEdit()
    {
        _settings?.CaptureCustom(Monitor);
        _onChanged(this, true);
    }

    private static int WarmthFromKelvin(int kelvin) =>
        Math.Clamp(GammaControl.NeutralKelvin - kelvin, 0, GammaControl.NeutralKelvin - GammaControl.MinKelvin);

    private static int KelvinFromWarmth(int warmth) =>
        Math.Clamp(GammaControl.NeutralKelvin - warmth, GammaControl.MinKelvin, GammaControl.NeutralKelvin);

    private void OnWarmth()
    {
        if (_suppress) return;
        Monitor.Kelvin = KelvinFromWarmth(_warmth.Value);
        ApplyGamma();
        ManualEdit();
    }

    /// <summary>
    /// Composed onto the baseline this display owns, so 6500K restores whatever
    /// ICC or colorimeter LUT was already loaded instead of flattening it to
    /// identity - and onto the baseline rather than onto the ramp that happens
    /// to be loaded, which is what stops warmth stacking on warmth.
    /// </summary>
    private void ApplyGamma()
    {
        if (!DisplayGamma.Apply(Monitor))
            _report($"{Monitor.DisplayName}: the graphics driver refused the colour change.");
    }

    public void ApplyBrightness(int raw)
    {
        if (!Monitor.SupportsBrightness) return;
        int v = Math.Clamp(raw, _brightness.Minimum, _brightness.Maximum);
        _suppress = true;
        _brightness.SetValueSilently(v);
        _suppress = false;
        Monitor.Brightness = v;
        _worker.Set(Monitor, DdcWorker.Feature.Brightness, v);
        RefreshLabels();
    }

    public void ApplyContrast(int raw)
    {
        if (!Monitor.SupportsContrast) return;
        int v = Math.Clamp(raw, _contrast.Minimum, _contrast.Maximum);
        _suppress = true;
        _contrast.SetValueSilently(v);
        _suppress = false;
        Monitor.Contrast = v;
        _worker.Set(Monitor, DdcWorker.Feature.Contrast, v);
    }

    public void ApplyWarmth(int kelvin)
    {
        int k = Math.Clamp(kelvin, GammaControl.MinKelvin, GammaControl.NeutralKelvin);
        _suppress = true;
        _warmth.SetValueSilently(WarmthFromKelvin(k));
        _suppress = false;
        Monitor.Kelvin = k;
        ApplyGamma();
    }

    /// <summary>
    /// Put this monitor back to the values its owner chose. False if none were
    /// ever saved, so the caller can say so rather than appear to do nothing.
    /// </summary>
    public bool RestoreCustom()
    {
        var e = _settings?.Find(Monitor.StableKey);
        if (e is not { HasCustom: true }) return false;

        if (e.CustomBrightnessPercent is double bp)
            ApplyBrightness(Presets.PercentToRaw(Monitor, bp));

        if (e.CustomContrastPercent is double cp)
            ApplyContrast(Presets.FromPercent(cp, Monitor.ContrastMin, Monitor.ContrastMax));

        if (e.CustomKelvin is int k)
            ApplyWarmth(k);

        return true;
    }

    /// <summary>Pull live values back off the monitor into the controls.</summary>
    public void SyncFromMonitor()
    {
        _suppress = true;
        if (Monitor.SupportsBrightness) _brightness.SetValueSilently(Monitor.Brightness);
        if (Monitor.SupportsContrast) _contrast.SetValueSilently(Monitor.Contrast);
        _warmth.SetValueSilently(WarmthFromKelvin(Monitor.Kelvin));
        _suppress = false;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (Monitor.SupportsBrightness)
        {
            // Say when the figure is a guess. An unqualified number reads as a
            // measurement, and nothing here was measured with a colorimeter.
            string note = Presets.IsKnown(Monitor) ? "" : ", estimated panel";
            _nits.Text = $"about {Presets.NitsFor(Monitor, Monitor.Brightness)} nits{note}";
        }
        else
        {
            _nits.Text = "";
        }
    }

    // ---------------------------------------------------------------- extras

    public void SetScreenBlankShortcutText(string shortcut)
    {
        if (_screenBlankShortcut == null) return;
        _screenBlankShortcut.Text = string.IsNullOrWhiteSpace(shortcut)
            ? "Set shortcut"
            : shortcut.Trim();
        _screenBlankShortcut.Links.Clear();
        _screenBlankShortcut.LinkArea = new LinkArea(0, _screenBlankShortcut.Text.Length);
    }

    public void SetScreenBlankState(bool blanked)
    {
        if (_screenBlankButton == null || _screenBlankShortcut == null) return;
        _screenBlankButton.Text = blanked ? "Restore screen" : "Blank screen";
        _screenBlankButton.Enabled = true;
        _screenBlankShortcut.Enabled = true;
        _tips.SetToolTip(_screenBlankButton, blanked
            ? "Close the blackout and restore the exact brightness saved before it."
            : "Temporarily cover this display in black and use its lowest supported brightness. Click it to restore.");
    }

    public void BeginFeatureLoad()
    {
        if (_featuresLoading || Monitor.FeaturesLoaded) return;
        _featuresLoading = true;
        _disclosure.Enabled = false;
        _disclosure.Text = "Reading this monitor's other controls...";
    }

    /// <summary>Called once the background capability probe finishes for this monitor.</summary>
    public void PopulateFeatures(bool expand = false)
    {
        if (IsDisposed || _grid.IsDisposed) return;

        _featuresLoading = false;

        int n = Monitor.Features.Count;
        if (n == 0)
        {
            _disclosure.Text = Monitor.HasPhysicalHandle
                ? "This monitor advertises no other adjustable controls."
                : "";
            _disclosure.Enabled = false;
            _disclosure.LinkColor = Theme.InkFaint;
            return;
        }

        _disclosure.Enabled = true;
        _disclosure.Links.Clear();
        _disclosure.Text = $"Show {n} more control{(n == 1 ? "" : "s")} on this monitor";
        _disclosure.LinkColor = Theme.Info;
        _disclosure.LinkArea = new LinkArea(0, _disclosure.Text.Length);

        if (expand) ToggleExtras();
    }

    private void ToggleExtras()
    {
        if (!_extrasBuilt)
        {
            var grid = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            FeatureControls.Build(grid, 0, Monitor, _worker, _report, _tips);
            _extras.Controls.Add(grid);
            _extrasBuilt = true;
        }

        _extras.Visible = !_extras.Visible;
        int n = Monitor.Features.Count;
        _disclosure.Text = _extras.Visible
            ? "Hide the other controls"
            : $"Show {n} more control{(n == 1 ? "" : "s")} on this monitor";
        _disclosure.LinkArea = new LinkArea(0, _disclosure.Text.Length);
    }

    // ------------------------------------------------------------- highlight

    /// <summary>Set the card's width and hold it there against AutoSize.</summary>
    public void SetWidth(int width)
    {
        int w = Math.Max(320, width);
        MinimumSize = new Size(w, 0);
        MaximumSize = new Size(w, 0);
        Width = w;
    }

    public void Highlight()
    {
        _highlighted = true;
        Invalidate();

        _highlightTimer ??= NewHighlightTimer();
        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    private System.Windows.Forms.Timer NewHighlightTimer()
    {
        var t = new System.Windows.Forms.Timer { Interval = 1400 };
        t.Tick += (_, _) =>
        {
            t.Stop();
            _highlighted = false;
            if (!IsDisposed) Invalidate();
        };
        return t;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _highlightTimer?.Stop();
            _highlightTimer?.Dispose();
            _tips.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Base);

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, Theme.Radius, Theme.Card);
        Theme.StrokeRound(g, r, Theme.Radius,
            _highlighted ? Theme.AmberLight : Theme.Line,
            _highlighted ? 2f : 1f);

        base.OnPaint(e);
    }
}

/// <summary>A small rounded tag. Cheaper to read than a sentence.</summary>
internal sealed class ChipLabel : Control
{
    private readonly Color _color;

    public ChipLabel(string text, Color color)
    {
        _color = color;
        Text = text;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Small;
        // Purely a label. Control.TabStop defaults to true, so without this the
        // Tab order stops on something with no focus cue and no key handling.
        TabStop = false;
        var size = TextRenderer.MeasureText(text, Theme.Small);
        Size = new Size(size.Width + 16, 20);
        Margin = new Padding(0, 2, 6, 0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, Height / 2, Color.FromArgb(38, _color));
        TextRenderer.DrawText(g, Text, Font, r, _color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>A flat button that actually honours its colours, unlike the stock one.</summary>
internal sealed class FlatButton : Control
{
    private bool _hover;
    private bool _down;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }

    public FlatButton(string text)
    {
        Text = text;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Body;
        Height = 30;
        Width = 84;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    // A custom Control is not a Button: it gets no Space/Enter handling, no
    // focus rectangle and no accessible role for free. Without these, every
    // preset - global and per-monitor - plus Identify, Refresh and Warmth off
    // was mouse-only, and Narrator announced them as anonymous client areas.
    protected override bool IsInputKey(Keys key) =>
        key is Keys.Space or Keys.Enter || base.IsInputKey(key);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled || (e.KeyCode != Keys.Space && e.KeyCode != Keys.Enter)) return;
        _down = true;
        Invalidate();
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (!Enabled || (e.KeyCode != Keys.Space && e.KeyCode != Keys.Enter)) return;
        _down = false;
        Invalidate();
        OnClick(EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); _down = false; Invalidate(); }

    protected override AccessibleObject CreateAccessibilityInstance() =>
        new ButtonAccessibleObject(this);

    /// <summary>Lets assistive tech press the button, not merely describe it.</summary>
    internal void PerformClick() => OnClick(EventArgs.Empty);

    private sealed class ButtonAccessibleObject : ControlAccessibleObject
    {
        private readonly FlatButton _owner;
        public ButtonAccessibleObject(FlatButton owner) : base(owner) => _owner = owner;
        public override AccessibleRole Role => AccessibleRole.PushButton;
        public override string DefaultAction => "Press";
        public override void DoDefaultAction() => _owner.PerformClick();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);

        Color face = Primary
            ? (_down ? Theme.Amber : _hover ? Theme.AmberLight : Theme.Amber)
            : (_down ? Theme.Sunken : _hover ? Theme.CardHover : Theme.Card);
        Color text = Primary ? Color.FromArgb(28, 20, 8) : (Enabled ? Theme.Ink : Theme.InkFaint);
        Color edge = Primary ? Color.FromArgb(0, 0, 0, 0) : (_hover ? Theme.AmberDim : Theme.Line);

        Theme.FillRound(g, r, 7, Enabled ? face : Theme.Card);
        if (edge.A > 0) Theme.StrokeRound(g, r, 7, edge);
        if (Focused)
        {
            var inner = Rectangle.Inflate(r, -3, -3);
            Theme.StrokeRound(g, inner, 5, Color.FromArgb(190, Theme.AmberLight), 1.5f);
        }
        TextRenderer.DrawText(g, Text, Font, r, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _down = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _down = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _down = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}
