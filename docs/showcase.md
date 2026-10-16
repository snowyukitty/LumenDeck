# Source-bound showcase

The images show the 1.4.1 source checkpoint with fictitious displays. They
prove interface layout, not hardware compatibility, calibrated luminance, energy
savings or a published release. The original charcoal/amber brand assets remain.

## Outputs

| Artifact | Purpose |
| --- | --- |
| `assets/screenshot.png` | Complete native documentation window |
| `assets/portfolio-native.png` | Complete native 8:5 window, original pixels |
| `assets/portfolio-1200x750.png` | Uniformly downsampled portfolio image |
| `assets/portfolio-480x300.png` | Exact-size portfolio card derivative |
| `assets/portfolio-320x200.png` | Exact-size compact card derivative |
| `assets/screenshot-compact.png` | DPI-scaled 620x480 logical minimum window, with scrollable detail |
| `assets/screenshot-stacked.png` | Two stacked fixtures with the selector reflowing to the side |
| `assets/theme-paper.png`, `theme-ocean.png`, `theme-violet.png` | Same native documentation window in the alternate palettes |
| `assets/screenshot.json` | Source commit, loaded assembly hashes, native DPI/dimensions and image hashes |

Capture dimensions follow the host's real monitor DPI. At 150%, the documentation
window is 1524x1094, the native portfolio master is 1800x1125 and the minimum window
is 930x720 physical pixels. The receipt, rather than these examples, records the
actual run. The portfolio window has an exact 8:5 ratio, with rounding upwards to
whole eight/five-pixel units at fractional DPI. Export requires at least 1200x750.
The application's ordinary default remains 940x820 logical pixels.

Windows limits normal window dimensions to the desktop's maximum tracking size
(see [Form.SetBoundsCore](https://github.com/dotnet/winforms/blob/v10.0.0/src/System.Windows.Forms/System/Windows/Forms/Form.cs)).
On a small hosted desktop, the layout checks exercise the largest reachable 8:5
window. Export explicitly refuses an undersized master instead of enlarging it;
public capture generation requires a desktop that permits at least 1200x750.

## Repeat

Prerequisites: Windows, .NET 10 SDK, Python with Pillow, restored dependencies
(`dotnet restore LumenDeck.slnx`), and committed implementation/capture tools.
From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/capture-showcase.ps1
python -B -m unittest discover -s test -p test_showcase_export.py
```

The harness runs on a private desktop without switching the foreground. It injects
fake monitor transport, isolated settings and fake gamma/startup collaborators.
It positions the window at a real monitor's coordinates to adopt native DPI, then
rebuilds the synthetic cards as discovery would. No Windows scaling setting changes,
hardware discovery or physical display writes occur. The capture path does not
send synthetic DPI messages or manually enlarge fonts.

`MainForm.DrawToBitmap` captures actual controls with complete nonclient bounds.
This is a control render, not a desktop photograph or a DWM-composited Mica preview;
see Microsoft's [DrawToBitmap API documentation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.drawtobitmap?view=windowsdesktop-10.0)
for the rendering API's limitations.

The two displays are Studio 27 (landscape, 62% brightness, contrast 50) and Reading
24 (portrait, 45% brightness, contrast unsupported). Their generic profile estimates
are 203 and 160 nits. Neutral warmth is a requested gamma baseline, not a measured
white point. The stacked capture changes the second display's rectangle/position
and selects it. All identities are fictitious; the owner's screenshot is not included.

Each run uses a fresh ignored capture directory. The native harness records the
loaded GUI/core build identities and hashes alongside every image's dimensions,
DPI, client size and hash. Before replacing public assets, the exporter verifies
the entire set against the current committed source and compiled assemblies.
Stale source, changed pixels, false dimensions, distorted aspect ratios and an
undersized portfolio master are rejected. Native images are copied unchanged;
three portfolio derivatives use uniform Pillow LANCZOS downsampling. The public
receipt contains relative filenames, without machine paths or personal displays.

## Pixel review and limits

Inspect the native images and the exact 480x300 and 320x200 files, not only a zoomed
preview. At 480x300 the selected outline, landscape/portrait relationship and compact
adjustment rows must remain understandable. At 320x200 the same shapes and control hierarchy
must still identify the desk remote. Helper copy belongs to the native view.

The full AppTests suite checks the showcase sizes, selected principal-control
visibility and managed/native horizontal scrollbars. Separate desk-remote tests
exercise synthetic DPI messages and system-colour mode; native cross-display tests
exercise the host's available DPI transitions. Those fixtures do not establish
foreground dragging, actual contrast-theme changes, Narrator speech or real monitor
acceptance. Source/build identity is recorded rather than inferred from the installed app.
