# LumenDeck

Brightness, contrast and colour temperature for **every** monitor on a Windows
PC — external ones over DDC/CI, a laptop's built-in panel over WMI — from one
window or one command.

Windows itself cannot do this. Its brightness slider and the `Fn` brightness
keys drive `WmiMonitorBrightnessMethods`, which exists **only for an internal
laptop panel**. Plug in external monitors and there is simply no slider to find:
you are meant to reach behind the screen and press buttons. LumenDeck talks to
the monitors directly over DDC/CI — the control channel that rides the video
cable and drives the same values as the on-screen menu.

Runs as an ordinary user. No administrator rights, no drivers, no service.

```
lumendeck-cli --list
lumendeck-cli --preset Night
lumendeck-cli -m "left" --brightness 55 --warmth 5000
```

## What makes it different

**Presets match perceived luminance, not slider numbers.** An identical DDC
value means different light on different panels: on a 400-nit panel and a
250-nit one, 43 and 76 look the same to the eye. Setting every monitor to "50"
is exactly what produces a desk where one screen glares and its neighbour looks
dead. LumenDeck models each panel as `nits ≈ floor + (peak − floor) × pct` and
solves for the value that hits the target luminance.

Panels it has no profile for get a generic estimate — and the UI *says* it is an
estimate rather than presenting a guess as a measurement. Add your own in
`%APPDATA%\LumenDeck\panels.json`.

**Colour temperature works even when the monitor refuses.** The obvious way to
reduce blue light is the monitor's own RGB gain registers. Many monitors
advertise those registers and then ignore every write to them — measured by
sweeping the full 0–100 range and reading back an unchanged value thirteen times
in a row, while every call reported success. Some offer no warm preset at all,
only 6500K and *cooler*. So warmth is applied as a per-display GPU gamma ramp,
composed on top of whatever ICC or colorimeter profile is already loaded, and
"off" restores that profile exactly instead of flattening it.

Windows Night light is not an alternative here: it is one global switch that
tints every attached monitor.

**Every monitor gets its own controls, discovered from that monitor.** Panels
genuinely differ: one exposes input source, picture mode and speaker volume,
its neighbour exposes black levels and no volume at all. LumenDeck reads each
monitor's MCCS capability string and builds only the controls that monitor
actually claims — input source, colour preset, picture mode, volume, sharpness,
RGB gain, black levels, power mode, factory reset — plus per-monitor preset
buttons so a single odd screen can be fixed without touching the others.

Nothing is offered on a guess. Reading a VCP code a monitor does not implement
does not fail; it answers with a plausible number. One panel here returns `80`
for three RGB black-level codes it never advertised, which reads exactly like a
real setting in need of correction. So a control appears only where the monitor
listed the code, and when a monitor reports a *current* value it never
advertised, the UI says `Unknown (0x07)` rather than naming it from the standard
table and stating a confident falsehood.

**It never asks you to trust a monitor number.** `\\.\DISPLAYn` is not a stable
identity — the numbering carries gaps from past hot-plugging, and need not match
what Windows paints on screen during *Identify*. LumenDeck labels monitors by
model and physical position, keys saved settings on EDID identity (falling back
to the physical port when two panels are genuinely identical), and has its own
**Identify** button that puts each monitor's name on its own glass.

## Install

Grab a build from [Releases](../../releases), or:

```powershell
git clone https://github.com/snowyukitty/LumenDeck
cd LumenDeck
dotnet build -c Release
```

Requires the .NET 10 SDK to build. The published release is self-contained and
needs nothing installed.

Two executables come out of the build:

| | |
|---|---|
| `LumenDeck.exe` | the window and tray icon |
| `lumendeck-cli.exe` | the command line |

They are separate binaries on purpose. A GUI-subsystem process cannot stream
stdout back to the shell that launched it or make it wait for an exit code, so a
single executable pretending to be both prints nothing when piped.

## Command line

```
--list                  Every monitor and its current settings
--json                  Machine-readable output
--diagnose              Why a monitor does or does not respond
--features              Extra controls each monitor advertises

-m, --monitor <text>    Only monitors whose name, position or device matches
-p, --preset <name>     Day | Evening | Night
-b, --brightness <n>    0-100, or a relative step: +10, -10
-w, --warmth <kelvin>   3000-6500, or "off"
```

Exit codes: `0` done, `1` nothing matched, `2` a monitor refused the change.

## When a monitor does not appear

Run `lumendeck-cli --diagnose`. Common causes, in order of likelihood:

- **DDC/CI is switched off in the monitor's own menu.** Several brands ship it
  disabled. It is usually under Settings, OSD or System.
- **A KVM switch or some USB-C docks do not pass DDC through.** Nothing on the
  PC side can fix that.
- **The panel is a laptop's built-in display.** That is handled, but only where
  the OEM implements the ACPI brightness interface.

## Configuration

`%APPDATA%\LumenDeck\`

| File | |
|---|---|
| `settings.json` | saved colour temperature per monitor, keyed on EDID identity |
| `panels.json` | your own panel luminance profiles; an example is written on first run |
| `error.log` | written only if something throws |
| `diag.log` | only when `LUMENDECK_DIAG=1` |

## Design notes

The interesting problems here were not the UI. They are written up in
[docs/notes.md](docs/notes.md) — including a physical monitor handle that is
legitimately `0`, why `SetVCPFeature` returning success means nothing, and why
working set is the wrong instrument for finding a leak.

## Licence

MIT. See [LICENSE](LICENSE).
