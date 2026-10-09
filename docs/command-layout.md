# Command layout contract

`DeskCommandBar` presents commands without monitor or settings I/O.
`CommandSection` assigns its available width and grows vertically when its
`FlowLayoutPanel` wraps. `FlatButton` supports opt-in automatic sizing; existing
card/dialog hosts keep their explicit dimensions.

## Minimum size and row proposals

Capture the intended Size as MinimumSize before enabling AutoSize. Preferred
width is `max(minimum, min(natural, available))`. It therefore remains at least
the minimum for every proposal, including a positive width of one pixel. A row
narrower than that floor cannot contain the button; hosts must provide enough
space for their declared minima. The application's minimum window width does.

A zero proposal is unbounded. The tagged
[WinForms 10.0.11 FlowLayout implementation](https://raw.githubusercontent.com/dotnet/winforms/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Layout/FlowLayout.cs)
constrains the first item of a row and measures later items unbounded before
deciding whether they belong on a new row. Wrapped label height follows the
constrained content width. Preferred-size queries only read control state and
measure text; they do not set bounds, create a control handle or request layout.

## Shared measurement and paint space

`TextInsets` adds the caller's Padding to the automatically sized button's
device-scaled left/right 10 and top/bottom 6 pixel insets. Both paths consume
these same side values, so rounding a total logical inset separately cannot
introduce a different content width at fractional scaling factors.

The face rectangle has width `Width - 1`. Paint content width is therefore
`Width - 1 - insets.Horizontal`. Measurement subtracts
`insets.Horizontal + 1` from preferred width, which is the same expression.
The equivalent identity applies to height. Both calls use WordBreak and
NoPrefix, including TextRenderer's own glyph padding. Text remains inside the
automatic insets; the focus stroke sits three pixels inside the face and does
not add another text inset. Manually sized buttons respect explicit Padding
without adding the automatic insets. Empty paint space draws no label.

## Property changes and literal names

Text/Font changes request parent layout after base notifications. The tagged
[Control.PerformLayout implementation](https://raw.githubusercontent.com/dotnet/winforms/v10.0.11/src/System.Windows.Forms/System/Windows/Forms/Control.cs)
dispatches OnLayout with framework layout suspension; it does not assign Text
or Font. Event subscribers that independently mutate those properties remain
the host's responsibility. Native tests check that explicit parent layout after
a text change introduces no extra TextChanged/FontChanged events.

Literal ampersands appear in both text and the default accessible name.
AccessibleName overrides the live Text, including an explicit empty string.
These buttons expose no Alt mnemonic shortcut; their keyboard activation is
Space/Enter, with the command-specific guidance supplied by the host.

## Verification limits

Focused `--commands-only` checks cover constrained/unbounded repeated measure,
minimum floors, no handle/bounds mutation, late text/font/padding changes and
restoration, row containment, literal/explicit names and fixed hosts. Native
geometry/font simulations cover 100/150/200%; optional PNG output permits
visual inspection. These are caller tests, not independently executed reviewer
tests. Physical DPI transitions, reader announcements and real monitor behavior
remain separate acceptance work.
