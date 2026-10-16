using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LumenDeck;

internal static partial class Program
{
    private static string FileDigest(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static Size ShowcaseSize(Size desired, Size trackLimit, bool portfolio)
    {
        if (!portfolio) return new Size(Math.Min(desired.Width, trackLimit.Width), Math.Min(desired.Height, trackLimit.Height));
        int unit = Math.Min((int)Math.Ceiling(desired.Width / 8.0), Math.Min(trackLimit.Width / 8, trackLimit.Height / 5));
        return new Size(unit * 8, unit * 5);
    }

    private static void CheckShowcase(string directory, string renderDirectory)
    {
        Assert(ShowcaseSize(new Size(1200, 750), new Size(1040, 788), true) == new Size(1040, 650),
            "small native desktops retain an exact 8:5 window within their tracking limit");
        Assert(ShowcaseSize(new Size(1500, 938), new Size(2000, 1200), true) == new Size(1504, 940),
            "fractional DPI rounds the native portfolio up uniformly");
        Assert(ShowcaseSize(new Size(1016, 729), new Size(1040, 788), false) == new Size(1016, 729),
            "reachable ordinary window dimensions are unchanged");
        if (renderDirectory != null) Directory.CreateDirectory(renderDirectory);
        var captures = new Dictionary<string, object>();
        foreach (bool stacked in new[] { false, true })
        {
            using var fixture = new Fixture(directory, enumerate: _ =>
            {
                var displays = DeskDisplays(2);
                if (stacked)
                {
                    displays[1].Rect = new Native.RECT { Left = -30000, Right = -28080, Top = -28920, Bottom = -27840 };
                    displays[1].PositionLabel = "below";
                }
                return displays;
            }, allDisplays: false);
            var form = fixture.Form;
            // Use real monitor DPI on the private desktop. Neither synthetic DPI
            // messages nor manually scaled fonts belong in public source captures.
            form.Location = Screen.PrimaryScreen.WorkingArea.Location + new Size(40, 40);
            Application.DoEvents();
            PumpUntil(() => !Private<bool>(form, "_dpiLayoutPending") && !Private<bool>(form, "_deskLayoutPending"), "showcase native DPI settles");
            var refresh = FocusRefresh(form);
            PumpUntil(() => refresh.IsCompleted, "showcase discovery at native DPI");
            refresh.GetAwaiter().GetResult();
            PickMapDisplay(form, stacked ? 1 : 0);

            var sizes = stacked ? new[] { ("stacked", new Size(1016, 729)) }
                : new[] { ("documentation", new Size(1016, 729)), ("portfolio", new Size(1200, 750)), ("compact", new Size(620, 480)) };
            foreach (var (name, logicalSize) in sizes)
            {
                // Form.SetBoundsCore clamps to the host's MaxWindowTrackSize,
                // including on small hosted desktops. Exercise reachable bounds;
                // the exporter still refuses any master smaller than 1200x750.
                var requested = ShowcaseSize(form.LogicalToDeviceUnits(logicalSize), SystemInformation.MaxWindowTrackSize, name == "portfolio");
                form.Size = requested;
                Application.DoEvents();
                PumpUntil(() => !Private<bool>(form, "_dpiLayoutPending") && !Private<bool>(form, "_deskLayoutPending"), "showcase final layout settles");
                Assert(form.Size == requested, $"{name} capture has the requested native outer bounds: expected={requested}, actual={form.Size}, dpi={form.DeviceDpi}, limit={SystemInformation.MaxWindowTrackSize}");
                var map = Private<LayoutMap>(form, "_map");
                Assert(map.Visible && (!stacked || map.Dock == DockStyle.Left), "showcase retains its usable display selector");
                var list = Private<FlowLayoutPanel>(form, "_list");
                var principal = Controls(form).OfType<MonitorCard>().Where(c => c.Visible)
                    .SelectMany(c => Controls(c).OfType<Slider>()).Single(c => c.Principal);
                Assert(list.ClientRectangle.Contains(new Rectangle(list.PointToClient(principal.PointToScreen(Point.Empty)), principal.Size)),
                    "showcase principal adjustment fits the viewport");
                Assert(!list.HorizontalScroll.Visible && (GetWindowLong(list.Handle, -16) & 0x00100000) == 0,
                    "showcase has no managed or native horizontal scrollbar");
                if (name == "documentation")
                {
                    var menu = Private<PreferencesMenu>(form, "_preferencesMenu");
                    foreach (var palette in Theme.Palettes.Where(p => p.Id != "amber"))
                    {
                        ThemeChoice(menu, palette.Id).PerformClick();
                        SettleSettings(fixture);
                        Assert(Theme.Id == palette.Id && !list.HorizontalScroll.Visible &&
                            list.ClientRectangle.Contains(new Rectangle(list.PointToClient(principal.PointToScreen(Point.Empty)), principal.Size)),
                            $"{palette.Id} native showcase preserves the compact adjustment layout");
                        if (renderDirectory == null) continue;
                        string themeName = $"theme-{palette.Id}.png";
                        string themePath = Path.Combine(renderDirectory, themeName);
                        using var themed = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(themed, new Rectangle(Point.Empty, form.Size));
                        themed.Save(themePath);
                        captures[themeName] = new
                        {
                            width = themed.Width, height = themed.Height, deviceDpi = form.DeviceDpi,
                            clientSize = new[] { form.ClientSize.Width, form.ClientSize.Height }, sha256 = FileDigest(themePath),
                        };
                    }
                    ThemeChoice(menu, "amber").PerformClick();
                    SettleSettings(fixture);
                }
                if (renderDirectory == null) continue;
                string filename = name + ".png";
                string path = Path.Combine(renderDirectory, filename);
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(path);
                captures[filename] = new
                {
                    width = bitmap.Width, height = bitmap.Height, deviceDpi = form.DeviceDpi,
                    clientSize = new[] { form.ClientSize.Width, form.ClientSize.Height }, sha256 = FileDigest(path),
                };
            }
            Assert(fixture.Transport.Writes == 0 && fixture.GammaWrites == 0 && fixture.StartupWrites == 0,
                "showcase requests no display or startup changes");
        }
        if (renderDirectory == null) return;
        var gui = typeof(MainForm).Assembly;
        var core = typeof(AppSettings).Assembly;
        var receipt = new
        {
            guiBuild = gui.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,
            coreBuild = core.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,
            guiAssemblySha256 = FileDigest(gui.Location), coreAssemblySha256 = FileDigest(core.Location), captures,
        };
        File.WriteAllText(Path.Combine(renderDirectory, "capture.json"),
            JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
