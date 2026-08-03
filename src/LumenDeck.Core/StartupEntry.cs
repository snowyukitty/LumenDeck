using Microsoft.Win32;

namespace LumenDeck;

/// <summary>
/// The "start with Windows" toggle.
///
/// Implemented as a value under HKCU Run rather than a scheduled task or a
/// shortcut hidden in the Startup folder, for one reason: it is the place people
/// and tools actually look. Task Manager's Startup tab lists it, any autostart
/// audit finds it, and removing it by hand is one delete. An app that installs
/// itself somewhere less obvious is an app you cannot get rid of.
///
/// Off by default. A monitor utility has no business adding itself to boot
/// without being asked - though there is a real reason to want it: a GPU gamma
/// ramp does not survive a reboot, so without this the colour temperature
/// silently reverts every morning.
///
/// HKCU only, so it never needs elevation.
/// </summary>
internal static class StartupEntry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LumenDeck";

    /// <summary>Path recorded in the registry, quoted so a path with spaces survives.</summary>
    private static string CommandLine
    {
        get
        {
            string exe = Environment.ProcessPath ?? "";
            return string.IsNullOrEmpty(exe) ? "" : "\"" + exe + "\"";
        }
    }

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string s && s.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// True when the entry exists but points somewhere else - after the app has
    /// been moved or reinstalled. Left stale it launches the old copy, or
    /// nothing at all, and looks like the setting simply stopped working.
    /// </summary>
    public static bool IsStale
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                if (key?.GetValue(ValueName) is not string s || s.Length == 0) return false;
                return !string.Equals(s.Trim(), CommandLine.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Returns true if the change landed, verified by reading it back.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key == null) return false;

            if (enabled)
            {
                string cmd = CommandLine;
                if (string.IsNullOrEmpty(cmd)) return false;
                key.SetValue(ValueName, cmd, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            return false;
        }

        // Read back rather than trusting the write, in the usual way.
        return IsEnabled == enabled;
    }

    /// <summary>Rewrite the path after the executable has moved. No-op when correct.</summary>
    public static void RepairIfStale()
    {
        if (IsEnabled && IsStale) Set(true);
    }
}
