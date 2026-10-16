# Night saver: entry, low brightness and recovery

The frequent action stays first in the toolbar, with the tray as a second entry.
The active label is **End Night saver** and uses the existing accent style. This
names the mode being ended without confusing it with the **Night** reading preset
or Windows sleep. The button reserves its longer label at the current DPI; changing
state does not move adjacent commands. No new icon or settings schema is needed.

**Desk actions > Keep working on** selects the retained display. The remembered
choice wins; otherwise the primary display is selected, then the first available
display. The selector is locked during a session. Opening Night saver does not
expand that section. Its target is visible in the expanded options and its selected
display is named in button/tray help.

The work display's target is `min + floor((max - min) * 0.05)`, using its reported
brightness range. Examples: 0-100 requests 5, 0-255 requests 12, and 20-220 requests
30. Already lower values are left alone. This fixed cap is deliberately separate
from the Night preset's estimated luminance and editable reading preferences.
A hardware setting is not measured light: no absolute luminance or darkness is
promised, and no additional gamma or work-screen overlay is introduced.

Original raw brightness is persisted before dimming. Ending or exiting restores
that original; disconnected or failed restores remain owed. Interrupted sessions
recover on discovery. A manual slider, preset or Custom request takes over and
ending Night saver leaves that newer choice intact. Automatic preset and Custom
requests skip the dimmed work display, including warmth, and retain its recovery
snapshot. A consumed schedule period is not replayed when Night saver ends.
Displays blanked separately retain their own restore ownership.

Start status says the brightness was **requested**, since worker writes are
asynchronous. Existing card observations distinguish confirmation and failure.
Unsupported work displays stay unchanged and produce a warning; failed snapshot
saves refuse the dim. Single-display behavior is unchanged: the command is disabled.

Regression evidence covers persisted-before-write ordering, never-brighten behavior,
nonzero floors and 0-255 ranges, refresh, manual takeover, automatic schedule
protection, exit/restart recovery and failed/disconnected restoration. Native UI
checks cover collapsed start/end visibility, stable toolbar height/width, tray
parity, minimum windows, screenshot-sized controls and mixed-DPI round trips.
These synthetic checks do not replace physical-panel darkness acceptance.
