using LumenDeck;

internal static partial class Program
{
    private static void CheckCliArguments()
    {
        foreach (string[] args in new string[][]
        {
            ["--preset", "Night", "-b", "bad"], ["-b", "20", "-c", "bad"],
            ["-b", "20", "-w", "bad"], ["--monitor", "--brightness", "20"],
            ["--monitor"], ["--monitor", ""], ["--monitor", " "], ["-m", "--json", "-b", "20"],
            ["-b", "20", "--brighness", "30"], ["-b", "20", "--brightness", "30"],
            ["--list", "--LIST"], ["--list", "left"], ["--list", "-b", "20"],
            ["--diagnose", "--features"], ["--support-report", "-b", "20"],
            ["--support-report", "-m", "private-filter"], ["--help", "-b", "20"],
            ["--json"], ["--features", "--json"], ["-m", "left"],
            ["-p", "unknown"], ["-p"], ["-b"], ["-c"], ["-w"],
            ["-b", "101"], ["-c", "999"], ["-w", "2999"], ["-w", "6501"],
            ["-b", "2147483648"], ["-c", "+2147483648"], ["-b", "1.5"],
            ["-b", "1,000"], ["-b", "+-10"], ["-w", ""], ["-w", "3000K"],
            ["-m", "left", "-m", "right", "-b", "20"], ["--version", "--help"],
        })
            CheckCliAdmission(args, Cli.ExitNoMatch, admitted: false);
        foreach (string[] args in new string[][]
        {
            ["--power"], ["--power", "off"], ["-b", "20", "--power", "on"],
        })
            CheckCliAdmission(args, Cli.ExitWriteRefused, admitted: false);
        foreach (string[] args in new string[][]
        {
            [], ["--help"], ["-h"], ["/?"], ["--version"], ["-v"],
            ["--list"], ["--list", "--json", "-m", "left"],
            ["--diagnose", "--monitor", "right"], ["--features", "-m", "Example"],
            ["--support-report"], ["--support-report", "--json"],
            ["-p", "Night", "-b", "55", "-c", "45", "-w", "5000", "-m", "left"],
            ["--preset", "CUSTOM"], ["--preset", "day"], ["--preset", "Evening"],
            ["--brightness", "0"], ["--contrast", "100"], ["-b", "+10"], ["-c", "-10"],
            ["-b", "+2147483647"], ["-c", "-2147483648"],
            ["-w", "3000"], ["-w", "6500"], ["--warmth", "OFF"],
            ["--BRIGHTNESS", "50", "--MONITOR", "Example display"],
        })
            CheckCliAdmission(args, 17, admitted: true);
    }

    private static void CheckCliAdmission(string[] args, int expected, bool admitted)
    {
        int calls = 0;
        bool sameArgs = false;
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        int exit;
        try
        {
            Console.SetOut(output);
            Console.SetError(errors);
            exit = Cli.Run(args, request => { calls++; sameArgs = ReferenceEquals(request, args); return 17; });
        }
        finally { Console.SetOut(originalOut); Console.SetError(originalError); }
        Assert(exit == expected && calls == (admitted ? 1 : 0) && (!admitted || sameArgs)
            && output.ToString() == "" && (admitted ? errors.ToString() == "" : errors.ToString().Length > 0),
            $"CLI preflight {(admitted ? "admits once" : "rejects before execution")}: {string.Join(" ", args)}");
    }
}
