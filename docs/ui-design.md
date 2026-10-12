# Native interface design

The development interface uses a restrained slate palette and warm amber accents.
The desktop surface, command area and monitor cards have separate tonal levels.
Cards use larger display names, a subtle vertical gradient, a fading inner rim,
an inset shadow and a divider below the display identity. This is opaque material
shading, not desktop blur or transparency.
The shared theme owns colors and fonts, so reference windows and editors use
the same visual vocabulary as the main window.

On the lightest card surface, muted text has a calculated contrast ratio of
6.47:1 and secondary text 4.97:1. Primary-button text is 8.87:1 against amber
and 6.69:1 against its distinct pressed surface.
These token calculations complement native render inspection; they do not
establish a full accessibility audit or high-contrast-mode compatibility.

Brightness and warmth use amber tracks; contrast uses a neutral track. Every
slider has a framed value readout, a small grip and an explicit focus ring.
Readouts use amber for brightness/warmth and neutral text for contrast. Hovering
a readout highlights its frame and shows the hand cursor for exact entry.
Unavailable sliders show an empty neutral track and a dash, alongside the existing
availability explanation. Their accessible state still reports the unavailable control.
The desk map preserves physical layout and adds a small brightness rail inside
each display. A sunken well separates the map from the commands; selection uses
a stronger outline than hover. Unsupported brightness uses only neutral shading.

Clicking a map display aligns its adjustment area and selects its brightness
control, or warmth if brightness is unavailable. Cards with native input focus
retain an amber edge and side marker. Keyboard navigation keeps the current
control type when possible. Selecting or rendering controls does not request
hardware changes; adjustment keys and direct gestures retain their existing
input ownership, exact-entry and accessibility behavior.

The header uses stronger name typography, a quiet version chip and a secondary
descriptor when width permits. It yields in short windows. Command captions
and actions are separated by fine rules; secondary buttons have a subtle top
highlight. Connection help and Settings use quiet buttons that reveal their
surface on hover, preserving keyboard focus and the full hit target. Primary
buttons use a dark focus ring against amber and a distinct pressed color.
Informational blackout notes are neutral; caution chips add an outline and a
visible exclamation mark. Chip sizes and slider fonts follow their scaled control
fonts. The header caches its sized icon and owns its derived metadata font.
Each card caches one current surface by size, focus/highlight, DPI and header
geometry; transparent children reuse it instead of rebuilding the material.
Empty position labels produce no chip. Paint resources and replaced caches are
disposed; there is no new animation loop.

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
