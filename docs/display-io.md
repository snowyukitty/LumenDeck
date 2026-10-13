# Display I/O and request ordering

`DdcWorker` owns GUI brightness/contrast requests, advertised VCP writes,
synchronous blackout restores and tray-return value observations.
`IDdcTransport` is an internal dependency boundary, not a public plugin API.
`NativeDdcTransport` uses the existing Win32/WMI calls and monitor pacing;
tests inject a scripted transport with no native handles.

## Ownership and locks

The private queue lock protects pending/current request dictionaries, active
operation counts, and optimistic/readback brightness and contrast assignments.
Those model members are plain fields with no callbacks or hidden lock acquisition.
`Set` publishes a fresh request identity and its requested model value in one
transition. GUI callers must not assign the value before or after `Set`.

Each monitor's `WriteGate` orders worker transport, value observation and
synchronous restore. A worker selects target keys, then acquires `WriteGate`
before atomically claiming a pending request and counting it as active. Restore
can therefore remove an older pending request before the worker claims it.
Native handle protection and pacing remain in `Monitor.UseDdc`'s separate I/O
lock. The nesting order is `WriteGate` then the short queue lock or native I/O
lock; the queue lock never spans transport, waits, joins or callbacks.

Current request identity survives dequeue. Readback can mutate only its own
field and only while that exact request remains current, with validation and
mutation under the same queue lock. Equal values do not make two requests the
same operation. Completing an older request never removes its replacement.
Brightness, contrast, monitor reference identity and each VCP code coalesce
independently. Native VCP writes do not mutate brightness/contrast model fields.

## Restore, observations and cancellation

Restore holds `WriteGate`, removes old pending brightness, installs a restore
request identity and performs immediate transport outside the queue lock. A
later `Set` replaces that identity, so restore completion cannot overwrite its
optimistic value. The newer hardware write follows restore. Transport success
is returned independently of whether its model commit was superseded.

Tray return re-reads values of controls marked available by the last scan,
without changing support flags or ranges. It skips controls with current work,
uses observation identities for fresh reads, and leaves values alone when reads
fail or newer requests arrive. A full Refresh still rebuilds monitor availability,
can reapply warmth and can recover blackouts. Enumeration and CLI operations use
separate monitor objects and do not share the GUI worker's request state.

`ClearPending` removes queued work and invalidates active model commits/retries.
It cannot cancel native I/O already authorized or in progress. `Idle` means no
pending requests or active write/restore/tray-read finalization; it excludes
failure-observer delivery. It is a snapshot, not a promise that no later producer
will enqueue work.

Dispose rejects later enqueues, cancels pending identities and joins the writer
for at most 1.5 seconds. The writer releases its own signal after its final use.
A callback can dispose its worker without joining itself. A hung native driver
can outlive this join bound; this is not a native-operation timeout guarantee.

## Verification and failures

External brightness/contrast final values retain three write attempts and an
80ms settle delay, with readback and retry for mismatches or refusals. Successful
driver submission alone is not proof of the display value. Final unreadable
values remain requested values and produce a failure notice; successful reads
can restore the observed value. Superseded/cancelled work does not commit or
report final failure. WMI and arbitrary VCP writes retain their existing driver
return semantics without final-value verification. D6 hardware-off is excluded.

One terminal failure invokes a captured subscriber list after both worker locks
are released. Each subscriber is isolated from exceptions in another subscriber;
observer exceptions cannot enter transport retry handling. UI handlers must
marshal, recheck Monitor reference ownership, and read the current model instead
of applying an old value payload. Card notices describe earlier unconfirmed
adjustments, not an assertion that the latest adjustment is still failing.

The barrier-controlled cases in `test/ControlTests/DdcWorkerChecks.cs` cover
selected contention, freshness, restore, observation, lifecycle and failure
schedules. They do not establish native hardware compatibility, Narrator or
complete WinForms dispatch/disposal behavior. Run the real-monitor acceptance
checks before declaring a hardware-verified release.

## Presentation observations

Observe returns an immutable snapshot under the same queue lock: Requested,
LastObserved and Observed/Pending/Confirmed/Unconfirmed/Failed. These snapshots
are keyed by Monitor reference plus feature/code, never the selected UI index.
Enqueue preserves the last observation before publishing its optimistic model value.
Matching DDC readback updates both; a mismatch preserves the request and records the
actual read value. An accepted WMI write has no matching readback and stays unconfirmed,
including restoration. Superseded request completions cannot update snapshots.
ClearPending clears the observation generation during discovery replacement.

The GUI's disposed-with-card timer only reads this state. The existing failure event
and persistent notice remain separate, so later requests do not erase failure history.
This adds no transport reads, retries or new hardware operations. It does not change
per-monitor write ordering, gamma composition or saved raw restoration values.
