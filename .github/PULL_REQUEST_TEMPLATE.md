## What this changes

<!-- One or two sentences. -->

## How it was verified

<!-- CI cannot test hardware: a GitHub runner has no display that answers
     DDC/CI. So say what you ran against real monitors, and on which. -->

- [ ] `dotnet build LumenDeck.slnx -c Release` is clean
- [ ] Ran against real monitors, and said which below
- [ ] `test\Test-Startup.ps1` passes
- [ ] `test\Test-RebuildLeak.ps1` passes

Monitors tested on:

## If this touches the DDC layer

- [ ] Any value that matters is **read back**, not trusted to the call's return.
      MCCS "set" is fire-and-forget; `SetVCPFeature` returning `true` means the
      request reached the driver and nothing more.
- [ ] No VCP code is read or written unless the monitor advertised it in its
      capability string. Unimplemented codes answer with plausible numbers
      rather than failing.
