using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using LumenDeck.Material;
using Microsoft.Win32;

namespace LumenDeck.MaterialBench;

// A bounded, device-free scene: no engine, settings store, UI Automation or input injection.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("--capture" or "--capture-visible"))
        { Console.Error.WriteLine("Usage: MaterialBench --capture|--capture-visible <new-output-directory>"); return 2; }
        bool requireActive = args[0] == "--capture";
        string output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output)) { Console.Error.WriteLine("Use a new output directory."); return 2; }
        Directory.CreateDirectory(output);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            int result = 1;
            try { await Capture(app, output, requireActive); result = 0; }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(output, "failure.json"), JsonSerializer.Serialize(new
                { status = "blocked", error = ex.Message, capture = "Owned-scene screen rectangle; no PrintWindow/RenderTargetBitmap substitution." }));
                Console.Error.WriteLine(ex);
            }
            finally { app.Shutdown(result); }
        };
        return app.Run();
    }

    private static async Task Capture(System.Windows.Application app, string output, bool requireActive)
    {
        var area = SystemParameters.WorkArea;
        double width = Math.Min(880, area.Width - 100), height = Math.Min(830, area.Height - 100);
        if (width < 660 || height < 610) throw new InvalidOperationException("The work area cannot contain the controlled scene.");
        var pattern = new PatternScene();
        var background = new Window { Title = "Material Bench · synthetic backdrop", Content = pattern,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = area.Left + 20, Top = area.Top + 20,
            Width = width + 70, Height = height + 70, Topmost = true, ShowInTaskbar = false };
        var host = new BenchHost();
        var window = new MaterialWindow(host) { Title = "Material Bench · synthetic scenes only",
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = area.Left + 45, Top = area.Top + 45, Width = width, Height = height, Topmost = true };
        var rows = new List<object>();
        void SaveManifest(string status)
        {
            using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var assembly = typeof(MaterialWindow).Assembly;
            File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new {
                schemaVersion = 2, status, capturedAt = DateTimeOffset.UtcNow,
                productBuild = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant(),
                captureMethod = "GDI CopyFromScreen, exact owned client rectangle; occlusion checks before and after each capture",
                focusProtocol = requireActive ? "Foreground target required before and after capture." : "Visible-only; foreground unavailable/other is not active-material proof.",
                scene = "Synthetic owner covers captured region. Mica may incorporate real wallpaper; keep Mica images private.",
                exclusions = "No hardware control, product settings, policy changes or injected input.",
                limitations = "SDR pixels; no HDR calibration, accessibility acceptance, other Windows builds or performance claim.",
                environment = new { windows = Environment.OSVersion.Version.ToString(), runtime = RuntimeInformation.FrameworkDescription,
                    transparency = personalize?.GetValue("EnableTransparency"), highContrast = SystemParameters.HighContrast,
                    nativeCompositionEnabled = OwnedCapture.CompositionEnabled(), windowWidthDip = width, windowHeightDip = height }, rows
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            background.Show(); window.Owner = background; await Task.Delay(150); window.Show(); window.Activate(); await Task.Delay(500);
            var focusDeadline = DateTime.UtcNow.AddSeconds(10);
            async Task AwaitFocus()
            {
                while (!OwnedCapture.IsForeground(window) && DateTime.UtcNow < focusDeadline) await Task.Delay(100);
                if (!OwnedCapture.IsForeground(window)) throw new InvalidOperationException("Foreground target unavailable within ten seconds; no input was injected.");
            }
            if (requireActive) await AwaitFocus();
            host.Status = "Automatic synthetic-scene capture · no user action required";
            window.Title = "Material Bench · capturing synthetic scenes";
            foreach (var (finish, material) in new[] { ("smoke", "glass"), ("smoke", "solid"), ("smoke", "mica"),
                ("frost", "glass"), ("frost", "solid"), ("frost", "mica"), ("ceramic", "solid") })
            {
                host.Finish = finish; host.Material = material; window.RefreshState(); await Task.Delay(500);
                foreach (bool alternate in new[] { false, true })
                {
                    if (requireActive) { window.Activate(); await AwaitFocus(); }
                    pattern.SetPattern(alternate); background.UpdateLayout(); await Task.Delay(500);
                    string name = $"{finish}-{material}-{(alternate ? "b" : "a")}.png";
                    var root = (FrameworkElement)window.FindName("Root");
                    string foregroundBefore = OwnedCapture.ForegroundState(window);
                    var bounds = OwnedCapture.Capture(window, root, Path.Combine(output, name), requireActive);
                    string foregroundAfter = OwnedCapture.ForegroundState(window);
                    var dpi = VisualTreeHelper.GetDpi(window);
                    var card = (FrameworkElement)window.FindName("ControlSurface");
                    Point cardStart = card.TranslatePoint(new Point(0, 0), root);
                    var surface = (LinearGradientBrush)window.Resources["Surface"];
                    rows.Add(new { name, finish, material, pattern = alternate ? "b" : "a", width = bounds.Width, height = bounds.Height,
                        dpi = dpi.PixelsPerInchX, active = window.IsActive, foregroundBefore, foregroundAfter,
                        dwmBackdrop = OwnedCapture.Backdrop(new WindowInteropHelper(window).Handle),
                        foundationAlpha = ((SolidColorBrush)window.Resources["Foundation"]).Color.A / 255.0,
                        surfaceAlpha = surface.GradientStops.Select(s => s.Color.A / 255.0).ToArray(),
                        rois = new {
                            foundation = new { x = (int)(8 * dpi.DpiScaleX), y = (int)(200 * dpi.DpiScaleY), width = (int)(10 * dpi.DpiScaleX), height = (int)(70 * dpi.DpiScaleY) },
                            card = new { x = (int)((cardStart.X + 18) * dpi.DpiScaleX), y = (int)((cardStart.Y + 80) * dpi.DpiScaleY),
                                width = (int)(10 * dpi.DpiScaleX), height = (int)(120 * dpi.DpiScaleY) }
                        } });
                    SaveManifest("partial");
                    Console.WriteLine("Captured " + name);
                }
            }
            SaveManifest("complete");
        }
        catch { SaveManifest("blocked"); throw; }
        finally { window.Shutdown(); background.Close(); }
    }

    private sealed class BenchHost : IMaterialHost
    {
        public string Finish = "smoke", Material = "glass";
        public string Status = "Automatic synthetic scene · no hardware control";
        public MaterialSnapshot Capture() => new(1, "synthetic-primary", [
            new("synthetic-primary", "Studio 27", "centre · primary", new(0, 0, 2560, 1440), true, true, 0, 100, 62,
                "62%", "Observed 62% · synthetic display", "Estimated 203 nits · generic profile", true, 0, 100, 50, "", 4600, false, "", ""),
            new("synthetic-left", "Desk 24", "left", new(-1920, 180, 1920, 1080), false, true, 0, 100, 55,
                "55%", "Synthetic display", "", false, 0, 100, 0, "", 6500, false, "", "")
        ], false, Status, false, false, true, "synthetic-primary",
            Finish, Material, false, false, false, false, false, "Synthetic scene · no settings accessed", "");
        public void Execute(MaterialAction action, string key, int generation, int value = 0) { }
        public void CloseRequested() => System.Windows.Application.Current.Shutdown();
        public void MinimiseRequested() { }
        public void VisibilityChanged() { }
    }

    private sealed class PatternScene : Canvas
    {
        public PatternScene() { ClipToBounds = true; SetPattern(false); }
        public void SetPattern(bool alternate)
        {
            Children.Clear(); Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(alternate ? "#F2B77C" : "#376579"));
            string[] colors = alternate ? ["#D86055", "#E8CD80", "#728C63", "#915E7C"] : ["#263E60", "#588F9A", "#5B4A79", "#B2748C"];
            for (int i = 0; i < 18; i++)
            {
                var stripe = new Rectangle { Width = 90, Height = 1800, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i % 4])),
                    RenderTransform = new RotateTransform(18) };
                SetLeft(stripe, i * 110 - 350); SetTop(stripe, -120); Children.Add(stripe);
            }
            for (int y = 0; y < 5; y++)
            {
                var label = new TextBlock { Text = "SYNTHETIC MATERIAL BACKDROP · A B C 0123456789", FontSize = 30,
                    FontWeight = FontWeights.Bold, Foreground = Brushes.White, Opacity = 0.65 };
                SetLeft(label, 80); SetTop(label, 75 + y * 170); Children.Add(label);
            }
        }
    }
}
