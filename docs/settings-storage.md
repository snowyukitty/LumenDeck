# Settings storage and recovery

`settings.json` contains preferences (including the app palette `ThemeId`), saved warmth and Custom levels, edited preset
targets, the preset schedule, shortcuts,
the night-work display choice, and recovery markers for reversible screen
blackouts. GUI and CLI share the file under `%APPDATA%\LumenDeck`.

## Save outcomes

Successful saves confirm that this writer committed its captured snapshot.
They do not prove power-loss durability, hardware readback, or successful
future startup. An unconfirmed save can have changed the file already; runtime
rollback of a preference or shortcut is not a rollback of disk bytes.

The window keeps its observed save outcome in Settings, independently of the
transient footer. Retry captures a fresh full snapshot of active values through
the same background writer. It does not replay failed preference/shortcut drafts,
discover displays or adjust hardware. Busy/Failed permit retry; changed or invalid
sources require reopening or repair. Save status is an observation of this
window's attempts, not a live filesystem watcher or cross-process synchronization.

The window tracks edit revisions alongside captured snapshots. A successful older
snapshot can resolve a write error but cannot label newer tracked edits as saved.
Waiting edits can be saved manually or by the normal debounce/coalescing path.
Blackout-state notifications advance that revision too; restoration during a
pending write requests a fresh snapshot after settlement, preserving the cleared
recovery marker. Native checkpoint and forced-termination limits still apply.

| Outcome | Behavior and next action |
|---|---|
| Saved | The writer advances its source snapshot for its next save. |
| Busy | Another cooperating writer holds the mutex. No staging was requested by this writer; retry after it finishes. |
| Source changed | Existing bytes differ, the file was removed, or a file appeared after an absent-source load. This save is refused. Quit and reopen before saving again. |
| Unavailable | Loading failed or the JSON was unusable. The app uses session defaults and preserves the source; check storage before reopening. |
| Failed | Serialization or storage did not confirm completion. Check permissions, locks and available storage; the result does not guarantee unchanged bytes. |

Preferences, shortcuts, preset targets and the schedule retain their previous active value while
saving, then activate on confirmed completion; failure keeps that value. Both preference
surfaces disable JSON options with a pending explanation. Shortcut registration
uses current stable-key cards after completion, even after Refresh. Storage
success and Windows registration refusal remain separate results. Other live settings, such as a work-display choice or current
monitor adjustment, can remain in effect for the session without confirmed
persistence. CLI reports settings failures separately from monitor refusal and
returns exit code 2; monitor requests happened before this save, so inspect state
before retrying an adjustment.

Blackout admission still saves its original-brightness recovery marker before
lowering brightness or showing the overlay. A failed checkpoint aborts the new
blackout. Restoration attempts still run even if storage is unavailable. A
retained disk marker can cause another recovery attempt at the next launch.

Before repairing or resetting a damaged file, restore any active blackouts and
exit normally. Back up the file privately; it contains monitor names and
identities. Inspect recovery fields before removing it. Do not reset it merely
to dismiss a warning while brightness recovery remains unresolved. Reopen after
repairing storage or replacing the file so the app loads a fresh source.

## Implementation boundary

AppSettings loads and deserializes one captured byte snapshot. BOM text handling
retains legacy UTF-8/UTF-16/UTF-32 compatibility. Existing shape/value repair
remains, including a null monitor list and the withdrawn shortcut migration.
Unparseable, null-root or unreadable files are retained instead of being replaced
by defaults. Unknown root and per-monitor fields round-trip as JSON extension
data for compatibility with newer versions.

Save serializes on the settings object's owning thread. SettingsFileStore owns a
copy of those bytes and compares the original source under a per-path named
Windows mutex. It uses nonblocking admission; ownership, staging, commit and
release stay on one thread, including abandoned-owner recovery. Unique
same-directory staging uses CreateNew, Flush(true), then File.Replace for an
existing source or File.Move for first creation. Source validation repeats after
staging. Cleanup deletes only that invocation's staged file. Interrupted processes
can leave staging behind; those files are not automatically swept or loaded.

The mutex is global and hashes an absolute, case-normalized path. The local
cross-process tests do not establish multi-session permissions or security
against arbitrary native processes. Path aliases/junctions are not resolved to
one filesystem identity. A noncooperating editor can change the file between
the final comparison and replacement; validation is not a filesystem
compare-and-swap primitive. There is no merge of conflicting documents, no
transaction with gamma/registry/hardware state, and no power-loss guarantee.

Ordinary GUI JSON saves now run in the background: preferences, shortcuts,
work-display selection and debounced updates. PrepareSave captures serialized
bytes on the owning thread and creates a one-shot SettingsSaveOperation. Its
write executes once; completion runs on the capturing thread and holds admission
through UI settlement. An abandoned, unstarted operation cannot later write.
An unexecuted success claim cannot activate a preference. Do not call Task.Run
on the mutable AppSettings object or await inside the named-mutex write itself.

MainForm permits one write at a time. Ordinary changes while pending mark dirty
without capturing an old payload. Their next snapshot is taken after completion
and after successful requested preferences/shortcuts have become active. Late
completion on a disposed owner releases admission without touching its controls
or activating its draft. A saved file can therefore differ from the disposed
owner's in-memory draft state; no file rollback is implied.

Synchronous Save returns Busy while a prepared operation awaits UI completion,
even if its disk work already ended. Blackout admission then refuses before
dimming; restoration attempts still proceed, and subsequent ordinary saves can
capture the settled recovery state. All managed FormClosing events wait by
cancellation while either settings or profile saving is pending. Retry Exit
afterward; it captures a final current snapshot synchronously before cleanup.
Forced disposal/process termination and actual Windows shutdown behavior are
not covered by this ordering guarantee.

Blackout/final-exit saves, Windows startup operations and gamma-baseline saves
remain synchronous. This change improves ordinary settings-write responsiveness,
not every storage or native-hardware operation in the application.

See [the isolated checks](../test/README.md) for coverage and remaining acceptance.
