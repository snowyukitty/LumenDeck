using System.Diagnostics;

namespace LumenDeck;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Arguments are the console binary's job: a WinExe cannot stream stdout
        // to the shell that launched it. Point people at it instead of quietly
        // ignoring what they typed.
        if (args.Length > 0)
        {
            MessageBox.Show(
                "Command line options belong to lumendeck.exe, the console build." +
                Environment.NewLine + Environment.NewLine +
                "Try:  lumendeck --help",
                "LumenDeck", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 2;
        }

        // One instance only. A second copy would open its own DDC handles to the
        // same monitors and the two would fight over every slider.
        using var single = new Mutex(true, @"Local\LumenDeck.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show(
                "LumenDeck is already running.\nLook for its icon in the notification area.",
                "LumenDeck", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        PanelDatabase.Load();
        PanelDatabase.WriteExampleIfMissing();

        Application.Run(new MainForm());

        GC.KeepAlive(single);
        return 0;
    }

    /// <summary>
    /// Show the failure and write it down. An unattended crash that leaves no
    /// trace is the one failure mode that cannot be diagnosed later.
    /// </summary>
    private static void Report(Exception ex)
    {
        if (ex == null) return;
        try
        {
            Directory.CreateDirectory(AppSettings.Directory);
            string log = Path.Combine(AppSettings.Directory, "error.log");
            File.AppendAllText(log,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}");

            MessageBox.Show($"{ex.Message}\n\nWritten to:\n{log}",
                "LumenDeck - unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch
        {
            Debug.WriteLine(ex);
        }
    }
}
