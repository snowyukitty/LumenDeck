using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckFeatureAccessibility()
    {
        foreach (string position in new[] { "left", "right", "", " " })
        {
            using var monitor = Display("feature-accessibility-" + position);
            monitor.Description = "Studio & Desk";
            monitor.PositionLabel = position;
            monitor.Features.AddRange(new[]
            {
                new VcpFeature { Definition = VcpCatalog.Find(0x62), Current = 20, Max = 100 },
                new VcpFeature { Definition = VcpCatalog.Find(0xDC), Current = 0, AllowedValues = new() { 0, 1 } },
                new VcpFeature { Definition = VcpCatalog.Find(0x60), Current = 0x11, AllowedValues = new() { 0x11, 0x12 } },
                new VcpFeature { Definition = VcpCatalog.Find(0x04) },
                new VcpFeature { Definition = new VcpCatalog.Definition(0x87, "Gain & trim", VcpCatalog.Kind.Continuous,
                    Description: null, Risky: true), Current = 10, Max = 100 },
            });
            var transport = new FakeDdcTransport();
            using var worker = new DdcWorker(transport);
            using var form = new PreviewForm { ClientSize = new Size(600, 500) };
            using var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            using var tips = new ToolTip();
            using var nativeSelector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            string selectorShortcut = nativeSelector.AccessibilityObject.KeyboardShortcut;
            int reports = 0;
            int row = FeatureControls.Build(grid, 0, monitor, worker, _ => reports++, tips);
            form.Controls.Add(grid);
            form.Show();
            string context = string.IsNullOrWhiteSpace(position) ? monitor.DisplayName : $"{monitor.DisplayName} ({position})";
            Assert(row == 5, "advertised extra-control rows are retained across accessible presentation changes");
            for (int i = 0; i < monitor.Features.Count; i++)
            {
                var feature = monitor.Features[i];
                var editor = grid.GetControlFromPosition(1, i);
                var caption = (Label)grid.GetControlFromPosition(0, i);
                string action = feature.Definition.Kind == VcpCatalog.Kind.Action ? "Run " : "";
                Assert(editor.AccessibilityObject.Name == $"{context}: {action}{feature.Name}",
                    "each native editor exposes literal display, optional position and its actual feature");
                Assert(caption.Text == feature.Name && caption.AccessibilityObject.Name == $"{context}: {feature.Name}" &&
                    !caption.UseMnemonic, "feature captions retain literal feature names without advertising mnemonics");
                Assert(string.IsNullOrEmpty(caption.AccessibilityObject.KeyboardShortcut),
                    $"literal caption {feature.Name} exposes no phantom Alt mnemonic; actual '{caption.AccessibilityObject.KeyboardShortcut}'");
                Assert(editor is ComboBox
                    ? editor.AccessibilityObject.KeyboardShortcut == selectorShortcut
                    : string.IsNullOrEmpty(editor.AccessibilityObject.KeyboardShortcut),
                    $"literal names preserve native shortcuts for {feature.Name}; actual '{editor.AccessibilityObject.KeyboardShortcut}', selector baseline '{selectorShortcut}'");
                string description = editor.AccessibilityObject.Description ?? "";
                Assert(string.IsNullOrEmpty(feature.Definition.Description) || description.Contains(feature.Definition.Description),
                    "native editor descriptions include available catalog guidance");
                Assert(description.Contains("confirmation") == (feature.Definition.Kind == VcpCatalog.Kind.Action ||
                    feature.Definition.Kind == VcpCatalog.Kind.Select && feature.Definition.Risky),
                    "confirmation descriptions follow existing risky selection and action behavior");
            }
            var volume = (Slider)grid.GetControlFromPosition(1, 0);
            var picture = (ComboBox)grid.GetControlFromPosition(1, 1);
            var input = (ComboBox)grid.GetControlFromPosition(1, 2);
            var factory = (Button)grid.GetControlFromPosition(1, 3);
            Assert(picture.AccessibilityObject.Role == AccessibleRole.ComboBox && factory.AccessibilityObject.Role == AccessibleRole.PushButton,
                "native selector and action roles remain available");
            Assert(picture.SelectedIndex == 0 && input.SelectedIndex == 0 && volume.Value == 20,
                "accessible metadata preserves existing selected and slider values");
            Assert(factory.Text == "Run" && factory.AccessibilityObject.Description.Contains("cannot be undone"),
                "one-shot actions keep their visible label and expose existing reversal limits");
            Assert(grid.GetControlFromPosition(1, 4).AccessibleDescription == "" &&
                grid.GetControlFromPosition(1, 4).AccessibilityObject.Description.Contains("range"),
                "features without catalog guidance retain native slider range information without invented help");
            Assert(worker.Idle && transport.Writes.IsEmpty && reports == 0,
                "construction and reading all extra-control accessibility properties send no writes or reports");

            // Only the safe picture-mode selector and continuous volume are activated.
            // Input switching and reset actions retain their existing dialogs, uninvoked.
            picture.SelectedIndex = 1;
            WaitFeatureWriter(worker);
            Assert(transport.Writes.Count == 1 && transport.Writes.TryPeek(out var selection) &&
                ReferenceEquals(selection.Monitor, monitor) && selection.Code == 0xDC && selection.Value == 1 && reports == 1,
                "safe selector changes still reach the correct fake display and advertised feature once");
            volume.SetValueSilently(21);
            Assert(transport.Writes.Count == 1, "silent slider synchronization sends no extra-control request");
            typeof(Slider).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(volume, new object[] { new KeyEventArgs(Keys.Right) });
            WaitFeatureWriter(worker);
            Assert(transport.Writes.Count == 2 && transport.Writes.Last().Code == 0x62 &&
                transport.Writes.Last().Value == 22 && ReferenceEquals(transport.Writes.Last().Monitor, monitor),
                "continuous extra-control changes retain their fake transport target");
            Assert(picture.AccessibilityObject.Name == $"{context}: Picture mode" &&
                volume.AccessibilityObject.Name == $"{context}: Speaker volume",
                "value changes preserve stable display-and-feature accessible names");
        }
    }

    private static void WaitFeatureWriter(DdcWorker worker)
    {
        Assert(SpinWait.SpinUntil(() => worker.Idle, 3000), "fake feature request settles within the test deadline");
    }
}
