namespace LumenDeck;

internal class KeyboardHelpForm : ReferenceForm
{
    public KeyboardHelpForm() : base("Keyboard controls",
            "Press F1 in the main window to open this guide. Here, Page Up / Page Down scroll the text; Home / End go to the start / end. Escape closes the guide. In the main window, these keys act on the focused control.",
        [
            new("Move between controls", "Tab moves to the next available control; Shift + Tab moves back. Press Space or Enter on a focused command button. Moving focus away before releasing cancels that press. Disabled controls are unavailable. With Ctrl or Alt held, slider adjustment keys and F2/Enter do not act."),
            new("Move between displays", "The schematic desk selector supports arrows to change selection and Enter to open controls. All displays reveals the complete card list. In the main window, Ctrl + Page Down selects the next display card; Ctrl + Page Up selects the previous one, wrapping at either end. Selection stays on the same available control when possible, otherwise brightness or warmth. The selected control scrolls into view. These keys move selection without adjusting a display."),
            new("Brightness and contrast", "Right / Up increases by one hardware unit; Left / Down decreases by one. Hold Shift for ten units. Home selects the minimum; End selects the maximum. Page Up / Page Down make larger adjustments. Brightness's usual readout is a mapped percentage; exact entry uses the hardware range."),
            new("Colour warmth", "Right / Up requests warmer colour, 50 kelvin lower. Left / Down requests cooler colour, 50 kelvin higher. Hold Shift for 500 kelvin. Home requests neutral 6500 K; End requests the warmest 3000 K. The number describes a requested tint, not a measured colour temperature."),
            new("Enter an exact value", "Focus a slider and press F2 or Enter, or click its number. The dialog shows the display/control, unit and range. Brightness and contrast use hardware units; warmth accepts kelvin. Apply sends an adjustment request: check the screen and card warnings. Cancel or Escape discards the draft."),
            new("Keep your place after Refresh", "Refresh returns selection to the same display/control when it is still available and scrolls it into view. Choosing another control during reading takes priority. A removed display or unavailable control returns selection to Refresh."),
            new("Screen blackout shortcuts", "Choose Set shortcut, or the current shortcut label, on a monitor card to configure that display's blackout shortcut. There is no built-in global blackout shortcut. A saved shortcut can still fail Windows registration; check the status message. Use Restore screen on its card or click the black overlay to restore it."),
        ]) { }
}
