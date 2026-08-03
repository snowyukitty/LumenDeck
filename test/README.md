# Tests

Two PowerShell checks that need **real monitors**, which is why they are not in
CI: a GitHub runner has no display that answers DDC/CI, so the workflow can only
prove the binaries build and start.

Build first (`dotnet build LumenDeck.slnx -c Release`), then:

```powershell
powershell -File test\Test-Startup.ps1
powershell -File test\Test-RebuildLeak.ps1 -Cycles 25
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
