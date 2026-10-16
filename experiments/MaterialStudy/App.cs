using System.IO;
using System.Windows;

namespace LumenDeck.MaterialStudy;

internal sealed class App : Application
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var app = new App();
            var window = new StudyWindow();
            if (args.Length == 2 && args[0] == "--render")
            {
                string output = Path.GetFullPath(args[1]);
                Directory.CreateDirectory(output);
                window.ShowInTaskbar = false;
                window.Left = -20000;
                window.Top = -20000;
                window.Loaded += (_, _) => window.Dispatcher.InvokeAsync(() =>
                {
                    try { window.VerifyAndRender(output); }
                    catch (Exception ex)
                    {
                        File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString());
                        Environment.ExitCode = 1;
                    }
                    window.Close();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            else if (args.Length != 0)
                return 2;
            app.Run(window);
            return Environment.ExitCode;
        }
        catch (Exception ex)
        {
            if (args.Length == 2 && args[0] == "--render")
                File.WriteAllText(Path.Combine(Path.GetFullPath(args[1]), "failure.txt"), ex.ToString());
            else MessageBox.Show(ex.Message, "LumenDeck material study");
            return 1;
        }
    }
}
