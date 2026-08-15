# Changelog

Notable changes, newest first. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Every monitor card can temporarily blank that one display with a black,
  borderless overlay and its minimum supported brightness. Clicking the overlay
  or using the optional per-monitor global shortcut restores the exact saved
  brightness. Exit and interrupted-start recovery are reversible too.

### Fixed

- MCCS `VCP D6` hardware-off is no longer exposed by the window, global
  shortcuts, generic controls, or CLI. Firmware can switch off the DDC receiver
  needed for software wake, so a reversible blackout replaces that unsafe path.
- Brightness writes no longer wait behind capability probes for unrelated
  monitors. DDC traffic and handle lifetime are serialised per physical display
  rather than behind one desk-wide lock, and optional controls are discovered
  only when their disclosure is opened.
- The final brightness or contrast value is now read back. A value silently
  dropped by monitor firmware is retried twice; if it still does not stick, the
  UI returns to the value the hardware actually reports instead of claiming the
  requested value was applied.

## [1.1.0] — 2026-08-05

**The first published release.** LumenDeck was source-only until this tag. The
version numbers already in its history — 1.0.0 in two projects, 1.1.0 in a third
— were never attached to anything anyone could download, and they did not agree
with each other. They agree now, and this is the number they agree on.

The two fixes at the top are why there was no earlier release worth making: a
blue light filter that delivers a colour other than the one on its own label,
and that gets warmer every time you start it, is worse than no blue light
filter.

### Fixed

- **Warmth was far stronger than the temperature it claimed.** A gamma ramp
  holds *encoded* values; a display raises them to roughly 2.2 before emitting
  any light. The linear white points were multiplied straight onto the encoded
  ramp, so every setting overshot by that exponent. `Night`, labelled 4600 K,
  measured about 3060 K on all four monitors on the development desk. Each ratio
  is now raised to `1/2.2` before it reaches the ramp; 4600 K measures 4600 K.

- **Warmth composed onto warmth on every launch.** `SetDeviceGammaRamp` outlives
  the process that called it, so the enumerator kept capturing LumenDeck's own
  warm ramp and recording it as the display's untouched baseline — once per
  launch, per Refresh and per display change. "Warmth off" faithfully restored
  that polluted copy, so nothing undid it. Measured on the development desk: all
  four displays emitting ~3100 K while `settings.json` recorded `"Kelvin": 6500`
  on every one. Each display's true baseline is now stored, alongside a
  signature of the last ramp written, so a ramp read back later can be
  identified as LumenDeck's own rather than mistaken for the display's.

- **The presets were a one-way door.** Pressing Day, Evening or Night
  overwrote brightness and warmth on every monitor with nothing to go back to.

- **`--version` printed the wrong assembly's version.** `Cli.cs` lives in
  LumenDeck.Core, so `--version` reported the engine's number rather than the
  command line's. Both projects happened to carry 1.0.0, which is exactly why it
  went unnoticed. The bug report template asks people to paste this number.

- **"Start minimised" killed the process at launch.** Setting the window state
  before the window had ever been shown made the first resize hide the form to
  the tray, which reassigns `ShowInTaskbar`, which recreates the window handle,
  which resizes the form again. The recursion overflowed the stack
  (`0xC00000FD`) with no window, no tray icon and no `error.log`, because a
  stack overflow cannot be caught.

- **`-m, --monitor` was ignored by every command that only reads.** The filter
  was resolved after `--list`, `--json`, `--diagnose` and `--features` had
  already printed, so `lumendeck-cli --list -m left` listed every monitor and
  exited 0 — an option that looks exactly like it worked. It now narrows those
  too, and reports no match rather than quietly widening.

- **`"HasCustom": true` was being written into `settings.json`.** A derived
  property that is ignored on the way back in, so editing it did nothing — in a
  file meant to be hand-editable.

### Added

- **Custom mode**, beside Day / Evening / Night in the window, in the tray menu
  and as `--preset Custom`. Your own brightness, contrast and warmth are saved
  per monitor when you move a slider, never overwritten by a preset, and seeded
  from whatever a monitor was already set to — so the very first press of a
  preset is reversible.

- **`-c, --contrast`** on the command line, absolute or as a relative step.
  Contrast could already be read by `--list` and restored by `--preset Custom`;
  there was no way to set it.

- **`gamma-baselines.json`** in `%APPDATA%\LumenDeck\`, holding each display's
  untouched ramp and a signature of the last one LumenDeck wrote. Deleting it is
  safe: an ordinary display is recovered from the shape of its ramp, and a
  calibrated one is left alone rather than guessed at.

- **`test/Test-Gamma.ps1`**, which reads the real ramp off the GPU and works
  forward to the light it produces. Both faults above were invisible from inside
  the app — every value it could read back was a value it had written — so the
  measurement has to start outside it.

- **"Start minimised"** in the tray menu. The setting existed in `settings.json`
  with nothing in the product to set it.

- The version, including the commit it was built from, in `--diagnose` output.

- `LUMENDECK_ANONYMISE=1`, which replaces monitor names with `Display A`, `B`,
  `C` in the window, `--list` and `--diagnose`, so a screenshot or a pasted
  diagnostic can be shared without publishing which monitors you own.

### Changed

- One version for all three projects, in `Directory.Build.props`. CI now asserts
  that `--version` and both executables' file versions agree with it.

- CI builds every branch rather than only `main`.

### Removed

- The built-in table of panel classes (`*office-ips`, `*gaming-va`, `*budget`,
  `*hdr400`). Four of its five entries could never be selected: the matcher
  skips keys beginning with `*` and `panels.json` had no syntax for naming a
  class, so every desktop monitor got the generic fallback while the table
  implied a choice was being made. Luminance profiles come from your own
  `panels.json`; a monitor without one is labelled an estimate.

[1.1.0]: https://github.com/snowyukitty/LumenDeck/releases/tag/v1.1.0
