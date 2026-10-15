# Display layout and adjustment repairs

Reviewed on 2026-10-15, starting from `1c532f5` on a clean worktree.
Scope: this repository's display UI, CLI adjustment handling and regression tests.

## Reproduced problems

- Three displays with different vertical offsets received three tiny schematic
  rows. Tile labels were clipped, including the selected indicator.
- Cards created after asynchronous discovery used fixed caption/slider dimensions
  with scaled fonts. Brightness could break across lines and the large percentage
  could lose its bottom edge.
- Restoring the header inside a dock-layout pass could leave it overlapping the
  map with bounds retained from a different window width or DPI.
- The Night saver selector escaped a narrow command row. Native DPI crossings
  also repeated row layout before the final width settled; fractional scaling
  introduced a transient extra pixel of command height.
- A refused GUI gamma request still changed the model and could overwrite saved
  warmth and Custom. Explicit CLI brightness/contrast silently accepted unsupported
  controls; refused writes could overwrite Custom with unrelated current levels.
- Leading whitespace could make a signed CLI percentage step execute as an
  absolute percentage, inconsistent with numeric validation.

## Changes and evidence

Readout height and caption width follow measured text and DPI. Schematic rows
reserve space for readable identifiers, and short identifiers stay on one line.
Minimum outer-window dimensions scale from 620x480 logical pixels. Header changes
queue one final dock pass; DPI completion settles parent widths before measuring
command rows. Nested Night saver controls wrap within their available width.

Gamma application is injectable at the main-window boundary. A failed manual or
preset request restores the previous model/readout and keeps the existing warning;
only accepted manual warmth changes replace Custom. CLI explicit control requests
return failure for unsupported controls and preserve Custom on refusal. Presets
continue to skip controls that are unavailable on a display.

New regression scenarios cover screenshot-like staggered displays, rebuilding
cards after native DPI transitions, text height/width, section overlap, gamma
refusal and successful retry through the real window's background save, and CLI
refusal/relative-step handling. Existing layout bounds and DPI work limits remain
enforced. Tests use isolated files and fake hardware writers.

The window fixture now waits for queued initial layout, sizes its wide-layout
scenario in logical units, and clamps requested client sizes to the native minimum.
This avoids asserting on WinForms' stale cached ClientSize after an impossible
below-minimum request. These changes do not relax production layout assertions.

Local logs and native PNGs are in the ignored `artifacts/review-20261015` directory.
The existing public showcase receipt remains evidence for its recorded older
source; it is not evidence for these repairs.

Verified locally:

- Release solution and both test-project builds with warnings treated as errors.
- Full ControlTests: 4,586 checks passed.
- Full AppTests: 2,194 checks passed, including the added screenshot and gamma
  failure scenarios. Focused desk-remote capture: 430 checks passed.
- Native 125%/150% DPI round trips: 64 checks passed with the original limits;
  final command layout counts were 24/25, card layout counts 116, and command
  size-change counts 2/3. Earlier 100%/150% crossings also passed during repair.
- Release assembly source-path privacy and injected canary checks passed.
- Native screenshot fixtures at 125% and 150% were inspected for readable
  values, unbroken captions and non-overlapping sections.

The source-path script initially hit this host's PowerShell execution policy;
the same checked-in script passed with a process-local `-ExecutionPolicy Bypass`.
No machine policy was changed. Hosted CI was not dispatched.

## Limits

No physical display brightness, gamma, blackout, or power operation was performed.
Native windows were rendered on a private test desktop without switching the
input desktop. Physical driver behavior, foreground dragging/compositor appearance,
Narrator speech and clean-machine installation remain separate acceptance work.
The running installed application is not replaced by building a portable candidate.
