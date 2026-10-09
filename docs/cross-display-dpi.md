# Cross-display DPI layout repair

Version 1.2.1 addresses pauses and command/text flickering when moving the main
window between monitors with different Windows scaling.

## Cause and repair

Client-size, font and command-height changes triggered card width measurements
while WinForms was still scaling descendants. Nested AutoSize tables and flows
repeatedly measured mixed old/new metrics. Command height changes also repeatedly
resized the card viewport. Waiting for `Form.DpiChanged` alone was too late.

The repair suspends descendant layout at `OnGetDpiScaledSize`, with
`OnDpiChanged` as a fallback. WinForms still applies fonts, scaling and Windows'
suggested bounds. One queued completion releases suspension without replaying
intermediate measurements, settles command wrapping and pins final card widths.
Card grid/flow constraints are batched, owner layout is guarded against reentry,
and command sections/bar use double buffering. Window handles, DPI awareness and
monitor settings are preserved.

See the primary [Windows size-query documentation](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-getdpiscaledsize)
and the [.NET 10 WinForms implementation](https://github.com/dotnet/winforms/blob/v10.0.0/src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/ContainerControl.cs).

## Evidence and limits

Actual MainForm windows crossed real 96- and 120-DPI monitor coordinates on a
private Windows test desktop, using four synthetic display models, fake transport
and temporary settings. The desktop never became the input desktop.

| Observation per crossing | Before | After |
| --- | --- | --- |
| Move plus queued UI work | 2.4–3.3 seconds | Approximately 0.3–0.6 seconds |
| Command-subtree layouts | 73–100 | 19–20 |
| Card-subtree layouts | Over 1,500 in an intermediate profile | 176 |

Timings are informational and host-dependent. Regression assertions bound layout
counts and bar height churn. Six alternating crossings check destination DPI,
stable repeated dimensions, command bounds, card constraints, no stale horizontal
scroll extent, retained window/card identities and unchanged scan/gamma-save
counts. Native 100% and 125% bitmap renders were inspected.

A deterministic size-query/intermediate-resize test runs on single-DPI CI hosts;
actual crossing checks explicitly report a skip when mixed DPI is unavailable.

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --cross-display-dpi-only
```

This verifies native notifications and final layout. Continuous foreground pointer
dragging, compositor-frame flicker, screen-reader output and other DPI combinations
remain manual acceptance cases. No physical-monitor writes are made by the tests.
