# Native desk remote

The primary sequence is screen identity, supported adjustment, observed result,
and recovery. The native WinForms architecture, Core/GUI/CLI boundary and tray
remain intact. Existing brand assets are unchanged.

## Hierarchy and reflow

The default shows one selected MonitorCard. The selector applies one uniform scale
and translation to Windows virtual-desktop rectangles. It preserves negative
coordinates, adjacency, vertical overlap and pixel-resolution proportions. Small
vertical offsets must never become separate rows. This is Windows desktop geometry,
not a measurement of the screens' physical sizes or emitted light.
Selection has a strong outline; compact tiles show only their number. Larger tiles
can also show SELECTED and the name. The caption and control header retain the
complete identity without forcing oversized tiles into the working area.
A vertically stacked desk uses a 220-DIP side selector when the window has room,
and returns above the detail at compact sizes. The selector never vanishes merely
to make the window fit. The brand header yields first.

All displays reveals the same cards, preserving an efficient multi-display workflow.
Entering a card synchronizes the map selection; returning to the single-display
view keeps that screen and control. Spatial arrows prefer a neighbour with overlap
on the perpendicular axis, then the nearest centre in the requested half-plane.
Ties use displayed order. Edges stop; Ctrl+Page Up/Down remains the wrapping route.
Home/End use layout order. Arrow selection never transiently enters a slider;
Enter/Space or a pointer click explicitly opens the selected screen's controls.
Ctrl+Page Up/Down navigates across every card in either mode. Selection survives
rebuilds by StableKey, while callbacks and observations remain bound to the exact
Monitor object for that discovery generation. Background completion does not focus
a control or activate the window.

Brightness is the initial adjustment target. Unsupported brightness stays explained;
warmth becomes the target instead. All adjustments share a compact 30-DIP row and
the same value typography. Readouts reserve 72 DIP and measure endpoint strings,
including Kelvin, rather than enlarging the brightness percentage. Contrast,
warmth, profile editing, per-display presets and blackout stay
accessible below. Extra advertised controls retain their existing disclosure.
The header repeats name, position and capability summary. Estimated nits remain
secondary and explicitly estimated. Warmth is a gamma tint target, never a measured
colour preview.

Night saver is first in the always-visible toolbar. The same position becomes
End Night saver while active, with space reserved for the longer label. Identify,
Refresh, Help and Settings remain visible. Desk actions expands desk-wide presets,
warmth restoration and the Night saver work-display choice. Activating Night saver
does not expand this settings section or consume adjustment space. At short heights
the command area scrolls independently, preserving the adjustment viewport. Individual
blackout and Night saver still own separate recovery snapshots and restoration paths.

Night saver requests at most 5% of the retained display's hardware control range,
rounded down. It never raises an already darker display and does not use the Night
reading preset's estimated nits. The selected display and target are in accessible
help; expanded options show the target beside the selector. Status describes a
request, with confirmation left to the card's observation. Automatic presets skip
the dimmed display; manual brightness choices take over. See [Night saver](night-saver.md).

## Evidence and accessibility

DdcWorker keeps a locked per-monitor/per-feature observation alongside its existing
ordered request model: requested value, last observation and pending/confirmed/
unconfirmed/failed state. Matching DDC readback is confirmation of a reported value,
not measured light. Accepted WMI writes remain unconfirmed. Superseded requests cannot
commit observations; rescan discards the old observation generation. Recovery does not
claim WMI readback it did not perform. The UI timer reads these snapshots without I/O
and is disposed with its card. Existing persistent failure notices remain independent.

The selector participates in Tab order, supports arrows/Enter and exposes a list with
named selectable items and activation actions. Stale accessible items become unavailable
instead of targeting replacement displays. Sliders retain native hardware units and
keyboard precision and expose exact entry as an accessible default action.
Windows contrast mode uses system foreground/background tokens rather than amber as
the sole signal. Contrast palette construction is fixture-tested; live Narrator output
and Windows contrast-theme switching remain manual acceptance, not inferred from a
property check. No automatic light/dark theme tracking is added.

The theme shares fonts. Paint paths and temporary brushes are disposed; cards cache
one current surface including contrast state. Each card disposes its observation timer,
ToolTip and cached bitmap. Tests also retain the existing repeated rebuild/disposal,
DPI transition, shortcut, overlay, settings and tray lifecycle coverage.

The card list reconciles stale native horizontal scrollbars after layout, including
observation-label updates. It retains real overflow and vertical scroll position;
selection no longer toggles AutoScroll. Regression tests check both
HorizontalScroll.Visible and Windows' WS_HSCROLL style: the managed property alone
missed a stale native scrollbar. This was reproduced after adjustments at native
150% scaling and repaired before the final captures.

The compact-window review treats typography, geometry and material separately:
keep readable text, reserve the working plane for everyday controls, and disclose
secondary commands. Review the same viewport after an adjustment, not just its
initial state. The staggered three-display fixture checks proportional geometry,
compact readouts and visible per-display presets at native 125% and 150% scaling.

The source-bound capture procedure and actual-size review are in [showcase](showcase.md).
100/150/200% fixture renders replay native DPI layout and explicitly scale fonts for
the isolated 96-DPI HDC. They are simulated DPI proofs, not a claim of physical monitor
acceptance. Native crossing checks use the host's available monitor scaling and
remain separate; the current checkpoint exercised 125% and 150%.

## Windows material

**Settings > Use Windows Mica title bar** is off by default. It uses the documented
`DWMWA_SYSTEMBACKDROP_TYPE` main-window material on Windows 11 build 22621 and
later, resets caption/text colors to the system defaults only after a successful
request, and restores the slate frame when disabled. High contrast skips the
request. The native frame retains Windows resizing, caption buttons and rounded
corner handling. The setting activates only after its save succeeds; refused
storage leaves both the active preference and frame unchanged.

The client remains opaque. No frame extension, transparency key, window opacity
or desktop capture is used. WinForms transparent child backgrounds repaint their
parent; they do not provide a compositor-backed glass layer. A whole-window Mica
implementation would need a separate alpha-correct renderer and validation of
GDI text, scrollbars, resizing and mixed DPI. Acrylic is reserved for a possible
future transient flyout, rather than the persistent control window.

Windows owns the material's appearance and solid fallbacks. A successful DWM
readback establishes requested intent, not visible glass. See Microsoft's
[Mica guidance](https://learn.microsoft.com/en-us/windows/apps/design/style/mica)
and [DWM attribute contract](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute).

Verification uses the actual WinForms controls and synthetic display fixtures.
Inspect native renders at the minimum window size, regular widths and simulated
150%/200% component sizes. Bounds checks cover wrapping, scrolling, focus routing
and unavailable controls. Native transitions run on a private test desktop using
the host's available monitor scaling, with bounded layout work and stable dimensions; see the
[DPI repair evidence](cross-display-dpi.md). Foreground pointer dragging, other DPI
combinations and screen-reader announcements still need manual acceptance.
The isolated renderer does not capture DWM-composited title-bar appearance.
Real-wallpaper Mica, active/inactive transitions, disabled transparency, Battery
Saver and remote sessions remain manual acceptance items.
