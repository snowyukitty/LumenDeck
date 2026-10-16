using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LumenDeck.Material;

/// <summary>Presentation only. All commands return to the single existing application controller.</summary>
public partial class MaterialWindow : Window
{
    private readonly IMaterialHost _host;
    private readonly DispatcherTimer _timer;
    private MaterialSnapshot? _snapshot;
    private MaterialDisplay? Selected => _snapshot?.Displays.FirstOrDefault(d => d.Key == _snapshot.SelectedKey);
    private string _finish = "", _backdropName = "", _mapSignature = "";
    private int _backdrop = 2;
    private bool _sync, _shutdown, _exporting, _refreshQueued;
    private HwndSource? _source;
    private NativeBackdrop.Result _native = new(false, null, "");
    private MaterialAction _exactAction;
    private string _exactKey = "";
    private int _exactGeneration;
    private readonly Dictionary<string, Button> _tiles = [];

    public MaterialWindow(IMaterialHost host)
    {
        _host = host;
        InitializeComponent();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
            (_, _) => RefreshState(), Dispatcher);
        _timer.Stop();
        SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source?.AddHook(WindowMessage);
            ApplyBackdrop();
        };
        IsVisibleChanged += (_, _) =>
        {
            _host.VisibilityChanged();
            if (IsVisible) { RefreshState(); _timer.Start(); }
            else _timer.Stop();
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) _host.MinimiseRequested(); };
        Closing += (_, e) =>
        {
            if (_shutdown) return;
            e.Cancel = true;
            // Let WPF finish cancellation before the application may close this window for real.
            Dispatcher.BeginInvoke(_host.CloseRequested, DispatcherPriority.Normal);
        };
        Closed += (_, _) => { _timer.Stop(); _source?.RemoveHook(WindowMessage); };
        PreviewKeyDown += WindowKeyDown;
        foreach (var slider in new[] { Brightness, Contrast, Warmth })
            slider.PreviewKeyDown += SliderKeyDown;
        RefreshState();
    }

    public static string NormalizeFinish(string? finish) => finish is "frost" or "ceramic" ? finish : "smoke";
    public static string NormalizeBackdrop(string? backdrop) => backdrop is "glass" or "solid" ? backdrop : "mica";
    public void Shutdown() { _shutdown = true; _timer.Stop(); Close(); }
    public void Reveal()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private IntPtr WindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is 0x1A or 0x15 or 0x31E && !_refreshQueued)
        {
            _refreshQueued = true;
            Dispatcher.BeginInvoke(() => { _refreshQueued = false; if (!_shutdown) SelectFinish(_finish); }, DispatcherPriority.Background);
        }
        return IntPtr.Zero;
    }

    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    private void Set(string name, string value) => Resources[name] = Brush(value);

    private void SelectFinish(string finish)
    {
        _finish = finish;
        bool dark = finish == "smoke", ceramic = finish == "ceramic";
        Set("Ink", dark ? "#F0F1F3" : "#252A30");
        Set("Muted", dark ? "#D4DCE7" : "#3F4A56");
        Set("Accent", dark ? "#FFD49C" : "#684516");
        Set("AccentWash", dark ? "#242E2923" : "#1478531F");
        Set("AccentEdge", dark ? "#887C6B53" : "#6678531F");
        Set("Edge", dark ? "#33D2DBE5" : "#2A444D59");
        Set("Divider", dark ? "#21C4CEDD" : "#20535C65");
        Set("Hover", dark ? "#354F5863" : "#2447515B");
        Set("Pressed", dark ? "#60545F6C" : "#3547515B");
        Set("Track", dark ? "#FF959FAE" : "#FF65727F");
        Set("Thumb", dark ? "#F3F3F0" : "#FFFFFF");
        Set("StudyBar", dark ? "#AE171C24" : ceramic ? "#F0EEE9" : "#B9ECF0F3");
        Set("MenuSurface", dark ? "#242A33" : ceramic ? "#FCFAF6" : "#F4F7FA");
        Resources["SurfaceRim"] = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(dark ? "#65EAF2FF" : "#E6FFFFFF"),
            (Color)ColorConverter.ConvertFromString(dark ? "#1F9DADBF" : "#454E6276"), 90);
        if (SystemParameters.HighContrast)
        {
            foreach (string key in new[] { "Ink", "Muted", "Accent", "Edge", "Divider", "Track" }) Resources[key] = SystemColors.WindowTextBrush;
            foreach (string key in new[] { "AccentWash", "Hover", "Pressed", "StudyBar", "Surface", "Thumb", "MenuSurface" }) Resources[key] = SystemColors.WindowBrush;
            Resources["AccentEdge"] = SystemColors.HighlightBrush;
        }
        ApplyBackdrop();
    }

    private void ApplyBackdrop()
    {
        bool dark = _finish == "smoke", ceramic = _finish == "ceramic";
        _native = NativeBackdrop.Apply(this, dark, _backdrop, ceramic || _exporting);
        var recipe = SurfaceRecipe.For(dark, ceramic, _native.Requested, _backdrop == 3);
        Resources["Foundation"] = SystemParameters.HighContrast ? SystemColors.WindowBrush : Brush(recipe.Foundation);
        Resources["Surface"] = SystemParameters.HighContrast ? SystemColors.WindowBrush : recipe.Surface;
        if (SystemParameters.HighContrast) Resources["SurfaceRim"] = SystemColors.WindowTextBrush;
        SurfaceShadow.Visibility = SystemParameters.HighContrast ? Visibility.Collapsed : Visibility.Visible;
        MaterialNote.Text = _exporting
            ? "LumenDeck · opaque reference render; native backdrop is not captured"
            : $"{char.ToUpperInvariant(_finish[0])}{_finish[1..]} · {(_native.Requested ? (_backdrop == 3 ? "Glass · Windows controls blur and inactive appearance" : "Mica · wallpaper-informed") : "Solid")}";
        ThemeButton.Content = $"Theme · {char.ToUpperInvariant(_finish[0])}{_finish[1..]} ▾";
        AutomationProperties.SetName(ThemeButton,
            $"Theme, {_finish}, {(_native.Requested ? (_backdrop == 3 ? "Glass" : "Mica") : "Solid")} surface");
    }



    public void RefreshState()
    {
        if (_shutdown) return;
        var state = _host.Capture();
        var previous = _snapshot;
        _snapshot = state;
        string finish = NormalizeFinish(state.Finish), backdrop = NormalizeBackdrop(state.Backdrop);
        if (_finish != finish || _backdropName != backdrop)
        {
            _backdropName = backdrop;
            _backdrop = backdrop switch { "glass" => 3, "solid" => 1, _ => 2 };
            SelectFinish(finish);
        }
        if (previous == null || previous.SelectedKey != state.SelectedKey || previous.Generation != state.Generation)
            ExactRow.Visibility = Visibility.Collapsed;
        NightButton.Content = state.NightActive ? "End Night saver" : "Night saver";
        NightButton.IsEnabled = state.NightAvailable;
        RefreshButton.IsEnabled = !state.Reading;
        IdentifyButton.IsEnabled = !state.Reading && state.Displays.Count > 0;
        Message.Text = state.Status;
        Message.SetResourceReference(TextBlock.ForegroundProperty, state.Warning ? "Accent" : "Muted");
        AutomationProperties.SetLiveSetting(Message, System.Windows.Automation.AutomationLiveSetting.Polite);
        SaveNote.Text = state.SaveStatus + (string.IsNullOrEmpty(state.ScheduleStatus) ? "" : " · " + state.ScheduleStatus);
        EmptyState.Visibility = state.Displays.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Text = state.Reading ? "Reading displays…" : "No displays available. Try Refresh or open Connection help in Settings.";
        RefreshMap();
        var display = Selected;
        SurfaceFrame.Visibility = display == null ? Visibility.Collapsed : Visibility.Visible;
        if (display == null) { DisplayName.Text = state.Reading ? "Reading displays…" : "No display selected"; DisplayInfo.Text = ""; DisplayRole.Text = ""; return; }
        DisplayName.Text = display.Name;
        DisplayInfo.Text = $"{display.Position} · {display.Bounds.Width} × {display.Bounds.Height}";
        DisplayRole.Text = $"DISPLAY {Array.FindIndex(state.Displays.ToArray(), d => d.Key == display.Key) + 1}" + (display.Primary ? " · PRIMARY" : "");
        _sync = true;
        try
        {
            bool ready = !state.Reading;
            Brightness.IsEnabled = BrightnessValue.IsEnabled = ready && display.BrightnessSupported && !display.Blanked;
            Contrast.IsEnabled = ContrastValue.IsEnabled = ready && display.ContrastSupported;
            Warmth.IsEnabled = WarmthValue.IsEnabled = ready;
            SyncSlider(Brightness, display.BrightnessMin, display.BrightnessMax, display.Brightness);
            SyncSlider(Contrast, display.ContrastMin, display.ContrastMax, display.Contrast);
            SyncSlider(Warmth, 0, 3500, 6500 - display.Kelvin);
            BrightnessValue.Content = display.BrightnessSupported ? display.BrightnessText : "—";
            BrightnessNote.Text = display.BrightnessStatus + (string.IsNullOrEmpty(display.Estimate) ? "" : "\n" + display.Estimate);
            ContrastValue.Content = display.ContrastSupported ? display.Contrast.ToString(CultureInfo.InvariantCulture) : "—";
            ContrastNote.Text = display.ContrastStatus;
            WarmthValue.Content = display.Kelvin >= 6500 ? "off" : $"{display.Kelvin}K";
            WarningText.Text = display.Warning;
            WarningText.Visibility = string.IsNullOrWhiteSpace(display.Warning) ? Visibility.Collapsed : Visibility.Visible;
            DayButton.IsEnabled = EveningButton.IsEnabled = ReadingButton.IsEnabled = ready;
        }
        finally { _sync = false; }
    }

    private static void SyncSlider(Slider slider, int minimum, int maximum, int value)
    {
        // Do not replace an in-progress gesture with an asynchronous readback.
        if (slider.IsMouseCaptureWithin) return;
        slider.Minimum = minimum;
        slider.Maximum = Math.Max(minimum + 1, maximum);
        slider.Value = Math.Clamp(value, minimum, Math.Max(minimum + 1, maximum));
    }

    private void RefreshMap()
    {
        var displays = _snapshot!.Displays;
        string signature = string.Join("|", displays.Select(d => $"{d.Key}:{d.Bounds}"));
        if (signature != _mapSignature)
        {
            _mapSignature = signature;
            _tiles.Clear(); Map.Children.Clear();
            if (displays.Count > 0)
            {
                int left = displays.Min(d => d.Bounds.Left), top = displays.Min(d => d.Bounds.Top);
                Map.Width = Math.Max(1, displays.Max(d => d.Bounds.Left + d.Bounds.Width) - left);
                Map.Height = Math.Max(1, displays.Max(d => d.Bounds.Top + d.Bounds.Height) - top);
                double unit = Math.Max(Map.Width / 230, Map.Height / 98);
                for (int i = 0; i < displays.Count; i++)
                {
                    var display = displays[i];
                    var button = new Button { Content = (i + 1).ToString(), Width = Math.Max(1, display.Bounds.Width), Height = Math.Max(1, display.Bounds.Height), FontSize = 12 * unit, MinHeight = 0, Padding = new(0) };
                    var border = new FrameworkElementFactory(typeof(Border));
                    border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4 * unit));
                    border.SetValue(Border.BorderThicknessProperty, new Thickness(unit));
                    border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                    border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                    var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                    presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
                    border.AppendChild(presenter);
                    var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
                    var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
                    focus.Setters.Add(new Setter(Control.BorderBrushProperty, SystemColors.HighlightBrush));
                    template.Triggers.Add(focus); button.Template = template;
                    Canvas.SetLeft(button, display.Bounds.Left - left); Canvas.SetTop(button, display.Bounds.Top - top);
                    AutomationProperties.SetName(button, $"Display {i + 1}, {display.Name}, {display.Position}");
                    button.Click += (_, _) => Send(MaterialAction.SelectDisplay, key: display.Key);
                    Map.Children.Add(button); _tiles.Add(display.Key, button);
                }
            }
        }
        foreach (var (key, tile) in _tiles)
        {
            bool selected = key == _snapshot.SelectedKey;
            tile.SetResourceReference(Control.BackgroundProperty, selected ? "AccentWash" : "StudyBar");
            tile.SetResourceReference(Control.BorderBrushProperty, selected ? "Accent" : "Edge");
            tile.SetResourceReference(Control.ForegroundProperty, selected ? "Accent" : "Muted");
            tile.IsEnabled = !_snapshot.Reading;
        }
    }

    private void Send(MaterialAction action, int value = 0, string? key = null)
    {
        if (_sync || _snapshot == null || _shutdown) return;
        _host.Execute(action, key ?? _snapshot.SelectedKey, _snapshot.Generation, value);
        RefreshState();
    }
    private void BrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Send(MaterialAction.Brightness, (int)Math.Round(e.NewValue));
    private void ContrastChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Send(MaterialAction.Contrast, (int)Math.Round(e.NewValue));
    private void WarmthChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Send(MaterialAction.Warmth, 6500 - (int)Math.Round(e.NewValue / 50) * 50);
    private void NightClick(object sender, RoutedEventArgs e) => Send(MaterialAction.NightSaver);
    private void DayClick(object sender, RoutedEventArgs e) => Send(MaterialAction.PresetDay);
    private void EveningClick(object sender, RoutedEventArgs e) => Send(MaterialAction.PresetEvening);
    private void ReadingClick(object sender, RoutedEventArgs e) => Send(MaterialAction.PresetNight);
    private void CustomClick(object sender, RoutedEventArgs e) => Send(MaterialAction.Custom);
    private void IdentifyClick(object sender, RoutedEventArgs e) => Send(MaterialAction.Identify);
    private void RefreshClick(object sender, RoutedEventArgs e) => Send(MaterialAction.Refresh);

    private MenuItem Item(string label, MaterialAction action, bool enabled = true, bool? check = null, string? key = null)
    {
        var item = new MenuItem { Header = label, IsEnabled = enabled, IsCheckable = check.HasValue, IsChecked = check == true };
        item.Click += (_, e) => { e.Handled = true; Send(action, check == true ? 0 : 1, key); };
        return item;
    }
    private ContextMenu Menu(object sender)
    {
        RefreshState();
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        menu.Resources.MergedDictionaries.Add(Resources);
        ((FrameworkElement)sender).ContextMenu = menu;
        return menu;
    }
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        var menu = Menu(sender); var s = _snapshot!; bool save = !s.SavePending && !s.PreferencesLocked;
        var finish = new MenuItem { Header = "Appearance" };
        foreach (var choice in AppearanceChoices()) finish.Items.Add(choice);
        menu.Items.Add(finish); menu.Items.Add(new Separator());
        menu.Items.Add(Item(s.Startup.HasValue ? "Start with Windows" : "Start with Windows (unavailable)", MaterialAction.Startup, save && s.Startup.HasValue, s.Startup == true));
        menu.Items.Add(Item("Start minimised", MaterialAction.StartMinimised, save, s.StartMinimised));
        menu.Items.Add(Item("Keep running when closed or minimised", MaterialAction.KeepRunning, save, s.KeepRunning));
        menu.Items.Add(Item("Reapply saved warmth after a display scan", MaterialAction.ReapplyWarmth, save, s.ReapplyWarmth));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Preset targets…", MaterialAction.PresetTargets, save));
        menu.Items.Add(Item("Preset schedule…", MaterialAction.Schedule, save));
        menu.Items.Add(Item("Retry settings save", MaterialAction.RetrySave, save));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Connection help", MaterialAction.ConnectionHelp));
        menu.Items.Add(Item("Keyboard controls", MaterialAction.KeyboardHelp));
        menu.Items.Add(Item("About LumenDeck", MaterialAction.About));
        menu.Items.Add(Item("Classic interface", MaterialAction.Classic, save));
        menu.Items.Add(Item("Exit", MaterialAction.Exit, !s.SavePending));
        menu.IsOpen = true;
    }
    private IEnumerable<Control> AppearanceChoices()
    {
        var s = _snapshot!; bool save = !s.SavePending && !s.PreferencesLocked;
        string note = s.SavePending ? "Saving settings… theme choices will be available when it finishes."
            : s.PreferencesLocked ? "Settings changed or could not be read. Exit and reopen before changing themes."
            : SystemParameters.HighContrast ? "Windows high contrast is active. Your theme is saved for later."
            : _finish == "ceramic" ? "Ceramic always uses a solid surface."
            : _backdropName != "solid" && !_native.Requested ? "Windows is showing a solid surface. Transparency is off or unavailable."
            : "";
        if (note.Length > 0)
            yield return new MenuItem { Header = new TextBlock { Text = note, MaxWidth = 260, TextWrapping = TextWrapping.Wrap },
                IsEnabled = false, Focusable = false, FontSize = 12, Opacity = 1 };
        yield return Item("Smoke · Dark", MaterialAction.FinishSmoke, save, _finish == "smoke");
        yield return Item("Frost · Light", MaterialAction.FinishFrost, save, _finish == "frost");
        yield return Item("Ceramic · Warm solid", MaterialAction.FinishCeramic, save, _finish == "ceramic");
        yield return new Separator();
        yield return Item("Material · Mica", MaterialAction.BackdropMica, save && _finish != "ceramic", _finish != "ceramic" && _backdropName == "mica");
        yield return Item("Material · Glass", MaterialAction.BackdropGlass, save && _finish != "ceramic", _finish != "ceramic" && _backdropName == "glass");
        yield return Item("Material · Solid", MaterialAction.BackdropSolid, save && _finish != "ceramic", _finish != "ceramic" && _backdropName == "solid");
        yield return new MenuItem { Header = new TextBlock {
            Text = "Mica follows your wallpaper. Glass blurs windows behind it while active; Windows may use a solid appearance when inactive.",
            MaxWidth = 280, TextWrapping = TextWrapping.Wrap }, IsEnabled = false, Focusable = false, FontSize = 12, Opacity = 1 };
    }
    private void ThemeClick(object sender, RoutedEventArgs e)
    {
        var menu = Menu(sender);
        foreach (var choice in AppearanceChoices()) menu.Items.Add(choice);
        menu.IsOpen = true;
    }
    private void DeskActionsClick(object sender, RoutedEventArgs e)
    {
        var menu = Menu(sender); var s = _snapshot!; bool ready = !s.Reading && s.Displays.Count > 0;
        menu.Items.Add(Item("Day · all displays", MaterialAction.AllDay, ready));
        menu.Items.Add(Item("Evening · all displays", MaterialAction.AllEvening, ready));
        menu.Items.Add(Item("Night · all displays", MaterialAction.AllNight, ready));
        menu.Items.Add(Item("Custom · all displays", MaterialAction.AllCustom, ready));
        menu.Items.Add(Item("Warmth off · all displays", MaterialAction.WarmthOff, ready));
        menu.Items.Add(new Separator());
        var work = new MenuItem { Header = "Keep dimmed during Night saver", IsEnabled = ready && !s.NightActive && !s.SavePending && !s.PreferencesLocked };
        foreach (var d in s.Displays) work.Items.Add(Item(d.Name + (d.Primary ? " · primary" : ""), MaterialAction.WorkDisplay, true, d.Key == s.WorkKey, d.Key));
        menu.Items.Add(work); menu.IsOpen = true;
    }
    private void DisplayActionsClick(object sender, RoutedEventArgs e)
    {
        var menu = Menu(sender); var d = Selected; bool ready = d != null && !_snapshot!.Reading;
        menu.Items.Add(Item(d?.Blanked == true ? "Restore screen" : "Blank screen", MaterialAction.Blank, ready));
        menu.Items.Add(Item("Screen shortcut…", MaterialAction.Shortcut, ready));
        menu.Items.Add(Item("Brightness estimate…", MaterialAction.Profile, ready));
        menu.Items.Add(Item("Advanced controls (classic)…", MaterialAction.Advanced, ready));
        menu.Items.Add(Item("Dismiss adjustment notice", MaterialAction.DismissWarning, ready));
        menu.IsOpen = true;
    }

    private void OpenExact(MaterialAction action)
    {
        var d = Selected; if (d == null) return;
        if (action == MaterialAction.Brightness && !Brightness.IsEnabled || action == MaterialAction.Contrast && !Contrast.IsEnabled || action == MaterialAction.Warmth && !Warmth.IsEnabled) return;
        _exactAction = action; _exactKey = d.Key; _exactGeneration = _snapshot!.Generation;
        var (min, max, value, unit) = ExactRange(d);
        ExactLabel.Text = $"{action}: {min}–{max} {unit}. Enter applies; Escape cancels.";
        ExactInput.Text = value.ToString(CultureInfo.InvariantCulture); ExactError.Text = "";
        ExactRow.Visibility = Visibility.Visible; ExactInput.Focus(); ExactInput.SelectAll(); ExactInput.BringIntoView();
    }
    private (int Min, int Max, int Value, string Unit) ExactRange(MaterialDisplay d) => _exactAction switch
    {
        MaterialAction.Brightness => (d.BrightnessMin, d.BrightnessMax, d.Brightness, "hardware units"),
        MaterialAction.Contrast => (d.ContrastMin, d.ContrastMax, d.Contrast, "hardware units"),
        _ => (3000, 6500, d.Kelvin, "kelvin"),
    };
    private void ExactBrightnessClick(object sender, RoutedEventArgs e) => OpenExact(MaterialAction.Brightness);
    private void ExactContrastClick(object sender, RoutedEventArgs e) => OpenExact(MaterialAction.Contrast);
    private void ExactWarmthClick(object sender, RoutedEventArgs e) => OpenExact(MaterialAction.Warmth);
    private void ApplyExactClick(object sender, RoutedEventArgs e)
    {
        var d = Selected;
        if (d == null || d.Key != _exactKey || _snapshot!.Generation != _exactGeneration) { ExactRow.Visibility = Visibility.Collapsed; return; }
        var (min, max, _, _) = ExactRange(d);
        if (!int.TryParse(ExactInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < min || value > max)
        { ExactError.Text = $"Enter a whole number from {min} to {max}."; return; }
        ExactRow.Visibility = Visibility.Collapsed;
        Send(_exactAction, value);
    }
    private void CancelExactClick(object sender, RoutedEventArgs e) => ExactRow.Visibility = Visibility.Collapsed;
    private void ExactKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ApplyExactClick(sender, e); e.Handled = true; }
        if (e.Key == Key.Escape) { ExactRow.Visibility = Visibility.Collapsed; e.Handled = true; }
    }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1) { Send(MaterialAction.KeyboardHelp); e.Handled = true; }
        if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        {
            OpenExact(Contrast.IsKeyboardFocusWithin || ContrastValue.IsKeyboardFocusWithin ? MaterialAction.Contrast :
                Warmth.IsKeyboardFocusWithin || WarmthValue.IsKeyboardFocusWithin ? MaterialAction.Warmth : MaterialAction.Brightness);
            e.Handled = true;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.PageUp or Key.PageDown && _snapshot is { Displays.Count: > 0 } s)
        {
            int index = Array.FindIndex(s.Displays.ToArray(), d => d.Key == s.SelectedKey);
            Send(MaterialAction.SelectDisplay, key: s.Displays[(index + (e.Key == Key.PageUp ? s.Displays.Count - 1 : 1)) % s.Displays.Count].Key);
            e.Handled = true;
        }
    }

    private void SliderKeyDown(object sender, KeyEventArgs e)
    {
        var slider = (Slider)sender;
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Enter)) return;
        e.Handled = true;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0) return;
        if (e.Key == Key.Enter)
        {
            OpenExact(slider == Contrast ? MaterialAction.Contrast : slider == Warmth ? MaterialAction.Warmth : MaterialAction.Brightness);
            return;
        }
        double step = slider.SmallChange * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1);
        double target = e.Key switch
        {
            Key.Home => slider.Minimum,
            Key.End => slider.Maximum,
            Key.PageUp => slider.Value + slider.LargeChange,
            Key.PageDown => slider.Value - slider.LargeChange,
            Key.Right or Key.Up => slider.Value + step,
            _ => slider.Value - step,
        };
        slider.Value = Math.Clamp(target, slider.Minimum, slider.Maximum);
    }

    public void RenderReference(string path)
    {
        _exporting = true; ApplyBackdrop(); UpdateLayout();
        try
        {
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(Root.ActualWidth * 1.5), (int)Math.Ceiling(Root.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
            bitmap.Render(Root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path); encoder.Save(stream);
        }
        finally { _exporting = false; ApplyBackdrop(); }
    }
}
