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
    private readonly string _displayContext;

    /// <summary>Raised after any change. The flag says whether a person moved a control by hand.</summary>
    private readonly Action<MonitorCard, bool> _onChanged;
    private readonly Action<MonitorCard> _onScreenBlankToggle;
    private readonly Action<MonitorCard> _onConfigureScreenBlankHotkey;
    private readonly Action<MonitorCard> _onFeaturesRequested;

    private readonly Action<string> _report;
    private readonly Func<Monitor, bool> _applyGamma;

    private readonly Slider _brightness;
    private readonly Slider _contrast;
    private readonly Slider _warmth;
    private readonly Label _nits;
    private readonly Label _reading;
    private readonly Label _contrastReading;
    private readonly System.Windows.Forms.Timer _observationTimer = new() { Interval = 180 };
    private readonly Label _featureNote;
    private readonly TableLayoutPanel _grid;
    private readonly Panel _extras;
    private readonly LinkLabel _disclosure;
    private readonly LinkLabel _screenBlankShortcut;
    private readonly FlatButton _screenBlankButton;
    private readonly AdjustmentNotice _adjustmentNotice;

    private bool _suppress;
    private bool _highlighted;
    private bool _extrasBuilt;
    private bool _featuresLoading;
    private Bitmap _surface;
    private (Size Size, int State, int Dpi, int Divider, Padding Padding) _surfaceKey;

    // One ToolTip for the whole card, disposed with it. A `new ToolTip()` per
    // button looks harmless and is not: cards are rebuilt on every display
    // change and every Refresh, so each rebuild stranded a handful of native
    // tooltip windows that nothing ever released.
    private readonly ToolTip _tips = new();
    private readonly Dictionary<string, FlatButton> _presetButtons = new(StringComparer.OrdinalIgnoreCase);

    // One reusable timer for the highlight pulse, for the same reason - and so
    // a second click cannot leave an earlier timer running against a card that
    // is being disposed.
    private System.Windows.Forms.Timer _highlightTimer;

    public MonitorCard(Monitor m, AppSettings settings, DdcWorker worker,
                       Action<MonitorCard, bool> onChanged, Action<string> report,
                       Action<MonitorCard> onScreenBlankToggle,
                       Action<MonitorCard> onConfigureScreenBlankHotkey,
                       Action<MonitorCard> onFeaturesRequested, Action onConnectionHelp = null,
                       Action<MonitorCard> onProfileRequested = null, Func<Monitor, bool> applyGamma = null)
    {
        Monitor = m;
        _settings = settings;
        _worker = worker;
        _onChanged = onChanged ?? ((_, _) => { });
        _report = report ?? (_ => { });
        _applyGamma = applyGamma ?? DisplayGamma.Apply;
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
        _displayContext = string.IsNullOrWhiteSpace(m.PositionLabel) ? m.DisplayName : $"{m.DisplayName} ({m.PositionLabel})";
        _adjustmentNotice = new AdjustmentNotice(_displayContext, onConnectionHelp != null);
        _adjustmentNotice.ConnectionHelpRequested += () => onConnectionHelp?.Invoke();
        _grid.Controls.Add(_adjustmentNotice, 0, 1);
        _grid.SetColumnSpan(_adjustmentNotice, 2);
        int row = 2;

        _brightness = MakeSlider(m.SupportsBrightness, m.BrightnessMin, m.BrightnessMax, m.Brightness);
        _brightness.Name = "brightness";
        if (m.SupportsBrightness)
        {
            _brightness.Principal = true;
            _brightness.Font = Theme.PrincipalValue;
            _brightness.Height = 76;
        }
        _brightness.Format = v => $"{Presets.RawToPercent(Monitor, v):0}%";
        _brightness.AccessibleName = $"{_displayContext}: Brightness (hardware units)";
        _brightness.ExactValueUnit = "hardware units";
        _brightness.AccessibleValueFormat = v => $"{v} hardware units; displayed {Presets.RawToPercent(Monitor, v):0}% brightness";
        _brightness.AccessibleDescription = "Range and exact entry use hardware units; the visual readout is a mapped percentage. Arrows change one unit, Shift changes ten. Press F2 or Enter for exact entry.";
        _tips.SetToolTip(_brightness, "Click the number or press F2 to enter an exact hardware value. Arrows: one unit; Shift: ten units.");
        _brightness.ValueChanged += (_, _) => OnBrightness();
        AddRow(row++, "Brightness", _brightness, m.SupportsBrightness ? null : "unavailable");

        _reading = new Label
        {
            Name = "adjustment-reading", AutoSize = true, Font = Theme.Small,
            ForeColor = Theme.InkMuted, BackColor = Color.Transparent,
            Margin = new Padding(2, 0, 0, 4), UseMnemonic = false,
        };
        _grid.Controls.Add(_reading, 1, row++);
        _observationTimer.Tick += (_, _) => UpdateObservation();
        _observationTimer.Start();

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
        if (onProfileRequested != null)
        {
            var edit = new LinkLabel
            {
                Text = "Edit brightness estimate…", AutoSize = true, LinkColor = Theme.Info,
                Name = "brightness-profile",
                ActiveLinkColor = Theme.AmberLight, VisitedLinkColor = Theme.Info,
                Margin = new Padding(0, 0, 0, 8), AccessibleName = $"{_displayContext}: Edit brightness estimate profile",
            };
            edit.LinkClicked += (_, _) => onProfileRequested(this);
            _grid.Controls.Add(edit, 1, row++);
        }

        _contrast = MakeSlider(m.SupportsContrast, m.ContrastMin, m.ContrastMax, m.Contrast);
        _contrast.Neutral = true;
        _contrast.Name = "contrast";
        _contrast.AccessibleName = $"{_displayContext}: Contrast (hardware units)";
        _contrast.ExactValueUnit = "hardware units";
        _contrast.AccessibleValueFormat = v => $"{v} hardware units";
        _contrast.AccessibleDescription = "The readout, range and exact entry use hardware units. Arrows change one unit, Shift changes ten. Press F2 or Enter for exact entry.";
        _tips.SetToolTip(_contrast, "Click the number or press F2 to enter an exact hardware value. Arrows: one unit; Shift: ten units.");
        _contrast.ValueChanged += (_, _) => OnContrast();
        AddRow(row++, "Contrast", _contrast, m.SupportsContrast ? null : "unavailable");
        _contrastReading = new Label
        {
            Name = "contrast-reading", AutoSize = true, Font = Theme.Small,
            ForeColor = Theme.InkMuted, BackColor = Color.Transparent, Visible = false,
            Margin = new Padding(2, 0, 0, 4), UseMnemonic = false,
        };
        _grid.Controls.Add(_contrastReading, 1, row++);


        if ((!m.SupportsBrightness || !m.SupportsContrast) && onConnectionHelp != null)
        {
            var help = new LinkLabel
            {
                Text = "Why are controls unavailable?", AutoSize = true,
                AccessibleName = $"{_displayContext}: Why are controls unavailable?",
                Name = "connection-help",
                LinkColor = Theme.Info, ActiveLinkColor = Theme.AmberLight,
                VisitedLinkColor = Theme.Info, Font = Theme.Small,
                BackColor = Color.Transparent, Margin = new Padding(2, 0, 0, 8),
            };
            help.LinkClicked += (_, _) => onConnectionHelp();
            _grid.Controls.Add(help, 1, row++);
            _tips.SetToolTip(help, DisplaySupport.Describe(m).Guidance);
        }

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
        _warmth.Name = "warmth";
        if (!m.SupportsBrightness)
        {
            _warmth.Principal = true;
            _warmth.Font = Theme.PrincipalValue;
            _warmth.Height = 76;
        }
        _warmth.AccessibleName = $"{_displayContext}: Colour temperature (kelvin)";
        _warmth.ExactValueUnit = "kelvin";
        _warmth.AccessibleValueFormat = v => v == 0 ? "6500 kelvin; neutral colour requested" : $"{KelvinFromWarmth(v)} kelvin requested";
        _warmth.AccessibleDescription = "Range and exact entry use kelvin. Right or Up requests warmer colour, 50 kelvin lower; Left or Down requests cooler colour, 50 kelvin higher. Shift changes 500 kelvin. Home requests neutral 6500 kelvin; End requests the warmest 3000 kelvin. Press F2 or Enter for exact entry. Values are requests, not colour measurements.";
        _warmth.SmallChange = 50;
        _warmth.EditValue = KelvinFromWarmth;
        _warmth.FromEditValue = WarmthFromKelvin;
        _tips.SetToolTip(_warmth, "Click the number or press F2 to enter Kelvin. Arrows: 50 K; Shift: 500 K. 6500 K restores neutral colour.");
        _warmth.ValueChanged += (_, _) => OnWarmth();
        AddRow(row++, "Warmth", _warmth, null);
        _grid.Controls.Add(new Label
        {
            Text = "Gamma tint target · not a measured colour preview", AutoSize = true,
            Font = Theme.Small, ForeColor = Theme.InkFaint, Margin = new Padding(2, 0, 0, 4),
        }, 1, row++);

        // Per-monitor blacking is deliberately a Windows overlay plus the
        // lowest supported brightness. It is less power-efficient than DDC
        // hardware-off, but every recovery path remains under app control.
        var blankRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 5, 0, 3),
            BackColor = Color.Transparent,
        };
        _screenBlankButton = new FlatButton("Blank screen")
        {
            Name = "screen-blank",
            Width = 126,
            Enabled = true,
            Margin = new Padding(0, 0, 12, 6),
        };
        _screenBlankButton.Click += (_, _) => _onScreenBlankToggle(this);
        blankRow.Controls.Add(_screenBlankButton);

        _screenBlankShortcut = new LinkLabel
        {
            Name = "screen-shortcut",
            Font = Theme.Small,
            LinkColor = Theme.Info,
            ActiveLinkColor = Theme.AmberLight,
            VisitedLinkColor = Theme.Info,
            LinkBehavior = LinkBehavior.NeverUnderline,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Enabled = true,
            Margin = new Padding(0, 8, 0, 6),
            BackColor = Color.Transparent,
        };
        _screenBlankShortcut.LinkClicked += (_, _) => _onConfigureScreenBlankHotkey(this);
        SetScreenBlankShortcutText(_settings?.ScreenBlankHotkeyFor(m.StableKey));
        blankRow.Controls.Add(_screenBlankShortcut);
        blankRow.Controls.Add(new Label
        {
            Text = m.SupportsBrightness ? "min brightness" : "blackout only",
            AutoSize = true, Font = Theme.Small, ForeColor = Theme.InkMuted,
            Margin = new Padding(8, 8, 0, 6), BackColor = Color.Transparent,
        });
        AddRow(row++, "Screen", blankRow, null);
        SetScreenBlankState(false);

        // ---- per-monitor presets ------------------------------------------
        var presets = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent,
        };
        foreach (var level in Presets.Levels)
        {
            string name = level.Name;
            var b = new FlatButton(name) { Name = "preset-" + name,
                AccessibleName = $"{_displayContext}: {name}", Quiet = true, Width = 78, Margin = new Padding(0, 0, 6, 6) };
            b.Click += (_, _) =>
            {
                // Resolve at click time: targets can change while this card lives.
                var target = _settings?.PresetLevel(name) ?? Presets.ByName(name);
                ApplyBrightness(Presets.BrightnessFor(Monitor, target.Nits));
                ApplyWarmth(target.Kelvin);
                _onChanged(this, false);
                _report($"{Monitor.DisplayName}: {name} - estimated {target.Nits}-nit target and {target.Kelvin}K tint requested. Check any card warnings.");
            };
            _presetButtons[name] = b;
            presets.Controls.Add(b);
        }

        // Set apart from the three above, because it is not a fourth level: it
        // is the way back from them.
        var custom = new FlatButton(Presets.CustomName) { Name = "preset-custom",
            AccessibleName = $"{_displayContext}: {Presets.CustomName}", Width = 78, Margin = new Padding(10, 0, 0, 6) };
        custom.Click += (_, _) =>
        {
            if (RestoreCustom())
            {
                _onChanged(this, false);
                _report($"{Monitor.DisplayName}: your own settings requested. Check any card warnings.");
            }
            else
            {
                _report($"{Monitor.DisplayName} has no saved settings of its own yet - " +
                        "move a slider and they are remembered.");
            }
        };
        custom.SetHelp(_tips,
            "Request this monitor's remembered manual levels.\n" +
            "Slider edits replace them; presets keep them.\n" +
            "Changes save in the background; check Settings for save status.");
        presets.Controls.Add(custom);
        SetPresetHelp(_settings?.PresetLevels ?? Presets.Levels);

        _grid.Controls.Add(presets, 1, row++);

        // ---- extras, collapsed --------------------------------------------
        _disclosure = new LinkLabel
        {
            Text = m.HasPhysicalHandle ? "Show this monitor's other controls" : "",
            Name = "other-controls",
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
        SetDisclosureText(_disclosure.Text);
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
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.Transparent,
        };

        host.Controls.Add(new Label
        {
            Text = m.DisplayName,
            UseMnemonic = false,
            Font = Theme.H1,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Margin = new Padding(0, 1, 10, 0),
            BackColor = Color.Transparent,
        });

        if (!string.IsNullOrEmpty(m.SizeLabel))
            host.Controls.Add(Chip(m.SizeLabel, Theme.InkMuted));

        if (!string.IsNullOrWhiteSpace(m.PositionLabel))
            host.Controls.Add(Chip(m.PositionLabel, Theme.Info));

        if (m.IsInternalPanel)
            host.Controls.Add(Chip("built-in", Theme.InkMuted));
        else if (!m.SupportsBrightness)
            host.Controls.Add(Chip("brightness unavailable", Theme.Warn));

        host.Controls.Add(Chip(m.SupportsBrightness
            ? m.SupportsContrast ? "brightness · contrast · gamma tint" : "brightness · gamma tint"
            : "gamma tint available", Theme.InkMuted));

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

    /// <summary>
    /// This card requested a brightness: a slider, exact entry, preset, Custom
    /// or the schedule. Lets the window tell a person's choice from its own
    /// temporary levels, such as Night saver's dim.
    /// </summary>
    public event Action<MonitorCard> BrightnessRequested;

    private void OnBrightness()
    {
        if (_suppress || !Monitor.SupportsBrightness) return;
        _worker.Set(Monitor, DdcWorker.Feature.Brightness, _brightness.Value);
        BrightnessRequested?.Invoke(this);
        RefreshLabels();
        ManualEdit();
    }

    private void OnContrast()
    {
        if (_suppress || !Monitor.SupportsContrast) return;
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
        if (ApplyGamma(KelvinFromWarmth(_warmth.Value))) ManualEdit();
    }

    /// <summary>
    /// Composed onto the baseline this display owns, so 6500K restores whatever
    /// ICC or colorimeter LUT was already loaded instead of flattening it to
    /// identity - and onto the baseline rather than onto the ramp that happens
    /// to be loaded, which is what stops warmth stacking on warmth.
    /// </summary>
    private bool ApplyGamma(int kelvin)
    {
        int previous = Monitor.Kelvin;
        bool accepted = false;
        Monitor.Kelvin = kelvin;
        try { accepted = _applyGamma(Monitor); }
        finally
        {
            // A refused request must not become saved warmth or Custom. Restore
            // the readout as well, including when the driver wrapper throws.
            if (!accepted) Monitor.Kelvin = previous;
            _warmth.SetValueSilently(WarmthFromKelvin(Monitor.Kelvin));
        }
        if (!accepted)
        {
            ShowWarmthFailure();
            _report($"{Monitor.DisplayName}: the graphics driver refused the colour change.");
        }
        return accepted;
    }

    public void ApplyBrightness(int raw)
    {
        if (!Monitor.SupportsBrightness || _settings.ScreenBlankActiveFor(Monitor.StableKey)) return;
        int v = Math.Clamp(raw, _brightness.Minimum, _brightness.Maximum);
        _suppress = true;
        _brightness.SetValueSilently(v);
        _suppress = false;
        _worker.Set(Monitor, DdcWorker.Feature.Brightness, v);
        BrightnessRequested?.Invoke(this);
        RefreshLabels();
    }

    public void ApplyContrast(int raw)
    {
        if (!Monitor.SupportsContrast) return;
        int v = Math.Clamp(raw, _contrast.Minimum, _contrast.Maximum);
        _suppress = true;
        _contrast.SetValueSilently(v);
        _suppress = false;
        _worker.Set(Monitor, DdcWorker.Feature.Contrast, v);
    }

    public void ApplyWarmth(int kelvin)
    {
        int k = Math.Clamp(kelvin, GammaControl.MinKelvin, GammaControl.NeutralKelvin);
        ApplyGamma(k);
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

    public void ShowWriteFailure(DdcWorker.Feature feature)
    {
        var control = feature switch
        {
            DdcWorker.Feature.Brightness => AdjustmentControl.Brightness,
            DdcWorker.Feature.Contrast => AdjustmentControl.Contrast,
            _ => AdjustmentControl.ExtraControls,
        };
        if (feature is DdcWorker.Feature.Brightness or DdcWorker.Feature.Contrast)
            SyncFromMonitor();
        _adjustmentNotice.RecordFailure(control);
    }

    public void ShowWarmthFailure() => _adjustmentNotice.RecordFailure(AdjustmentControl.Warmth);

    private void RefreshLabels()
    {
        UpdateObservation();
        if (Monitor.SupportsBrightness)
        {
            var estimate = Presets.EstimateFor(Monitor, Monitor.Brightness);
            _nits.Text = estimate.DisplayText;
            _nits.AccessibleDescription = estimate.Explanation;
            _tips.SetToolTip(_nits, estimate.Explanation);
        }
        else
        {
            _nits.Text = "";
            _nits.AccessibleDescription = "";
            _tips.SetToolTip(_nits, null);
        }
    }

    private void UpdateObservation()
    {
        if (_reading == null || IsDisposed) return;
        if (_contrastReading != null && Monitor.SupportsContrast)
        {
            var contrast = _worker.Observe(Monitor, DdcWorker.Feature.Contrast);
            _contrastReading.Visible = contrast.Kind != DdcWorker.ObservationKind.Observed;
            _contrastReading.Text = $"Contrast requested {contrast.Requested} · {contrast.Kind.ToString().ToLowerInvariant()} · last observed {contrast.LastObserved?.ToString() ?? "unknown"} hardware units";
            _contrastReading.AccessibleName = $"{_displayContext}: {_contrastReading.Text}";
        }
        if (!Monitor.SupportsBrightness)
        {
            _reading.Text = "No brightness interface detected. Warmth remains available via GPU gamma.";
            return;
        }
        var state = _worker.Observe(Monitor, DdcWorker.Feature.Brightness);
        string observed = state.LastObserved is int raw ? $"{Presets.RawToPercent(Monitor, raw):0}%" : "unknown";
        string requested = $"{Presets.RawToPercent(Monitor, state.Requested):0}%";
        _reading.Text = state.Kind switch
        {
            DdcWorker.ObservationKind.Pending => $"Requested {requested} · waiting for readback · last observed {observed}",
            DdcWorker.ObservationKind.Confirmed => $"Read back {observed} · reported by display, not measured light",
            DdcWorker.ObservationKind.Unconfirmed => $"Requested {requested} · unconfirmed · last observed {observed}",
            DdcWorker.ObservationKind.Failed => $"Request {requested} failed or differed · last observed {observed}",
            _ => $"Last observed {observed} · exact entry: click value or F2",
        };
        _reading.AccessibleName = $"{_displayContext}: {_reading.Text}";
    }

    // ---------------------------------------------------------------- extras

    internal void SetPresetHelp(IEnumerable<Presets.Level> levels)
    {
        foreach (var level in levels)
            if (_presetButtons.TryGetValue(level.Name, out var button))
                button.SetHelp(_tips, DeskCommandBar.PresetHelp(level, "on this monitor only"));
    }

    public void SetScreenBlankShortcutText(string shortcut)
    {
        if (_screenBlankShortcut == null) return;
        _screenBlankShortcut.Text = string.IsNullOrWhiteSpace(shortcut)
            ? "Set shortcut"
            : shortcut.Trim();
        _screenBlankShortcut.Links.Clear();
        _screenBlankShortcut.LinkArea = new LinkArea(0, _screenBlankShortcut.Text.Length);
        _screenBlankShortcut.AccessibleName = $"{_displayContext}: Configure blank-screen shortcut";
        string help = string.IsNullOrWhiteSpace(shortcut)
            ? "No shortcut assigned. Choose a blank-screen shortcut for this display."
            : $"Saved shortcut: {shortcut.Trim()}. Change or clear the blank-screen shortcut for this display.";
        _screenBlankShortcut.AccessibleDescription = help;
        _tips.SetToolTip(_screenBlankShortcut, help);
    }

    public void SetScreenBlankState(bool blanked)
    {
        if (_screenBlankButton == null || _screenBlankShortcut == null) return;
        _screenBlankButton.Text = blanked ? "Restore screen" : "Blank screen";
        _screenBlankButton.AccessibleName = $"{_displayContext}: {_screenBlankButton.Text}";
        _screenBlankButton.Enabled = true;
        _screenBlankShortcut.Enabled = true;
        _brightness.Enabled = !blanked && Monitor.SupportsBrightness;
        _screenBlankButton.SetHelp(_tips, blanked
            ? Monitor.SupportsBrightness
                ? "Close the blackout and retry any saved brightness recovery. Check the status message if recovery cannot be confirmed."
                : "Close the blackout. Hardware brightness control is unavailable; any pending brightness recovery needs working monitor controls."
            : Monitor.SupportsBrightness
                ? "Temporarily cover this display in black and use its lowest supported brightness. Click it to restore."
                : "Temporarily cover this display in black. Hardware brightness control is unavailable. Click it to restore.");
    }

    public void BeginFeatureLoad()
    {
        if (_featuresLoading || Monitor.FeaturesLoaded) return;
        _featuresLoading = true;
        _disclosure.Enabled = false;
        SetDisclosureText("Reading this monitor's other controls...");
    }

    /// <summary>Called once the background capability probe finishes for this monitor.</summary>
    public void PopulateFeatures(bool expand = false)
    {
        if (IsDisposed || _grid.IsDisposed) return;

        _featuresLoading = false;

        int n = Monitor.Features.Count;
        if (n == 0)
        {
            SetDisclosureText(Monitor.HasPhysicalHandle
                ? "This monitor advertises no other adjustable controls."
                : "");
            _disclosure.Enabled = false;
            _disclosure.LinkColor = Theme.InkFaint;
            return;
        }

        _disclosure.Enabled = true;
        _disclosure.Links.Clear();
        SetDisclosureText($"Show {n} more control{(n == 1 ? "" : "s")} on this monitor");
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
        SetDisclosureText(_extras.Visible
            ? "Hide the other controls"
            : $"Show {n} more control{(n == 1 ? "" : "s")} on this monitor");
        _disclosure.LinkArea = new LinkArea(0, _disclosure.Text.Length);
    }

    private void SetDisclosureText(string text)
    {
        _disclosure.Text = text;
        _disclosure.AccessibleName = string.IsNullOrEmpty(text) ? "" : $"{_displayContext}: {text}";
    }

    // ------------------------------------------------------------- highlight

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (_grid == null) return base.GetPreferredSize(proposedSize);
        // Panel's unconstrained preferred width makes percentage-column flows
        // wrap during measurement even when they fit at the pinned card width.
        int width = MaximumSize.Width > 0 ? MaximumSize.Width
            : Math.Max(MinimumSize.Width, proposedSize.Width > 0 ? proposedSize.Width : Width);
        int contentWidth = Math.Max(1, width - Padding.Horizontal);
        return new Size(width, _grid.GetPreferredSize(new Size(contentWidth, 0)).Height + Padding.Vertical);
    }

    /// <summary>Width and content signature of the last complete constraint pass.</summary>
    private int _laidOutWidth = -1;
    private int _laidOutContent;

    /// <summary>
    /// What a constraint pass depends on besides width: which controls exist,
    /// which are visible and how long their text is. Walking ~60 controls costs
    /// microseconds; the pass it can skip costs tens of milliseconds.
    /// </summary>
    private int ContentSignature()
    {
        var hash = new HashCode();
        void Add(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                hash.Add(child.Visible);
                hash.Add(child.Text?.Length ?? 0);
                Add(child);
            }
        }
        Add(_grid);
        return hash.ToHashCode();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        _laidOutWidth = -1;
        base.OnFontChanged(e);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        _laidOutWidth = -1;
        base.OnDpiChangedAfterParent(e);
    }

    /// <summary>Set the card's width and hold it there against AutoSize.</summary>
    public void SetWidth(int width)
    {
        int w = Math.Max(320, width);
        // A full pass measures every row and costs tens of milliseconds per
        // card. Restoring a window raises several resizes at an unchanged
        // width; repeating the pass for each is what made restoring stall.
        int content = ContentSignature();
        if (w == _laidOutWidth && Width == w && content == _laidOutContent) return;
        SuspendLayout();
        _grid.SuspendLayout();
        try
        {
            MinimumSize = new Size(w, 0);
            MaximumSize = new Size(w, 0);
            Width = w;
            // AutoSize flows otherwise measure one unbounded line and can push
            // their children past the fixed card edge. Constrain before measuring
            // height, including labels whose preferred width can exceed a cell.
            int gridWidth = ClientSize.Width - Padding.Horizontal;
            // Cards are created after asynchronous discovery, potentially on a
            // scaled monitor. Their fonts can be scaled before the fixed column.
            _grid.ColumnStyles[0].Width = Math.Max(LogicalToDeviceUnits(96),
                _grid.Controls.Cast<Control>().Where(c => _grid.GetColumn(c) == 0 && _grid.GetColumnSpan(c) == 1)
                    .Select(c => TextRenderer.MeasureText(c.Text, c.Font).Width + c.Margin.Horizontal).DefaultIfEmpty(0).Max());
            foreach (Control control in _grid.Controls)
            {
                int available = gridWidth - (_grid.GetColumnSpan(control) == 2
                    ? 0 : (int)_grid.ColumnStyles[0].Width) - control.Margin.Horizontal;
                if (_grid.GetColumn(control) == 0 && _grid.GetColumnSpan(control) == 1) continue;
                if (control is FlowLayoutPanel flow)
                {
                    flow.SuspendLayout();
                    try
                    {
                        flow.MaximumSize = new Size(Math.Max(1, available), 0);
                        flow.Width = Math.Max(1, available);
                        foreach (Control child in flow.Controls)
                            if (child is Label)
                                child.MaximumSize = new Size(Math.Max(1, available - child.Margin.Horizontal), 0);
                    }
                    finally { flow.ResumeLayout(true); }
                }
                else if (control is Label)
                    control.MaximumSize = new Size(Math.Max(1, available), 0);
            }
            _laidOutWidth = w;
            _laidOutContent = content;
        }
        finally
        {
            _grid.ResumeLayout(true);
            ResumeLayout(true);
        }
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
            _observationTimer.Dispose();
            _highlightTimer?.Stop();
            _highlightTimer?.Dispose();
            _tips.Dispose();
            _surface?.Dispose();
            _surface = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnEnter(EventArgs e) { base.OnEnter(e); Invalidate(); }
    protected override void OnLeave(EventArgs e) { base.OnLeave(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 5) { base.OnPaint(e); return; }
        var header = _grid.GetControlFromPosition(0, 0);
        int dividerY = header == null ? -1 : _grid.Top + header.Bottom + LogicalToDeviceUnits(3);
        var key = (Size, (_highlighted ? 2 : 0) | (ContainsFocus ? 1 : 0) | (Theme.HighContrast ? 4 : 0), DeviceDpi, dividerY, Padding);
        // Transparent children repaint the parent. Cache one current surface per
        // card so they reuse a blit rather than rebuilding paths and gradients.
        if (_surface == null || _surfaceKey != key)
        {
            _surface?.Dispose();
            _surface = new Bitmap(Width, Height);
            using var graphics = Graphics.FromImage(_surface);
            PaintSurface(graphics, dividerY);
            _surfaceKey = key;
        }
        e.Graphics.DrawImageUnscaled(_surface, Point.Empty);
        base.OnPaint(e);
    }

    private void PaintSurface(Graphics g, int dividerY)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Base);

        int radius = LogicalToDeviceUnits(Theme.Radius);
        var r = new Rectangle(1, 1, Width - 3, Height - 4);
        var shadow = r;
        shadow.Offset(0, LogicalToDeviceUnits(2));
        Theme.FillRound(g, shadow, radius, Theme.Shadow);
        Theme.FillSurface(g, r, radius, Theme.CardTop, Theme.Card);
        Theme.SurfaceRim(g, r, radius);
        bool selected = _highlighted || ContainsFocus;
        Theme.StrokeRound(g, r, radius,
            selected ? _highlighted ? Theme.AmberLight : Theme.Amber : Color.FromArgb(160, Theme.Line),
            selected ? 1.5f : 1f);
        if (dividerY >= 0)
        {
            using var divider = new Pen(Color.FromArgb(100, Theme.Line));
            g.DrawLine(divider, Padding.Left, dividerY, Width - Padding.Right, dividerY);
        }
        if (selected)
        {
            int inset = LogicalToDeviceUnits(12);
            Theme.FillRound(g, new Rectangle(LogicalToDeviceUnits(4), inset,
                LogicalToDeviceUnits(3), Math.Max(1, Height - inset * 2)), LogicalToDeviceUnits(1), Theme.Amber);
        }
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
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = text;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Small;
        // Purely a label. Control.TabStop defaults to true, so without this the
        // Tab order stops on something with no focus cue and no key handling.
        TabStop = false;
        UpdateChipSize();
        Margin = new Padding(0, 2, 6, 0);
    }

    private string LabelText => !Theme.HighContrast && _color == Theme.Warn ? "! " + Text : Text;

    private void UpdateChipSize()
    {
        var text = TextRenderer.MeasureText(LabelText, Font);
        Size = new Size(text.Width + LogicalToDeviceUnits(16),
            Math.Max(LogicalToDeviceUnits(20), text.Height + LogicalToDeviceUnits(4)));
    }

    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateChipSize(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UpdateChipSize(); }
    protected override void RescaleConstantsForDpi(int oldDpi, int newDpi)
    {
        base.RescaleConstantsForDpi(oldDpi, newDpi);
        UpdateChipSize();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, Height / 2, Color.FromArgb(38, _color));
        Theme.StrokeRound(g, r, Height / 2, Color.FromArgb(60, _color));
        TextRenderer.DrawText(g, LabelText, Font, r, Theme.HighContrast ? SystemColors.WindowText : _color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>A flat button that actually honours its colours, unlike the stock one.</summary>
