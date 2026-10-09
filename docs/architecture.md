# Architecture and extension map

Use this map to find where a change belongs and which existing fixture can prove
its behavior. It describes the current source, including unreleased features;
it is not a public SDK or a runtime plugin contract.

## Product layers

| Layer | Entry point | Responsibility |
| --- | --- | --- |
| Windows GUI | [Program.cs](../src/LumenDeck/Program.cs), [MainForm.cs](../src/LumenDeck/MainForm.cs) | Single-instance window, command orchestration, cards, owned help/editor windows, tray and blackout sessions |
| Console | [Program.cs](../src/LumenDeck.Cli/Program.cs), [Cli.cs](../src/LumenDeck.Core/Cli.cs) | Argument parsing, console output and exit codes; starts with help when no arguments are supplied |
| Engine | [LumenDeck.Core](../src/LumenDeck.Core/LumenDeck.Core.csproj) | Discovery, DDC/CI and WMI, GPU gamma, estimates, profiles, settings and worker request ordering |

Both frontends reference Core. Core does not reference WinForms, but targets
Windows and uses native display APIs. Its types stay internal, exposed to the
frontends and test assemblies through `InternalsVisibleTo`; extracting it into a
supported package would require a separate API and compatibility design.

The GUI queues brightness/contrast and advertised extra-control writes through
[DdcWorker](../src/LumenDeck.Core/DdcWorker.cs). The CLI uses its own enumeration
and command path; it does not reuse the GUI's worker instance or request state.
[Display I/O design](display-io.md) defines ordering, readback, cancellation and
the limits of driver success. Warmth uses the GPU gamma path rather than that
brightness transport.

## Choose an extension route

| Change | Start here | Preserve and verify |
| --- | --- | --- |
| A desk command or preset | [DeskCommandBar](../src/LumenDeck/DeskCommandBar.cs), MainForm, [Presets](../src/LumenDeck.Core/Presets.cs) | Keep presentation separate from I/O; preserve remembered Custom levels and blackout ownership. Resolve a preset's active target through `AppSettings.PresetLevel` at request time, never from the built-in table. Use command-layout and actual MainForm checks. |
| A scheduled change | [PresetSchedule](../src/LumenDeck.Core/PresetSchedule.cs), `AppSettings.Schedule`, [MainForm.Schedule](../src/LumenDeck/MainForm.Schedule.cs), [PresetScheduleView](../src/LumenDeck/PresetScheduleView.cs) | Keep period arithmetic pure and take "now" from `MainFormHost.Now`. Request a period once, only when displays can be adjusted, and let manual changes stand until the next period. The editor previews through the window's own `Outlook`, so a preview cannot promise a request the checks would not make. Use `--schedule-only` in both test projects. |
| A preset target | [AppSettings](../src/LumenDeck.Core/AppSettings.cs) `PresetTargets`, [PresetEditorView](../src/LumenDeck/PresetEditorView.cs), MainForm.Settings | Store only overrides; keep unknown names and fields. Build drafts with `WithPresetLevels` and install them only after a confirmed save through MainForm's `SettingsDraft`. Use `--presets-only` in both test projects. |
| Another advertised VCP control | [VcpCatalog](../src/LumenDeck.Core/VcpCatalog.cs), [Capabilities](../src/LumenDeck.Core/Capabilities.cs), [FeatureControls](../src/LumenDeck/FeatureControls.cs) | Offer known control codes advertised by the monitor. For select controls with no advertised values, discovery falls back to catalog values. Preserve unknown-value labels and risky-action confirmation. Test discovery/presentation before deliberate hardware acceptance. |
| An estimate or editable panel profile | [PanelDatabase](../src/LumenDeck.Core/PanelDatabase.cs), [LuminanceEstimate](../src/LumenDeck.Core/LuminanceEstimate.cs), [PanelProfileStore](../src/LumenDeck.Core/PanelProfileStore.cs), [ProfileEditorView](../src/LumenDeck/ProfileEditorView.cs) | Estimates remain estimates; profile saves preserve other entries and unknown fields. Check previews, invalid inputs, conflicting writers and the owning modal editor. |
| A new window or dialog | [WindowChrome](../src/LumenDeck/WindowChrome.cs) | Call `WindowChrome.Apply(this)` from `OnHandleCreated` so the title bar, scrollbars and combo boxes match the theme. Never assign `ShowInTaskbar` on the main window after it exists: it recreates every child control. |
| An offline reference window | [ReferenceForm](../src/LumenDeck/ReferenceForm.cs), [ReferenceView](../src/LumenDeck/ReferenceView.cs), [MainFormHost](../src/LumenDeck/MainFormHost.cs) | Reuse pinned Close, local scrolling keys and owned-window lifetime. Inject non-activating factories for native lifecycle tests. Reading help should not scan or adjust a display. |
| A brightness transport/backend | [IDdcTransport](../src/LumenDeck.Core/DdcTransport.cs), DdcWorker, [MonitorService](../src/LumenDeck.Core/MonitorService.cs) | Start with scripted transport ordering/readback tests. Discovery and the CLI have separate integration paths; implementing the transport alone does not add a complete backend. |

`DdcWorker.SetVcp` does not queue code `0xD6` at any value. Callers must still supply
advertised, appropriate controls; queueing an arbitrary code is not evidence that
the monitor implements it. Screen blackout uses an owned overlay plus supported
brightness recovery instead of monitor power-off. See [the monitor lessons](notes.md)
before adding a native operation.

For extra controls, start with `--capabilities-only` in ControlTests to check
parsing and value-label rules, then `--feature-accessibility-only` for native
presentation and fake request routing. Parser checks do not exercise `Discover`,
which performs native reads. `Risky` changes warning colour and confirms select
changes; action buttons always confirm. It does not add confirmation to continuous
sliders. A new continuous control that needs confirmation requires a deliberate
interaction design before it is offered.

For responsive commands, follow the [command layout contract](command-layout.md).
For estimates and storage, follow [profile editing](profile-editing.md) and
[settings storage](settings-storage.md). Add contextual accessible names and
accurate requested/observed state when extending controls; an accepted driver
call is not proof that the screen changed.

## Native UI without an active desk

[MainFormHost](../src/LumenDeck/MainFormHost.cs) supplies the desktop window's
external collaborators. MainForm owns the supplied worker. The production
default loads real settings and can repair an existing enabled startup entry
during construction. Its first show scans displays and may reapply warmth,
restore interrupted blackout brightness and save settings. Neither constructing
nor showing the default window is a harmless test fixture.

Use the `Fixture` in [AppTests/Program.cs](../test/AppTests/Program.cs) as the
starting point for actual-window tests. It supplies temporary settings, fake
transport/discovery, gamma persistence and startup collaborators, hides the tray
icon, and creates non-activating windows. New help/editor factories must retain
that boundary, including their reveal behavior. Avoid calling the production GUI
entry point: it also loads profiles and may create the example profile file.

AppTests uses a private Windows desktop and checks logical selection there.
This is trusted test execution, not a security sandbox or evidence of physical
foreground keyboard delivery. Keep hardware, live readers, actual DPI changes
and clean-profile installation as separate acceptance work.

## Verification and delivery

Run the three initial commands in [Contributing](../CONTRIBUTING.md#working-on-the-code),
then select focused scenarios from [the test guide](../test/README.md). The control
suite links the controls under test; AppTests references the actual GUI assembly.
Choose the latter when orchestration, ownership or async window lifecycle matters.
Native renders supplement geometry assertions; inspect them when a visual change
could hide labels, recovery steps or the final action.

Product version comes from [Directory.Build.props](../Directory.Build.props).
The [packaging guide](packaging.md) describes the allowlisted portable payload,
extracted-binary probes and remaining install/download checks. Source builds,
locally verified candidates and published releases are distinct; consult the
[release readiness record](release-readiness.md) for the exact candidate identity
and [Releases](https://github.com/snowyukitty/LumenDeck/releases) for published app
files. A passing build or synthetic test does not authorize a release or prove
physical monitor compatibility.
