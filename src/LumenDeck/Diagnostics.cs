namespace LumenDeck;

/// <summary>
/// Opt-in instrumentation, enabled by setting LUMENDECK_DIAG=1.
///
/// It exists because working set cannot answer the question it looks like it
/// answers. The rebuild leak test showed memory climbing about half a megabyte
/// per rebuild, which reads as a leak - but a .NET process's working set also
/// grows simply because the GC has not run and has not returned pages to the OS.
/// The two are indistinguishable from outside the process.
///
/// GC.GetTotalMemory(forceFullCollection: true) settles it: it runs a blocking
/// collection first, so what it reports is memory that is genuinely still
/// reachable. If that number is flat across rebuilds, the working-set growth is
/// the allocator, not a leak.
///
/// Off by default - a forced full GC on every rebuild is not something a user
/// should pay for.
/// </summary>
internal static class Diagnostics
{
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("LUMENDECK_DIAG") == "1";

    private static readonly object Gate = new();

    public static string LogPath => Path.Combine(AppSettings.Directory, "diag.log");

    /// <summary>
    /// The message is built by a delegate so that nothing - not even the forced
    /// collection - happens when diagnostics are off.
    /// </summary>
    public static void Log(Func<string> message)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(AppSettings.Directory);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message()}{Environment.NewLine}");
            }
        }
        catch
        {
            // Instrumentation must never be able to break the thing it measures.
        }
    }

    /// <summary>Reachable managed bytes after a blocking full collection.</summary>
    public static long LiveManagedBytes() => GC.GetTotalMemory(forceFullCollection: true);

    /// <summary>Total controls in a control tree, to catch panels that were never released.</summary>
    public static int CountControls(Control root)
    {
        int n = 1;
        foreach (Control c in root.Controls) n += CountControls(c);
        return n;
    }
}
