using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LumenDeck.MaterialStudy;

public partial class StudyWindow : Window
{
    private sealed record Display(string Name, string Info, Rect Bounds, bool Primary);
    private readonly Display[] _displays =
    [
        new("Studio 24", "24-inch · 1920 × 1080 · DDC/CI", new(0, 720, 1920, 1080), false),
        new("Studio 32", "32-inch · 3840 × 2160 · DDC/CI", new(1920, 480, 3840, 2160), true),
        new("Reading 27", "27-inch · 1440 × 2560 · portrait", new(5760, 0, 1440, 2560), false),
    ];
    private readonly double[] _values = [45, 62, 38];
    private readonly double[] _contrasts = [50, 50, 50];
    private readonly double[] _warmths = [4600, 4600, 4600];
    private readonly List<Button> _tiles = [];
    private int _selected = 1;
    private string _finish = "smoke";
    private int _backdrop = 2;
    private bool _night, _ready, _sync, _exporting, _refreshQueued;
    private HwndSource? _source;
    private NativeBackdrop.Result _native = new(false, null, "Waiting for native window");

    public StudyWindow()
    {
        InitializeComponent();
        BuildMap();
        _ready = true;
        SelectDisplay(1);
        SelectFinish("smoke");
        SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source?.AddHook(WindowMessage);
            ApplyBackdrop();
        };
        Closed += (_, _) => _source?.RemoveHook(WindowMessage);
    }

    private IntPtr WindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Coalesce system policy changes; never react recursively to WM_THEMECHANGED.
        if (message is 0x1A or 0x15 or 0x31E && !_refreshQueued)
        {
            _refreshQueued = true;
            Dispatcher.BeginInvoke(() => { _refreshQueued = false; SelectFinish(_finish); }, DispatcherPriority.Background);
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
        Set("Muted", dark ? "#B9C0CC" : "#565E67");
        Set("Accent", dark ? "#EDC791" : "#78531F");
        Set("AccentWash", dark ? "#242E2923" : "#1478531F");
        Set("AccentEdge", dark ? "#887C6B53" : "#6678531F");
        Set("Edge", dark ? "#33D2DBE5" : "#2A444D59");
        Set("Divider", dark ? "#21C4CEDD" : "#20535C65");
        Set("Hover", dark ? "#354F5863" : "#2447515B");
        Set("Pressed", dark ? "#60545F6C" : "#3547515B");
        Set("Track", dark ? "#FF535B67" : "#FF929BA4");
        Set("Thumb", dark ? "#F3F3F0" : "#FFFFFF");
        Set("StudyBar", dark ? "#AE171C24" : ceramic ? "#F0EEE9" : "#B9ECF0F3");
        Resources["Surface"] = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(dark ? "#DD2D323B" : ceramic ? "#FFFCFAF6" : "#E8FFFFFF"),
            (Color)ColorConverter.ConvertFromString(dark ? "#EA24282F" : ceramic ? "#FFF8F5EF" : "#EEEFF3F5"), 90);
        if (SystemParameters.HighContrast)
        {
            foreach (string key in new[] { "Ink", "Muted", "Accent", "Edge", "Divider", "Track" }) Resources[key] = SystemColors.WindowTextBrush;
            foreach (string key in new[] { "AccentWash", "Hover", "Pressed", "StudyBar", "Surface", "Thumb" }) Resources[key] = SystemColors.WindowBrush;
            Resources["AccentEdge"] = SystemColors.HighlightBrush;
        }
        SelectButton(SmokeButton, finish == "smoke");
        SelectButton(FrostButton, finish == "frost");
        SelectButton(CeramicButton, ceramic);
        PaintSelection();
        ApplyBackdrop();
    }

    private void ApplyBackdrop()
    {
        bool dark = _finish == "smoke", ceramic = _finish == "ceramic";
        _native = NativeBackdrop.Apply(this, dark, _backdrop, ceramic || _exporting);
        Resources["Foundation"] = SystemParameters.HighContrast ? SystemColors.WindowBrush : Brush(_native.Requested
            ? (dark ? "#30141A23" : "#38E9EEF3")
            : (dark ? "#20262F" : ceramic ? "#F1EEE7" : "#E2E9EF"));
        BackdropButton.Content = ceramic ? "Backdrop: Solid" : _backdrop switch { 3 => "Backdrop: Glass", 1 => "Backdrop: Solid", _ => "Backdrop: Mica" };
        BackdropButton.IsEnabled = !ceramic;
        MaterialNote.Text = _exporting
            ? "Material study · opaque reference render; native backdrop is not captured"
            : _native.Description;
    }

    private void SelectButton(Button button, bool selected)
    {
        if (selected) button.SetResourceReference(Control.BackgroundProperty, "AccentWash");
        else button.Background = Brushes.Transparent;
        button.SetResourceReference(Control.ForegroundProperty, selected ? "Accent" : "Muted");
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        AutomationProperties.SetHelpText(button, selected ? "Selected finish" : "Preview this finish");
    }

    private void BuildMap()
    {
        for (int index = 0; index < _displays.Length; index++)
        {
            int displayIndex = index;
            var display = _displays[index];
            var button = new Button { Content = (index + 1).ToString(), Width = display.Bounds.Width, Height = display.Bounds.Height, FontSize = 340, Padding = new(0), MinHeight = 0 };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(110));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(32));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Control.BorderBrushProperty, SystemColors.HighlightBrush));
            template.Triggers.Add(focus);
            button.Template = template;
            Canvas.SetLeft(button, display.Bounds.Left);
            Canvas.SetTop(button, display.Bounds.Top);
            AutomationProperties.SetName(button, $"Display {index + 1}, {display.Name}" + (display.Primary ? ", primary" : ""));
            button.Click += (_, _) => SelectDisplay(displayIndex);
            Map.Children.Add(button);
            _tiles.Add(button);
        }
    }

    private void PaintSelection()
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].SetResourceReference(Control.BackgroundProperty, i == _selected ? "AccentWash" : "StudyBar");
            _tiles[i].SetResourceReference(Control.BorderBrushProperty, i == _selected ? "Accent" : "Edge");
            _tiles[i].SetResourceReference(Control.ForegroundProperty, i == _selected ? "Accent" : "Muted");
            AutomationProperties.SetHelpText(_tiles[i], i == _selected ? "Selected display" : "Select display");
        }
    }

    private void SelectDisplay(int index)
    {
        _selected = index;
        var display = _displays[index];
        DisplayName.Text = display.Name;
        DisplayInfo.Text = display.Info;
        DisplayRole.Text = $"DISPLAY {index + 1}" + (display.Primary ? " · PRIMARY" : "");
        _sync = true;
        Brightness.Value = _night ? (display.Primary ? Math.Min(5, _values[index]) : 0) : _values[index];
        Contrast.Value = _contrasts[index];
        Warmth.Value = _warmths[index];
        _sync = false;
        PaintValues();
        PaintSelection();
    }

    private void PaintValues()
    {
        if (!_ready) return;
        BrightnessValue.Text = $"{Brightness.Value:0}%";
        BrightnessNote.Text = _night
            ? (_displays[_selected].Primary ? "Night saver preview · primary kept at no more than 5%" : "Night saver preview · this secondary display would be blanked")
            : $"Example readback {Brightness.Value:0}% · reported by display";
        ContrastValue.Text = $"{Contrast.Value:0}";
        WarmthValue.Text = $"{Warmth.Value:0} K";
    }

    private void BrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (_ready && !_sync) _values[_selected] = e.NewValue; PaintValues(); }
    private void ContrastChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (_ready && !_sync) _contrasts[_selected] = e.NewValue; PaintValues(); }
    private void WarmthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (_ready && !_sync) _warmths[_selected] = e.NewValue; PaintValues(); }
    private void NightClick(object sender, RoutedEventArgs e)
    {
        _night = !_night;
        NightButton.Content = _night ? "End Night saver" : "Night saver";
        Brightness.IsEnabled = Contrast.IsEnabled = Warmth.IsEnabled = !_night;
        DayButton.IsEnabled = EveningButton.IsEnabled = ReadingButton.IsEnabled = !_night;
        Message.Text = _night ? "Night saver preview · primary dimmed, other displays blanked. End to restore the preview." : "Preview restored · your displays are unchanged.";
        SelectDisplay(_selected);
    }
    private void Preset(double brightness, double warmth)
    { Brightness.Value = brightness; Warmth.Value = warmth; Message.Text = "Example preset applied to this preview only."; }
    private void DayClick(object sender, RoutedEventArgs e) => Preset(75, 6500);
    private void EveningClick(object sender, RoutedEventArgs e) => Preset(40, 4600);
    private void ReadingClick(object sender, RoutedEventArgs e) => Preset(15, 3000);
    private void IdentifyClick(object sender, RoutedEventArgs e) => Message.Text = $"Preview selection: display {_selected + 1}, {_displays[_selected].Name}. No overlay is shown on your displays.";
    private void RefreshClick(object sender, RoutedEventArgs e) => Message.Text = "Three example displays · this preview does not scan hardware.";
    private void DetailsClick(object sender, RoutedEventArgs e) => Details.Visibility = Details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void SmokeClick(object sender, RoutedEventArgs e) => SelectFinish("smoke");
    private void FrostClick(object sender, RoutedEventArgs e) => SelectFinish("frost");
    private void CeramicClick(object sender, RoutedEventArgs e) => SelectFinish("ceramic");
    private void BackdropClick(object sender, RoutedEventArgs e) { _backdrop = _backdrop switch { 2 => 3, 3 => 1, _ => 2 }; ApplyBackdrop(); }

    internal void VerifyAndRender(string output)
    {
        var nativeRequests = new List<object>();
        foreach (int mode in new[] { 2, 3, 1 })
        {
            _backdrop = mode; ApplyBackdrop();
            nativeRequests.Add(new { mode, _native.Requested, _native.Readback, _native.Description });
        }
        _exporting = true;
        int checks = 0;
        void Check(bool passed, string message)
        { if (!passed) throw new InvalidOperationException(message); checks++; }
        double original = _values[1];
        NightClick(this, new());
        Check(Brightness.Value == 5 && !Brightness.IsEnabled, "Night saver preview must cap primary and disable adjustments");
        foreach (string finish in new[] { "smoke", "frost", "ceramic" })
        {
            SelectFinish(finish);
            Check(_night && _selected == 1 && _values[1] == original, "Finish switch changed the preview state");
        }
        SelectDisplay(0);
        Check(Brightness.Value == 0, "Secondary preview must be blanked");
        NightClick(this, new());
        SelectDisplay(1);
        Check(Brightness.Value == original && Brightness.IsEnabled, "End Night saver must restore preview");
        _values[1] = 2;
        NightClick(this, new());
        Check(Brightness.Value == 2, "Night saver preview must never brighten primary");
        NightClick(this, new());
        _values[1] = original;
        SelectDisplay(1);
        Brightness.Value = 37;
        SelectDisplay(0); SelectDisplay(1);
        Check(Brightness.Value == 37, "Display selection must retain the example value");
        Brightness.Value = original;
        _backdrop = 2;
        Message.Text = "Preview only · your displays are unchanged.";
        foreach (string finish in new[] { "smoke", "frost", "ceramic" })
        {
            SelectFinish(finish);
            foreach (var size in new[] { (Width: 880d, Height: 790d, Name: "regular"), (Width: 660d, Height: 610d, Name: "compact") })
            {
                Width = size.Width; Height = size.Height;
                UpdateLayout();
                Check(NightButton.ActualWidth >= 148, "Night saver target lost its reserved width");
                Check(ControlSurface.ActualWidth > 500, "Control surface clipped at supported width");
                Check(Brightness.ActualWidth > 400, "Brightness control clipped");
                Check(BackdropButton.ActualWidth > 100, "Backdrop selector clipped");
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(Root.ActualWidth * 1.5), (int)Math.Ceiling(Root.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
                bitmap.Render(Root);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, $"{finish}-{size.Name}.png"));
                encoder.Save(stream);
            }
        }
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(new
        {
            checks, render = "Opaque WPF client reference, not a DWM-composited screenshot",
            hardware = "No hardware or product settings dependencies", nativeRequests,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
