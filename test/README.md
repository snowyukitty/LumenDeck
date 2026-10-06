# Tests

Hardware-free control regression checks (isolated temporary settings and
off-desktop test overlays, with no physical monitor writes):

```powershell
dotnet run --project test/ControlTests/ControlTests.csproj -c Release
```

These run in CI and cover raw-unit keyboard precision, Shift steps, wheel
accumulation,
silent synchronization, blackout persistence, refresh rebinding, interrupted
recovery, exact raw-value serialization, persistence failure, group ownership,
work-display protection, individual wake, and disconnected-display retries.
They require
an interactive Windows session for WinForms; the real-monitor scripts below
remain separate.

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
   saver** and confirm that the work display's brightness and desktop remain
   unchanged, while every other available display becomes black at its
   supported minimum brightness.
2. Use **Restore night desk** from the window and the tray in separate trials.
   Confirm each saved hardware brightness value is restored, including panels
   whose range is not 0–100.
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
