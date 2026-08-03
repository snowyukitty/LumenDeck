using System.Management;

namespace LumenDeck;

/// <summary>
/// Brightness for a laptop's built-in panel.
///
/// DDC/CI reaches external monitors over the video cable and does not apply to
/// an internal panel: `GetPhysicalMonitorsFromHMONITOR` returns a handle, and
/// every brightness call against it fails. Internal panels are driven through
/// WMI instead - `WmiMonitorBrightness` to read and
/// `WmiMonitorBrightnessMethods.WmiSetBrightness` to write, both in root\wmi.
///
/// Without this the app is useless on a laptop, which is most Windows machines.
/// With it, a laptop with two external monitors gets one window controlling all
/// three, which is the whole point.
///
/// Two limits worth knowing:
///  - Only one internal panel is exposed, and only on hardware whose driver
///    implements the ACPI brightness interface. Desktops report nothing here,
///    which is the correct answer for them.
///  - `WmiSetBrightness` needs no elevation, but some OEM power-management
///    services fight it and re-apply their own value.
/// </summary>
internal static class WmiBrightness
{
    public sealed record Panel(string InstanceName, byte Current, byte[] Levels);

    /// <summary>
    /// Every internal panel WMI will admit to. Empty on a desktop, and empty is
    /// not an error - callers must treat it as "this machine has none".
    /// </summary>
    public static List<Panel> Query()
    {
        var found = new List<Panel>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"root\wmi"),
                new SelectQuery("WmiMonitorBrightness"));

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                using (mo)
                {
                    string instance = mo["InstanceName"] as string ?? "";
                    byte current = mo["CurrentBrightness"] is byte b ? b : (byte)0;
                    var levels = mo["Level"] as byte[] ?? Array.Empty<byte>();
                    found.Add(new Panel(instance, current, levels));
                }
            }
        }
        catch
        {
            // A machine with no ACPI brightness interface throws rather than
            // returning nothing. That is not a failure worth surfacing: it just
            // means there is no internal panel to control.
        }
        return found;
    }

    /// <summary>Set an internal panel's brightness as a percentage. Returns false if it did not take.</summary>
    public static bool Set(string instanceName, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"root\wmi"),
                new SelectQuery("WmiMonitorBrightnessMethods"));

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                using (mo)
                {
                    if (mo is not ManagementObject method) continue;
                    if (!string.IsNullOrEmpty(instanceName) &&
                        (mo["InstanceName"] as string) != instanceName) continue;

                    // Timeout 0 = apply immediately and do not revert.
                    method.InvokeMethod("WmiSetBrightness", new object[] { (uint)0, (byte)percent });
                    return true;
                }
            }
        }
        catch
        {
            // Reported to the caller as a plain false; the UI says the panel
            // refused rather than pretending the change landed.
        }
        return false;
    }

    /// <summary>Re-read one panel's current brightness, or -1 if it cannot be read.</summary>
    public static int Read(string instanceName)
    {
        foreach (var p in Query())
            if (string.IsNullOrEmpty(instanceName) || p.InstanceName == instanceName)
                return p.Current;
        return -1;
    }
}
