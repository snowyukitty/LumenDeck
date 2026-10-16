# Application preferences

Open **Settings** in the control window. The existing startup switches also stay
available in the tray; both surfaces render the same runtime/readback state.

The Settings menu keeps a save-status row even when another operation replaces
the bottom message. **Settings !** calls attention to an unconfirmed save.
**Retry saving active settings** captures the values currently in effect; it
does not reapply a failed preference or shortcut choice. Select that choice
again if you still want it. Pending saves disable retry. Newer adjustments stay
marked as waiting when an older snapshot succeeds; **Save active settings now**
can save them before the automatic save.

An externally changed or unreadable settings source disables retry and local
preference switches in both menus. Restore active blackouts and use tray Exit
before reopening; back up damaged settings privately before repair. Windows
startup registration remains a separate operation.
Choices save as you select them; no Apply button is required.

**Theme** switches the app palette without restarting or rebuilding the display
controls. Open editors retain their draft input and selection. Failed storage
keeps the previous palette and check; Windows high contrast has priority.
See [theme choices and implementation](themes.md).

**Preset targets…** opens a modal editor for the Day, Evening and Night
targets. It saves through the same background writer when you choose Save
targets; until that save is confirmed, presets keep the previous targets.
Opening, editing and saving never adjust a display.

**Use this display** in each mode copies an editor-opening snapshot of the
selected display's estimated brightness and current warmth into that draft.
Its source and values are shown above the modes. **Reset this mode** restores
just that draft's defaults; **Use defaults** resets every draft. All require
Save targets to persist. Capturing is unavailable during discovery, blackout or
Night saver, without brightness support or a usable model, and when brightness
is pending or unconfirmed. The reason remains visible and numeric editing is
still available. Close and reopen to capture newer observations. All draft
actions are disabled while the settings save is pending.

**Preset schedule…** is checked while the schedule is on; its tooltip names the
current period and the next change. The editor saves the same way. Saving an
enabled schedule requests the current period's preset when this window has not
already requested that period; the preview says which will happen. The window
remembers requested periods only for the current launch, so starting LumenDeck
requests the period in force after the first display scan.

**About LumenDeck…** shows the GUI version and complete recorded build, with
offline getting-started, keyboard, connection-help and licence information.
**Keyboard controls…** opens the key reference (also available with local F1).
These references remain available during a pending settings save. Each reuses
one owned window; Close or Escape dismisses it. Page Up/Down and Home/End scroll
its body, while Close stays visible. Reading a reference during a scan cancels
stale keyboard-selection recovery. Neither reference saves a preference or
requests a display adjustment. The About text describes navigation paths and
packaged file locations; it does not open files or submit a report.

| Option | Effect | Stored in |
| --- | --- | --- |
| Theme | Amber, Paper, Ocean or Violet; applies after a successful save, persists for the next launch, and respects Windows high contrast. Does not adjust displays. | settings.json (`ThemeId`) |
| Start with Windows | Register the current executable for this Windows account's sign-in. The check represents observed registration, not proof of a future launch. | HKCU Run registration |
| Start minimised | Minimise the next launch. With keep-running enabled it goes to the notification area; otherwise it stays on the taskbar. The current window stays as it is. | settings.json |
| Keep running when closed or minimised | Hide to the notification area instead of exiting on ordinary close. Use tray Exit to quit. Turning it off makes ordinary close exit and minimising stay on the taskbar. | settings.json |
| Reapply saved warmth after a display scan | Use saved warmth on startup, Refresh and display-configuration scans. Toggling neither applies warmth nor restores neutral output immediately. | settings.json |
| Use Windows Mica title bar | Optional main-window title-bar material on Windows 11 22H2 or later, with Windows solid fallbacks. Off by default; activates after a successful save. Content remains opaque and displays are not adjusted. | settings.json |

Settings opening reads the current startup registration but does not repair it,
write preferences or adjust a display. Existing startup repair still happens
when constructing the normal main window. Windows startup and minimised launch
are independent options. No scheduling, telemetry or automatic network work is
introduced.

Startup read state is nullable: unreadable registration is unavailable, rather
than confirmed disabled. The option is labeled and disabled in both menus until
an observation succeeds. A startup change requires a readable initial state,
uses the existing registry writer, then renders the observed readback. A refused
write, disagreement or lost readback reports an unconfirmed change through the
status line. There is no transaction or rollback spanning registry and JSON.
The legacy IsEnabled helper still maps unknown to false for existing consumers;
MainForm uses ReadState to retain the distinction.

Local preference changes capture a requested snapshot on the UI thread and write
it on a worker. The active flag and both menu checks remain unchanged until a
confirmed completion. JSON options are disabled while saving, with pending-save
descriptions; registry startup remains a separate operation. Failure retains the
previous active preference, Custom levels and recovery records. An unconfirmed
save does not promise unchanged disk bytes or power-loss durability.

Timer/work-display updates during a save coalesce into one fresh snapshot after
UI completion. Source protection and failure guidance remain in the shared writer.
Every managed FormClosing request is canceled while a settings/profile save is
pending, keeping cleanup/final capture after completion; use Exit again once it
settles. This is not a guarantee against OS-forced termination. Blackout, final
exit and gamma-baseline checkpoints still have synchronous work, so the app is
not entirely free of storage stalls. See [settings storage](settings-storage.md).

PreferencesMenu has no storage or monitor collaborators. It emits typed requests
without optimistic checks, and MainForm is the shared handler for both surfaces.
Synchronizing checks does not invoke requests. MainFormHost supplies registry
operations/readback, while the existing settings object owns local persistence.
Menu items include accessible descriptions of deferred behavior; the main status
stripe exposes its current text and sends accessible name-change notifications.
This is not a claim of completed Narrator acceptance.

AppTests runs the actual MainForm with fake registry responses and isolated
settings, testing refusal, mismatch, unknown/lost readback, cross-menu updates,
failed-storage runtime rollback, Custom preservation and no immediate monitor or
gamma action. The value-reader seam tests the core read adapter without registry
access. ControlTests checks command wrapping after adding Settings. Actual menu
rendering and larger-font artifacts can be produced without displaying a popup;
the package probe also constructs/renders an unavailable-state menu without
reading startup registration. Visible popup placement, native keyboard flow,
Narrator, mixed-DPI movement and real Windows startup registration remain separate
acceptance checks.
