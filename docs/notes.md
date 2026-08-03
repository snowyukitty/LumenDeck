# Design notes

Things that cost real time while building this. Every one of them produced a
plausible, confident, wrong result before it produced an error — which is the
whole reason they are worth writing down.

## A physical monitor handle can legitimately be zero

`GetPhysicalMonitorsFromHMONITOR` fills in a `HANDLE` per monitor. Almost every
Win32 handle uses `NULL` as its invalid value, so the natural way to record "this
monitor has no DDC handle" is to leave the field at `IntPtr.Zero` and test for
it.

On the hardware this was written against, the four monitors came back as handles
**0, 1, 2 and 3**.

The consequences were all silent:

- every read for the first monitor was skipped, so a perfectly healthy panel
  reported *no DDC support* in the UI;
- every write to it was dropped by the same guard in the write queue;
- its handle was never passed to `DestroyPhysicalMonitor`, because the disposer
  tested the same way — a leak on every re-enumeration.

Three separate hypotheses were chased and falsified first (COM initialisation
order from a WMI query, reading EDID out of the registry before the DDC call,
and firmware needing a settling delay after the handle is opened), because from
the outside the symptom looks exactly like a flaky monitor. What settled it was
one bare `GetMonitorBrightness` call placed inside the app at the same point,
bypassing every wrapper: it returned `true` with a correct value on a handle the
app had already written off, and printed `handle=0x0`.

**A physical monitor handle is an opaque driver-defined value with no documented
invalid sentinel.** Track validity in a separate boolean.

## `SetVCPFeature` returning true means nothing

MCCS "set" requests are fire-and-forget. There is no acknowledgement in the
protocol, so the return value tells you the request reached the driver and
nothing more.

A monitor was measured that advertises the RGB gain codes `0x16/0x18/0x1A` in
its own capability string and ignores every write to them. Sweeping blue across
`0, 10, 20 … 100` produced the identical read-back for all thirteen values, in
both directions, with every call reporting success.

Worse, the failure impersonated a partial success. Escalating to the monitor's
"User" colour preset moved the reported triple, and one channel happened to land
on exactly the requested number — because that is what the preset stores. It
read as "mostly applied, firmware clamped the rest". Not one write had taken
effect.

Two rules came out of it. **A partial-looking result is not evidence of partial
success.** And **a plausible fix that does not move the postcondition means go
and measure, not add another retry** — pacing the writes 120 ms apart was a
genuine fix to a genuine bug (MCCS does require a gap between consecutive
writes) and it changed nothing here, which was the tell.

## An unsupported VCP code answers with a plausible number

`GetVCPFeatureAndVCPFeatureReply` on codes a monitor does not implement does not
reliably fail. One panel returned `80` for the three RGB black-level codes —
reading perfectly as "black level is raised, fix it" — while its own capability
string listed none of them. A different monitor beside it *does* implement them,
so nothing in the output distinguishes the real reading from the invented one.

Read the capability string first and only act on codes it advertises.

## Working set cannot tell a leak from an allocator

Rebuilding the monitor list repeatedly showed memory climbing about half a
megabyte per rebuild — twenty-five rebuilds, thirteen megabytes, monotonic. That
reads as a leak.

GDI and USER handle counts were flat across the same run, which is the part that
actually matters (those are quota-limited and exhausting them breaks window
creation). The managed side needed a better instrument: `GC.GetTotalMemory(
forceFullCollection: true)` runs a blocking collection first, so what it reports
is memory that is genuinely still reachable.

Measured that way: **401 KB at the first rebuild, 399 KB at the twenty-first**,
with control and panel counts constant. There was no leak. The working-set
growth was the allocator not returning pages, and the forced collections brought
the working set *down* below where it started.

There was a real leak found on the way, though, and it was not the one the
numbers pointed at: `Controls.Clear()` removes controls without disposing them,
so every rebuild leaked the panels' window handles and fonts. Fixed by disposing
explicitly, and by sharing `Font` instances process-wide — a `Font` assigned to a
`Control` is not owned by it and survives the control's disposal.

## Colour temperature must compose, not overwrite

The first version built each gamma ramp from scratch: identity, multiplied by
the white-point factors. "Warmth off" therefore wrote a *flat* ramp.

That does not restore a display, it flattens it. Whatever LUT an ICC profile, a
colorimeter or an accessibility tool had loaded is silently discarded, and a
calibrated monitor comes back with different greys and a different black point —
after pressing a button labelled "off".

Capture each display's ramp before touching it, multiply onto that, and restore
the captured copy to turn it off.

## Raw VCP values are not percentages

MCCS lets a monitor report any brightness range it likes, and `0..255` is common.
The luminance model works in percent, so converting straight from "61 percent" to
a raw `61` would set a 0–255 panel to roughly a quarter of the intended
brightness — while the UI cheerfully reported the target luminance.

Read the range the monitor advertises and convert in both directions.

## Valid JSON is not usable data

`{"Monitors": null}` deserialises without complaint and throws a
`NullReferenceException` on first use. Because settings load inside the form's
constructor, that happens *before* the message loop exists, where
`Application.ThreadException` cannot catch it: the app dies at launch with no
window and no error dialog.

Repair the shape after deserialising, not just the parse. There is a regression
test for six flavours of broken settings file.

## Windows numbering is not an identity

`\\.\DISPLAYn` carries gaps from past hot-plugging — a four-monitor desk can
enumerate as `DISPLAY1, DISPLAY2, DISPLAY3, DISPLAY6` — and the suffix need not
match the number Windows paints on screen during *Identify*.

Acting on a guessed mapping adjusts the wrong monitor and looks exactly like
success. Settings are therefore keyed on EDID identity, with the physical port as
the tie-breaker when two panels are genuinely identical (same model, same product
code, no serial descriptor — which is normal for a matched pair bought together).

And the capability string's own `model()` field is not trustworthy either:
firmware gets copy-pasted across a product family, and a 27" panel has been seen
reporting a 31.5" sibling's model name. EDID plus the physical dimensions settle
it.
