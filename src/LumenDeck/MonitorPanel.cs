namespace LumenDeck;

/// <summary>
/// One monitor's block of controls.
///
/// Laid out with a TableLayoutPanel rather than absolute coordinates. The first
/// version used fixed Locations and it was wrong twice over: the estimated-nits
/// caption landed on top of the brightness slider, and nothing would have
/// survived 125% or 150% display scaling, which is exactly the audience for a
/// monitor utility.
/// </summary>
internal sealed class MonitorPanel : Panel
{
    public Monitor Monitor { get; }

    private readonly DdcWorker _worker;
    private readonly Action _onChanged;
    private readonly Action<string> _report;

    private readonly TrackBar _brightness;
    private readonly TrackBar _contrast;
    private readonly TrackBar _kelvin;
    private readonly Label _brightnessValue;
    private readonly Label _contrastValue;
    private readonly Label _kelvinValue;
    private readonly Label _nits;

    private bool _suppress;

    private static readonly Color Ink = Color.FromArgb(228, 228, 234);
    private static readonly Color InkDim = Color.FromArgb(150, 150, 158);
    private static readonly Color InkOff = Color.FromArgb(112, 112, 120);
    private static readonly Color Accent = Color.FromArgb(140, 175, 235);
    private static readonly Color Warn = Color.FromArgb(200, 150, 95);

    // Shared for the life of the process, deliberately.
    //
    // Assigning a Font to a Control does not transfer ownership: disposing the
    // control leaves the Font to its finalizer. Building ten of them per panel
    // on every rebuild was the whole of the ~0.5 MB per rebuild that the leak
    // test measured - handle counts stayed flat because the finalizers did
    // eventually run, but the managed heap climbed until a gen2 collection.
    // Six shared instances allocate nothing per rebuild and cannot be leaked.
    private static readonly Font FontTitle = new("Segoe UI Semibold", 10.5f);
    private static readonly Font FontBody = new("Segoe UI", 9f);
    private static readonly Font FontSmall = new("Segoe UI", 8.5f);
    private static readonly Font FontTiny = new("Segoe UI", 8f);
    private static readonly Font FontValue = new("Consolas", 10f);

    public MonitorPanel(Monitor m, DdcWorker worker, Action onChanged, Action<string> report)
    {
        Monitor = m;
        _worker = worker;
        _onChanged = onChanged;
        _report = report ?? (_ => { });

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(38, 38, 42);
        Padding = new Padding(12, 10, 12, 12);
        Margin = new Padding(0, 0, 0, 10);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 5,
            BackColor = Color.Transparent,
        };
        // caption | slider (absorbs all spare width) | value
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108f));

        // ---- header, spanning the full width -------------------------------
        var header = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent,
        };
        header.Controls.Add(new Label
        {
            Text = m.FriendlyName,
            Font = FontTitle,
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 0, 10, 0),
        });
        if (!string.IsNullOrEmpty(m.SizeLabel))
        {
            header.Controls.Add(new Label
            {
                Text = m.SizeLabel,
                Font = FontBody,
                ForeColor = InkDim,
                AutoSize = true,
                Margin = new Padding(0, 2, 10, 0),
            });
        }
        header.Controls.Add(new Label
        {
            Text = m.PositionLabel,
            Font = FontBody,
            ForeColor = Accent,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
        });

        grid.Controls.Add(header, 0, 0);
        grid.SetColumnSpan(header, 3);

        // ---- brightness -----------------------------------------------------
        _brightness = MakeSlider(m.SupportsBrightness ? m.BrightnessMin : 0,
                                 m.SupportsBrightness ? m.BrightnessMax : 100,
                                 m.SupportsBrightness ? m.Brightness : 0,
                                 m.SupportsBrightness);
        _brightnessValue = MakeValueLabel();
        AddRow(grid, 1, "Brightness", _brightness, _brightnessValue, m.SupportsBrightness);

        _nits = new Label
        {
            Font = FontTiny,
            ForeColor = InkDim,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(6, 0, 0, 4),
        };
        grid.Controls.Add(_nits, 1, 2);

        // ---- contrast -------------------------------------------------------
        _contrast = MakeSlider(m.SupportsContrast ? m.ContrastMin : 0,
                               m.SupportsContrast ? m.ContrastMax : 100,
                               m.SupportsContrast ? m.Contrast : 0,
                               m.SupportsContrast);
        _contrastValue = MakeValueLabel();
        AddRow(grid, 3, "Contrast", _contrast, _contrastValue, m.SupportsContrast);

        // ---- warmth ---------------------------------------------------------
        // Always enabled: this is a GPU gamma ramp, so it works even on a
        // monitor that answers no DDC at all.
        _kelvin = MakeSlider(GammaControl.MinKelvin, GammaControl.MaxKelvin, m.Kelvin, true);
        _kelvin.SmallChange = 100;
        _kelvin.LargeChange = 500;
        _kelvinValue = MakeValueLabel();
        AddRow(grid, 4, "Warmth", _kelvin, _kelvinValue, true);

        Controls.Add(grid);

        _brightness.Scroll += (_, _) => OnBrightness();
        _contrast.Scroll += (_, _) => OnContrast();
        _kelvin.Scroll += (_, _) => OnKelvin();

        RefreshLabels();
    }

    private static TrackBar MakeSlider(int min, int max, int value, bool enabled)
    {
        // A monitor reporting an empty range would otherwise throw when Value is
        // assigned; widen it by one and disable instead.
        if (max <= min) max = min + 1;
        return new TrackBar
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            TickStyle = TickStyle.None,
            Dock = DockStyle.Fill,
            Height = 30,
            Enabled = enabled,
            Margin = new Padding(6, 0, 6, 0),
        };
    }

    private static Label MakeValueLabel() => new()
    {
        Font = FontValue,
        ForeColor = Ink,
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 0, 0, 0),
    };

    private static void AddRow(TableLayoutPanel grid, int row, string caption,
                               TrackBar slider, Label value, bool supported)
    {
        grid.Controls.Add(new Label
        {
            Text = caption,
            Font = FontBody,
            ForeColor = supported ? Ink : InkOff,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(4, 6, 8, 0),
        }, 0, row);

        grid.Controls.Add(slider, 1, row);

        if (supported)
        {
            grid.Controls.Add(value, 2, row);
        }
        else
        {
            // The value cell carries the explanation instead of a number, so a
            // greyed-out slider never looks like a bug in the app.
            value.Dispose();
            grid.Controls.Add(new Label
            {
                Text = "no DDC",
                Font = FontSmall,
                ForeColor = Warn,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 0, 0),
            }, 2, row);
        }
    }

    // ------------------------------------------------------------ interaction

    private void OnBrightness()
    {
        if (_suppress || !Monitor.SupportsBrightness) return;
        Monitor.Brightness = _brightness.Value;
        _worker.Set(Monitor, DdcWorker.Feature.Brightness, _brightness.Value);
        RefreshLabels();
        _onChanged();
    }

    private void OnContrast()
    {
        if (_suppress || !Monitor.SupportsContrast) return;
        Monitor.Contrast = _contrast.Value;
        _worker.Set(Monitor, DdcWorker.Feature.Contrast, _contrast.Value);
        RefreshLabels();
        _onChanged();
    }

    private void OnKelvin()
    {
        if (_suppress) return;
        // Gamma writes go to the GPU and cost microseconds, so unlike DDC they
        // do not need the background queue.
        Monitor.Kelvin = _kelvin.Value;
        ApplyGamma();
        RefreshLabels();
        _onChanged();
    }

    /// <summary>
    /// Composed onto the baseline captured before the app touched this display,
    /// so 6500K restores whatever ICC or colorimeter LUT was already loaded
    /// instead of flattening it to identity.
    /// </summary>
    private void ApplyGamma()
    {
        if (!GammaControl.Apply(Monitor.DeviceName, Monitor.Kelvin, Monitor.BaselineRamp))
        {
            _report($"{Monitor.FriendlyName}: the graphics driver refused the colour change. " +
                    "Windows rejects gamma ramps it considers extreme.");
        }
    }

    /// <summary>Set a value programmatically without re-entering the change handlers.</summary>
    public void ApplyBrightness(int value)
    {
        if (!Monitor.SupportsBrightness) return;
        int v = Math.Clamp(value, _brightness.Minimum, _brightness.Maximum);
        _suppress = true;
        _brightness.Value = v;
        _suppress = false;
        Monitor.Brightness = v;
        _worker.Set(Monitor, DdcWorker.Feature.Brightness, v);
        RefreshLabels();
    }

    public void ApplyKelvin(int kelvin)
    {
        int v = Math.Clamp(kelvin, _kelvin.Minimum, _kelvin.Maximum);
        _suppress = true;
        _kelvin.Value = v;
        _suppress = false;
        Monitor.Kelvin = v;
        ApplyGamma();
        RefreshLabels();
    }

    /// <summary>Pull live values back off the monitor into the sliders.</summary>
    public void SyncFromMonitor()
    {
        _suppress = true;
        if (Monitor.SupportsBrightness)
            _brightness.Value = Math.Clamp(Monitor.Brightness, _brightness.Minimum, _brightness.Maximum);
        if (Monitor.SupportsContrast)
            _contrast.Value = Math.Clamp(Monitor.Contrast, _contrast.Minimum, _contrast.Maximum);
        _kelvin.Value = Math.Clamp(Monitor.Kelvin, _kelvin.Minimum, _kelvin.Maximum);
        _suppress = false;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (Monitor.SupportsBrightness)
        {
            _brightnessValue.Text = _brightness.Value.ToString();
            // Say when the figure is a guess. An unqualified number reads as a
            // measurement, and nothing here was measured with a colorimeter.
            string note = Presets.IsKnown(Monitor) ? "" : " (estimated panel)";
            _nits.Text = $"about {Presets.NitsFor(Monitor, _brightness.Value)} nits{note}";
        }
        else
        {
            _nits.Text = "";
        }

        if (Monitor.SupportsContrast) _contrastValue.Text = _contrast.Value.ToString();

        _kelvinValue.Text = _kelvin.Value >= GammaControl.NeutralKelvin
            ? "off"
            : _kelvin.Value + "K";
    }
}
