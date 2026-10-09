# Native interface design

The 1.2.1 interface uses a restrained slate palette and warm amber accents.
The desktop surface, command area and monitor cards have separate tonal levels;
cards add a subtle vertical gradient and a divider below the display identity.
The shared theme owns colors and fonts, so reference windows and editors use
the same visual vocabulary as the main window.

On the lightest card surface, muted text has a calculated contrast ratio of
7.01:1 and secondary text 5.39:1. Primary-button text is 8.87:1 against amber.
These token calculations complement native render inspection; they do not
establish a full accessibility audit or high-contrast-mode compatibility.

Brightness and warmth use amber tracks; contrast uses a neutral track. Every
slider has a framed value readout, a small grip and an explicit focus ring.
Unavailable sliders show a dash visually, alongside the existing availability
explanation. Their accessible state still reports the unavailable control.
The desk map preserves physical layout and adds a small brightness rail inside
each display, with an outline for hover or selection.

Clicking a map display aligns its adjustment area and selects its brightness
control, or warmth if brightness is unavailable. Cards with native input focus
retain an amber edge and side marker. Keyboard navigation keeps the current
control type when possible. Selecting or rendering controls does not request
hardware changes; adjustment keys and direct gestures retain their existing
input ownership, exact-entry and accessibility behavior.

The header uses stronger name typography, a quiet version chip and a secondary
descriptor when width permits. It yields in short windows. Command captions
and actions are separated by fine rules; secondary buttons have a subtle top
highlight. Buttons, sliders and cards use shared resources, with disposable
paint resources and no new animation loop.

Verification uses the actual WinForms controls and synthetic display fixtures.
Inspect native renders at the minimum window size, regular widths and simulated
150%/200% component sizes. Bounds checks cover wrapping, scrolling, focus routing
and unavailable controls. Native 100%/125% transitions now run on a private test
desktop, with bounded layout work and stable dimensions; see the
[DPI repair evidence](cross-display-dpi.md). Foreground pointer dragging, other DPI
combinations and screen-reader announcements still need manual acceptance.
