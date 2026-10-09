# Contributing

Thanks for looking. The most useful contributions are small and concrete.

## The single most useful thing: a panel profile

LumenDeck targets estimated luminance with a linear model using peak and floor
brightness in cd/m² (nits). Specific profiles live in your own
`%APPDATA%\LumenDeck\panels.json`; there is no built-in table of monitor models.
Without a matching profile, the app uses separate generic desktop/laptop estimates.

1. Run `lumendeck-cli --list` and choose a model-name substring for your profile.
   Review the name before sharing it; exclude serials or device identifiers.
2. Record peak and floor luminance, including their sources and picture mode.
   A rated peak from a spec sheet does not establish the floor at brightness 0.
3. Use **Edit brightness estimate…** on a card, or edit `panels.json` and restart
   the app to load it. If reporting
   a profile, use the Panel profile issue form with sources and any assumptions.

The UI always labels calculated nits as estimated and distinguishes user profiles
from generic ones. `Measured` defaults to `false`; set it to `true` only to declare
instrument-measured peak and floor endpoints. This declaration is not verified
by the app. Keep sources, conditions and assumptions in the profile's `Note`.
Even measured endpoints do not
verify the model at intermediate settings or guarantee perceived matching.

## Bug reports

Start with `lumendeck-cli --support-report`, or Connection help > View support
report in the window. Review the preview, then copy it into the issue. The
report includes observed control availability and native ranges, without names,
serials, device identifiers, paths or raw errors. It does not upload anything.
Display counts and ranges still describe a setup, so this is data minimization,
not an anonymity guarantee.

These report/profile-editing features describe the current development source.
If your downloaded release lacks them, describe the problem and name the release
or use `lumendeck-cli --version`; the support report is optional. You do not need
to share raw diagnostics to open a bug report.

`lumendeck-cli --diagnose` remains useful for deeper troubleshooting. It includes
EDID identity, device identifiers, the chosen backend and raw DDC results. Review
and redact it before sharing; a raw diagnostic is not the default public report.

## Working on the code

Use Windows and the .NET 10 SDK; see [building from source](README.md#build-from-source).
Run these commands from the repository root for a first local check:

```powershell
dotnet build LumenDeck.slnx -c Release -warnaserror
dotnet run --project test/ControlTests/ControlTests.csproj -c Release
dotnet run --project test/AppTests/AppTests.csproj -c Release
```

Start with the [architecture and extension map](docs/architecture.md) to find
the owning layer and existing test fixture. It covers commands, advertised VCP
controls, estimates/profiles, reference windows and transport backends.

CI builds with `/warnaserror` on a clean Windows runner and runs the isolated
control and main-window lifecycle checks. These use temporary settings and off-desktop
overlays without writing to physical monitors. They cover precision controls,
blackout recovery and night-session ownership.

AppTests references the actual GUI assembly and injects MainFormHost collaborators
for monitor enumeration, worker transport, settings, gamma persistence, startup
registration and profile opening. It exercises the real modal editor and window
cleanup without showing a tray icon. MainFormHost is an internal composition
boundary, not a public plugin or complete platform-abstraction API. Physical
warmth, hotkeys and overlays still require their established paths and checks.

AppTests runs on a private Windows desktop without switching the input desktop.
Its logical focus and simulated font/geometry checks do not prove physical
keyboard, screen-reader or mixed-DPI behavior. These are trusted local test
executables: the private desktop and injected collaborators are not a filesystem,
network or process security sandbox.

A GitHub runner has no display that answers DDC/CI. Hardware behavior still
needs local verification with the PowerShell tests in `test/` and the manual
night-desk checklist in [test/README.md](test/README.md). Some hardware tests
restart LumenDeck and temporarily replace its settings; read each script before
running it against an active desk.

Keep that physical acceptance separate from the first local check. In particular,
`Test-Startup.ps1` and `Test-RebuildLeak.ps1` launch the real application; they are
not additional hardware-free unit tests. Choose them deliberately after reading
their setup and cleanup behavior in [the test guide](test/README.md).

### Extending the command interface

Read-only guides can derive from `ReferenceForm` and supply `ReferenceSection`
content to its `ReferenceView`. The common window owns a pinned Close action,
local scrolling keys, Escape and reveal behavior. `KeyboardHelpForm` and
`AboutForm` use it. Inject a non-activating factory through `MainFormHost` for
native off-desktop lifecycle checks; tests must override Reveal to avoid Activate.

`DeskCommandBar` owns presentation and emits command events; keep monitor I/O
and settings decisions in the main-window orchestration. Add related actions to
a `CommandSection.Commands` row so the section grows when commands wrap.

For automatic `FlatButton` sizing, capture the intended minimum Size into
MinimumSize **before** setting AutoSize. Preferred size follows the current
Text/Font, and a constrained row can wrap a long label vertically. Explicitly
sized card/dialog hosts retain their bounds. Labels are literal text, including
`&`; these controls do not implement Alt mnemonic activation. AccessibleName
can provide an explicit contextual name without changing the visible label.

Run the focused `--commands-only` checks with a render directory for native
layout inspection, then the main-window checks for orchestration changes. Size
and font simulations complement physical DPI acceptance; see
[the test guide](test/README.md).

See [the command layout contract](docs/command-layout.md) for row proposals,
minimum floors, shared measurement/paint insets and property-change behavior.

### Two habits this codebase is built on

**Verify the postcondition, not the call.** MCCS "set" requests are
fire-and-forget: `SetVCPFeature` returning `true` means the request reached the
driver and nothing more. Monitors routinely accept a write and ignore it. If a
change matters, read it back.

**Never act on a VCP code the monitor did not advertise.** Reading an
unimplemented code does not fail — it answers with a plausible number, which is
indistinguishable from a real setting. Parse the capability string first.

Both of these, and several other traps that cost real time, are written up in
[docs/notes.md](docs/notes.md). It is worth ten minutes before touching the DDC
layer.

For worker scheduling, read [display I/O design](docs/display-io.md). New
backends should implement the internal transport boundary and add deterministic
fake-transport scenarios before hardware acceptance. Do not write requested
brightness or contrast into the GUI's monitor model separately from enqueueing.

## Style

Match what is there. Comments explain *why*, especially where the code looks
odd — most of the odd-looking code is odd because something failed the obvious
way first.
