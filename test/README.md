# Tests

Three PowerShell checks that need **real monitors**, which is why they are not in
CI: a GitHub runner has no display that answers DDC/CI, so the workflow can only
prove the binaries build and start.

Build first (`dotnet build LumenDeck.slnx -c Release`), then:

```powershell
powershell -File test\Test-Startup.ps1
powershell -File test\Test-RebuildLeak.ps1 -Cycles 25
powershell -File test\Test-Gamma.ps1
```

## Test-Startup.ps1

Time to window, and survival of a broken settings file.

Enumerating monitors over DDC used to happen inside the form constructor, so
nothing appeared for **6.7 seconds** after launch - measured, not estimated. It
now runs off the UI thread and the window shows in well under a second.

It also writes six flavours of damaged `settings.json` - including
`{"Monitors": null}`, which is valid JSON that throws on first use - and
requires a window every time. That failure mode is nasty precisely because it
happens before the message loop exists, so no exception handler can report it:
the app just never appears.

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

The colour temperature that is actually reaching the glass.

This one exists because the app shipped a warmth far stronger than the number on
its own slider and **nothing inside the app could tell**: every value it could
read back was a value it had written. The error was in what the display does
with those values afterwards, so the measurement has to start from the ramp on
the GPU and work forward to the light.

It reads each display's real gamma ramp, converts the encoded scale to emitted
light (`f^2.2`), and names the temperature that implies:

```powershell
powershell -File test\Test-Gamma.ps1                  # report
powershell -File test\Test-Gamma.ps1 -ExpectKelvin 4600
powershell -File test\Test-Gamma.ps1 -ExpectNeutral
```

Two regressions it catches, both of which shipped:

- **Wrong domain.** `--preset Night` asks for 4600 K. Before the fix this
  measured ~3060 K on all four monitors on the development desk.
- **Compounding.** Run `--preset Night` or relaunch the window several times and
  the reading must not move. It used to roughly halve the blue channel each time,
  because the app kept capturing its own warm ramp as the display's baseline.

A display carrying a real ICC or colorimeter LUT is called out separately: its
ramp is not a scaled identity, so the temperature reported is the tint on top of
the calibration rather than the whole story.
