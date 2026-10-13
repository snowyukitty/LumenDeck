# Accessibility behavior and verification

Everyday slider names include the same display name and position shown on the
card. This distinguishes identical monitor models. Brightness exposes both raw
hardware units and the displayed mapped percentage; contrast exposes its raw
hardware value. The description includes the hardware range and exact-entry
keyboard instructions.

Warmth uses an internal increasing warmth scale, but exposes Kelvin values and
sorted 3000–6500 K bounds matching exact entry. Right/Up moves toward warmer
colour (lower Kelvin); Left/Down moves toward cooler colour (higher Kelvin).
Shift changes 500 K, Home requests neutral 6500 K and End requests 3000 K. This
describes a requested tint, not a measured colour temperature.

Slider value changes notify assistive clients, including programmatic
synchronization. Synchronization still raises no adjustment callback; readers
do not cause another monitor request. Unchanged effective values do not emit
value-change events. Sliders expose Enter exact value as their default accessible action. No automation
value setter or UIA RangeValue pattern is added. Accessible descriptions alone do not establish screen-reader acceptance.

## Command activation

Desk commands and per-card presets expose their hover explanations through
`AccessibleDescription`, using the shared FlatButton.SetHelp presentation path.
Custom explains remembered manual levels and background-save status. Per-card
preset accessible names include the display name and position, preserving literal
ampersands so identical command labels can be distinguished. Native accessible
object checks verify these properties and that reading them dispatches no command
or transport write. This does not establish Narrator announcement behavior.

Per-display Blank screen/Restore screen names also include the display and
position. Their descriptions follow the current blank/restore presentation.
The shortcut link identifies its configuration action and describes the saved
binding or absence of one; a saved binding does not confirm Windows registered it.
Command-help checks cover both state directions, assigned/cleared shortcuts and
missing position labels without activating a blackout or shortcut.
Profile-editing links, unavailable-controls help and adjustment-notice help/dismiss
links also identify their originating display and position. The notice's alert
group and message label expose that context along with the complete warning;
dismissal clears their hidden warning names. Help still opens the shared offline
guide. Extra-control disclosure names follow the visible loading, empty,
show/hide and absent states through one presentation method, preserving literal
ampersands without inventing Alt shortcuts. Native accessible-object checks cover
same-model displays, missing positions, retained callbacks and zero fake transport
writes. These checks re-query properties; live reader announcements remain unrun.

Expanded extra controls apply the same display/optional-position context to
sliders, selectors, action buttons and captions. Action names include the actual
operation after Run. Descriptions expose catalog guidance, existing risky-selector
confirmation and action reversal limits. They do not add confirmation to a
continuous control. Literal display/feature ampersands stay intact; missing or
whitespace-only positions do not create empty parentheses. Sliders explicitly
report no Alt mnemonic, avoiding the native default's phantom shortcut from a
literal ampersand in their name. Direction-key adjustment remains unchanged.
Native checks preserve selector shortcut metadata and verify safe selector/slider
requests against a fake transport. Risky input switching and reset actions are
not activated by these checks; their existing dialog handlers are source-reviewed.

When hardware brightness is unavailable, help describes the blackout and leaves
any pending brightness recovery conditional on working monitor controls. It does
not promise a brightness change. These are re-queryable properties; live native
name/description-change delivery to a focused reader remains unverified.
Recovery help also treats a saved brightness snapshot as conditional, even when
controls are currently available. An existing overlay-only blackout stays
overlay-only across Refresh; restoring it does not change newly discovered
brightness controls. A later blackout can capture and use those controls.

Pending brightness recovery also exposes the card's persistent adjustment notice.
Startup with an unresolved recovery marker, failed restore notifications and
failed Restore screen retries keep that warning visible. An unrelated status
cannot erase it. A confirmed retry updates the command and clears the recovery
marker; on the same card, the earlier unconfirmed-adjustment notice remains until
dismissed, as with other adjustment notices. Refresh disposes that card and
reconstructs its notice from unresolved recovery state, so a confirmed Refresh
recovery creates a card without that warning. Dismissal does not retry or clear
recovery data.

Failed blackout recovery status names Restore screen and Refresh as retry actions,
without promising automatic polling or assuming DDC/CI for laptop panels. Offline
Connection help explains why the overlay can disappear before brightness is
restored, how saved recovery survives dismissal, and which controls to check.

The bottom status stripe contains a native read-only multiline text reader.
Long messages wrap and remain available through vertical scrolling rather than
being replaced by ellipsis. Tab can select the reader; arrow keys and selection
use the native text control, including Ctrl+C when the user chooses to copy.
Updates preserve the current command selection; warning and routine messages
keep distinct colours.
The fixed footer height preserves the card viewport. Native tests cover logical
tab traversal, read-only character/Delete messages, full accessible text and
scrolling to both ends at 100/150/200% font/geometry simulations. Foreground
keyboard delivery, physical DPI changes and live reader announcements remain
unverified.

Disabled command buttons share the Card surface, InkFaint label and Line border,
regardless of primary styling or hover. Native bitmap checks cover 100/150/200%
font and size simulations; visual inspection confirms the formerly dark primary
label is readable. This does not establish accessibility conformance or physical
mixed-DPI acceptance. Disabled activation is checked separately from appearance.

Desk command buttons opt into text-based automatic sizing with their original
dimensions as a minimum. Long labels move to a new row, and labels wider than
that row wrap inside a taller button. Measurement and paint share literal text
and wrapping flags; manual card/dialog hosts retain their specified dimensions.
Explicit AccessibleName values still override the full live Text. Literal `&`
is retained and does not advertise an Alt mnemonic. The tagged
[WinForms accessible-name implementation](https://raw.githubusercontent.com/dotnet/winforms/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Accessibility/Control.ControlAccessibleObject.cs)
otherwise removes mnemonic markers and derives an Alt shortcut from them.
The sizing implementation follows the tagged
[FlowLayout measurement contract](https://raw.githubusercontent.com/dotnet/winforms/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Layout/FlowLayout.cs),
which constrains the first item on a row and measures later items unbounded
before deciding whether they need a new row. Preferred-size queries neither
modify bounds nor request another layout.

Measurement and paint now consume the same side-scaled TextInsets, including
caller Padding, rather than scaling a total inset separately. Explicitly sized
buttons also respect their own Padding. See [the command layout contract](command-layout.md)
for the exact content-box identity and native verification limits.

Shared command buttons expose the PushButton role and Press action. Space and
Enter arm a press on key-down and dispatch once on a matching key-up; holding a
key does not dispatch repeated commands. A release without a preceding press
does nothing. Focus loss or an enabled-state change cancels the armed press.
Native cancel-mode messages also clear the press. Ctrl/Alt chords do not arm it;
Shift permits ordinary activation.

MSAA Pressed follows the enabled, live button's momentary painted press. It
clears on release/cancellation and before disposal destroys the native handle;
disposed buttons expose Unavailable. Other base state flags are preserved.
This adds no Checked/Selected state and does not claim a preset or adjustment
succeeded. A central press setter sends StateChange for changed pressed values
through ControlAccessibleObject.NotifyClients when a native handle exists.
Repeats do not resend this notification or create a native handle.
Disposal marks the command unavailable before notifying, and nested click
dispatch is refused while a click is in flight. Independent later clicks work.

These semantics follow the [MSAA push-button state definition](https://learn.microsoft.com/en-us/windows/win32/winauto/push-button)
and [object-state notification guidance](https://github.com/MicrosoftDocs/win32/blob/docs/desktop-src/WinAuto/object-state-constants.md).

KeyDown/KeyUp subscribers run before local keyboard processing. Handled or
suppressed input, or a subscriber disabling/disposing the button, cancels both
the armed key and painted press. Its later orphan release stays inert; a fresh
gesture works after interception ends. A subscriber can issue a direct click
and mark its event handled without a second click from local key-up processing.
This keyboard ownership rule does not change mouse or accessible click dispatch.

Mouse, direct and accessible activation share a click guard. Disabled (including
inherited disabled state), disposing or disposed buttons cannot dispatch their
command. A retained accessible object therefore cannot activate a disposed
button. A successful click consumes an armed keyboard press before callbacks,
so an interleaved MSAA/direct action and later key-up cannot double-dispatch.
A different activation key cannot complete the first key's press.

MouseEventArgs click dispatch accepts only MouseButtons.Left. Right, middle and
auxiliary buttons cannot execute a shared command; direct/keyboard/MSAA actions
use their existing non-mouse event path. WinForms 10.0.11's
[WmMouseUp](https://raw.githubusercontent.com/dotnet/winforms/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Control.cs)
passes the originating button in MouseEventArgs before OnMouseUp, so the command
guard enforces the primary-button policy. Callback tests cover this boundary;
they do not establish native hit testing or physical pointer delivery.

Handled activation events suppress character processing. The tagged
[WinForms key-event implementation](https://github.com/dotnet/winforms/blob/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Control.cs)
shows that this removes pending character messages rather than the matching
key-up message. These control checks synthesize events; they do not establish
user-driven native key routing, input-method or screen-reader acceptance.

## Offline keyboard reference

Slider keyboard processing first raises KeyDown for subscribers, then stops if
the event is handled/suppressed or the control has been disabled/disposed.
Ctrl/Alt combinations request neither a slider adjustment nor an exact-entry
draft, without claiming the event. Ordinary keys and Shift adjustments retain
their behavior; main-window command routing still runs before slider input.
This prevents an extra modifier on a navigation shortcut from changing a value
and lets future host commands own an event without duplicate local actions.
Wheel and pointer behavior are unchanged by this keyboard policy.
The event uses the supplied [modifier flags](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.keyeventargs.modifiers?view=windowsdesktop-10.0);
[SuppressKeyPress](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.keyeventargs.suppresskeypress?view=windowsdesktop-10.0)
also sets Handled, while the local guard checks both flags after subscribers run.

Local F1 in the main window and Settings > Keyboard controls open the same owned
modeless window. Opening is rejected for hidden, minimized, disabled/modal or
closing owners. A live reference is revealed rather than duplicated; closing
disposes it and the next request creates a new form. Owner closure also disposes
the reference. Opening it explicitly cancels pending scan-selection recovery,
including F1, which is processed before ordinary control key-down events.

ReferenceView renders a title, introduction and ReferenceSection heading/body
records without monitor or settings collaborators. New offline reference content
can reuse this wrapping, vertically scrolling presentation. KeyboardHelpForm
scopes Page Up/Down and Home/End to reference scrolling; Escape closes the form.
The native Close button stays outside the scrolling content. Static text remains
available to accessible clients. Content distinguishes hardware units, requested
Kelvin directions, opening an exact-entry draft, Apply's request and separately
configured blackout shortcuts. There is no demonstration that changes a display.

AppTests exercise the actual Settings and local command-key paths with a quiet
off-desktop reference factory. Repeat reveal is instrumented instead of calling
native Activate, to avoid foreground takeover in tests. They cover reuse,
recreation, unavailable owners, reading with help open/closed, owner disposal and
terminal late scan completion. Snapshot/counter checks observe no guide-driven
settings, scan, hardware/gamma or registration requests. Minimum and regular-size
start/end artifacts are rendered; bounds checks cover wrapping, final-section
reachability and the persistent Close action. Native user-driven F1/Tab routing,
repeat foreground reveal, mixed DPI and reader announcements remain acceptance work.

## Selection across display scans

MainForm consumes exact Ctrl + Page Down / Page Up command keys to select the
next / previous visible display card in layout order, including when navigation
is rejected. A unique selectable control with the same Name is preferred;
brightness then warmth are fallbacks. Toolbar selection enters the first / last
card, and a single card retains its current selectable control. Selection scrolls
the control, highlights the map and reports display index/name/position. Hidden,
minimized, disabled, closing or preferences-popup owners cannot navigate. During
reading, the command cancels recovery but does not move selection. Generation
and card ownership are rechecked after selection callbacks before scrolling.
Guide-local Page keys keep their reference meaning. No global hotkey is added.

Control.ProcessCmdKey bubbles to parents before slider input-key dispatch in
[WinForms 10.0.11 Control.cs](https://raw.githubusercontent.com/dotnet/winforms/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Control.cs).
AppTests invoke this parent route with explicit key data on isolated native
controls. They do not inject physical keys or establish end-to-end native queue
routing, mixed-DPI behavior or screen-reader announcements.

Before replacing cards, MainForm captures its logical ActiveControl. Card
targets use monitor StableKey and an explicit, unique Control.Name; display
captions, positions and card order do not identify them. Daily sliders, profile,
blank/shortcut, preset and disclosure controls have names. Future card controls
should supply a unique stable name. Advanced controls are not opened or loaded
to restore selection; unnamed or absent targets fall back to Refresh.

A removed or temporarily disabled selection is parked on persistent Connection
help. Surviving enabled controls remain selected. Enter, click, key-down and
mouse-down events after parking permanently cancel recovery, even if selection
later returns to help. A newer scan can inherit only an unclaimed parked record;
obsolete completion cannot replace current selection or remove its tracking.
Completion resolves the target only for the latest scan, unchanged visibility,
enabled/visible owner and unchanged parking selection. Owned visible windows
prevent recovery; hide/show invalidates the record, and a currently minimized or
closed owner cannot recover it.
Missing, ambiguous or unavailable targets use a selectable Refresh fallback.

Tracking is detached before selection callbacks. Reentrant changes of generation
or card ownership prevent scrolling an obsolete card. Recovery scrolls the
selected control into view so lower actions remain visible in short windows.
It assigns ActiveControl without calling Activate or requesting foreground ownership. The
tagged [WinForms ActiveControl implementation](https://github.com/dotnet/winforms/blob/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/ContainerControl.cs)
updates native child focus only when the ancestor already contains focus.

AppTests use real MainForm instances, native handles and synthetic displays with
identical captions and reversed enumeration order. They exercise logical
ActiveControl, scan barriers, cancellation history, help, missing/unsupported
targets, failure and overlapping completion. The off-desktop owner remains
outside the foreground. These checks do not inject physical keyboard input or
prove user-driven Tab traversal or reader announcements after Refresh.

## Notification path

The app uses the public ControlAccessibleObject.NotifyClients path for custom
slider value events and status name events. This avoids the reversed switch
guard in Control.AccessibilityNotifyClients shipped in .NET 10.0.11, without
changing global runtime switches or adding native notification P/Invoke in the
app. The accessible-object path still honors disabled client notifications.

The runtime-specific defect and corrected implementation are documented in
[WinForms issue 15176](https://github.com/dotnet/winforms/issues/15176).
The [10.0.11 Control source](https://github.com/dotnet/winforms/blob/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Control.cs)
and [accessible-object source](https://github.com/dotnet/winforms/blob/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Accessibility/Control.ControlAccessibleObject.cs)
describe the two paths. Do not set NoClientNotifications merely to make the
broken Control path run.

## Exact numeric entry

The reusable exact-entry dialog places the display/control name, explicit unit
and converted bounds in its body. Long target names wrap; the content scrolls
vertically when needed, while Apply and Cancel remain in a separate action row.
The numeric input has a target-specific accessible name and range description.
Apply sends an adjustment request; the note directs users to the screen and
card warnings rather than promising hardware success. Cancel and Escape discard
the draft. An unchanged accepted value sends no adjustment callback.

The slider latches cancellation when disabled or disposed, including during
dialog construction. Refresh disposing a card closes its draft. Bounds and
conversion delegate identity are checked again after the modal returns; a
changed target cannot dispatch the accepted draft. Conversion delegates must
have immutable semantics: identity checks cannot detect a mutated captured
closure. Current card bindings use a fixed monitor and pure conversions; a
future rebinding implementation must explicitly invalidate any open editor.

## Evidence and limits

AppTests checks actual card accessible objects with synthetic models and
isolated settings. A scoped current-process WinEvent hook observes delivery for
the expected window, OBJID_CLIENT and CHILDID_SELF. It checks sync without
adjustment callbacks, unchanged-value silence, standalone keyboard notifications
and status name notifications. Native hook registration and cleanup are verified.
The suite stops its isolated debounce timer during this check so unrelated
automatic checkpoints cannot be mistaken for a notification side effect.

Exact-entry checks run real modal forms on an isolated off-desktop owner with
fake transport and temporary settings. They cover Apply, unchanged entry,
Cancel, the Escape dialog-key path, Kelvin conversion, disabled/disposed targets,
changed bounds/converters and Refresh replacing a card. A rendered long-name
case checks wrapping and button reachability at the minimum size.

These checks do not operate physical monitors, change registration, copy data,
or run a screen reader. Narrator/NVDA/JAWS, mixed DPI, user-driven dialog keyboard
acceptance and complete Tab navigation remain acceptance work. Native events reaching a hook
do not prove that every assistive client resolves or announces them correctly.

## Desk selector

The schematic desk is a keyboard-focusable accessible list. Arrow keys change the
selected display while retaining selector focus; Enter opens its principal control.
Each item exposes its display name, position, selected state, screen bounds and
Select display action. All displays is a native checkbox. Stale items retain the old
identity and become unavailable after discovery replaces it. The selected outline,
written marker and card identity do not depend on colour alone.

The contrast palette uses Windows system colours. Fixture checks inspect native
accessible objects and render system-colour surfaces without changing the owner's
Windows settings. Narrator announcements and actual contrast-theme switching remain
manual acceptance. These checks do not establish a complete accessibility audit.
