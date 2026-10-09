# Brightness-profile editing

Each card's **Edit brightness estimate…** opens a local draft. Peak and floor
use nits (cd/m²), with positive peak and 0 <= floor < peak. The endpoint checkbox
is the author's declaration of instrument measurements. Source notes should
identify spec-sheet values, assumptions, instrument and picture mode. All current
nits and preset percentages remain linear-model estimates.

Peak and floor keep invalid typed or pasted text until corrected. Save stays
disabled with a pinned explanation for empty, non-numeric, fractional, negative
or oversized values. Native thousands formatting follows the current culture;
malformed group separators are rejected rather than interpreted as another
number. Remove still acts on an existing full-name entry independently of a draft.

The internal WholeNumberInput is shared with exact-value entry. It owns bounded
integer parsing and preservation of invalid drafts, with no storage or monitor
collaborators. Each host owns its validation wording and commit action. Profile
preview/save read the visible drafts; numeric text notifications cannot overwrite
pending-save state or re-enable its actions.

The preview captures brightness percentage when opened. It does not follow
hardware changes while the dialog is open. Save/remove perform no monitor I/O,
apply no preset and change no Custom levels. They update profile selection for
subsequent presets and refresh estimate labels on the current cards. Even if the
original card is replaced during display enumeration, the editor keeps only
immutable name/display/preview values, not a Monitor or worker reference.

The save key is the full FriendlyName used by existing matching, compared without
case. Identical names share the result.
If a model name is unavailable, FriendlyName falls back to a Windows display
device name, which can change with the connection; review profiles after changing
screens or ports.

A profile is selected by the legacy longest-substring rule:
a more specific existing match on another display can take precedence. Inherited
broader profiles supply the initial draft but are retained when a full-name entry
is created. Remove is enabled only for an exact full-name entry and previews the
next broader or generic match. It never deletes a broader profile.

The core PanelProfileStore reads a file snapshot without writing. It rejects
malformed input, duplicate JSON properties, case-ambiguous model keys and entries
that cannot be read as profiles. Source decoding is strict: UTF-8 (with or without
a BOM) and BOM-marked UTF-16/UTF-32 in either byte order are accepted, while malformed
byte sequences are rejected rather than replaced with another character.
Numeric-invalid and null legacy entries are
preserved; they do not become active profiles. Save preserves other entries and
unknown fields on the target while updating its four known fields. JSON whitespace
can change; backups retain predecessor bytes. Notes stay local and are excluded
from support reports. Friendly-name anonymization still does not redact arbitrary
source notes; review screenshots before sharing.

Commit uses a machine-wide named mutex for cooperating writers, compares original
bytes and existence again, writes a temporary file in the same folder and flushes
it. A new file is moved without overwrite. Existing files use
[File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0)
with a unique predecessor backup under profile-backups. The store removes only
its own temporary output on failure. Backups contain private profile data, are
not automatically pruned and can be copied back to panels.json after exiting the
app. There is no automatic rollback or upload.

A changed snapshot is refused; cancel and reopen to review the latest file.
Cooperating saves using the same normalized full path are serialized across Windows
sessions through the `Global\` mutex namespace;
see [Windows kernel object namespaces](https://learn.microsoft.com/en-us/windows/win32/termserv/kernel-object-namespaces).
Older copies using the former `Local\` lock must be exited before updating:
they do not participate in this lock. Path aliases and noncooperating external
editors can still race after the final check. An unavailable global mutex is a
failed save, never permission to write without coordination. Atomic replacement
is not universal compare-and-swap,
nor proof of power-loss durability on every filesystem. Filesystem failures are
reported with the draft retained, without exposing raw paths/errors in the UI.

Snapshot reads and commits run off the UI thread. One editor is admitted at a
time; a delayed read does not open a dialog after the owner is hidden or closed.
Hiding invalidates that opening session even if the owner is shown again before
the read settles; obsolete results and error prompts are discarded. The pending
admission remains occupied until that read completes, then a fresh request is
allowed.
During a save, fields and Save/Remove/Cancel are disabled. Escape, ordinary close
and owner close wait for the result; a canceled owner close must preserve the
worker and tray. The synchronous writer holds and releases its mutex on the
same background thread. Successful commits provide a read-only profile snapshot
for installation on the UI thread, without a second disk read. No save starts
merely by opening the editor.

An I/O exception can leave the caller unable to confirm the outcome. The failure
message therefore says **Save was not confirmed** and retains the draft; it does
not promise unchanged file bytes. Check panels.json and profile-backups before
retrying. There is no cancellation or fixed deadline after commit starts, and
the close guard does not guarantee completion through forced process termination
or OS shutdown.

The scrollable fields and fixed footer keep Save, Remove and Cancel reachable
in a short window. Accessible field names and contextual estimate descriptions
are set; real Narrator and full-window mixed-DPI acceptance remain separate.

The isolated AppTests suite now runs the actual MainForm/ShowDialog path with
synthetic displays and temporary files. It checks delayed-open admission and
suppression, modal failure/cancel/reopen, card rebuild during editing, canceled
tray Exit resource preservation and confirmed estimate installation on current
cards. The notification icon stays hidden, and native monitor/gamma/registry
effects are replaced for these scenarios. This verifies the lifecycle paths,
not their physical drivers or visible desktop/tray/foreground experience.
