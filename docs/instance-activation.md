# Existing-window activation

Launching the GUI again requests the running window to become visible. The
existing `Local\LumenDeck.SingleInstance` mutex remains the writer election.
The secondary branch returns before application initialization, panel/settings
loading or MainForm construction, including when activation fails.

`InstanceActivation` is an internal, optional reveal channel. The primary starts
it from Shown, after a form handle exists; the existing start-minimized path also
passes through Shown. Its nonblocking callback marshals to MainForm, coalesces
pending UI work and rechecks actual closing/disposal before showing the window.
Reveal only changes visibility, taskbar presence and window activation. Existing
maximized windows stay maximized. Tray-menu show retains its separate value-read
behavior. Ordinary minimize/close-to-tray keeps the channel alive; actual exit
marks the form closing and disposes the channel after the message loop.

## Scope and protocol

The pipe name includes protocol version, Windows session and a hash of the user
SID. Both endpoints request `PipeOptions.CurrentUserOnly`; Windows documents
account and elevation checks for this option. Naming is supplemented by native
client/server session checks. The secondary connects to the local `.` endpoint.
The primary claims one pipe instance and keeps that handle across exchanges;
channel collision disables optional activation without replacing the other pipe.

One message is four bytes: `4C 44 01 01` (LD, version 1, reveal). Short,
oversized, unknown-version and unknown-command messages cannot dispatch reveal.
The server returns one byte: 1 for accepted dispatch, 0 for refusal. The client
then sends a receipt byte so the server does not disconnect while its response
is still unread. A receipt is flow control and never another command.

The normal client has a two-second overall connect/write/read deadline. The
server limits each connected exchange to one second, including response receipt,
with shutdown cancellation. An idle or disconnected peer is dropped, and the
same pipe accepts the next request. Disposal cancels and closes pipe I/O without
synchronously waiting on the WinForms thread. A terminal listener failure leaves
the tray available and secondary startup uses its fallback message.

Accepted dispatch means UI work was queued or already pending. It does not
prove that the window has finished restoring or received foreground focus.
Foreground permission uses the OS-reported server PID only when its session and
executable path match the launching copy. Failure to verify that path or grant
permission does not prevent a visibility request. Copies at another path can
reveal the original window without granting it foreground permission. Windows
still controls actual focus.

## Evidence and remaining acceptance

The regression harness uses unique pipes and inert callbacks, including a
separate client process. Tests cover valid/repeated messages, refusal, malformed
frames, abrupt/idle peers, occupied or absent channels, missing acknowledgements,
an injected wrong expected session and shutdown. Test foreground grants are inert.
The extracted GUI probe repeats an exchange on its own unique pipe without using
the real mutex/channel, showing a real window or starting monitor services.

Source inspection verifies secondary early return and the reveal event path.
These isolated exchanges do not execute normal MainForm startup, prove the full
app's single-writer behavior at runtime, or test another Windows account/session,
mixed elevation, UAC or actual foreground policy. Before release:

1. Start normally, minimize/close to tray, then launch its shortcut and executable.
   Confirm the same PID remains and the controls are visible, without a second
   monitor scan or display adjustment.
2. Repeat while controls are visible/maximized and while startup is reading
   displays. Check the fallback when the receiver is not ready.
3. Confirm close-to-tray remains reachable by relaunch and actual Exit restores
   blackouts and permits a fresh primary instance.
4. Exercise simultaneous relaunches, modal windows and shutdown. Check normal
   user permissions and both mixed-elevation directions without weakening ACLs.

API references: [PipeOptions](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0),
[ConnectAsync](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeclientstream.connectasync?view=net-10.0),
[client session](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientsessionid),
[server PID](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeserverprocessid),
[foreground permission](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow),
[disconnect behavior](https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-disconnectnamedpipe).
