using System.Diagnostics;

internal static class ScenarioCleanup
{
    internal static int Complete(string directory, int result, TimeSpan timeout, TextWriter errors)
    {
        var deadline = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return result;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (deadline.Elapsed < timeout) { Thread.Sleep(20); continue; }
                errors.WriteLine($"FAIL: scenario directory cleanup failed after scenario exit {result}.");
                errors.WriteLine(ex);
                return 1;
            }
            catch (Exception ex)
            {
                errors.WriteLine($"FAIL: scenario directory cleanup failed after scenario exit {result}.");
                errors.WriteLine(ex);
                return 1;
            }
        }
    }
}
