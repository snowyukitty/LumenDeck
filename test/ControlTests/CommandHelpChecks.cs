using LumenDeck;

internal static partial class Program
{
    private static void CheckCommandHelp()
    {
        using var bar = new DeskCommandBar();
        int requests = 0;
        bar.LevelPicked += _ => requests++;
        bar.CustomPicked += () => requests++;
        var commands = Descendants(bar).OfType<FlatButton>().ToArray();
        foreach (var command in commands)
            Assert(!string.IsNullOrWhiteSpace(command.AccessibilityObject.Description),
                $"desk command {command.Text} exposes its purpose to accessibility clients");
        Assert(bar.ModeButtons["Day"].AccessibilityObject.Description.Contains("estimated"),
            "accessible desk preset help identifies an estimated target");
        var custom = bar.ModeButtons[Presets.CustomName].AccessibilityObject;
        Assert(custom.Description.Contains("background") && custom.Description.Contains("Settings"),
            "accessible Custom help distinguishes remembered levels from save confirmation");
        Assert(bar.RefreshButton.AccessibilityObject.Description.Contains("warmth")
            && bar.RefreshButton.AccessibilityObject.Description.Contains("brightness"),
            "Refresh explains saved warmth and blackout brightness side effects");
        foreach (bool active in new[] { true, false, true })
        {
            bar.SetNightSaverState(active);
            string help = bar.NightButton.AccessibilityObject.Description;
            Assert(bar.NightButton.Text == (active ? "Restore night desk" : "Night saver")
                && bar.NightButton.Primary == active,
                "night command label and emphasis follow its active session");
            Assert(help.Contains(active ? "pending" : "only where supported") && help.Contains("separate"),
                "night explanation follows restoration state, capability and session ownership");
        }
        bar.Enabled = false;
        Assert(commands.All(c => !string.IsNullOrWhiteSpace(c.AccessibilityObject.Description)),
            "disabled desk commands retain their accessible explanations");

        using var monitor = Display("command-help-only");
        monitor.Description = "Example Studio & Portrait";
        monitor.PositionLabel = "Right (portrait)";
        var transport = new FakeDdcTransport();
        using var worker = new DdcWorker(transport);
        using var card = new MonitorCard(monitor, null, worker,
            (_, _) => requests++, _ => requests++, _ => requests++, _ => requests++, _ => requests++);
        var presets = Descendants(card).OfType<FlatButton>()
            .Where(b => b.Name.StartsWith("preset-", StringComparison.Ordinal)).ToArray();
        Assert(presets.Length == Presets.Levels.Length + 1,
            "accessible command fixture includes all card presets and Custom");
        foreach (var command in presets)
        {
            var accessible = command.AccessibilityObject;
            Assert(accessible.Name.Contains(monitor.DisplayName) && accessible.Name.Contains(monitor.PositionLabel),
                $"card command {command.Text} identifies its display and position without losing the ampersand");
            Assert(!string.IsNullOrWhiteSpace(accessible.Description),
                $"card command {command.Text} exposes its scope and purpose to accessibility clients");
        }
        var cardCustom = presets.Single(b => b.Name == "preset-custom").AccessibilityObject;
        Assert(cardCustom.Description.Contains("background") && cardCustom.Description.Contains("Settings"),
            "accessible card Custom help includes background-save guidance");
        var blank = Descendants(card).OfType<FlatButton>().Single(b => b.Name == "screen-blank");
        var shortcut = Descendants(card).OfType<LinkLabel>().Single(b => b.Name == "screen-shortcut");
        foreach (bool supportsBrightness in new[] { false, true })
        foreach (bool blanked in new[] { false, true, false })
        {
            // Change only the fixture capability after construction: this does
            // not run brightness estimation or query profile storage.
            monitor.SupportsBrightness = supportsBrightness;
            card.SetScreenBlankState(blanked);
            string action = blanked ? "Restore screen" : "Blank screen";
            Assert(blank.AccessibilityObject.Name == $"{monitor.DisplayName} ({monitor.PositionLabel}): {action}",
                "screen command accessible name follows its live action and literal display context");
            Assert(blank.AccessibilityObject.Description.StartsWith(blanked ? "Close the blackout" : "Temporarily cover",
                StringComparison.Ordinal),
                "screen command accessible explanation follows its live recovery state");
            string description = blank.AccessibilityObject.Description;
            Assert(supportsBrightness
                ? description.Contains(blanked ? "any saved brightness recovery" : "lowest supported brightness")
                : description.Contains("brightness control is unavailable")
                    && !description.Contains("exact brightness") && !description.Contains("lowest supported brightness"),
                "screen help distinguishes brightness-capable and overlay-only presentation");
            Assert(blank.Enabled && shortcut.Enabled,
                "screen recovery and shortcut configuration remain enabled in either state");
        }
        foreach (string binding in new[] { "Ctrl+Alt+Shift+F12", "   ", null })
        {
            card.SetScreenBlankShortcutText(binding);
            Assert(shortcut.AccessibilityObject.Name.Contains(monitor.DisplayName)
                && shortcut.AccessibilityObject.Name.Contains(monitor.PositionLabel)
                && shortcut.AccessibilityObject.Name.Contains("shortcut"),
                "shortcut link identifies its configuration action and literal display context");
            Assert(shortcut.AccessibilityObject.Description.Contains(string.IsNullOrWhiteSpace(binding)
                ? "No shortcut" : binding),
                "shortcut explanation distinguishes assigned and unassigned bindings");
        }
        using var unnamedPosition = Display("command-help-no-position");
        unnamedPosition.Description = monitor.Description;
        unnamedPosition.PositionLabel = " ";
        using var secondCard = new MonitorCard(unnamedPosition, null, worker,
            (_, _) => requests++, _ => requests++, _ => requests++, _ => requests++, _ => requests++);
        var secondBlank = Descendants(secondCard).OfType<FlatButton>().Single(b => b.Name == "screen-blank");
        Assert(secondBlank.AccessibilityObject.Name == $"{monitor.DisplayName}: Blank screen",
            "screen command omits empty position punctuation while preserving the literal name");
        Assert(secondBlank.AccessibilityObject.Name != blank.AccessibilityObject.Name,
            "same-name displays with different position context have distinct screen command names");
        Assert(requests == 0 && transport.Writes.IsEmpty && worker.Idle,
            "constructing and reading command help dispatches no actions or hardware writes");
    }
}
