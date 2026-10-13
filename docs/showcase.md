# Source-bound showcase

The images show the 1.4.0 development candidate with fictitious displays. They prove interface
layout, not hardware compatibility, calibrated luminance, energy savings or a
published release. The original charcoal/amber brand assets are retained.

## Outputs

| Artifact | Purpose |
| --- | --- |
| `assets/screenshot.png` | Readable complete native documentation window, 1016x729 pixels |
| `assets/portfolio-1200x750.png` | Complete native 1200x750 landscape window, no composition or crop |
| `assets/portfolio-480x300.png` | Actual 480x300 portfolio card derivative |
| `assets/portfolio-320x200.png` | Actual 320x200 compact card derivative |
| `assets/screenshot-compact.png` | Complete 620x480 minimum window, with scrollable detail |
| `assets/screenshot-stacked.png` | Two stacked fixtures and the shared selector reflowing at the side |
| `assets/screenshot.json` | Source commit, build hashes, fixture, dimensions, scaling and image hashes |

The portfolio master is a genuinely implemented layout at a wider useful window
size. The application's ordinary default is 940x820; its minimum is 620x480.
Owners do not need the portfolio window size to use the app. No portrait surface
is stretched, and no controls are repainted. The complete native master happens
to match the portfolio aspect ratio, so broad side gutters are unnecessary.

## Repeat

Prerequisites: Windows, .NET 10 SDK, Python with Pillow, dependencies restored with
`dotnet restore LumenDeck.slnx`, and a clean committed implementation/capture toolset.
From the repository root:

```powershell
powershell -NoProfile -File scripts/capture-showcase.ps1
```

The harness creates a private desktop without switching the foreground. It injects
fake monitor transport, isolated settings, fake gamma/startup collaborators and
synthetic off-screen monitor rectangles. It never starts the production hardware
discovery path. `MainForm.DrawToBitmap` captures actual controls with complete
nonclient bounds. This API does not capture DWM-composited Mica or establish the
foreground appearance of Windows caption material.

The two showcase displays are Studio 27 (landscape, 62% brightness, contrast 50)
and Reading 24 (portrait, 45% brightness, contrast unsupported). Their generic
profile calculations are estimated 203 and 160 nits. Neutral warmth is a requested
gamma baseline, not a measured white point. The stacked capture changes only the
fixture rectangle and position to demonstrate the side selector.

The script refuses an uncommitted implementation, including version metadata. It copies native PNGs without
editing and makes both smaller images using uniform LANCZOS downsampling. The
receipt contains relative artifact names only, no machine paths or personal displays.
The capture suite also emits simulated 100/150/200% DPI and system-colour evidence
under the ignored `artifacts/showcase` directory. DPI replay uses scaled owned fonts
and child DPI because the private host's HDC remains 96 DPI; it does not alter
Windows settings and is not physical mixed-DPI acceptance.

## Pixel review

Inspect the original images, especially the exact 480x300 and 320x200 files, rather
than a zoomed browser preview. At 480x300 the selected screen outline, landscape/
portrait relationship and the principal 62% value must be understandable. At
320x200 the same shapes and control hierarchy must still identify the desk remote.
Small helper copy is intended to be read in the documentation/native window.

The design study read IconFlow's local product source, README and presentation
contract. The transferable principle is evidence at the delivered size: a real
source-bound output and honest limits. No IconFlow artwork, layout skin or copied
methodology prose is included in these assets.

The final pixel review accepted the six exported files after correcting a stale native
horizontal scrollbar reproduced during resizing. The regression checks Windows'
actual window style as well as the managed scrollbar property. Source version and
hashes are recorded in the receipt rather than inferred from an installed app.
