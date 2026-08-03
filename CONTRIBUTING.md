# Contributing

Thanks for looking. The most useful contributions are small and concrete.

## The single most useful thing: a panel profile

LumenDeck aims every monitor at the same *perceived* brightness, which needs two
numbers per panel: its rated peak luminance, and the floor it still emits at
brightness 0 (typically 40–50 nits, never zero).

Only a handful of panels are in the built-in table. If yours is not, it falls
back to a generic estimate — and says so. To contribute yours:

1. Run `lumendeck-cli --list` and note the monitor name exactly as shown.
2. Find the panel's rated brightness in cd/m² from the manufacturer's spec sheet.
3. Add it to `src/LumenDeck.Core/PanelDatabase.cs` and open a PR, or just open an
   issue with the name and the spec sheet link.

Please say whether the numbers are from a spec sheet or measured with a
colorimeter. The code distinguishes the two and the UI tells the user which it
is; that distinction should survive into the data.

## Bug reports

`lumendeck-cli --diagnose` output is worth more than a description. It prints
the physical handle, the EDID identity, the chosen backend and what each DDC
call actually returned.

## Working on the code

```powershell
dotnet build LumenDeck.slnx -c Release
powershell -File test\Test-Startup.ps1        # needs real monitors
powershell -File test\Test-RebuildLeak.ps1
```

CI builds with `/warnaserror` on a clean Windows runner. It cannot test
hardware — a GitHub runner has no display that answers DDC/CI — so the
PowerShell tests in `test/` are the real verification and they must be run
locally against actual monitors.

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

## Style

Match what is there. Comments explain *why*, especially where the code looks
odd — most of the odd-looking code is odd because something failed the obvious
way first.
