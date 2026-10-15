# Changelog

Notable changes, newest first. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

The current development candidate is 1.4.0, including the desk remote,
spatial keyboard navigation and optional Windows Mica title bar. It remains a test candidate
until release acceptance is complete.

### Added

- Desk remote presentation: selected-display detail, keyboard-accessible schematic
  selector, a stacked-desk side layout and one-action All displays mode.
- Principal supported adjustment with a larger exact-entry readout, and explicit
  requested/observed/pending/readback/unconfirmed/failure information. Accepted WMI
  writes are not presented as confirmed readback.
- Source-bound native screenshots, 1200x750 portfolio artwork and inspected
  480x300/320x200 derivatives, generated from isolated fictitious displays.

- **Settings > Use Windows Mica title bar** optionally requests the native
  main-window material on Windows 11 22H2 or later. The setting is off by default,
  activates after a successful save and leaves the client opaque. Unsupported
  systems keep the slate title bar.

- The window shows its version in the title bar and in a new header, and the
  notification icon's tooltip includes it. The header gives way in short windows.

### Changed

- Public preview generation follows native monitor DPI, retains an original-pixel
  master and validates capture/build hashes before exporting uniform derivatives.
- Arrow keys on the desk schematic follow screen positions, prefer aligned
  neighbours and stop at edges. Home/End select the first/last screen; Enter or
  Space explicitly opens its controls. All displays tracks the focused card.
- Selector accessibility distinguishes selection from focus, rejects hidden or
  disabled activation, and sends native events with the correct child identity.
- Portable quickstart instructions describe the shared reflowing desk remote.

- Refined the development interface with stronger display headings, restrained
  surface highlights and shadows, quiet help/settings commands, section rules
  and outlined amber/neutral readouts. Readout hover reveals exact-entry access;
  map hover and selection have different outlines. Caution chips have an explicit
  shape cue while blackout explanations remain neutral. Chip sizing and painted
  readout/header fonts follow scaled fonts; the header caches its sized icon.
- Primary buttons have a readable dark focus ring and distinct pressed feedback.
  Unavailable sliders no longer paint a value fill beside their dash readout.

- Version 1.2.0 refines the native dark interface with layered slate cards,
  quieter amber brightness and warmth controls, neutral contrast tracks, framed
  value readouts and monitor-map brightness indicators. Focused cards retain an
  amber outline and marker; compact windows keep their existing responsive layout.

- Night saver also dims the work display it keeps on, toward the Night preset's
  estimated target and only when the display is brighter. Its original
  brightness is saved first and restored by **Restore night desk**, by exiting,
  or at the next launch after an interrupted session. Choosing a brightness for
  that display during the night keeps your choice, Refresh keeps the dim, and a
  display blanked on its own during the night returns to its pre-night level.
  A dimmed level is never remembered as the display's Custom brightness.
- The window and its dialogs use a dark Windows title bar and border, with dark
  scrollbars and drop-down lists, instead of light system parts around a dark
  window.

### Fixed

- Large brightness readouts and caption columns now measure scaled text, including
  cards created after display discovery on a high-DPI monitor. Minimum window
  dimensions scale with DPI; restoring the header settles its docking bounds.
- Staggered display maps reserve readable rows and keep short tile identifiers
  on one line instead of clipping them into tiny rectangles.
- Night saver selectors wrap within narrow command rows. DPI changes settle the
  final command width before measuring children and avoid rounding-height churn.
- Refused GUI warmth requests retain the previous readout, saved warmth and
  Custom levels. Explicit CLI brightness/contrast requests fail for unsupported
  controls, preserve Custom on refusal, and parse padded relative steps consistently.

- Version 1.2.1 fixes stalls and command-bar flickering when moving the window
  between monitors with different DPI scaling. Intermediate font/size changes
  are batched before measuring command rows and monitor cards at their final
  width; dragging does not re-enumerate displays or change monitor settings.

- Clicking a display on the desk map now focuses its brightness control (or
  warmth when brightness is unavailable) and aligns its adjustment area in the
  viewport, including repeated selections and cards taller than the viewport.

- Restoring the window from the notification area no longer rebuilds the whole
  window, which stalled while every display card was recreated and shortcuts
  re-registered. Minimising and restoring no longer lay out every card twice, and
  scans no longer re-measure every card when the scrollbar appears; restoring a
  three-display synthetic window went from 0.5-1.5 s to under 0.2 s.

- **Settings > Preset schedule…** changes presets by time of day. Choose start
  times for Day, Evening, Night and optionally Custom; the schedule is off until
  you turn it on. While LumenDeck runs, including from the notification area,
  each period is requested once when it begins, after the first display scan of
  a launch, and after waking or a clock change. Manual changes stay until the
  next period, blanked displays are skipped, and a period that begins during a
  display scan waits for the scan. The editor previews the current and next
  period and whether saving requests a preset now; the schedule changes only
  after its save is confirmed. Settings and the notification icon show what the
  schedule is doing.

- **Settings > Preset targets…** edits the estimated luminance (1-1000 nits)
  and warmth (3000-6500 K) that Day, Evening and Night request. A preview shows
  each connected display's resulting brightness percentage from its own
  estimate, marking generic profiles and targets beyond a panel's floor or peak.
  Saving adjusts no display; new targets become active only after the settings
  save is confirmed, and an unconfirmed save keeps the previous targets and the
  draft. Desk, tray, per-monitor and CLI `--preset` requests use the saved
  targets; preset tooltips state them. Only edited presets are stored, so
  unedited ones keep following the built-in defaults.

### Fixed

- The desk map keeps the selected display's name visible after hover ends.
  Its identity caption has a separate bounded row, preserving literal names
  for accessibility while shortening long visible names. Right-click no longer
  navigates to a display card.

- Extra-control selectors and action buttons expose their display and actual
  feature to accessibility clients, with existing confirmation guidance.
  Missing positions no longer produce empty parentheses in extra-slider names;
  literal ampersands no longer invent Alt mnemonics for custom sliders.

- Per-display profile/help links and adjustment notices identify their display
  to accessibility clients. Extra-control show/hide and loading names follow
  their live action instead of losing context or retaining an earlier state.

- CLI warmth requests now respect GPU write refusal for direct adjustments,
  built-in presets and Custom restore. A refusal returns exit 2 and reports
  Requested, retaining previous current/saved warmth and Custom temperature.

- CLI checks the whole request before loading settings or discovering displays.
  Missing monitor selectors, unknown/duplicate options and invalid values no
  longer allow an earlier adjustment to run or a missing selector to target the
  whole desk. Absolute percentages and Kelvin must be within their documented
  ranges; signed percentage steps still stop at display limits. Information
  commands and adjustments must be separate. Withdrawn `--power` is rejected
  before execution, including when combined with other adjustments.

- Night saver tooltips and accessible explanations follow the live start/restore
  action in the toolbar and tray, including pending brightness recovery and
  separate blackout ownership. Refresh help explains possible saved-warmth and
  blackout-brightness side effects.

- Long status messages wrap in a selectable, read-only text area with vertical
  scrolling. Retry instructions remain available at narrow widths; the footer
  keeps its fixed height and warning colour.

- Failed blackout recovery explains how to retry with Restore screen or Refresh,
  without implying continuous background retries or a DDC/CI-only connection.
  Offline Connection help explains the still-dim screen, retained recovery data
  and laptop brightness path.

- Pending blackout brightness recovery keeps a per-display warning after startup
  or a failed restore. Failed Restore screen retries now explain that recovery
  remains pending and how to retry without clearing the saved brightness.

- Refresh keeps an overlay-only blackout free of brightness writes when monitor
  controls become available later. A new blackout saves the newly available
  brightness before lowering it; an existing hardware session keeps its original
  recovery snapshot across capability changes.

- Per-display blackout/recovery commands and shortcut configuration links expose
  their display context and current action to accessibility clients. Shortcut
  help distinguishes saved bindings from an unassigned shortcut.

- Desk commands and per-monitor presets expose their hover explanations to
  accessibility clients. Per-monitor preset names include the display and
  position, so repeated Day, Evening, Night and Custom buttons are distinguishable.

- Custom tooltips distinguish remembered manual levels from completed storage,
  and per-monitor preset feedback describes an estimated target request rather
  than implying measured output or confirmed hardware changes.

- Invalid or duplicate profile structures show a recoverable read error instead
  of escaping the asynchronous editor-open boundary. The file remains untouched,
  and reopening after an external repair works normally.

- Profile editing rejects malformed Unicode source bytes instead of silently
  replacing characters in another model's notes or unknown fields when saving.
  Valid UTF-8 and BOM-marked UTF-16/UTF-32 sources remain editable.

- Brightness-profile saves share a machine-wide writer lock across Windows
  sessions accessing the same full path. An unavailable lock reports an unconfirmed
  save and retains the draft instead of falling outside the store's error handling.

- The source installer preserves expandable user PATH entries and their registry
  type. Uninstall keeps launch entries when deleting an owned executable fails.

- Display names retain literal ampersands in cards, Connection help, Identify,
  exact-value targets and layout-map hover text instead of treating them as access keys.

- Release builds map source locations to a virtual root so compiler-emitted
  debug paths do not disclose the local checkout directory inside executables.

- Brightness estimate fields preserve invalid typed/pasted drafts and disable
  Save immediately. Native thousands formatting remains valid across locales;
  malformed numeric groups cannot silently become another luminance value.

- Exact value keeps invalid, fractional or out-of-range drafts visible and blocks
  Apply instead of silently using a clamped or previous value. Pinned guidance
  explains the allowed whole-number range; Cancel remains available.

- Brightness estimate validation stays visible beside Save in short windows and
  explains the required correction from the fields and action. Repairing an
  invalid draft preserves any previous unconfirmed-save warning.

- Display discovery recovery buttons grow with large text and wrap onto separate
  rows in narrow windows, keeping Refresh and Connection help fully readable.

- Support-report Copy skips feedback if its clipboard callback closes the preview.
  A live preview requests accessible success/failure notifications, matching About.

- Connection help ignores queued requests when its owner is hidden, minimized,
  disabled or closing. Repeated requests reveal its existing owned window, and
  reading help cancels pending scan focus recovery.

- Support-report feedback grows to fit larger text, and Copy/Close buttons size
  themselves from their labels. The report preview keeps its own scroll area.

- About skips copy feedback when a clipboard callback has already closed the window.

- Command sizing and painting share side-scaled text insets to avoid separate
  DPI rounding; explicitly sized buttons also respect their requested padding.

- Disabled primary command labels remain readable on the dark button face.
  Both button variants share a neutral disabled border, including during hover.

- Right, middle and auxiliary mouse clicks no longer dispatch shared commands.
  Primary clicks, keyboard activation and accessible/direct actions still work.

- Shared command buttons respect handled/suppressed keyboard events after host
  subscribers run, canceling a pending press rather than activating on release.
  Subscriber disposal also leaves no armed or painted keyboard press.

- Ctrl/Alt combinations no longer fall through to slider adjustments or exact
  entry when they are not recognized as main-window commands. Sliders also
  respect a KeyDown subscriber's handled/suppressed input or disposal.

### Added

- Connection help keeps Close outside the scrolling instructions and supports
  local Home/End/Page Up/Page Down reading keys. Its report and return actions
  size themselves to larger text.

- About offers an explicit Copy version action for issue reports, with the GUI
  version/build only and generic success/failure feedback. Close and feedback
  remain reachable when the reference body scrolls.

- Settings > About LumenDeck shows the running GUI version/build and offline
  getting-started, keyboard, support and licence information. One owned window
  is reused, with wrapping content and Close kept outside the scrolling body.

- Desk command buttons size themselves from their labels and fonts, preserving
  existing minimum footprints. Labels wider than a row wrap within the available
  width; text/font changes update layout without hiding the complete command.

- Shared command buttons expose momentary MSAA Pressed state and native state
  notifications for press/release/cancellation, without claiming preset success.

- Local Ctrl + Page Down / Page Up navigates display cards in layout order,
  preserving the selected control when available and scrolling it into view.
  Navigation wraps and reports display position without requesting adjustments.

- An offline keyboard reference opens with local F1 or Settings > Keyboard
  controls. Its read-only body wraps and scrolls while Close remains visible;
  guide-local page/home/end keys are distinguished from slider adjustments.
  One owned modeless window is reused, and opening it cancels stale scan-focus
  recovery without requesting scans, monitor adjustments or settings writes.
- Refresh preserves logical keyboard selection by stable display/control identity
  after replacing monitor cards. Reading parks an unavailable selection on help;
  user input, another selection or changed visibility cancels recovery. Latest
  scans inherit only unclaimed bookmarks; missing/unavailable targets fall back
  to Refresh, and owned windows keep their interaction priority.
- Exact numeric entry now shows the full display/control context, explicit unit
  and converted bounds in a reusable dialog. Cancel/Escape discard drafts, and
  disabled/disposed or changed-bound/converter targets cannot apply a stale value.
  Refresh replacing a card closes its exact-entry draft before adjustment.
- Everyday sliders identify the display and position to assistive clients, and
  expose hardware units or converted Kelvin bounds with keyboard guidance.
  Value synchronization sends accessibility notifications without adjustment
  callbacks; status notifications use the accessible-object API to avoid the
  .NET 10.0.11 Control notification regression. Native WinEvent checks cover delivery.
- The control viewport now distinguishes reading, completed zero-display scans
  and failed scans with contextual Refresh/Connection help actions. Empty or
  in-progress scans disable display adjustments in the window and tray and hide
  stale maps. Help remains available without another scan; failure guidance avoids
  raw exception messages. Scan-state cards wrap and scroll vertically at small sizes.
- Settings retains a save-status row and offers a persistence-only retry of
  active values. Failed preference/shortcut drafts are not replayed; newer edits
  cannot be labeled saved by an older snapshot. Changed/unusable sources keep
  file-backed preference switches disabled with reopening/repair guidance.
  Restoration notifications queue cleared recovery markers after pending writes.
- Ordinary GUI settings saves now write captured snapshots on a worker. Active
  preferences/shortcuts change only after confirmed completion; pending JSON
  choices explain their disabled state. Updates coalesce after UI settlement,
  shortcut completion follows current stable-key cards across Refresh, and close
  requests wait before final checkpoint/cleanup. One-shot admission prevents
  abandoned snapshots or writes ahead of UI completion from erasing recovery data.
- Settings use unique flushed staging files, a shared writer mutex and loaded
  source validation. Stale, deleted, unreadable and invalid sources are retained;
  unknown root/per-monitor fields survive updates. Preferences explain failures,
  failed shortcut saves retain the old shortcut, and CLI persistence failures
  return exit 2 without inventing a monitor refusal. Isolated checks cover
  cross-process contention and blackout recovery-record preservation.
- Short control windows hide the optional desk map and restore it when space
  permits. Monitor cards calculate their height at the fixed available width,
  reclaiming blank space. Actual-window checks cover scroll reachability,
  repeated resize and the visibility boundary; a synthetic development-build
  preview replaces the outdated README image and caption.
- A Settings menu in the control window exposes Windows startup, minimised
  launch, keep-running behavior and saved-warmth reapplication. It stays in sync
  with tray startup switches, retains the runtime preference on an unconfirmed
  save and labels unreadable startup registration as unavailable. Accessible
  status text explains outcomes; toggles do not adjust current monitor values.
- Isolated main-window lifecycle checks now use the actual GUI assembly and
  modal editor. An internal dependency boundary preserves default native
  behavior while allowing synthetic scans, temporary settings and fake I/O.
  Checks cover rebuild during editing, canceled exit, failure/reopen and late
  profile reads after hiding or closing the window.
- Hiding and re-showing the window no longer revives an obsolete profile-open
  request or error prompt. A current read failure updates the status line and
  releases the editor admission for a fresh retry.
- Profile reads and saves run in the background. Pending saves keep the UI
  responsive while blocking duplicate writes and ordinary close/cancel; failures
  retain the draft and distinguish an unconfirmed save from unchanged file bytes.
  Confirmed snapshots refresh estimates without a second disk read. Canceled
  main-window close preserves its monitor worker and tray.
- In-app brightness-profile editing with peak/floor, endpoint provenance and
  source notes. Preview current estimated nits and preset percentages without
  adjusting displays; save/remove preserve other profiles, retain backups and
  reject changed or ambiguous source files. Short windows keep actions visible.
- Opening the app again requests its existing control window instead of asking
  the user to find the tray icon. Activation has bounded local IPC, strict
  messages, account/session checks and a fallback when the primary is unavailable.
- Isolated activation checks cover repeated and cross-process requests, invalid
  frames, slow/unresponsive peers, callback refusal and shutdown. Extracted
  package probes also verify an isolated activation exchange.

- Hardware-free transport tests control DDC writes, readbacks, restores and
  failures with barriers, covering coalescing and request-ordering races.
- An internal transport boundary separates native DDC/WMI calls from worker
  scheduling, so future backends can reuse ordering and verification behavior.

- Adjustment failures remain on their monitor card with Connection help and an
  explicit Dismiss action. Brightness, contrast, warmth and extra-control
  failures accumulate without being hidden by later status messages or retries.
- Warmth reapply failures now reach the card; adjustment status messages
  distinguish requests from confirmed results. Late write-failure callbacks
  from replaced monitors are ignored.

- Monitor cards wrap long display names, screen shortcuts and preset buttons
  within their available width, and reclaim vertical space when widened.
  Hardware-free checks cover long names and shortcuts, resize reversal,
  parent bounds, overlap and simulated geometry/font scaling.

- Verified portable ZIPs now include an offline quickstart, licence and SHA256
  manifest. Unique build folders preserve earlier artifacts; CI uploads the
  verified ZIP and archive hash.
- Extracted-package checks verify content, hashes, x64 architecture, versions,
  bounded CLI probes and synthetic GUI startup without monitor access.

- A minimal support report can be previewed and copied from Connection help,
  or printed with `lumendeck-cli --support-report` and optional `--json`.
  Its explicit field allowlist excludes names, serials, device identifiers,
  paths and raw errors. It does not upload data or adjust a display.

- Offline Connection help reports hardware brightness and contrast separately,
  explains external and built-in display connection paths, and offers next
  steps for empty or failed scans. Cards link directly to the guide when a
  control is unavailable.

- Grouped desk commands wrap within the window instead of forcing a wider
  minimum width. Presets, display tools and Night desk remain separate, and
  the work-display label stays beside its selector when a row wraps.
- Hardware-free command layout checks cover resizing, simulated 100/150/200%
  scaling, command growth and rendering without monitor enumeration.

- Night saver keeps a selected work display unchanged and blanks the other
  displays at minimum brightness. One button in the window or tray restores
  the saved desk; independent blackouts remain independent.
- Slider readouts accept exact values by click, F2 or Enter. Brightness and
  contrast use one-unit keyboard and wheel steps, with Shift for ten units;
  colour temperature supports direct Kelvin entry and 50 K steps.
- Hardware-free regression checks cover precision controls, recovery
  persistence and night-session ownership, including disconnected displays.

- Every monitor card can temporarily blank that one display with a black,
  borderless overlay and its minimum supported brightness. Clicking the overlay
  or using the optional per-monitor global shortcut restores the exact saved
  brightness. Exit and interrupted-start recovery are reversible too.

### Fixed

- Shared command buttons require a matching Space/Enter press and release.
  Orphan/duplicate releases, focus loss and disable/re-enable cannot complete an
  old press. Disabled or disposed commands reject direct and assistive actions;
  Ctrl/Alt chords do not arm a plain press. Successful clicks consume an armed
  keyboard press before callbacks, avoiding duplicate MSAA/direct-plus-key-up
  dispatch. Native cancel-mode messages cancel presses; activation suppresses
  character processing.
- Calculated nits remain labeled as estimates for all profiles. User-profile
  selection and declared endpoint measurements are independent; missing Measured
  flags default false and explicit values survive loading. Reloads clear stale
  profiles when the replacement file is absent, empty or invalid.
- CLI list JSON keeps nitsAreEstimated but now always reports true; new
  luminanceProfile and profileEndpointsMeasured fields describe source and
  endpoint declarations without claiming measured current output.
- Public documentation distinguishes estimated luminance and model-implied
  warmth from physical measurements, qualifies calibration recovery and explains
  name aliasing limits. Tool comparisons use linked official feature descriptions.
- Panel-profile guidance now targets user panels.json rather than a removed
  built-in monitor table. Gamma-test messages describe model checks rather than
  measured physical colour temperature.
- Old worker and tray readbacks cannot overwrite a newer requested brightness
  or contrast value. Synchronous restore uses the same request freshness rules.
- Failure subscribers run outside hardware/queue locks; a throwing subscriber
  cannot duplicate notifications or stop later writes. Idle includes active
  write, restore and tray-read I/O.

- Installation and uninstall refuse a running app instead of force-killing it,
  preserving the normal blackout restore path. Uninstall removes only owned
  binaries and preserves unrelated files.
- Release verification restores its temporary runtime environment on failure,
  isolates native bundle extraction and no longer probes physical monitors.

- Bug reports now request the minimal support report first and allow reports
  when the app cannot start. Detailed diagnostics carry a privacy notice.

- Disabled contrast no longer implies that all DDC/CI control is missing.
  Inventory counts brightness across both DDC/CI and WMI, and failed or unchecked
  scans are not presented as current control availability.

- CLI help now names `lumendeck-cli` in runnable examples and accurately
  explains that a bare console invocation shows help; `LumenDeck.exe` opens
  the window.

- Restoring a blackout cancels pending brightness writes after finishing any
  in-flight write, preventing a stale minimum-brightness command from undoing
  the restore. Original hardware values are persisted without rounding.
- A blackout is refused if recovery data cannot be saved. Refresh re-applies
  minimum brightness for active blackouts, and presets skip blanked displays.
- Editing another control during a blackout no longer saves its temporary
  minimum brightness as the monitor's Custom brightness.

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

## 1.1.0 development baseline

This section records development work in source carrying version `1.1.0`; it
does not represent a published release or tag. The published download is listed
under [1.0.0](#100--2026-08-04) below. The next public version still needs to be
selected; local candidate version numbers do not establish publication.

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

- `LUMENDECK_ANONYMISE=1`, which replaces displayed monitor names with `Display A`,
  `B`, `C` in the window, `--list` and `--diagnose`. EDID identity, device paths
  and raw diagnostic details still require review/redaction before sharing.

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

## [1.0.0] — 2026-08-04

The first published GitHub download offered separate self-contained
`LumenDeck.exe` and `lumendeck-cli.exe` assets. See its [original release notes][1.0.0]
for the features in that build. The development baseline and Unreleased work
above are separate from that published download.

[1.0.0]: https://github.com/snowyukitty/LumenDeck/releases/tag/v1.0.0
