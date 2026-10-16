# Native material study

> Historical design study. The functional production integration now lives in
> `src/LumenDeck.Material`; see [the interface guide](../../docs/material-interface.md).

An isolated, interactive design experiment for LumenDeck. **This is not a new
production client or an installable upgrade.** It has no project reference to the
display engine, does not enumerate monitors, and does not read or write product
settings. All display identities, geometry and values are fictitious.

The current production themes change colour without changing composition.
This study explores a quieter hierarchy and a compositor-backed foundation:

- **Smoke:** neutral dark surfaces and restrained warm accents; the recommended
  starting direction with Mica.
- **Frost:** cool, light surfaces; compare Mica with Glass (Desktop Acrylic).
- **Ceramic:** warm, opaque surfaces. A deliberate finish and a reference for
  judging whether transparency adds anything useful.

Finish controls stay in a separate study footer. Production placement of appearance
preferences is not proposed here. Select a numbered monitor to change the working
display. Sliders, presets and Night saver modify the preview only. Night saver
remains fixed in the header; the preview caps the primary at 5%, never raises an
already dimmer value, and restores example values when ended. This simplified
preview is not a replacement for production recovery logic.

## Run and compare

From the repository root, with the .NET 10 Windows SDK:

```powershell
dotnet run --project experiments/MaterialStudy/MaterialStudy.csproj -c Release
```

Use **Backdrop** to cycle Mica, Glass and Solid. Ceramic is always opaque.
The native caption, resize frame and system buttons remain Windows-owned.
Close the study normally; it has no tray process or persistence.

The comparison keeps content, values and layout the same across finishes. The
display map uses one uniform scale for synthetic desktop rectangles. Compact
readouts, visible Night saver and explicit reported-brightness wording remain
requirements. Helper text moves below its relevant control; capability badges and
the full accent perimeter are removed from the working card.

## Implementation boundary

WPF provides an alpha-capable client surface for this investigation. The shipping
WinForms client is unchanged. No framework migration is approved or implied by
this experiment. The shipping solution and package do not reference it.

On Windows 11 build 22621 or later it requests documented DWM system backdrops:
Mica (`DWMSBT_MAINWINDOW`) or Acrylic (`DWMSBT_TRANSIENTWINDOW`). Acrylic is a
comparison option, not a claim that whole-window Acrylic is the best default.
It extends the native frame behind the WPF client and preserves per-pixel alpha.
There is no desktop screenshot sampling, undocumented blur API, transparency key,
whole-window opacity, web runtime or network dependency. The icon is the existing
product asset.

High contrast, disabled Windows transparency, incompatible systems, failed DWM
requests and Ceramic use an opaque foundation. System-setting/composition changes
are coalesced; hooks are removed on close. The study reads the Windows transparency
preference but never changes it. DWM readback only proves the configured request;
Windows may choose a solid fallback at runtime.

## Verification and remaining work

```powershell
dotnet build experiments/MaterialStudy/MaterialStudy.csproj -c Release /warnaserror
$study = Start-Process -FilePath experiments/MaterialStudy/bin/Release/net10.0-windows/LumenDeck.MaterialStudy.exe `
  -ArgumentList '--render', 'artifacts/material-study' -WindowStyle Hidden -PassThru -Wait
if ($study.ExitCode -ne 0) { throw "Material study verification failed" }
```

Render mode writes six WPF client images and `verification.json`: three finishes
at regular and compact sizes, with 32 checks covering control widths, stable
state across finish changes, display selection and Night saver preview restoration
and no-brightening. It also records native backdrop requests. Every image identifies
itself as an **opaque reference render**, which cannot prove desktop blur or native
frame quality. These are not shipping screenshots or implemented product features.

Local build passed with zero warnings/errors; all 32 checks passed. On the tested
Windows 11 host, DWM readback matched Mica, Acrylic and None. Native compositor
visual inspection was blocked: the Computer Use helper returned "native pipe is
unavailable ... The system cannot find the file specified. (os error 2)".
The reference renders were inspected, including compact scrolling.

Before adopting a new production shell, validate actual composition over varied
backgrounds, active/inactive transitions, resize/maximize/Snap, mixed-DPI moves,
disabled transparency, high contrast, keyboard/Narrator, remote desktop, resource
usage and hardware/recovery integration. The shipping client remains the baseline.
This study does not implement editors, exact-value entry, schedules or tray flows.

## Sources

- [Microsoft: Mica](https://learn.microsoft.com/en-us/windows/apps/design/style/mica)
  describes an opaque, wallpaper-influenced material for persistent surfaces.
- [Microsoft: Acrylic](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic)
  describes the translucent material and usage guidance.
- [DWM backdrop types](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type)
  defines the documented API and Windows build requirement.
- [Extend the DWM frame](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmextendframeintoclientarea)
  describes negative margins for a full client-area frame.
- [WinUI system backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops)
  is an alternative to evaluate before choosing a production UI framework. This
  experiment does not add the Windows App SDK.
