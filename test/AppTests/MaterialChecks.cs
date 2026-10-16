using LumenDeck;
using LumenDeck.Material;
using WpfButton = System.Windows.Controls.Button;
using WpfSlider = System.Windows.Controls.Slider;
using WpfText = System.Windows.Controls.TextBox;

internal static partial class Program
{
    private static void ClickMaterial(MaterialWindow window, string name) =>
        ((WpfButton)window.FindName(name)).RaiseEvent(new System.Windows.RoutedEventArgs(WpfButton.ClickEvent));

    private static void CheckMaterial(string directory, string renderDirectory)
    {
        if (renderDirectory != null) Directory.CreateDirectory(renderDirectory);
        using (var fixture = new Fixture(directory, enumerate: _ => DeskDisplays(3), allDisplays: false,
            onRestore: (_, _) => true))
        {
            var host = (IMaterialHost)fixture.Form;
            var window = fixture.Form.OpenMaterialWindow(false);
            window.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            window.Left = -30000; window.Top = -30000; window.ShowInTaskbar = false; window.ShowActivated = false;
            window.Show(); Application.DoEvents(); window.RefreshState();
            var state = host.Capture(); string primary = state.Displays.Single(d => d.Primary).Key;
            void Send(MaterialAction action, int value = 0, string key = null)
            { var s = host.Capture(); host.Execute(action, key ?? s.SelectedKey, s.Generation, value); window.RefreshState(); Application.DoEvents(); }
            Assert(window.IsVisible && !fixture.Form.Visible && state.Displays.Count == 3,
                "material shell shares the existing controller and real synthetic inventory");
            int scans = fixture.Scans, startup = fixture.StartupWrites, gamma = fixture.GammaWrites, writes = fixture.Transport.Writes;
            window.RefreshState(); window.RefreshState();
            Assert(fixture.Scans == scans && fixture.Transport.Writes == writes && fixture.GammaWrites == gamma,
                "material snapshots and redraws perform no device operations");
            Send(MaterialAction.SelectDisplay, key: primary);
            var brightness = (WpfSlider)window.FindName("Brightness");
            brightness.Value = 37;
            PumpUntil(() => fixture.Worker.Idle, "material brightness write");
            Assert(host.Capture().Displays.Single(d => d.Key == primary).Brightness == 37 && fixture.Transport.Writes > writes,
                "actual WPF slider routes brightness to the existing worker");
            Assert(fixture.Settings.Find(primary).CustomBrightnessPercent == 37,
                "material manual brightness captures Custom through the shared card path");
            Send(MaterialAction.Warmth, 4200);
            Assert(fixture.GammaWrites > gamma && fixture.Settings.Find(primary).CustomKelvin == 4200,
                "material warmth routes through gamma acceptance and saves Custom");
            Send(MaterialAction.PresetDay);
            PumpUntil(() => fixture.Worker.Idle, "material preset");
            Assert(fixture.Settings.Find(primary).CustomBrightnessPercent == 37 && fixture.Settings.Find(primary).CustomKelvin == 4200,
                "material presets preserve Custom");
            Send(MaterialAction.Custom);
            PumpUntil(() => fixture.Worker.Idle, "material Custom restore");
            Assert(host.Capture().Displays.Single(d => d.Key == primary) is { Brightness: 37, Kelvin: 4200 },
                "material Custom restores both brightness and warmth");
            ClickMaterial(window, "BrightnessValue");
            var input = (WpfText)window.FindName("ExactInput"); input.Text = "bad";
            writes = fixture.Transport.Writes;
            typeof(MaterialWindow).GetMethod("ApplyExactClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(window, [input, new System.Windows.RoutedEventArgs()]);
            Assert(fixture.Transport.Writes == writes && ((System.Windows.Controls.TextBlock)window.FindName("ExactError")).Text.Length > 0,
                "invalid material exact entry is rejected without a write");
            input.Text = "41";
            typeof(MaterialWindow).GetMethod("ApplyExactClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(window, [input, new System.Windows.RoutedEventArgs()]);
            PumpUntil(() => fixture.Worker.Idle, "material exact entry");
            Assert(host.Capture().Displays.Single(d => d.Key == primary).Brightness == 41,
                "material exact entry applies to its captured display");
            void SliderKey(System.Windows.Input.Key key)
            {
                brightness.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                    System.Windows.PresentationSource.FromVisual(window), 0, key)
                    { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            }
            SliderKey(System.Windows.Input.Key.Right);
            PumpUntil(() => fixture.Worker.Idle, "material keyboard adjustment");
            Assert(host.Capture().Displays.Single(d => d.Key == primary).Brightness == 42,
                "material keyboard arrow requests one hardware unit");
            SliderKey(System.Windows.Input.Key.Left);
            SliderKey(System.Windows.Input.Key.Enter);
            Assert(((System.Windows.FrameworkElement)window.FindName("ExactRow")).Visibility == System.Windows.Visibility.Visible,
                "material slider Enter opens exact entry");
            ClickMaterial(window, "BrightnessValue"); input.Text = "draft";
            foreach (var action in new[] { MaterialAction.FinishFrost, MaterialAction.FinishCeramic, MaterialAction.FinishSmoke })
            {
                writes = fixture.Transport.Writes; gamma = fixture.GammaWrites;
                Send(action); SettleSettings(fixture); window.RefreshState();
                Assert(fixture.Transport.Writes == writes && fixture.GammaWrites == gamma && fixture.Scans == scans && input.Text == "draft",
                    "saved material changes preserve draft and cause no hardware work");
                Assert(AppSettings.Load(fixture.SettingsPath).MaterialFinish == fixture.Settings.MaterialFinish,
                    "material finish persists only after confirmed save");
                ClickMaterial(window, "SettingsButton");
                var menu = ((WpfButton)window.FindName("SettingsButton")).ContextMenu;
                Application.DoEvents();
                var appearance = (System.Windows.Controls.MenuItem)menu.Items[0];
                appearance.IsSubmenuOpen = true; Application.DoEvents();
                Assert(menu.ActualWidth > 180 && appearance.Items.Count == 7 &&
                    appearance.Items.OfType<System.Windows.Controls.MenuItem>().Count(i => i.IsChecked) == 2,
                    "material settings and appearance submenu expose saved choices");
                if (renderDirectory != null)
                {
                    RenderMaterialElement(menu, Path.Combine(renderDirectory, fixture.Settings.MaterialFinish + "-settings.png"));
                    var popup = (System.Windows.Controls.Primitives.Popup)appearance.Template.FindName("PART_Popup", appearance);
                    RenderMaterialElement((System.Windows.FrameworkElement)popup.Child, Path.Combine(renderDirectory, fixture.Settings.MaterialFinish + "-appearance.png"));
                }
                appearance.IsSubmenuOpen = false; menu.IsOpen = false;
            }
            Send(MaterialAction.SelectDisplay, key: "desk-fixture-1");
            Assert(!((WpfSlider)window.FindName("Contrast")).IsEnabled &&
                ((System.Windows.FrameworkElement)window.FindName("ExactRow")).Visibility == System.Windows.Visibility.Collapsed,
                "selection closes a stale draft and disables unsupported contrast");
            writes = fixture.Transport.Writes;
            Send(MaterialAction.Contrast, 70);
            Assert(fixture.Transport.Writes == writes, "unsupported material adjustment cannot reach transport");
            Send(MaterialAction.SelectDisplay, key: primary);
            Send(MaterialAction.NightSaver);
            PumpUntil(() => fixture.Worker.Idle, "material Night saver");
            Assert(host.Capture().NightActive && fixture.Settings.NightDimActiveFor(primary) &&
                host.Capture().Displays.Single(d => d.Key == primary).Brightness <= 5,
                "material Night saver uses actual recovery records and dims the primary");
            Send(MaterialAction.NightSaver);
            Assert(!host.Capture().NightActive && !fixture.Settings.NightDimActiveFor(primary),
                "material Night saver ends through the shared restoration path");
            Send(MaterialAction.ConnectionHelp);
            Assert(fixture.ConnectionHelpCreations == 1 && Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp").Visible,
                "connection help opens while the classic controller is hidden");
            Private<ConnectionHelpForm>(fixture.Form, "_connectionHelp").Close();
            using (var closeEditor = new System.Windows.Forms.Timer { Interval = 20 })
            {
                closeEditor.Tick += (_, _) => { fixture.PresetEditor?.Close(); fixture.ScheduleEditor?.Close(); fixture.Editor?.Close(); };
                closeEditor.Start();
                Send(MaterialAction.PresetTargets);
                Assert(fixture.PresetEditorCreations == 1, "preset editor is available from the material shell");
                Send(MaterialAction.Schedule);
                Assert(fixture.ScheduleEditor != null, "schedule editor is available from the material shell");
                Send(MaterialAction.Profile);
                PumpUntil(() => fixture.EditorCreations == 1 && !Private<bool>(fixture.Form, "_profileEditorOpening"), "material profile editor");
                Assert(fixture.ProfileErrors == 0, "profile editor opens with the material window as owner");
                closeEditor.Stop();
            }
            SettleSettings(fixture);
            var old = host.Capture(); Send(MaterialAction.Refresh);
            PumpUntil(() => fixture.Scans > scans && !host.Capture().Reading, "material refresh");
            PumpUntil(() => fixture.Worker.Idle, "material refresh settle");
            writes = fixture.Transport.Writes;
            host.Execute(MaterialAction.Brightness, primary, old.Generation, 91);
            Assert(fixture.Transport.Writes == writes, "stale material generation cannot adjust replacement handles");
            Send(MaterialAction.KeepRunning, 1); SettleSettings(fixture);
            window.Close(); Application.DoEvents(); Application.DoEvents();
            Assert(!fixture.Form.IsDisposed && !window.IsVisible && !fixture.TrayDisposed,
                "material close respects keep-running and retains controller/tray");
            Assert(fixture.Form.RequestReveal(), "singleton activation admits the material shell");
            PumpUntil(() => window.IsVisible, "material reveal");
            Assert(!fixture.Form.Visible && fixture.StartupWrites == startup,
                "material reveal does not expose a second controller or mutate startup");
            if (renderDirectory != null)
            {
                foreach (var action in new[] { MaterialAction.FinishSmoke, MaterialAction.FinishFrost, MaterialAction.FinishCeramic })
                {
                    Send(action); SettleSettings(fixture); window.RefreshState();
                    window.RenderReference(Path.Combine(renderDirectory, fixture.Settings.MaterialFinish + ".png"));
                    window.Width = 660; window.Height = 610; window.UpdateLayout();
                    Assert(((WpfSlider)window.FindName("Brightness")).ActualWidth > 400,
                        "material minimum-size brightness stays usable");
                    window.RenderReference(Path.Combine(renderDirectory, fixture.Settings.MaterialFinish + "-compact.png"));
                    window.Width = 880; window.Height = 830; window.UpdateLayout();
                }
            }
            Send(MaterialAction.Classic); SettleSettings(fixture);
            Assert(fixture.Form.Visible && !window.IsVisible && !fixture.Settings.UseMaterialInterface,
                "classic interface remains reachable and its preference persists");
            Private<PreferencesMenu>(fixture.Form, "_preferencesMenu").Items.OfType<ToolStripMenuItem>()
                .Single(i => i.Text == "Material interface").PerformClick();
            SettleSettings(fixture);
            Assert(window.IsVisible && !fixture.Form.Visible && fixture.Settings.UseMaterialInterface,
                "classic settings return to the same material window");
            Send(MaterialAction.KeepRunning, 0); SettleSettings(fixture);
        }
        using (var fixture = new Fixture(directory, refuseSettingsSave: true, enumerate: _ => DeskDisplays(1), allDisplays: false))
        {
            var host = (IMaterialHost)fixture.Form;
            var window = fixture.Form.OpenMaterialWindow(false);
            window.Left = -30000; window.Top = -30000; window.ShowInTaskbar = false; window.ShowActivated = false; window.Show();
            var s = host.Capture();
            host.Execute(MaterialAction.FinishFrost, s.SelectedKey, s.Generation);
            SettleSettings(fixture); window.RefreshState();
            Assert(fixture.Settings.MaterialFinish == "smoke" && host.Capture().Warning,
                "failed material preference save preserves the active appearance with a warning");
        }
        using (var entered = new ManualResetEventSlim())
        using (var release = new ManualResetEventSlim())
        using (var fixture = new Fixture(directory, enumerate: _ => DeskDisplays(1), allDisplays: false,
            writeSettings: write => { entered.Set(); release.Wait(); return write(); }))
        {
            try
            {
                var host = (IMaterialHost)fixture.Form;
                var window = fixture.Form.OpenMaterialWindow(false);
                window.Left = -30000; window.Top = -30000; window.ShowInTaskbar = false; window.ShowActivated = false; window.Show();
                var s = host.Capture(); host.Execute(MaterialAction.FinishFrost, s.SelectedKey, s.Generation);
                Assert(entered.Wait(TimeSpan.FromSeconds(3)), "material preference write reaches controlled pending state");
                window.Close(); Application.DoEvents(); Application.DoEvents();
                Assert(!fixture.Form.IsDisposed && window.IsVisible && fixture.Settings.MaterialFinish == "smoke",
                    "material close waits for a pending save without activating the draft");
                release.Set(); SettleSettings(fixture);
                Assert(fixture.Settings.MaterialFinish == "frost", "pending material save activates only after confirmation");
            }
            finally { release.Set(); }
        }
    }

    private static void RenderMaterialElement(System.Windows.FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * 1.5),
            (int)Math.Ceiling(element.ActualHeight * 1.5), 144, 144, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }
}
