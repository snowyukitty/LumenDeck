using System.Reflection;
using LumenDeck;
using Display = LumenDeck.Monitor;

internal static partial class Program
{
    private static List<Display> DeskDisplays(int count) => Enumerable.Range(0, count).Select(i => new Display
    {
        StableKey = $"desk-fixture-{i}", Description = new[] { "Studio 27", "Reading 24", "Laptop", "Reference 27" }[i],
        PositionLabel = new[] { "left · primary", "right · portrait", "below · built-in", "far right" }[i],
        IsPrimary = i == 0, IsInternalPanel = i == 2,
        Rect = new Native.RECT { Left = -30000 + (i % 2) * 1920, Top = -30000 + (i / 2) * 1920,
            Right = -30000 + (i % 2) * 1920 + (i == 1 ? 1080 : 1920),
            Bottom = -30000 + (i / 2) * 1920 + (i == 1 ? 1920 : 1080) },
        SupportsBrightness = i != 3, Brightness = i == 0 ? 62 : 45, BrightnessMin = 0, BrightnessMax = 100,
        SupportsContrast = i == 0, Contrast = 50, ContrastMin = 0, ContrastMax = 100,
    }).ToList();

    private static void CheckDeskRemote(string directory, string renderDirectory)
    {
        if (renderDirectory != null) Directory.CreateDirectory(renderDirectory);
        CheckDeskWriteBinding(directory);

        foreach (int count in new[] { 1, 2, 4 })
        {
            using var fixture = new Fixture(directory, enumerate: _ => DeskDisplays(count), allDisplays: false);
            var form = fixture.Form;
            var map = Private<LayoutMap>(form, "_map");
            var cards = Controls(form).OfType<MonitorCard>().ToArray();
            Assert(cards.Count(c => c.Visible) == 1, "production default presents one selected display");
            foreach (var size in new[] { new Size(1184, 711), new Size(1000, 690), new Size(740, 740), new Size(604, 441) })
            {
                form.ClientSize = size;
                Application.DoEvents();
                Assert(map.Visible, "screen selection remains available at ordinary and minimum sizes");
                for (int i = 0; i < count; i++)
                {
                    PickMapDisplay(form, i);
                    var card = cards.Single(c => c.Visible);
                    Assert(card.Monitor == fixture.LastScan.OrderBy(m => m.Rect.Left).ThenBy(m => m.Rect.Top).ElementAt(i), "selection routes the exact monitor object");
                    Assert(map.AccessibilityObject.GetChild(i).State.HasFlag(AccessibleStates.Selected), "accessible selection matches the visible display");
                    Assert(map.AccessibilityObject.GetChild(i).DefaultAction == "Select display", "accessible display offers an action");
                    var primary = Controls(card).OfType<Slider>().Single(s => s.Principal);
                    Assert(primary.Enabled && primary.Name == (card.Monitor.SupportsBrightness ? "brightness" : "warmth"), "principal control uses the useful supported adjustment");
                    int readoutWidth = (int)typeof(Slider).GetField("_readoutW", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(primary);
                    Assert(new[] { primary.Minimum, primary.Maximum }.All(value => TextRenderer.MeasureText(primary.Format(value), primary.Font).Width <= readoutWidth),
                        "principal control can display both range endpoints, including Kelvin");
                    Assert(primary.AccessibilityObject.DefaultAction == "Enter exact value", "principal value exposes exact entry to automation clients");
                    var list = Private<FlowLayoutPanel>(form, "_list");
                    Assert(list.ClientRectangle.Contains(new Rectangle(list.PointToClient(primary.PointToScreen(Point.Empty)), primary.Size)), $"selected principal control fits the usable viewport: count={count}, size={size}, dpi={form.DeviceDpi}, list={list.ClientSize}, bar={Private<DeskCommandBar>(form, "_bar").Size}, max={Private<DeskCommandBar>(form, "_bar").MaximumSize}, primary={primary.Bounds}");
                }
                PickMapDisplay(form, 0);
                if (renderDirectory != null)
                {
                    using var image = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    image.Save(Path.Combine(renderDirectory, $"desk-{count}-{size.Width}-{size.Height}.png"));
                }
            }
            Assert(fixture.Transport.Writes == 0, "selection, accessibility reads and capture never write hardware");
            var bar = Private<DeskCommandBar>(form, "_bar");
            bar.SetExpanded(true);
            bar.ScrollControlIntoView(bar.NightButton);
            Assert(bar.ClientRectangle.Contains(new Rectangle(bar.PointToClient(bar.NightButton.PointToScreen(Point.Empty)), bar.NightButton.Size)),
                "Night saver remains reachable in the compact desk command viewport");
            bar.SetExpanded(false);
            form.SetAllDisplays(true);
            Assert(cards.All(c => c.Visible), "all-display adjustment remains one action away");
            form.SetAllDisplays(false);
            map.AccessibilityObject.GetChild(count - 1).DoDefaultAction();
            Assert(cards.Single(c => c.Visible).Monitor == fixture.LastScan.OrderBy(m => m.Rect.Left).ThenBy(m => m.Rect.Top).Last(), "accessible activation switches the selected detail");
        }
        CheckStackedDesk(directory, renderDirectory);
        CheckDeskDpi(directory, renderDirectory);
    }
    private static void CheckDeskWriteBinding(string directory)
    {
        using var fixture = new Fixture(directory, enumerate: _ => DeskDisplays(2), allDisplays: false);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var form = fixture.Form;
        var cards = Controls(form).OfType<MonitorCard>().ToArray();
        var first = cards.Single(c => c.Monitor.StableKey == "desk-fixture-0");
        var other = cards.Single(c => c != first);
        fixture.Transport.OnWrite = (monitor, _, _) =>
        {
            if (monitor == first.Monitor)
            {
                entered.Set();
                if (!release.Wait(10000)) throw new TimeoutException("synthetic write barrier");
            }
            return true;
        };
        try
        {
            first.ApplyBrightness(74);
            PumpUntil(() => entered.IsSet, "pending display write enters barrier");
            var pending = fixture.Worker.Observe(first.Monitor, DdcWorker.Feature.Brightness);
            Assert(pending.Kind == DdcWorker.ObservationKind.Pending && pending.Requested == 74 && pending.LastObserved == 62,
                "requested value is distinct from the last observation while writing");
            PickMapDisplay(form, 1);
            Assert(other.Visible && !first.Visible, "selection changes while the old display write is pending");
            Assert(fixture.Worker.Observe(other.Monitor, DdcWorker.Feature.Brightness).LastObserved == 45,
                "selection cannot copy a pending request onto a different identity");
            release.Set();
            PumpUntil(() => fixture.Worker.Idle, "pending write settles");
            Assert(fixture.Worker.Observe(first.Monitor, DdcWorker.Feature.Brightness).Kind == DdcWorker.ObservationKind.Confirmed &&
                fixture.Worker.Observe(other.Monitor, DdcWorker.Feature.Brightness).Kind == DdcWorker.ObservationKind.Observed,
                "readback belongs only to the requesting monitor");
            fixture.Transport.OnWrite = (_, _, _) => false;
            first.ApplyBrightness(80);
            PumpUntil(() => fixture.Worker.Idle, "failed write settles");
            var failed = fixture.Worker.Observe(first.Monitor, DdcWorker.Feature.Brightness);
            Assert(failed.Kind == DdcWorker.ObservationKind.Failed && failed.Requested == 80 && failed.LastObserved == 74,
                "failed request preserves both desired and observed values");
            Assert(other.Visible && !first.Visible, "failure delivery never switches the selected display");
            fixture.Transport.OnWrite = null;
            other.Monitor.IsInternalPanel = true;
            other.ApplyBrightness(55);
            PumpUntil(() => fixture.Worker.Idle, "WMI-style write settles without readback");
            var internalResult = fixture.Worker.Observe(other.Monitor, DdcWorker.Feature.Brightness);
            Assert(internalResult.Kind == DdcWorker.ObservationKind.Unconfirmed && internalResult.LastObserved == 45,
                "accepted WMI-style writes never claim a confirmed observation");
            var map = Private<LayoutMap>(form, "_map");
            map.Focus();
            typeof(LayoutMap).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(map, [new KeyEventArgs(Keys.Left)]);
            Assert(first.Visible && form.ActiveControl == map, "selector arrows move the detail and retain keyboard focus");
            var staleAccessible = map.AccessibilityObject.GetChild(0);
            var refresh = FocusRefresh(form);
            PumpUntil(() => refresh.IsCompleted, "reconnect scan settles");
            refresh.GetAwaiter().GetResult();
            staleAccessible.DoDefaultAction();
            Assert(staleAccessible.State == AccessibleStates.Unavailable && staleAccessible.Bounds.IsEmpty,
                "an old accessible display cannot activate a replacement identity");
            Assert(first.IsDisposed && Controls(form).OfType<MonitorCard>().Single(c => c.Visible).Monitor.StableKey == "desk-fixture-0",
                "reconnect retains stable selection while replacing old monitor objects");
            Assert(fixture.Worker.Observe(fixture.LastScan[0], DdcWorker.Feature.Brightness).Kind == DdcWorker.ObservationKind.Observed,
                "old failure cannot become the reconnected monitor's observation");
        }
        finally { release.Set(); }
    }

    private static void CheckDeskDpi(string directory, string renderDirectory)
    {
        foreach (int dpi in new[] { 96, 144, 192 })
        {
            using var scaledFonts = new DpiFontSet();
            using var fixture = new Fixture(directory, enumerate: _ => DeskDisplays(2), allDisplays: false);
            var form = fixture.Form;
            form.ClientSize = new Size(1000, 690);
            var originalFonts = Controls(form).Prepend(form).Select(c => (Control: c, Font: c.Font)).ToArray();
            if (dpi != form.DeviceDpi)
            {
                // Synthetic WM_DPICHANGED drives WinForms' real DPI path on a private
                // desktop. It does not change Windows monitor scaling or emitted light.
                var rect = new Native.RECT { Left = -30000, Top = -30000,
                    Right = -30000 + (int)(form.Width * dpi / (double)form.DeviceDpi),
                    Bottom = -30000 + (int)(form.Height * dpi / (double)form.DeviceDpi) };
                IntPtr memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<Native.RECT>());
                try
                {
                    System.Runtime.InteropServices.Marshal.StructureToPtr(rect, memory, false);
                    SendMessage(form.Handle, 0x02E0, (IntPtr)((dpi << 16) | dpi), memory);
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(memory); }
            }
            Application.DoEvents();
            if (dpi != 96)
            {
                // The isolated host HDC stays at 96 DPI. Replay font scaling too,
                // and set child DPI before rescaling custom metrics, rather than
                // presenting an unscaled 96-DPI font as a 200% pixel proof.
                var field = typeof(Control).GetProperty("DeviceDpiInternal", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var item in originalFonts)
                {
                    field.SetValue(item.Control, dpi);
                    var font = new Font(item.Font.FontFamily, item.Font.Size * dpi / 96f, item.Font.Style);
                    scaledFonts.Add(font);
                    item.Control.Font = font;
                    if (item.Control is Slider)
                        typeof(Slider).GetMethod("RescaleConstantsForDpi", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(item.Control, [96, dpi]);
                }
                form.PerformLayout();
                Application.DoEvents();
            }
            Assert(form.DeviceDpi == dpi, $"native WinForms DPI path adopts synthetic {dpi} DPI");
            var card = Controls(form).OfType<MonitorCard>().Single(c => c.Visible);
            var principal = Controls(card).OfType<Slider>().Single(c => c.Principal);
            var readoutWidth = (int)typeof(Slider).GetField("_readoutW", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(principal);
            Assert(TextRenderer.MeasureText(principal.Format(principal.Value), principal.Font).Width <= readoutWidth,
                $"principal value is readable without clipping at {dpi} DPI");
            var list = Private<FlowLayoutPanel>(form, "_list");
            Assert(!list.HorizontalScroll.Visible && card.Width <= list.ClientSize.Width, $"shared detail fits at {dpi} DPI");
            Assert(fixture.Transport.Writes == 0, "DPI changes request no hardware adjustment");
            if (renderDirectory != null)
            {
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                image.Save(Path.Combine(renderDirectory, $"desk-dpi-{dpi}.png"));
            }
        }
        var contrast = Theme.ContrastMode;
        try
        {
            Theme.ContrastMode = () => true;
            using var fixture = new Fixture(directory, enumerate: _ => DeskDisplays(2), allDisplays: false);
            Assert(Theme.Base == SystemColors.Window && Theme.Ink == SystemColors.WindowText && Theme.Amber == SystemColors.WindowText,
                "contrast mode uses Windows system surfaces and readable foregrounds");
            Assert(Controls(fixture.Form).OfType<Slider>().All(s => s.AccessibilityObject.Role == AccessibleRole.Slider),
                "contrast palette preserves slider semantics");
            if (renderDirectory != null)
            {
                using var image = new Bitmap(fixture.Form.Width, fixture.Form.Height);
                fixture.Form.DrawToBitmap(image, new Rectangle(Point.Empty, fixture.Form.Size));
                image.Save(Path.Combine(renderDirectory, "desk-system-colors.png"));
            }
        }
        finally { Theme.ContrastMode = contrast; }
    }

    private static void CheckStackedDesk(string directory, string renderDirectory)
    {
        using var fixture = new Fixture(directory, enumerate: _ =>
        {
            var displays = DeskDisplays(2);
            displays[1].Rect = new Native.RECT { Left = -30000, Right = -28080, Top = -28920, Bottom = -27840 };
            displays[1].PositionLabel = "below";
            return displays;
        }, allDisplays: false);
        var form = fixture.Form;
        var map = Private<LayoutMap>(form, "_map");
        form.ClientSize = new Size(1000, 690);
        Application.DoEvents();
        Assert(map.Dock == DockStyle.Left, "stacked desk uses a compact side selector when width permits");
        PickMapDisplay(form, 1);
        if (renderDirectory != null)
        {
            using var image = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
            image.Save(Path.Combine(renderDirectory, "desk-stacked.png"));
        }
        form.ClientSize = new Size(604, 441);
        Application.DoEvents();
        Assert(map.Dock == DockStyle.Top && map.Visible, "stacked selector reflows above detail in a compact window");
        Assert(Controls(form).OfType<MonitorCard>().Single(c => c.Visible).Monitor.StableKey == "desk-fixture-1",
            "selector reflow preserves monitor identity without requesting a write");
        Assert(fixture.Transport.Writes == 0, "side-selector layout performs no display writes");
    }

    private sealed class DpiFontSet : List<Font>, IDisposable
    {
        public void Dispose() { foreach (var font in this) font.Dispose(); }
    }

}
