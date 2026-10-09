namespace LumenDeck;

/// <summary>Validate the complete request before settings or display access.</summary>
internal static class CliArguments
{
    internal sealed record Error(int ExitCode, string Message);

    internal static Error Validate(string[] args)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string option = Canonical(args[i]);
            if (option == null) return Invalid($"Unknown argument \"{args[i]}\". Try --help.");
            if (option == "--power")
                return new(Cli.ExitWriteRefused, "--power was withdrawn because MCCS D6 can prevent software wake. " +
                    "Use the window's Blank screen action or shortcut instead.");
            if (!seen.Add(option)) return Invalid($"Specify {option} only once.");
            if (option is not ("--monitor" or "--preset" or "--brightness" or "--contrast" or "--warmth")) continue;

            if (++i == args.Length || Canonical(args[i]) != null || args[i].StartsWith("--", StringComparison.Ordinal))
                return Invalid($"{option} requires a value. Try --help.");
            string value = args[i];
            if (string.IsNullOrWhiteSpace(value)) return Invalid($"{option} requires a nonempty value.");
            if (option == "--preset" && !Presets.IsCustom(value) && Presets.ByName(value) == null)
                return Invalid("--preset requires Day, Evening, Night or Custom.");
            if (option is "--brightness" or "--contrast")
            {
                bool relative = value[0] is '+' or '-';
                if (!int.TryParse(value, out int number) || (!relative && (number < 0 || number > 100)))
                    return Invalid($"{option} requires 0-100 or a signed whole-number step such as +10 or -10.");
            }
            if (option == "--warmth" && !value.Equals("off", StringComparison.OrdinalIgnoreCase) &&
                (!int.TryParse(value, out int kelvin) || kelvin < GammaControl.MinKelvin || kelvin > GammaControl.MaxKelvin))
                return Invalid("--warmth requires 3000-6500 or off.");
        }

        string[] modes = ["--help", "--version", "--support-report", "--list", "--diagnose", "--features"];
        var selected = modes.Where(seen.Contains).ToArray();
        bool adjusts = seen.Any(x => x is "--preset" or "--brightness" or "--contrast" or "--warmth");
        if (selected.Length > 1 || (selected.Length != 0 && adjusts))
            return Invalid("Use one information command, or adjustment options; do not combine them.");
        if (selected.Any(x => x is "--help" or "--version" or "--support-report") && seen.Contains("--monitor"))
            return Invalid("--monitor applies to --list, --diagnose, --features or adjustments only.");
        if (seen.Contains("--json") && !seen.Contains("--list") && !seen.Contains("--support-report"))
            return Invalid("Use --json with --list or --support-report only.");
        if (args.Length != 0 && selected.Length == 0 && !adjusts)
            return Invalid("Nothing to do. Try --help.");
        return null;
    }

    private static Error Invalid(string message) => new(Cli.ExitNoMatch, message);

    private static string Canonical(string option) => option.ToLowerInvariant() switch
    {
        "-m" or "--monitor" => "--monitor",
        "-p" or "--preset" => "--preset",
        "-b" or "--brightness" => "--brightness",
        "-c" or "--contrast" => "--contrast",
        "-w" or "--warmth" => "--warmth",
        "-h" or "--help" or "/?" => "--help",
        "-v" or "--version" => "--version",
        "--list" or "--diagnose" or "--features" or "--json" or "--support-report" or "--power" => option.ToLowerInvariant(),
        _ => null,
    };
}
