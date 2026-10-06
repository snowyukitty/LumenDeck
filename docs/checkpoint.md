# Night saver checkpoint — 2026-10-06

## Objective and scope

Keep one selected work display usable during overnight work while the other
displays become as dark as the reversible control path permits. Add exact
slider input and retain a dependable way back to the saved desk.

Implementation lives in `src/LumenDeck` and `src/LumenDeck.Core`; regression
coverage lives in `test/ControlTests`. The README, changelog, contributor guide,
test guide and Windows build workflow describe and validate the change.

## Delivered behavior

- **Night saver / Restore night desk** is available in the window and tray.
  The Windows primary display is the default work display; the selection can
  be changed and is remembered by stable monitor identity.
- A night session restores only its own blackouts. Existing individual
  blackouts retain their separate restore controls.
- Recovery data is saved before a blackout starts. Exact raw brightness is
  retained, older percentage-based recovery data remains supported, and failed
  restores retain their retry marker.
- Queued writes and synchronous restores are ordered per monitor. A stale
  minimum-brightness write cannot run after its restore.
- Refresh preserves active blackouts. Newly attached displays remain visible.
  Presets skip blanked screens and temporary minimum brightness cannot replace
  the user's Custom brightness.
- Slider readouts support click, F2 and Enter for exact values. Brightness and
  contrast use hardware units; colour temperature accepts Kelvin directly.

## Verification at this checkpoint

- `dotnet build LumenDeck.slnx -c Release --no-restore /warnaserror` passed with
  zero warnings and zero errors.
- `dotnet run --project test/ControlTests/ControlTests.csproj -c Release` passed
  all 31 checks, using isolated settings and off-desktop overlays without
  physical monitor writes.
- CLI `--help` and `--version` smoke checks passed.
- `git diff --check` passed.

The workflow now runs the same isolated control checks before publishing its
build artifacts. Hosted CI status must be read from the corresponding GitHub
run; a local pass does not establish a hosted pass.

## Remaining validation and next steps

Real-monitor startup, rebuild-leak and gamma scripts were not run for this
checkpoint. Visual acceptance, exact hardware readback during a night session,
hot-plug behavior, crash recovery on real panels, and DPI readability remain to
be checked with [the manual acceptance checklist](../test/README.md#manual-night-desk-acceptance).
Actual power savings have not been measured. The mode uses minimum brightness
and a black overlay; it does not power monitors off through MCCS D6.

This checkpoint does not publish a release or change the product version.
Complete hardware acceptance before preparing the next release. Coordination
leases are session-local and should be checked again before further edits.
