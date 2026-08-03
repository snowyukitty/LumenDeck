namespace LumenDeck;

/// <summary>
/// Console front end. All the behaviour lives in <see cref="Cli"/> so the GUI
/// and this share one implementation; this file is only the entry point and the
/// last-resort error boundary.
/// </summary>
internal static class CliProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            // With no arguments, print help rather than doing something. A tool
            // that silently changes every monitor when run bare is a tool people
            // stop trusting.
            if (args.Length == 0) args = new[] { "--help" };
            return Cli.Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("lumendeck-cli: " + ex.Message);
            return 3;
        }
    }
}
