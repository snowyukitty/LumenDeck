namespace LumenDeck;

/// <summary>Offline guidance that can be rendered without reading or writing a display.</summary>
internal sealed class ConnectionGuide : UserControl
{
    private readonly TableLayoutPanel _content;
    private readonly Label _inventory;
    private readonly Label _displays;
    private bool _layingOut;
    private SupportReport.Document _report;
    public event Action ReturnRequested;
    public event Action<SupportReport.Document> ReportRequested;

    public ConnectionGuide()
    {
        Theme.Colors(this, back: () => Theme.Base, fore: () => Theme.Ink);
        Font = Theme.Body;
        AutoScroll = true;
        Padding = new Padding(20);
        AccessibleName = "Connection guidance";
        AccessibleRole = AccessibleRole.Grouping;
        _content = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, BackColor = Color.Transparent,
        };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddText("Get your displays connected", Theme.H1, () => Theme.Ink);
        AddText("A greyed-out slider means that control did not respond. It does not tell us which part of the connection caused it.", Theme.Body, () => Theme.InkMuted);
        _inventory = AddText("", Theme.Body, () => Theme.Ink);
        _displays = AddText("", Theme.Small, () => Theme.InkMuted);
        var report = new FlatButton("View support report") { AutoSize = true, MinimumSize = new Size(170, 30), Margin = new Padding(0, 0, 0, 16) };
        report.Click += (_, _) =>
        {
            if (_report != null) ReportRequested?.Invoke(_report);
        };
        _content.Controls.Add(report);
        AddText("External monitor", Theme.H2, () => Theme.Ink);
        AddText("1. Open the monitor's own on-screen menu and look for DDC/CI under Settings, System or Other. Enable it if available.\n\n2. If you use a dock, hub or KVM, try a direct video cable to the computer. Some connections do not pass monitor-control commands.\n\n3. Choose Refresh in the main window after changing the setting or cable. Contrast and extra controls may remain unavailable even when brightness works.", Theme.Body, () => Theme.InkMuted);
        AddText("Built-in laptop display", Theme.H2, () => Theme.Ink);
        AddText("Built-in panels use the laptop's Windows brightness interface. Try the brightness slider in Windows Settings. If that is unavailable too, check the laptop manufacturer's display-driver guidance. DDC/CI menu settings do not apply to this path; hardware contrast is often unavailable.", Theme.Body, () => Theme.InkMuted);
        AddText("No displays found?", Theme.H2, () => Theme.Ink);
        AddText("Check that the display is enabled in Windows Settings > System > Display and connected to an active video output. Reconnect it if needed, then Refresh. If Windows sees it but LumenDeck still does not, include that detail in a bug report.", Theme.Body, () => Theme.InkMuted);
        AddText("An adjustment was not confirmed?", Theme.H2, () => Theme.Ink);
        AddText("The card keeps a notice when a change fails or cannot be confirmed. Check the screen and, for hardware brightness or contrast, its own on-screen menu before relying on the displayed value. A failed readback can leave the requested value on the slider.\n\nA retry does not clear the notice automatically. Dismiss notice only hides the message; it does not retry or confirm the change. Refresh rebuilds the cards and reads hardware controls again. Refresh can also reapply saved warmth and recover active blackouts, so it is more than a read-only check. Warmth uses the graphics driver; hardware brightness readings cannot confirm a colour change.", Theme.Body, () => Theme.InkMuted);
        AddText("Screen is back, but still dim?", Theme.H2, () => Theme.Ink);
        AddText("Restoring a blackout removes its black overlay first. If the saved brightness cannot be restored, the card keeps Restore screen available and shows an unconfirmed-adjustment notice. Check the display's connection and brightness controls, then choose Restore screen to retry that display. Choose Refresh after changing the cable or monitor settings to discover its controls again and retry saved recovery.\n\nLumenDeck keeps the saved brightness for another recovery attempt. Recovery can also be retried at the next launch or after Windows reports a display configuration change; it is not retried continuously while idle. Dismiss notice only hides the warning and does not cancel recovery. For a built-in laptop panel, check Windows brightness controls rather than a DDC/CI menu.", Theme.Body, () => Theme.InkMuted);
        AddText("You still have options", Theme.H2, () => Theme.Ink);
        AddText("You can try Screen blackout as a reversible black overlay without hardware brightness. Colour warmth uses the GPU rather than DDC/CI and depends on driver support. Neither replaces a missing hardware brightness control.\n\nUse View support report above, or lumendeck-cli --support-report, for a minimal report that excludes names and device identifiers. Review before sharing. Advanced --diagnose output includes identifiers and is a separate option for deeper troubleshooting.", Theme.Body, () => Theme.InkMuted);
        var back = new FlatButton("Back to controls") { AutoSize = true, MinimumSize = new Size(150, 30), Margin = new Padding(0, 8, 0, 8) };
        back.Click += (_, _) => ReturnRequested?.Invoke();
        _content.Controls.Add(back);
        Controls.Add(_content);
    }

    public void SetMonitors(IEnumerable<Monitor> monitors, DisplayScanState state = DisplayScanState.Ready)
    {
        var snapshot = monitors.ToList();
        _report = SupportReport.Create(snapshot, state, Cli.VersionText, Environment.OSVersion.Version);
        _inventory.Text = state switch
        {
            DisplayScanState.Reading => "Reading displays…",
            DisplayScanState.NotStarted => "Displays have not been checked yet.",
            DisplayScanState.Failed => "Could not refresh display information. Return to the main window and try Refresh again.",
            _ => DisplaySupport.Inventory(snapshot, offerHelp: false),
        };
        _displays.Text = state != DisplayScanState.Ready ? "Current control availability is not known." : string.Join("\n\n",
            snapshot.Select(DisplaySupport.Describe).Select(s => $"{s.Name}\n{s.Controls}\n{s.Guidance}"));
    }

    private Label AddText(string text, Font font, Func<Color> color)
    {
        var label = Theme.Colors(new Label
        {
            Text = text, Font = font, AutoSize = true, UseMnemonic = false,
            Margin = new Padding(0, 0, 0, 12), BackColor = Color.Transparent,
        }, fore: () => color());
        _content.Controls.Add(label);
        return label;
    }

    public void ScrollReference(Keys key)
    {
        int end = Math.Max(0, _content.Height + Padding.Vertical - ClientSize.Height);
        int page = Math.Max(1, ClientSize.Height - LogicalToDeviceUnits(40));
        int next = key switch
        {
            Keys.Home => 0, Keys.End => end,
            Keys.PageUp => -AutoScrollPosition.Y - page,
            Keys.PageDown => -AutoScrollPosition.Y + page,
            _ => -AutoScrollPosition.Y,
        };
        AutoScrollPosition = new Point(0, Math.Clamp(next, 0, end));
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_content != null && !_layingOut)
        {
            _layingOut = true;
            try
            {
                int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                foreach (var label in _content.Controls.OfType<Label>())
                    if (label.MaximumSize.Width != width) label.MaximumSize = new Size(width, 0);
                foreach (var button in _content.Controls.OfType<FlatButton>())
                    if (button.MaximumSize.Width != width) button.MaximumSize = new Size(width, 0);
            }
            finally { _layingOut = false; }
        }
        base.OnLayout(e);
    }
}
