# Native desk remote

The primary sequence is screen identity, supported adjustment, observed result,
and recovery. The native WinForms architecture, Core/GUI/CLI boundary and tray
remain intact. Existing brand assets are unchanged.

## Hierarchy and reflow

The default shows one selected MonitorCard. The schematic selector keeps relative
left/right and above/below positions and portrait/landscape cues. Equalized slots
make it explicitly non-metric; neither tile fill nor geometry simulates emitted
light. Selection has a strong outline and a written SELECTED marker. Names wrap
inside taller tiles; the caption and control header retain the complete identity.
A vertically stacked desk uses a 220-DIP side selector when the window has room,
and returns above the detail at compact sizes. The selector never vanishes merely
to make the window fit. The brand header yields first.

All displays reveals the same cards, preserving an efficient multi-display workflow.
Entering a card synchronizes the map selection; returning to the single-display
view keeps that screen and control. Spatial arrows prefer a neighbour in the same
schematic row/column, then the nearest candidate in the requested half-plane.
Ties use displayed order. Edges stop; Ctrl+Page Up/Down remains the wrapping route.
Home/End use layout order. Arrow selection never transiently enters a slider;
Enter/Space or a pointer click explicitly opens the selected screen's controls.
Ctrl+Page Up/Down navigates across every card in either mode. Selection survives
rebuilds by StableKey, while callbacks and observations remain bound to the exact
Monitor object for that discovery generation. Background completion does not focus
a control or activate the window.

Brightness has the principal readout. Unsupported brightness stays explained;
warmth becomes principal instead. Readout width measures endpoint strings, including
Kelvin. Contrast, warmth, profile editing, per-display presets and blackout stay
accessible below. Extra advertised controls retain their existing disclosure.
The header repeats name, position and capability summary. Estimated nits remain
secondary and explicitly estimated. Warmth is a gamma tint target, never a measured
colour preview.

Identify, Refresh, Connection help and Settings remain visible. Desk actions expands
desk-wide presets, warmth restoration and Night saver. Active Night saver reveals
its recovery section even when other desk actions are collapsed. At short heights
the command area scrolls independently, preserving the adjustment viewport. Individual
blackout and Night saver still own separate recovery snapshots and restoration paths.

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

Selection settles FlowLayout's virtual extent and resets native scrolling in the
single-display view. A regression test checks both HorizontalScroll.Visible and
Windows' WS_HSCROLL style: the managed property alone missed a stale native scrollbar
after a wide-to-narrow resize followed by repeated selection. This was caught in
pixel review, reproduced, and repaired before the final captures.

The source-bound capture procedure and actual-size review are in [showcase](showcase.md).
100/150/200% fixture renders replay native DPI layout and explicitly scale fonts for
the isolated 96-DPI HDC. They are simulated DPI proofs, not a claim of physical monitor
acceptance. Existing native 100/125% crossing checks remain separate.

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
and unavailable controls. Native 100%/125% transitions now run on a private test
desktop, with bounded layout work and stable dimensions; see the
[DPI repair evidence](cross-display-dpi.md). Foreground pointer dragging, other DPI
combinations and screen-reader announcements still need manual acceptance.
The isolated renderer does not capture DWM-composited title-bar appearance.
Real-wallpaper Mica, active/inactive transitions, disabled transparency, Battery
Saver and remote sessions remain manual acceptance items.
