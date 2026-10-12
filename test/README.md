# Tests

Capability parsing and extra-control value labels, using in-memory data only:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --capabilities-only
```

These checks cover nested advertised value groups, hexadecimal tokens, whitespace,
missing/truncated blocks, invalid tokens and unknown-current-value labels. They
also keep advertised power data separate from the extra-control catalog. This
path returns before WinForms initialization and creates no monitors or settings.
It does not exercise native discovery, select-value fallback or hardware reads;
those remain separate integration and physical acceptance work. Full ControlTests
includes these data checks.

Desk-map identity and routing checks, on an isolated desktop with synthetic displays:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --map-caption-only <render-directory>
```

The optional render directory receives native map images at 240/604/1000 pixels
and a normal-name selection. Twenty-one checks cover persistent selection, temporary
hover preview, literal accessible names, missing positions, left/right/middle click
routing, caption/display separation and clearing a replaced or empty desk.
These are component checks, not foreground focus, mixed-DPI or screen-reader acceptance.

Window-level map selection and keyboard display navigation:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --navigation-only
```

Synthetic three-display checks click the actual map at three window sizes,
including repeated selections after scrolling away. They require the selected
brightness (or available warmth) control to own selection and fit the viewport,
with the card header aligned when space allows, and verify navigation performs
no display writes or settings changes. A subsequent Right key changes only the
selected display. Map and keyboard navigation also cover focus callbacks that
start a replacement scan. These run on an isolated desktop without physical input.

Extra-control native accessibility and fake request-routing checks:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --feature-accessibility-only
```

These 168 checks cover sliders/selectors/actions/captions, left/right/missing/
whitespace-only positions, display and feature ampersands, catalog descriptions,
confirmation limits and initial values. Native selector shortcut metadata is
compared with an ordinary selector; Slider must not invent an Alt mnemonic.
Every stock caption also exposes no shortcut with UseMnemonic disabled, including
Gain & trim; this is checked on the actual native accessible object.
Property reads and construction send zero fake requests. Safe picture selection
and a reflected Right key on volume each send one correctly targeted request;
silent synchronization sends none. No physical-handle flag, real transport,
risky selection or reset action is used. Full ControlTests includes these checks.
The earlier Gain & trim native Alt+space failure was reproduced before the
Slider override. Reflected key delivery does not establish foreground keyboard
behavior or Narrator announcements.

Native per-display link and notice checks use off-desktop, nonactivating forms:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --card-links-only
```

These 72 checks read actual accessible objects on same-model displays at left,
right and without a position. They cover profile/help/dismiss names, literal
ampersands, extra-control loading/empty/show/hide/absent states, warning clearing,
retained navigation and no fake transport writes. The temporary presentation
handle flag is cleared in finally before monitor disposal; no real handle exists.
This suite is also part of full ControlTests. Foreground keys, Narrator and live
native name-change announcement delivery are not exercised.

CLI warmth result checks use a per-call fake writer and temporary settings:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --cli-warmth-only
```

These 20 checks cover rejected writes with and without Custom capture, refusal
summary/exit 2, saved-file reload, accepted preset versus explicit warmth off,
absent settings entries, defensive bounds and writer exception propagation.
They exercise the shared helper and summary; the three production command
callsites are source-reviewed, not black-box hardware-executed. The full
ControlTests suite includes these checks. No physical GPU write is performed.

Hardware-free CLI preflight checks can run without initializing WinForms:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --cli-arguments-only
```

These 67 checks exercise the same admission wrapper used by the console entrypoint
with a recording executor. Invalid complete requests must emit an error, return
the expected status and never enter that executor. Valid aliases, filters, read
commands and combined adjustments must reach it exactly once with unchanged
arguments. Cases include missing selectors, later invalid values after a preset,
duplicate aliases, mode conflicts, numeric boundaries/overflow and withdrawn
power commands. They make no real settings or display calls; successful hardware
execution and rollback are outside this contract. The full ControlTests suite
also includes these checks.

Scenario-directory cleanup has a focused hardware-free check:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --scenario-cleanup-only
```

Eight checks use actual temporary directories and exclusive file locks. They
cover successful status preservation (including high-bit exit codes), explicit
cleanup failure with the original scenario result, recovery after releasing a
lock, and bounded retry of a transient lock. The worker retries I/O/access
failures for up to ten seconds; an exhausted or unexpected cleanup failure
reports exit 1 rather than throwing over the scenario result. These checks also
run in full AppTests, whose corrected harness passes 1,451 checks locally.

The integration assessment includes the parent entrypoint, private-desktop
dispatch, injected MainFormHost collaborators, temporary storage and Fixture
settlement/disposal. Eight additional caller black-box probes pass through the
actual apphost and direct dotnet DLL: argument round-trip, exit 17, high-bit exit
and forged-worker rejection before log creation, once per host. These assess
trusted test orchestration; physical focus, readers, monitor I/O and hostile-code
containment remain separate. ControlTests was not rerun for this test-only change.

The transient-lock check
establishes success after unlock; scheduler delay can let the handle close
before the first delete attempt, so it does not guarantee a retry iteration.

After a Release solution build, check compiler output paths:

```powershell
powershell -NoProfile -File test/Test-ReleasePathsChecks.ps1
```

The three product DLLs must contain the virtual source prefix and no physical
checkout prefix in UTF-8 or either UTF-16 byte alignment. CI runs this after
Build, with injected path canaries covering both separators, JSON-escaped
backslashes and UTF-16 byte alignments. This checks compiler path mapping,
not all secrets or nested symbol data;
final compressed bundle contents still require publication review. Debug builds
keep physical source locations for normal local breakpoint resolution.

Compressed-bundle path checks have a separate hardware-free regression command:

```powershell
powershell -NoProfile -File test/Test-BundleSourcePaths.ps1
```

Twenty minimal v6 bundle fixtures exercise compressed/uncompressed content,
private UTF-8 paths with either separator, JSON-escaped backslashes and UTF-16
paths at both alignments,
missing mapped paths, missing/ambiguous markers, unsupported versions, invalid
offsets/sizes/names, missing/duplicate assemblies and bounded decoded output.
They never execute fixture bytes. Package verification checks the real extracted
EXEs with the same reader before probes; full package tests include these cases.

Portable-package regression checks, after `scripts/build-release.ps1` prints
the produced archive path:

```powershell
powershell -NoProfile -File test\Test-Package.ps1 -Archive <zip-path>
```

These verify the actual extracted binaries and reject extra/tampered contents,
version mismatch and injected launch failure. They also check installer refusal
with an injected running app, without changing the real installation. See
[packaging verification](../docs/packaging.md) for the remaining clean-machine
and install/uninstall acceptance work.

Installer source preflight checks can also run without launching or installing:

```powershell
powershell -NoProfile -File test/Test-InstallSource.ps1 -Archive <zip-path>
```

Installer state-preservation checks run independently in either PowerShell host:

```powershell
powershell -NoProfile -File test/Test-InstallState.ps1
```

These execute the actual PATH helper against an in-memory registry double and
the actual uninstall body against temporary files. They check raw expandable
PATH text/value-kind preservation, idempotent updates, absent/unsupported values,
and locked GUI/CLI deletion failures before launch-entry changes. Registry and
shortcut collaborators are controlled; no real installation or user registry is
changed. These 24 checks also run in the full package suite. Partial executable
removal is not rolled back, and real install/update/uninstall remains manual.

They copy the installer and archive payload into a temporary fixture. Actual PE
headers and version resources cover missing/truncated files, absent metadata,
wrong architecture, invalid header offsets and mismatched builds of one release.
A valid pair reaches an injected stop at directory creation. No executable,
shortcut, registry, user PATH or real installation is changed. These checks are
also included in package regression checks; they do not prove full installation.

Hardware-free control regression checks (isolated temporary settings and
off-desktop test overlays, with no physical monitor writes):

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release
```

These run in CI.

Brightness estimate editor and asynchronous-save checks can run separately:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --profile-only <render-directory>
```

They use temporary profile files and fake blocked/failing writers. Validation
checks verify a pinned correction beside disabled Save at a short viewport,
accessible field/action descriptions, no write from an invalid draft and recovery
without erasing a preceding unconfirmed-save warning. Optional PNG output includes
the invalid draft at 460x440. Typed/pasted endpoint drafts, pending-save text
notifications and shared integer parsing with en-US/fr-FR/hi-IN thousands
formatting are covered. These checks are also in full ControlTests.


Preset target storage, editor and window-lifecycle checks can run separately:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --presets-only <render-directory>
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --presets-only
```

ControlTests covers override storage (defaults not written, unknown names and
fields preserved, load clamping, duplicate entries), invalid drafts, preview
limits, every save outcome and 460-pixel/200% layouts; the optional directory
receives editor PNGs. AppTests drives the actual modal editor from the Settings
menu: failed and pending saves keep previous targets, tray Exit is refused
until the save settles, and desk, card and startup tooltips and requests use
the saved targets without a display write during editing. Both are also in the
full suites.

Night saver, minimise/restore and main-window checks can run separately:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --night-only
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --night-dim-only
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --restore-only
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --main-window-only <render-directory>
```

`--night-only` uses a fake transport: the work display is dimmed only below its
brightness, its marker is saved before the write, and refused, disconnected,
exit and interrupted-session restores keep or clear it correctly. `--restore-only`
requires minimise/restore to keep the same window handle, cards, widths and
shortcut registrations, and prints informational timings. The main-window check
also requires Windows to report the dark frame. Timings on a private desktop are
indicative only.

Preset schedule checks use fixed clock values:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --schedule-only <render-directory>
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --schedule-only
```

ControlTests covers time parsing, periods across midnight, single-slot and
clashing schedules, storage compatibility and the editor's validation, preview
and save outcomes, with optional PNGs. AppTests drives the actual window with a
fake clock: one request per period, manual changes kept, Refresh not repeating
a period, deferral during a scan, a period that begins while the editor is open
(its preview refreshes instead of promising the request twice) and enabling the
schedule during a gated scan (deferred and requested once). Neither waits for
real time or observes physical displays.

Exact-value modal entry checks can run separately with optional native PNG output:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --exact-entry-only <render-directory>
```

They exercise the actual modal loop with synthetic sliders. Out-of-range, negative,
non-numeric, empty, fractional and overflow drafts remain visible through blur,
Enter and spinner actions, with Apply disabled and pinned accessible guidance.
Repairing a draft restores stepping and applies the displayed integer once.
Cancel/Escape, stale target/bounds/converter rejection and Kelvin conversion remain
covered. A synthetic target containing an ampersand checks the actual modal
label's literal-text behavior; native before/after PNGs reproduced and verified
the fix. The targeted run passes 77 checks. Full AppTests includes these cases
on its private desktop; foreground, physical DPI and reader acceptance remain
manual.

The actual-main-window About checks can run with optional native PNG output:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --about-only <render-directory>
```

They verify menu routing, owned-window reuse/cleanup, GUI assembly metadata,
offline content, pinned Close, reading-state focus recovery, and 100/150/200%
geometry/font simulations. Fake transport and isolated settings confirm opening
and reading request no scan, adjustment, registration or settings write. The
full AppTests suite includes these checks and the existing keyboard-reference
regressions after extracting the common ReferenceForm.

Connection-help checks also invoke the actual support-report modal from its
View support report button. They verify native input is disabled for both help
and MainForm, repeated previews restore input, programmatic owner Hide/Show
preserves the modal boundary, and the actual tray Exit handler closes the owner
tree and ends the modal loop. These run on the private desktop without copying
to the real clipboard. They do not exercise real tray interaction or production
foreground activation.

AppTests automatically launch a worker on a new private Windows desktop, verify
its exact desktop identity before creating controls, and never switch the input
desktop. Creation/identity failures stop the run; there is no interactive fallback.
The worker belongs to a kill-on-close Job Object from process creation, with a
three-minute worker deadline and bounded termination/log cleanup. Its UTF-8 output
and exit code are relayed by the parent. Windows 10/Server 2016 or newer is required
for atomic job-list process attributes. The normal CI AppTests command uses this
same runner; no hosted run was triggered as part of local verification.

Support-report layout checks can run separately with optional native PNG output:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --support-report-only <render-directory>
```

These use a fake clipboard and native controls on the private desktop. At a fixed
minimum-window viewport, 100/150/200% font simulations check initial, failed-copy
and successful-copy feedback, readable/reachable Copy and Close actions, and a
usable scrolling preview. Exact preview/copy equality and no copy on opening are
also checked. They are included in full AppTests; these simulations do not prove
physical mixed-DPI behavior, Windows text-size transitions or reader delivery.

The support-report checks also use clipboard callbacks that close the preview
before success or an exception. They assert no status mutation or native handle
recreation after disposal. Live copy feedback requests UIA notifications; these
tests do not assert delivery to a physical screen reader.

Display discovery recovery checks can run separately with optional PNG output:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --discovery-only <render-directory>
```

They cover reading, empty, failed and successful scans with fake enumeration,
including close/supersession and help inventory updates. Additional 100/150/200%
font simulations at narrow and wide card widths check readable recovery labels,
vertical-scroll reachability, no horizontal scrolling and explicit-action routing.
Reading disables Refresh while keeping help available. These checks are included
in full AppTests and use the same private-desktop runner; font simulations do not
prove physical DPI/text-size transitions.

Connection-help reading/layout checks also run on the private desktop:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --connection-help-only <render-directory>
```

At the minimum viewport, 100/150/200% font simulations check local Home/End and
Page Up/Down routing, scroll movement, a pinned Close/Cancel action, complete Back
button reachability at End, and readable report/return action labels. Native PNGs
can be rendered at Home and End. Full AppTests includes these cases plus existing
MainForm connection-help scan-state integration. No real report dialog, clipboard
or monitor access is exercised by the layout checks; physical input, DPI and
screen-reader behavior remain manual acceptance.

The connection-help run also checks actual command-bar ownership/reuse, revealing
a hidden reference, reopen/owner cleanup, and direct requests against hidden,
disabled, minimized and disposed owners. A controlled background scan verifies
that opening then closing help cancels stale focus recovery, with no extra scan
or adjustment. QuietConnectionHelp overrides Reveal without native Activate;
physical foreground/reveal acceptance remains manual.

Night-command help has an actual MainForm lifecycle check:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --night-command-help-only
```

Eleven checks cover toolbar start, tray restore, live accessible descriptions,
and preservation of an independently blanked display. Synthetic overlay-only
displays use temporary settings on the private desktop; no hardware, real startup
or physical focus claim is involved. The scenario also runs in full AppTests.

Run the process/failure checks separately before changing the runner:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --desktop-runner-checks
```

The seven checks cover argument fidelity, normal/high-bit exit status, stderr,
desktop identity rejection, timeout cleanup including a worker descendant, and
preservation of the worker result when a locked log prevents cleanup. Cleanup
failure fails the parent with exit 1 and relays the saved worker output/status;
it cannot turn a failed worker into a pass. Both apphost and direct
`dotnet <AppTests.dll>` launch paths each pass seven checks on source `3a73764`.
The earlier 944-check full suite belongs to the original runner change; the
current full-suite evidence is recorded in [release readiness](../docs/release-readiness.md).

Quiet forms retain ShowWithoutActivation, WS_EX_NOACTIVATE and the foreground
assertions. A private desktop isolates UI activation; it is not a filesystem or
credential sandbox. Fake monitor/registry/gamma collaborators and temporary
storage remain required. Isolated foreground assertions do not prove focus or
physical key delivery on the interactive desktop. Real input, mixed DPI and
screen-reader acceptance remain separate checks. In .NET 10, ShowDialog explicitly
restores an active/owner window when it exits, which is why moving windows off
screen alone was insufficient isolation for the earlier integrated run.
See [the framework source](https://github.com/dotnet/winforms/blob/v10.0.0/src/System.Windows.Forms/System/Windows/Forms/Form.cs),
[the desktop API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createdesktopw),
and [atomic job attributes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute).

Command-button checks create native handles in an off-desktop owner and
synthesize keyboard/click events. They cover orphan/duplicate key-up, held-key
repeats, mismatched keys, focus loss, direct/inherited disable and re-enable,
Ctrl/Alt chords, Shift activation, interleaved MSAA/direct actions while armed,
native cancel-mode processing and retained accessible objects after disposal.
Enabled direct/MSAA actions still dispatch once. Full native Tab/key routing,
input-method and actual screen-reader acceptance remain manual work.

To isolate these checks while developing a command control:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --buttons-only
```

An optional directory after `--buttons-only` saves native button-state PNGs.

Command-help accessibility can be checked without displaying a window:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --command-help-only
```

These checks read actual accessible objects for desk commands and per-card
presets, including disabled commands, literal display names/positions and Custom
background-save guidance. They also check Blank screen/Restore screen transitions,
shortcut configuration descriptions and display names without a position label.
They distinguish brightness-capable help from unavailable hardware controls.
The current targeted suite has 63 checks. Synthetic displays, a fake transport and null settings
keep the check free of hardware and user-settings access. No command is pressed.
They are also included in full ControlTests; Narrator remains a manual check.

Blackout capability-transition checks can run separately:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --blackout-rebind-only
```

These 28 checks use fake monitor transport, temporary settings and non-activating
overlays outside the desktop. They reproduce the missing-snapshot brightness
write after Refresh, check overlay-only and hardware sessions across unavailable
and newly available controls, preserve persisted raw snapshots, and verify a new
session adopts new capabilities only after saving its current brightness.
No physical display or real user settings are accessed.

Complete status reading can be checked separately:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --status-only <render-directory>
```

The 36 private-desktop checks cover logical tab traversal, native read-only
character/Delete messages, full accessible text, unchanged command selection,
warning/routine colours and scrolling to both ends at 344/604/1000px widths and
100/150/200% font/geometry simulations. Optional PNGs show the message beginning
and final retry actions. No clipboard command or hardware request is issued;
foreground keyboard delivery and physical DPI remain separate acceptance work.

Pending blackout recovery feedback uses:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --blackout-feedback-only
```

These 39 checks run on the private test desktop with temporary recovery settings
and a fake restore transport. They cover startup warning visibility, failed
recovery notifications, unavailable/rejected retries, unchanged saved snapshots,
confirmed recovery and notice dismissal. Startup with available controls and
actual Refresh-button retries also check live and reloaded persisted markers,
the original raw snapshot, rejected recovery and successful card reconstruction.
No blackout overlay, real monitor write,
startup registration or real user settings access is required.

Appearance checks compare both disabled variants and their hover states, retain
the enabled amber face, and render at 100/150/200% font and geometry scaling.
These simulations do not verify physical mixed-DPI transitions.

Command-layout checks can run separately with optional native PNG output:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --commands-only <render-directory>
```

They cover late label/font changes, oversized labels wrapping inside their row,
restoring the original footprint, literal punctuation and explicit accessible
names, and manually sized hosts. The complete suite also runs these checks.

Preferred-size queries are checked with empty, one-pixel, bounded and maximum
proposals: they preserve minima and leave bounds/native handles unchanged.
Native manual-button ink positions verify explicit padding separately from
outer bounds. Parent layout after TextChanged must not raise extra text/font
notifications. See [the layout contract](../docs/command-layout.md).

Settings checks use isolated files, synthetic recovery records and separate
test processes. They cover first creation/replacement, staging and lost-result
failures, locked/read-only files, unchanged predecessor bytes, owned-temp cleanup,
source edits/removal/creation, invalid/unreadable fallback preservation, legacy
BOM encodings, unknown fields, abandoned mutex ownership, cross-process busy and
stale writers, plus distinct CLI persistence/monitor-refusal outcomes.
The actual MainForm suite separately checks preference rollback, recovery advice
and blackout refusal before dimming on a source conflict. No real monitor or
user-file restoration is performed. A focused run is available:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --settings-only
```

Luminance-profile checks use temporary files and synthetic displays: missing and
explicit endpoint flags, longest-name selection, generic desktop/laptop models,
reload invalidation, safe example creation, CLI JSON metadata and accessible card
descriptions. Measured-profile cards also run width/font wrapping checks. No
profile fixture is loaded from or written to the user's app directory.

Editor checks also cover full-name versus broader selection, unknown-field and
Unicode preservation, byte-exact predecessor backups, stale/malformed/duplicate
source rejection, mutex contention, locked/read-only failures, draft validation,
remove/cancel behavior, alias privacy and a fixed footer in short windows.
Rendering includes a narrow editor and simulated 200% geometry/font scaling;
these do not replace full-app DPI or Narrator acceptance.

Background-save checks pause an injected writer with barriers: UI messages still
run while draft/actions are locked, duplicate clicks do not write twice, and
Cancel/Escape/ordinary editor and owner close cannot discard a pending result.
Real non-activating owned forms verify WinForms close cancellation. Other cases
cover UI-thread success delivery, synchronous disposal from a success callback,
late completion after disposal, unexpected exceptions, and a temporary-file
commit whose success result is deliberately lost. Read-only committed snapshots
refresh synthetic same-model estimates without rereading an external edit.
These checks do not instantiate MainForm or exercise its modal ShowDialog,
monitor/tray cleanup, delayed-open admission or actual OS shutdown.

The separate main-window suite references the actual GUI assembly:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release
```

Preference checks run in that suite with fake startup registration and temporary
settings. They cover shared menu synchronization, write refusal/readback mismatch,
unknown read state, runtime rollback on refused storage, retained Custom records,
next-launch semantics and no immediate warmth/hardware adjustment. The registry
read adapter is separately exercised through an injected value reader. Real
Windows registration and popup keyboard/Narrator behavior remain acceptance work.
Slider accessibility checks use identical-model synthetic displays with distinct
positions, non-percent hardware bounds and the reversed warmth/Kelvin mapping.
They observe native value/name events with a current-process WinEvent hook,
including synchronization without adjustment callbacks and keyboard changes on
an unbound slider. The hook is released in finally. Exact-entry checks use real
modal forms on an off-desktop owner and fake transport: Apply/no-op, Cancel,
Escape dialog-key handling, Kelvin conversion, disable/disposal, changed bounds
or converters and Refresh replacing the card are covered. Long-name rendering
checks wrapping and action reachability at the minimum size. Full Tab flow,
mixed DPI, UIA RangeValue and actual Narrator acceptance are not established.

Refresh-selection checks use actual MainForm ActiveControl, synthetic identical
models with distinct stable keys and reversed enumeration order, and delayed
scan barriers. They cover named daily/action targets, surviving Settings,
Refresh re-enable, user selection away/back, keyboard and pointer intent, open
help, hide/show, unsupported or missing targets, scan failure and supersession.
The off-desktop owner is checked against the foreground window; no foreground
keyboard injection or real monitor write is used. Native user-driven Tab flow
and screen-reader focus announcements remain acceptance work.

To isolate display-rebuild selection and minimum-viewport reachability:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --focus-only
```
Keyboard-reference checks open the actual MainForm Settings/local F1 path with
an off-desktop form factory. They cover reuse and recreation, pending-save menu
availability, hidden/disabled owners, reading while open/closed, owner disposal
and terminal late scans. The reveal hook is instrumented to avoid native Activate.
Body wrapping, Page/Home/End scrolling, final-section reachability and pinned Close
are checked at minimum and regular sizes, with optional rendered artifacts.
Snapshots/counters check no guide-driven settings, scan, monitor/gamma or
registration requests. Actual F1/Tab routing, repeat foreground reveal, reader
announcements and mixed DPI remain manual acceptance work.

For reference-only layout/copy verification (optional final argument renders PNGs):

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --keyboard-help-only "$env:TEMP/LumenDeck-keyboard-preview"
```

Display-navigation checks call the actual child-to-parent ProcessCmdKey route
with explicit modifier data on native off-desktop controls. They cover layout
order, wrapping, toolbar entry, named roles, unsupported/missing/ambiguous
fallbacks, minimum viewport visibility, map/status updates, one/zero-card cases,
disabled/hidden owners, recovery cancellation during reading, guide locality and
reentrant selection starting a replacement scan. Settings bytes and fake I/O
counters remain unchanged during ordinary navigation. These are protected-method
routing checks; they do not inject keys or verify the native message queue,
preferences-popup routing, minimized-owner behavior or screen-reader output.

Slider-input checks exercise Ctrl/Alt combinations with all adjustment and
exact-entry keys, external handled/suppressed events, subscriber disposal and
ordinary/Shift steps. Editor factories and callback counters detect accidental
actions without showing a modal editor. AppTests repeat modified keys on actual
brightness/contrast/warmth card sliders and retain the parent navigation checks.
These explicit protected-method events do not simulate physical keyboard state.

Button-input ownership checks cover handled/suppressed presses and releases,
orphan releases, fresh gestures, intercepted repeats, subscriber-issued direct
clicks and subscriber disposal. Private arm/paint state is checked alongside
callback counts. Actual MainForm Refresh checks verify no generation/scan starts
on intercepted input, then complete one synthetic scan from a fresh gesture.
Off-desktop native controls and isolated files are used, without physical input.

Button-accessibility checks query Pressed/Unavailable, retain a disposed object,
and observe StateChange through a process/window/object-scoped native WinEvent
hook. They cover keyboard repeat/release, focus-loss callbacks, accessible
activation, pointer callbacks, disabled/disposed cancellation and absent handles.
Nested click dispatch and subsequent independent clicks are checked separately.
They do not establish physical input routing or screen-reader announcements.

Non-primary pointer tests send MouseEventArgs through the protected down/click/up
callbacks for right/middle/auxiliary buttons, then verify one left-button click.
MainForm checks inject those click arguments into actual Refresh and verify no
new scan/generation. They do not send physical mouse input or native mouse
messages; the WinForms native dispatch mapping is checked against tagged source.

Background settings checks pause injected writers before disk work and after
commit. UI messages still run, checks/shortcuts remain active at prior values,
duplicate requests are refused, ordinary edits coalesce, and a pending checkpoint
cannot dim a display. Actual tray Exit and simulated ApplicationExitCall/
WindowsShutDown close arguments retain resources until completion. Accepted exit
then writes final live state after every older worker. Lost results, unexpected
writer exceptions, unexecuted success claims and disposed-owner completion are
covered. Shortcut completion after Refresh uses fake registration callbacks and
current cards; no global hotkeys are registered by the suite. Core checks prove
immutable payloads, capturing-thread completion, single execution and permanent
cancellation of abandoned operations. Simulated close reasons do not prove real
Windows shutdown, forced termination or clean-machine behavior.
Recovery checks cover persistent failure feedback after unrelated footer messages,
fresh active-value retry without replaying a failed preference, source guards in
both menus, and an older snapshot succeeding before a newer edit. Synthetic
restoration notifications verify that cleared recovery markers follow an older
pending snapshot. Retry is instrumented for no scan, gamma or transport action.
To render Settings without displaying a popup on the user's desktop:

```powershell
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --render-dir <output-directory>
```

The menu renders normal, failed, source-changed and waiting states with the actual
renderer; the larger-font view stresses text layout and
does not simulate a complete Windows DPI transition.

That command also renders the actual MainForm with two fictitious displays,
and its reading/empty/failed viewport states at minimum and regular sizes.
Discovery checks hold enumeration behind a barrier, open the actual offline guide
in a nonactivating off-desktop form, and verify its reading/failed/empty/completed
report state without another scan. They cover manual retry through empty, failure
and success, disabled adjustment dispatch in both surfaces, no horizontal scrolling,
action reachability, and late results after exit or a newer configuration scan.
The test host supplies a help-form factory while production constructs the same
ConnectionHelpForm normally. No physical monitors or startup registration are used.

Mica preference checks cover the build/high-contrast eligibility boundary,
confirmed saves and reloads, disabling, refused-save rollback, native DWM request
readback and identical opaque client rendering. Fake transport, gamma and startup
write counts stay unchanged. DWM readback proves intent, not visible wallpaper
composition. Button checks also cover readable material/focus color pairs and
distinct primary keyboard-press rendering.

The two-display fixture includes a portrait display without contrast support. Window checks cover
minimum size, short/narrow and short/wide wrapping, repeated resize, the
239/240/241 logical-pixel map visibility boundary, compact card heights and
scrolling every visible slider/action on the final card fully into view.
The 240-pixel rule is a map visibility trigger, not a guaranteed minimum viewport.
These checks use the host's current DPI; they do not establish mixed-DPI behavior.

To refresh the public development preview after inspecting the generated PNG:

```powershell
Copy-Item -LiteralPath <output-directory>/main-window-780-1000.png -Destination docs/assets/screenshot.png
Copy-Item -LiteralPath <output-directory>/main-window-780-1000.json -Destination docs/assets/screenshot.json
```

The adjacent JSON records the synthetic fixture, assembly version, client/outer
dimensions, DPI and rendering method. Its version describes a working-tree build;
it is not proof that the image corresponds to a released artifact. The fixture
is injected before MainForm construction and uses temporary settings and fake
transport/registration callbacks. Preview rendering does not discover or rename
the user's displays, read their profiles, register startup or adjust monitors.

It injects isolated settings, synthetic displays, fake DDC transport, startup
registration and gamma-save callbacks, then shows MainForm and its real modal
editor off-desktop without activation or a visible tray icon. Tests exercise
single pending-open admission, late-read suppression after hide/close, native
modal owner disabling, display-change rebuild during editing, pending-save tray
Exit cancellation, worker/tray preservation, confirmed current-card estimates,
failure/cancel/reopen and settled close cleanup. WM_DISPLAYCHANGE is sent only to
the test window; IsWindowEnabled checks the native modal state rather than the
managed Enabled property. Diagnostics are explicitly disabled before rebuilding.
Profile flows issue no transport writes; one deliberate fake write subsequently
proves the worker survived canceled close. These checks run in CI but do not
prove real tray visibility, foreground activation, gamma ramps, hotkeys,
physical monitor disposal, OS shutdown or full-window DPI/Narrator behavior.
The harness keeps a real Application.Run loop active, rejects cross-thread
control access and throws unhandled UI exceptions instead of showing error
dialogs. Hide/re-show success and failure results are discarded; a current read
failure releases admission and can be retried. Accepted close terminates the
owned worker and disposes the notification component once; repeated disposal
does not repeat the gamma-save callback. Same-friendly-name synthetic displays
with different stable keys intentionally share the documented model profile.

The native enabled-state check follows
[IsWindowEnabled](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowenabled).
The outer-loop harness avoids synchronization-context teardown when a standalone
pump ends; the current
[WinForms loop implementation](https://github.com/dotnet/winforms/blob/main/src/System.Windows.Forms/System/Windows/Forms/Application.ThreadContext.cs)
explains that distinction. Local runtime tests, rather than that moving source
branch, establish the behavior verified above.

The remaining control checks cover raw-unit keyboard precision, Shift steps, wheel
accumulation,
silent synchronization, blackout persistence, refresh rebinding, interrupted
recovery, exact raw-value serialization, persistence failure, group ownership,
work-display protection, individual wake, and disconnected-display retries.
They require
an interactive Windows session for WinForms; the real-monitor scripts below
remain separate.

Command-bar checks use the production presentation controls in a non-activating
window outside the desktop. They cover 604/620/780/1100-pixel client widths,
returning to a narrow width, simulated geometry and font scaling at 100/150/200%,
and adding a command without a manual layout refresh. These simulations do not
replace moving the full app between monitors with different Windows DPI settings.

Monitor-card checks use a long synthetic model name and a multi-modifier screen
shortcut at 400/570/740-pixel widths, including returning to the wide layout.
They check card and parent bounds, flow-child overlap and height recovery with
simulated 100/150/200% geometry and font scaling. The rendering command also
writes `card-layout-<width>-<scale>.png`; these are isolated presentation previews,
not evidence of full-window DPI transitions or actual monitor behavior.

Adjustment-feedback checks cover readback-to-slider synchronization, accumulating
failure categories, duplicate presentation, accessible summary text, help,
explicit dismissal without display writes and new failures after dismissal.
Synchronization and retries retain the historical notice. Card bounds checks and
renders include visible warnings. These tests inject card failures; they do not
simulate real driver failures or prove worker callback ordering, Narrator live
announcements or full-app disposal/rebuild races.

`DdcWorkerChecks.cs` injects an internal `IDdcTransport` and uses explicit
entry/release barriers to choose worker interleavings; timeouts are failure
watchdogs. It checks successful/refused readback supersession (including equal
requested values), independent controls/monitor identities/VCP codes, retry
bounds, silent drops, unreadable final values, callback lock release and
exception isolation, cancellation, disposal and synchronous restore ordering.
It also checks tray observations against new requests and Idle during active
I/O. Fake monitors have no native handles; no native transport is invoked.

These schedules prove their tested software interleavings, not every possible
thread schedule, full-app deferred UI delivery or native driver timeout behavior.
See [display I/O design](../docs/display-io.md) before adding a backend or another
readback path.

Activation checks use unique named pipes and inert callbacks, including a child
test process that only requests reveal and exits. They cover acknowledgements,
back-to-back requests, wrong/oversized messages, disconnected and idle peers,
missing or unresponsive servers, session rejection with an injected expected
session, callback refusal and shutdown. Foreground grants are replaced with
inert delegates. No normal app, real mutex, monitor controller or user settings
are started. For a targeted run:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --activation-only
```

These tests do not prove actual full-app foreground behavior, mixed-elevation or
cross-account Windows compatibility. The secondary-startup branch and reveal
event paths are source-audited; exercise the full app manually before release.

To render the isolated command bar for visual inspection:

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release -- --render-dir "$env:TEMP\LumenDeck-layout-review"
```

The previews use a synthetic display name and never enumerate physical monitors.
Inspect the PNGs after UI changes; passing bounds checks alone cannot prove that
text and grouping look right.

The same checks cover Connection help's observed control summaries, WMI versus
external guidance, empty/unchecked/failed scans, scroll access to the final action
and card navigation. The optional rendering command also writes guide and
synthetic monitor-card PNGs. Opening the guide performs no hardware scan or
settings writes; Refresh remains the main window's existing rebuild action,
which can reapply saved warmth and recover active blackouts.

Support-report checks inject private markers into names, EDID serials, device
identifiers and errors; assert the JSON field allowlist; verify pending and
failed scans omit old records; and exercise CLI validation, JSON failure output,
exact preview/copy payloads and clipboard failure using an injected writer.
They do not change the real clipboard. The rendering option also writes a
synthetic `support-report.png` preview.

Three PowerShell checks that need **real monitors**, which is why they are not in
CI: a GitHub runner has no display that answers DDC/CI. The isolated control
checks above verify software behavior; these scripts verify actual hardware.

Build first (`dotnet build LumenDeck.slnx -c Release`), then:

```powershell
powershell -File test\Test-Startup.ps1
powershell -File test\Test-RebuildLeak.ps1 -Cycles 25
powershell -File test\Test-Gamma.ps1
```

## Manual night-desk acceptance

Run this checklist on the built GUI before declaring a hardware-verified release.
Record initial brightness values and restore the desk when finished.

1. Select the intended centre monitor in **Keep working on**. Start **Night
   saver** and confirm that the work display stays usable and dims toward the
   configured Night target when initially brighter. If already at or below that
   target, its brightness stays unchanged. Every other available display becomes
   black at its supported minimum brightness. A display without brightness support
   must report that it could not be dimmed.
2. Use **Restore night desk** from the window and the tray in separate trials.
   Confirm each saved hardware brightness value is restored, including the work
   display and panels whose range is not 0–100. A manual brightness or preset
   choice on the work display during the night takes precedence over its saved value.
3. Blank one screen individually before entering Night saver. Group restore
   must leave that independent blackout in place.
4. Click one night overlay or use its existing shortcut. That screen should
   wake independently, and the group button should reflect remaining screens.
5. Refresh while Night saver is active. Confirm overlays follow their stable
   monitor identities and remain at minimum brightness. Try a preset and
   confirm blanked screens stay dark.
6. Disconnect and reconnect a blanked display, then restore. A failed restore
   must retain recovery data and allow retry rather than claim success.
7. Exit while the mode is active and confirm brightness is restored. In a
   separate controlled trial, terminate the process and relaunch; confirm
   interrupted blackouts recover from saved settings.
8. Click a slider's number and use F2 / Enter to edit brightness, contrast and
   Kelvin. Test Apply, Cancel, range limits, one-unit arrows, Shift steps, and
   readability at the desktop's DPI settings.

Minimum brightness plus a black image is not hardware-off. Measure power at
the wall if an energy-saving claim is needed; software checks cannot establish
watt savings or the visual behavior of every panel.

## Test-Startup.ps1

Time to window, survival of a broken settings file, and starting minimised.

Enumerating monitors over DDC used to happen inside the form constructor, so
nothing appeared for **6.7 seconds** after launch - measured, not estimated. It
now runs off the UI thread and the window shows in well under a second.

It also writes six flavours of damaged `settings.json` - including
`{"Monitors": null}`, which is valid JSON that throws on first use - and
requires a window every time. That failure mode is nasty precisely because it
happens before the message loop exists, so no exception handler can report it:
the app just never appears.

The last case launches with `StartMinimised` on, which is the one path with no
window to wait for — and which used to end in `0xC00000FD`, `STACK_OVERFLOW`,
because hiding to the tray before the window existed reassigned `ShowInTaskbar`,
recreated the handle, resized the form and re-entered the same path. No window,
no tray icon, no `error.log`: a stack overflow cannot be caught. It asserts the
process is still alive and that it reached the monitors, since there is nothing
on screen to look at.

## Test-RebuildLeak.ps1

Posts `WM_DISPLAYCHANGE` repeatedly, which drives the same rebuild path as the
Refresh button, and watches GDI and USER object counts via `GetGuiResources`.
Those are the quota-limited resources; exhausting them ends in
"Error creating window handle".

`Controls.Clear()` removes controls **without disposing them**, so an earlier
version leaked every panel's window handles and fonts on each rebuild. The
counts are now flat across 25 rebuilds.

Note that working-set memory still climbs during the run and that is *not* a
leak - see [docs/notes.md](../docs/notes.md). Set `LUMENDECK_DIAG=1` to make the
app log reachable managed bytes after a forced collection, which is the
measurement that actually answers the question.

## Test-Gamma.ps1

An independent GPU gamma-ramp readback and model check. It reads the ramp through
GDI, estimates linear channel ratios using `f^2.2`, and infers tint with the
white-point table. This can catch encoding-domain and compounding errors while
the app's own requested values look correct. It does not measure emitted light
or actual colour temperature; those require an optical instrument.

```powershell
powershell -File test\Test-Gamma.ps1                  # report
powershell -File test\Test-Gamma.ps1 -ExpectKelvin 4600
powershell -File test\Test-Gamma.ps1 -ExpectNeutral
```

Two regressions it catches, both of which shipped:

- **Wrong domain.** `--preset Night` asks for 4600 K. Before the fix this
  implied ~3060 K in the readback model on the development desk.
- **Compounding.** Run `--preset Night` or relaunch the window several times and
  the reading must not move. It used to roughly halve the blue channel each time,
  because the app kept capturing its own warm ramp as the display's baseline.

A ramp that is not a scaled identity is called out separately. Its origin cannot
be determined from that shape alone, and the simple tint estimate may be
unreliable. The checks do not establish ICC/HDR compatibility or calibration
restoration on a physical screen.
