namespace LumenDeck;

/// <summary>
/// Builds the per-monitor extra controls from what that monitor actually
/// advertises.
///
/// Nothing here is a fixed layout. Two monitors on the same desk genuinely
/// offer different things - one exposes input source and picture mode, another
/// exposes speaker volume and nothing else - so the controls are generated from
/// each panel's own capability string rather than shown greyed out everywhere.
/// A control that appears is one that monitor said it has.
/// </summary>
internal static class FeatureControls
{
    private static readonly Color Ink = Color.FromArgb(228, 228, 234);
    private static readonly Color InkDim = Color.FromArgb(150, 150, 158);
    private static readonly Color Warn = Color.FromArgb(210, 155, 95);
    private static readonly Font FontBody = new("Segoe UI", 9f);
    private static readonly Font FontSmall = new("Segoe UI", 8.5f);
    private static readonly Font FontValue = new("Consolas", 10f);

    /// <summary>
    /// Add a row per discovered feature to <paramref name="grid"/>, starting at
    /// <paramref name="row"/>. Returns the next free row.
    /// </summary>
    public static int Build(TableLayoutPanel grid, int row, Monitor monitor,
                            DdcWorker worker, Action<string> report)
    {
        foreach (var f in monitor.Features)
        {
            grid.RowCount = Math.Max(grid.RowCount, row + 1);

            var caption = new Label
            {
                Text = f.Name,
                Font = FontBody,
                ForeColor = f.Definition.Risky ? Warn : Ink,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(4, 7, 8, 0),
            };
            grid.Controls.Add(caption, 0, row);

            Control editor = f.Definition.Kind switch
            {
                VcpCatalog.Kind.Continuous => BuildSlider(f, monitor, worker, out var valueLabel1, report),
                VcpCatalog.Kind.Select => BuildDropdown(f, monitor, worker, report),
                _ => BuildActionButton(f, monitor, worker, report),
            };
            grid.Controls.Add(editor, 1, row);

            // The third column shows the live value for a slider, and stays
            // empty for the others - a dropdown already shows its own state.
            if (f.Definition.Kind == VcpCatalog.Kind.Continuous && editor.Tag is Label live)
                grid.Controls.Add(live, 2, row);

            string tip = f.Definition.Description;
            if (!string.IsNullOrEmpty(tip))
            {
                var t = new ToolTip { AutoPopDelay = 15000 };
                t.SetToolTip(caption, tip);
                t.SetToolTip(editor, tip);
            }

            row++;
        }
        return row;
    }

    private static Control BuildSlider(VcpFeature f, Monitor m, DdcWorker worker,
                                       out Label valueLabel, Action<string> report)
    {
        int max = Math.Max(1, f.Max);
        var bar = new TrackBar
        {
            Minimum = 0,
            Maximum = max,
            Value = Math.Clamp(f.Current, 0, max),
            TickStyle = TickStyle.None,
            Dock = DockStyle.Fill,
            Height = 30,
            Margin = new Padding(6, 0, 6, 0),
        };

        var live = new Label
        {
            Text = f.Current.ToString(),
            Font = FontValue,
            ForeColor = Ink,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        bar.Scroll += (_, _) =>
        {
            f.Current = bar.Value;
            live.Text = bar.Value.ToString();
            worker.SetVcp(m, f.Code, bar.Value);
        };

        bar.Tag = live;
        valueLabel = live;
        return bar;
    }

    private static Control BuildDropdown(VcpFeature f, Monitor m, DdcWorker worker, Action<string> report)
    {
        var box = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 3, 6, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(52, 52, 58),
            ForeColor = Color.White,
        };

        foreach (int v in f.AllowedValues)
            box.Items.Add(new ValueItem(v, f.LabelFor(v)));

        int index = f.AllowedValues.IndexOf(f.Current);
        if (index >= 0) box.SelectedIndex = index;

        bool syncing = false;
        box.SelectedIndexChanged += (_, _) =>
        {
            if (syncing || box.SelectedItem is not ValueItem item) return;

            // Changing the input source hands the screen to another machine, and
            // the person cannot then read a status bar on it. Confirm first.
            if (f.Definition.Risky)
            {
                var answer = MessageBox.Show(
                    $"Set {f.Name} on {m.FriendlyName} to {item.Label}?\n\n{f.Definition.Description}",
                    "LumenDeck", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (answer != DialogResult.OK)
                {
                    syncing = true;
                    box.SelectedIndex = Math.Max(0, f.AllowedValues.IndexOf(f.Current));
                    syncing = false;
                    return;
                }
            }

            f.Current = item.Value;
            worker.SetVcp(m, f.Code, item.Value);
            report($"{m.FriendlyName}: {f.Name} -> {item.Label}");
        };

        return box;
    }

    private static Control BuildActionButton(VcpFeature f, Monitor m, DdcWorker worker, Action<string> report)
    {
        var b = new Button
        {
            Text = "Run",
            Dock = DockStyle.Fill,
            Height = 26,
            Margin = new Padding(6, 3, 6, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(52, 52, 58),
            ForeColor = Color.White,
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(90, 74, 60);

        b.Click += (_, _) =>
        {
            var answer = MessageBox.Show(
                $"{f.Name} on {m.FriendlyName}?\n\n{f.Definition.Description}\n\nThis cannot be undone from here.",
                "LumenDeck", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (answer != DialogResult.OK) return;

            // MCCS defines these as write-1-to-trigger.
            worker.SetVcp(m, f.Code, 1);
            report($"{m.FriendlyName}: {f.Name} sent. Press Refresh once the monitor settles.");
        };

        return b;
    }

    private sealed record ValueItem(int Value, string Label)
    {
        public override string ToString() => Label;
    }
}
