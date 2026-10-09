using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LumenDeck;

/// <summary>
/// Optional single-instance reveal channel. Mutex ownership still decides which
/// process may create the real application; this channel never starts a writer.
/// </summary>
internal sealed class InstanceActivation : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<bool> _acceptReveal;
    private readonly string _name;
    private readonly int _peerTimeoutMs;
    private readonly uint _session;
    private NamedPipeServerStream _pipe;
    private bool _started;
    private bool _disposed;
    public Task Completion { get; private set; } = Task.CompletedTask;
    private static ReadOnlySpan<byte> ShowMessage => "LD\u0001\u0001"u8;

    internal InstanceActivation(Func<bool> acceptReveal, string name = null, int peerTimeoutMs = 1000,
        uint? expectedSession = null)
    {
        _acceptReveal = acceptReveal ?? throw new ArgumentNullException(nameof(acceptReveal));
        if (peerTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(peerTimeoutMs));
        _name = name ?? ChannelName();
        _peerTimeoutMs = peerTimeoutMs;
        _session = expectedSession ?? CurrentSession();
    }

    public bool Start()
    {
        lock (_gate)
        {
            if (_disposed || _started || string.IsNullOrEmpty(_name)) return false;
            _started = true;
            try
            {
                _pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance, 16, 16);
                var pipe = _pipe;
                var stop = _stop.Token;
                Completion = Task.Run(() => ListenAsync(pipe, stop));
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _pipe?.Dispose();
                return false;
            }
        }
    }

    private async Task ListenAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await pipe.WaitForConnectionAsync(stop).ConfigureAwait(false);
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
                    deadline.CancelAfter(_peerTimeoutMs);
                    byte[] frame = new byte[4];
                    int length = await pipe.ReadAsync(frame, deadline.Token).ConfigureAwait(false);
                    if (length != frame.Length || !pipe.IsMessageComplete) continue;
                    bool accepted = false;
                    if (frame.AsSpan().SequenceEqual(ShowMessage) &&
                        GetNamedPipeClientSessionId(pipe.SafePipeHandle, out uint session) && session == _session &&
                        !stop.IsCancellationRequested)
                    {
                        // This callback must be a nonblocking dispatch, not UI or I/O.
                        try { accepted = _acceptReveal(); }
                        catch { /* Refuse without stopping subsequent requests. */ }
                    }
                    await pipe.WriteAsync(new byte[] { accepted ? (byte)1 : (byte)0 }, deadline.Token).ConfigureAwait(false);
                    // Disconnect can discard unread pipe bytes. A bounded receipt
                    // keeps the acknowledgement alive until the client reads it.
                    byte[] receipt = new byte[1];
                    await pipe.ReadExactlyAsync(receipt, deadline.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
                {
                    // Partial, slow and disconnected peers cannot hold the listener.
                }
                finally
                {
                    if (!stop.IsCancellationRequested) pipe.Disconnect();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // Activation is optional. The app remains usable through its tray.
        }
        finally { pipe.Dispose(); }
    }

    public static async Task<bool> RequestAsync(string name = null, int timeoutMs = 2000,
        Action<uint> grantForeground = null)
    {
        if (timeoutMs <= 0) return false;
        try
        {
            name ??= ChannelName();
            if (string.IsNullOrEmpty(name)) return false;
            using var deadline = new CancellationTokenSource(timeoutMs);
            using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeoutMs, deadline.Token).ConfigureAwait(false);
            if (!GetNamedPipeServerSessionId(pipe.SafePipeHandle, out uint serverSession) || serverSession != CurrentSession())
                return false;
            // Ask Windows for the connected server identity, never trust a PID
            // supplied in protocol bytes. Visibility does not depend on this grant.
            if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint processId))
            {
                try
                {
                    if (grantForeground != null) grantForeground(processId);
                    else if (IsSameExecutable(processId)) AllowSetForegroundWindow(processId);
                }
                catch { /* Foreground policy may refuse; still request visibility. */ }
            }
            await pipe.WriteAsync(ShowMessage.ToArray(), deadline.Token).ConfigureAwait(false);
            byte[] response = new byte[1];
            await pipe.ReadExactlyAsync(response, deadline.Token).ConfigureAwait(false);
            try { await pipe.WriteAsync(new byte[] { 2 }, deadline.Token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { /* ACK was already read. */ }
            return response[0] == 1;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException or
                                      UnauthorizedAccessException or ObjectDisposedException or ArgumentException)
        {
            return false;
        }
    }

    private static string ChannelName()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            string user = identity.User?.Value;
            if (string.IsNullOrEmpty(user)) return null;
            string token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user)))[..16];
            using var process = Process.GetCurrentProcess();
            return $"LumenDeck.Activate.v1.{process.SessionId}.{token}";
        }
        catch { return null; }
    }

    private static uint CurrentSession()
    {
        using var process = Process.GetCurrentProcess();
        return (uint)process.SessionId;
    }

    private static bool IsSameExecutable(uint processId)
    {
        try
        {
            using var server = Process.GetProcessById(checked((int)processId));
            return server.SessionId == (int)CurrentSession() &&
                string.Equals(server.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _stop.Cancel();
            _pipe?.Dispose();
        }
        // Do not synchronously wait on an async callback or on the WinForms thread.
        _ = Completion.ContinueWith(_ => _stop.Dispose(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe, out uint clientSessionId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerSessionId(SafePipeHandle pipe, out uint serverSessionId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
