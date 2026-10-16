# Functional material interface

The default desktop interface uses WPF for Smoke, Frost and Ceramic finishes.
It shares the existing WinForms controller, worker, tray, settings store, schedule,
blackout recovery and Night saver lifecycle. It is not a second monitor engine.
The standalone study under `experiments/MaterialStudy` remains a historical design
fixture; launch the installed LumenDeck application for actual control.

## Everyday controls

- Select a display on the Windows desktop map. Its rectangles retain one uniform
  scale and translation, including negative origins and portrait/stacked layouts.
- Adjust brightness, contrast and warmth. Click a value or focus its slider and
  press F2/Enter for exact entry. Hardware units and kelvin are explicitly labeled.
  Arrow keys step by one unit (50 K for warmth); Shift makes ten-unit steps.
  Ctrl+Page Up/Down switches displays without changing their settings.
- Day, Evening, Night and Custom below the sliders affect the selected display.
  Desk actions applies presets across the desk and chooses the Night saver work
  display. Manual adjustments and Custom use the existing persistence path.
- Night saver stays visible at the top. It requests at most 5% hardware brightness
  on the retained work display and darkens the others; End Night saver uses the
  existing restoration records. These are requests, not measured darkness.
- Display actions contains blackout/restore, the screen shortcut, the brightness
  estimate editor and advanced controls. Advanced controls opens the classic panel
  for advertised input source, volume, RGB and other optional monitor features.
- Settings contains preset targets, schedule, startup preferences, help and Exit.
  The footer reports saving and scheduling state; adjustment feedback distinguishes
  requested values from observations. Estimates follow the current brightness.

## Appearance and compatibility

The **Theme · <current finish>** button sits immediately left of Settings and
opens a single menu with Smoke (dark), Frost (light), Ceramic (warm solid), then
Mica, Glass and Solid material choices. It remains visible in the minimum-size
window alongside Settings and Night saver. Settings > Appearance remains an
alternate entrance to the same choices and save guards.
Smoke and Frost can request Mica, Glass (DWM Acrylic) or Solid. Ceramic always uses
an opaque surface. Windows high contrast takes priority; unavailable backdrops or
disabled Windows transparency fall back to an opaque foundation. A successful DWM
call does not prove the compositor produced a particular visual appearance.

The menu explains pending saves, protected settings, Windows high contrast,
Ceramic's solid surface and unavailable transparency. Ceramic leaves the material
choices unchecked and disabled; choosing Smoke or Frost restores the saved
material preference. The button and its accessibility name report the active
finish; the footer reports the effective material, including Solid fallback.

`MaterialFinish`, `MaterialBackdrop` and `UseMaterialInterface` are saved only
through the normal confirmed-settings transaction. A failed save retains the live
choice and draft input. Unknown values fall back without being silently rewritten.
Appearance changes do not enumerate displays or send brightness/gamma commands.

Settings > Classic interface saves the classic panel as the preferred interface.
In that panel, Settings > Material interface switches back. Opening Advanced
controls switches for the current session without changing that preference.
The classic panel retains All displays, spatial map navigation, four palette
themes and the complete optional control surface. Shared editors retain their
classic palette. The material shell does not pretend those editors were rewritten.

Closing and minimising follow the saved keep-running preference. A normal Exit
waits for pending saves and follows the original restoration/disposal path. A
second launch reveals the active presentation through the existing singleton
activation channel. WinForms/WPF modeless keyboard interop is enabled explicitly.

## Boundary and verification

`LumenDeck.Material` references neither Core nor the hardware/settings assemblies.
It receives immutable snapshots and sends commands to `MainForm.Material.cs` on
the UI thread. A command resolves the current stable display key and scan
generation; obsolete scan generations cannot write to replacement handles.
Visible snapshots refresh every 250 ms without device I/O; hidden windows stop
their timer. Both presentations share one controller and worker.

AppTests exercises the actual WPF controls against synthetic transport and isolated
settings: manual adjustments, Custom, exact-entry rejection, unavailable controls,
appearance/draft persistence, stale generations, Night saver recovery, editor
ownership, tray/reveal, switching presentations and pending/failed saves.
The extracted-package startup probe loads WPF XAML/resources and material fallback
without opening a real device controller or touching user settings.

Run `powershell -File scripts/capture-material.ps1` from committed source to
generate six window references, the theme menu and their [receipt](assets/material-reference.json).
These are opaque WPF client renders, not screenshots of desktop blur. Actual
foreground keyboard/pointer behavior, mixed-DPI movement, Narrator, real Windows
contrast themes, compositor appearance and physical device restoration still need
manual acceptance; automated fixture success does not establish those outcomes.
