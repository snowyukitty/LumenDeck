using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

/// <summary>Runs native UI tests on a private desktop without switching the input desktop.</summary>
internal static class IsolatedDesktopRunner
{
    private const string Worker = "--desktop-worker";
    private const string Prefix = "LumenDeck-tests-";
    internal sealed record Result(int ExitCode, bool TimedOut, string Output);
    internal sealed class CleanupException(Result result, Exception inner)
        : Exception("Private test logs could not be removed after worker termination.", inner)
    {
        internal Result WorkerResult { get; } = result;
    }
    internal static string WorkerDesktop { get; private set; }
    internal static string WorkerLog { get; private set; }

    internal static bool EnterWorker(ref string[] args)
    {
        if (args.Length == 0 || args[0] != Worker) return false;
        if (args.Length < 3 || !args[1].StartsWith(Prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(args[1][Prefix.Length..], "N", out _))
            throw new InvalidOperationException("Invalid isolated test worker identity.");
        string desktop = ObjectName(GetThreadDesktop(GetCurrentThreadId()));
        if (desktop != args[1]) throw new InvalidOperationException("Test worker is not on its assigned private desktop.");
        using var input = OpenInputDesktop(0, false, 1);
        if (!input.IsInvalid && ObjectName(input.DangerousGetHandle()) == desktop)
            throw new InvalidOperationException("Refusing to run UI tests on the input desktop.");
        var output = new StreamWriter(args[2], append: false, new UTF8Encoding(false)) { AutoFlush = true };
        Console.SetOut(output);
        Console.SetError(output);
        WorkerDesktop = desktop;
        WorkerLog = args[2];
        Console.WriteLine("PASS: worker is bound to its assigned private desktop; no desktop switch requested.");
        args = args[3..];
        return true;
    }

    internal static Result Run(string[] args, TimeSpan timeout, Action<string> holdLogForCheck = null)
    {
        string directory = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string log = Path.Combine(directory, "worker.log");
        Result result = null;
        try
        {
            string name = Prefix + Guid.NewGuid().ToString("N");
            using var desktop = CreateDesktop(name, null, IntPtr.Zero, 0, 0xC7, IntPtr.Zero);
            if (desktop.IsInvalid) throw Error("Create private test desktop");
            using var job = CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid) throw Error("Create test process job");
            var limits = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = 0x2000 } }; // KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<ExtendedLimits>()))
                throw Error("Set test process cleanup limit");

            // Atomic job membership: even a parent crash during CreateProcess cannot
            // leave a worker running outside the kill-on-close job. No inherited handles.
            nuint size = 0;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            IntPtr attributes = Marshal.AllocHGlobal(checked((int)size));
            IntPtr jobList = Marshal.AllocHGlobal(IntPtr.Size);
            bool initialized = false;
            try
            {
                if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)) throw Error("Initialize process attributes");
                initialized = true;
                Marshal.WriteIntPtr(jobList, job.DangerousGetHandle());
                if (!UpdateProcThreadAttribute(attributes, 0, (nuint)0x2000D, jobList, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                    throw Error("Bind worker to cleanup job");
                var startup = new StartupInfoEx { Startup = new StartupInfo
                { Size = Marshal.SizeOf<StartupInfoEx>(), Desktop = name }, Attributes = attributes };
                string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing test host executable.");
                var command = new List<string> { executable };
                if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                    command.Add(typeof(IsolatedDesktopRunner).Assembly.Location);
                command.AddRange([Worker, name, log]);
                command.AddRange(args);
                var line = new StringBuilder(string.Join(" ", command.Select(Quote)));
                if (!CreateProcess(executable, line, IntPtr.Zero, IntPtr.Zero, false,
                    0x08080000, IntPtr.Zero, Environment.CurrentDirectory, ref startup, out var process))
                    throw Error("Start isolated test worker"); // CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT
                using var handle = new SafeKernelHandle(process.Process);
                using var thread = new SafeKernelHandle(process.Thread);
                try
                {
                    uint wait = WaitForSingleObject(handle, checked((uint)timeout.TotalMilliseconds));
                    bool timedOut = wait == 258;
                    if (wait != 0 && !timedOut) throw Error("Wait for isolated test worker");
                    if (timedOut) StopJob(job);
                    if (WaitForSingleObject(handle, 10000) != 0)
                        throw new InvalidOperationException("Stopped test worker did not become terminal.");
                    if (!GetExitCodeProcess(handle, out uint code)) throw Error("Read test worker exit code");
                    // Stop any descendants before reading logs or releasing the desktop.
                    StopJob(job);
                    string output = File.Exists(log) ? File.ReadAllText(log, Encoding.UTF8) : "";
                    return result = new Result(unchecked((int)code), timedOut, output);
                }
                finally { StopJob(job); }
            }
            finally
            {
                if (initialized) DeleteProcThreadAttributeList(attributes);
                Marshal.FreeHGlobal(jobList);
                Marshal.FreeHGlobal(attributes);
            }
        }
        finally
        {
            if (result != null) holdLogForCheck?.Invoke(log);
            // Termination is asynchronous. Even after the job reports no active
            // processes, descendant file handles can take a moment to close.
            var cleanup = Stopwatch.StartNew();
            while (true)
            {
                try { Directory.Delete(directory, recursive: true); break; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (cleanup.Elapsed < TimeSpan.FromSeconds(10)) { Thread.Sleep(20); continue; }
                    if (result != null) throw new CleanupException(result, ex);
                    throw;
                }
            }
        }
    }

    private static void StopJob(SafeKernelHandle job)
    {
        if (!TerminateJobObject(job, 124)) throw Error("Stop test process job");
        var deadline = Stopwatch.StartNew();
        while (true)
        {
            if (!QueryInformationJobObject(job, 1, out var accounting, Marshal.SizeOf<BasicAccounting>(), IntPtr.Zero))
                throw Error("Read test job cleanup state");
            if (accounting.ActiveProcesses == 0) return;
            if (deadline.Elapsed > TimeSpan.FromSeconds(10)) throw new InvalidOperationException("Test job did not become empty.");
            Thread.Sleep(10);
        }
    }

    // CommandLineToArgvW/CRT quoting, including empty values and trailing backslashes.
    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    private static string ObjectName(IntPtr handle)
    {
        var name = new StringBuilder(256);
        if (handle == IntPtr.Zero || !GetUserObjectInformation(handle, 2, name, name.Capacity * 2, out _))
            throw Error("Read desktop identity");
        return name.ToString();
    }
    private static Win32Exception Error(string operation) => new(Marshal.GetLastWin32Error(), operation);

    private sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeKernelHandle() : base(true) { }
        public SafeKernelHandle(IntPtr value) : base(true) => SetHandle(value);
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }
    private sealed class SafeDesktopHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeDesktopHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseDesktop(handle);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
        public ushort Show, ReservedSize;
        public IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo Startup; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint LimitFlags;
        public nuint MinWorkingSet, MaxWorkingSet;
        public uint ActiveProcesses;
        public nuint Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BasicAccounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }
    [DllImport("user32.dll", EntryPoint = "CreateDesktopW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeDesktopHandle CreateDesktop(string name, string device, IntPtr mode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr handle);
    [DllImport("user32.dll", SetLastError = true)] private static extern SafeDesktopHandle OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint id);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int length, out int needed);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeKernelHandle CreateJobObject(IntPtr security, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(SafeKernelHandle job, int infoClass, ref ExtendedLimits info, int size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(SafeKernelHandle job, uint code);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool QueryInformationJobObject(SafeKernelHandle job, int infoClass, out BasicAccounting info, int size, IntPtr returned);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
        bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeKernelHandle handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(SafeKernelHandle process, out uint code);
}
