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

## …but "the ramp loaded right now" is not the baseline

The fix above was right, and its implementation was wrong in a way no test the
app could run on itself would ever have caught: the symptom looked like a colour
bug and the cause was a lifetime bug.

`SetDeviceGammaRamp` outlives the process that called it. Quit the app and the
warm ramp is still on the display. The enumerator captured whatever ramp was
loaded and called it "the ramp this display had before the app touched it",
which is true exactly once. On the next launch it captured the app's *own* warm
ramp and composed the saved warmth onto it again — and so did every Refresh
click and every `WM_DISPLAYCHANGE`, both of which re-enumerate.

What that predicts is what users reported:

- screens drift warmer the longer the app has been in use, on every make of
  monitor, because the compounding lives in the GPU and not in the panel;
- **"Warmth off" does not undo it**, because it faithfully restores a polluted
  capture;
- neither does the Day preset, whose 6500 K writes that same capture back.

Measured on the development desk, all four displays were delivering ~3100 K
while `settings.json` recorded `"Kelvin": 6500` on every one of them. The
settings said neutral and the glass was orange. There is no way back from inside
that design.

Storing the baseline is only half the repair, because a stored baseline is no
use if the app cannot tell whether the ramp it just read is its own. Two records
per display: the baseline, and a signature of the last ramp written. A ramp that
matches the signature is ours, so the display's own state is the stored
baseline.

The signature goes stale after a crash or a wiped settings directory, so
recognition falls back on shape. A ramp the app wrote onto an identity baseline
is the identity scaled by one constant per channel, with red untouched — warming
only ever removes green and blue. Nothing else produces that shape: a real
calibration LUT is a measured curve and strays from linear by whole percent, and
a ramp that dims red belongs to some other tool. When the shape matches, the
original baseline is recoverable exactly, because it was identity. When it does
not, the app declines to guess, which is the case where guessing would throw
away somebody's calibration.

`test/Test-Gamma.ps1` measures this from outside the app, which is the only
place it is visible.

## A gamma ramp is not in the same units as the light it makes

The same measurement turned up a second, independent fault, and this one was in
the arithmetic.

A gamma ramp holds **encoded** values. The display raises them to roughly 2.2
before any light comes out, so scaling a ramp entry by `f` scales the emitted
light by `f^2.2`, not by `f`.

The white points in `GammaControl` are linear light ratios — correctly derived,
clearly commented as linear — and were multiplied straight onto the encoded
ramp. Every setting therefore overshot by that exponent. The Night preset asks
for 4600 K, whose blue is 0.6345 of full; applied to the encoded ramp that emits
`0.6345^2.2 = 0.37`, a white point nearer **3000 K** — a visibly orange screen,
on any monitor, at a setting labelled 4600 K.

Nothing inside the app could catch this. Every value it could read back was the
value it had written; the error is in what the display does with them
afterwards. It needed a measurement that starts from the ramp and works forward
to the light.

Raise each linear ratio to `1/2.2` before it touches the ramp:
`(e · f^(1/2.2))^2.2 = e^2.2 · f`. Requested 4600 K now measures 4600 K.

The general lesson is worth more than the constant: **a correct number in the
wrong domain is still wrong, and it will not look wrong in any log.** The
comment saying "linear" was accurate and sat three lines above the code that
used it as though it were encoded.

## A setting nothing in the product could set

`StartMinimised` sat in `settings.json` from the first commit with no menu item,
no checkbox and no command-line flag. The only way to switch it on was to edit
the JSON by hand — so nobody did, and nobody found that switching it on kills the
process before it draws anything.

Setting `WindowState = Minimized` in the form constructor means the first
`WM_SIZE` arrives before the window has ever been shown. The minimise-to-tray
handler reacts to it by assigning `ShowInTaskbar`, and **assigning
`ShowInTaskbar` makes WinForms recreate the window handle** — which resizes the
form, which re-enters the handler with the state still `Minimized`. There is no
floor to the recursion until the stack runs out: exit code `0xC00000FD`,
`STACK_OVERFLOW`.

Nothing reports it. A stack overflow cannot be caught, so
`Application.ThreadException` never fires, `error.log` is never written, no
window appears and no tray icon appears. The app is simply absent — and the
launch that exercises this path is the one at login, which is precisely the
launch nobody is watching.

Two lessons, and the second is the bigger one. Hiding a window is not free:
`ShowInTaskbar`, `FormBorderStyle` and `Opacity` all recreate the handle, so any
resize handler that assigns them can re-enter itself. And **a setting with no way
to set it is untested by definition.** It is not dormant, it is unexplored. The
fix moved the minimise to `OnShown`, where the ordinary minimise path already
worked, and put the toggle in the tray menu so the path has a user who can reach
it.

## A version number that was right by coincidence

`--version` read `typeof(Cli).Assembly`. `Cli.cs` lives in `LumenDeck.Core`, so
what it printed was the engine's version and not the command line's. It had been
wrong from the beginning and looked perfect, because both projects happened to
carry `1.0.0`: the two numbers agreed for the same reason a stopped clock does.

The moment they were bumped apart — the window to 1.1.0, the other two left
behind — the command line began confidently reporting a version that belonged to
no executable anyone could download. `.github/ISSUE_TEMPLATE/bug_report.yml`
asks every reporter to paste that number.

One version now lives in `Directory.Build.props` for all three projects, and CI
asserts that what `--version` prints matches it and matches the file version on
both executables. **Agreement between two values is not evidence that either is
derived from the right source** — check where the number comes from, not whether
it looks right.

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
