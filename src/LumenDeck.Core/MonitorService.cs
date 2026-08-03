using System.Runtime.InteropServices;
using System.Text;

namespace LumenDeck;

/// <summary>One physical monitor, with its DDC handle and what it will answer.</summary>
internal sealed class Monitor : IDisposable
{
    public IntPtr PhysicalHandle;

    /// <summary>
    /// Whether <see cref="PhysicalHandle"/> is real.
    ///
    /// Do NOT infer this by comparing the handle to IntPtr.Zero. A physical
    /// monitor handle is an opaque driver-defined value and is not required to
    /// be non-zero: on the hardware this was written against, the four monitors
    /// came back as handles 0, 1, 2 and 3. Treating 0 as "no handle" silently
    /// skipped every read and every write for the first monitor, never released
    /// its handle, and made a perfectly healthy panel report "no DDC support".
    /// </summary>
    public bool HasPhysicalHandle;
    public string DeviceName = "";        // \\.\DISPLAY3 - never shown as an identity
    public string DeviceInterfaceId = "";
    public string Description = "";
    public EdidInfo Edid;
    public Native.RECT Rect;
    public bool IsPrimary;

    /// <summary>How this monitor's brightness is reached. Not every panel speaks DDC.</summary>
    public enum Backend { None, Ddc, Wmi }

    public Backend BrightnessBackend = Backend.None;

    /// <summary>Set for a laptop's built-in panel, which is driven through WMI rather than DDC.</summary>
    public bool IsInternalPanel;

    /// <summary>WMI InstanceName of the internal panel, when there is one.</summary>
    public string WmiInstanceName = "";

    public bool SupportsBrightness;
    public int BrightnessMin, BrightnessMax, Brightness;

    public bool SupportsContrast;
    public int ContrastMin, ContrastMax, Contrast;

    public string CapabilityString = "";

    /// <summary>Why this monitor did or did not answer, for --diagnose.</summary>
    public string Diagnostic = "no physical monitor handle";

    /// <summary>Colour temperature currently applied as a gamma ramp, in kelvin.</summary>
    public int Kelvin = GammaControl.NeutralKelvin;

    /// <summary>
    /// The gamma ramp this display had before the app touched it - an ICC
    /// profile, a colorimeter's LUT, or plain identity. Warmth is composed onto
    /// this and "off" restores it exactly.
    /// </summary>
    public ushort[] BaselineRamp;

    /// <summary>
    /// Key that settings are stored under. Assigned by MonitorService, which is
    /// the only place that can see all the attached monitors at once and so the
    /// only place that can guarantee two of them never collide.
    ///
    /// Never \\.\DISPLAYn: that suffix is not stable across cable swaps, is not
    /// necessarily contiguous (gaps appear after hot-plugging), and does not
    /// reliably match the number Windows paints on the screen.
    /// </summary>
    public string StableKey = "";

    /// <summary>The port this monitor is plugged into. Distinguishes two identical panels.</summary>
    public string PortInstance
    {
        get
        {
            var parts = (DeviceInterfaceId ?? "").Split('#');
            return parts.Length >= 3 ? parts[1] + "/" + parts[2] : DeviceName;
        }
    }

    /// <summary>What to call it in the UI. Never the device number.</summary>
    public string FriendlyName
    {
        get
        {
            string n = Edid?.Display;
            if (!string.IsNullOrWhiteSpace(n)) return n;
            if (!string.IsNullOrWhiteSpace(Description) && Description != "Generic PnP Monitor") return Description;
            return DeviceName;
        }
    }

    public string SizeLabel
    {
        get
        {
            double d = Edid?.DiagonalInches ?? 0;
            return d > 1 ? $"{d:0.#}\"" : "";
        }
    }

    /// <summary>Assigned by MonitorService from the desktop layout.</summary>
    public string PositionLabel = "";

    public void Dispose()
    {
        if (HasPhysicalHandle)
        {
            Native.DestroyPhysicalMonitor(PhysicalHandle);
            HasPhysicalHandle = false;
        }
    }
}

internal static class MonitorService
{
    /// <summary>
    /// Enumerate every attached monitor and open a DDC handle for each.
    ///
    /// Slow - a few hundred ms per monitor, and this is why it must never run on
    /// the UI thread. <paramref name="withCapabilities"/> is off by default
    /// because the capability string is by far the slowest DDC request and
    /// nothing in the UI needs it; fetching it at startup cost seconds of dead
    /// window for information nobody looked at.
    /// </summary>
    public static List<Monitor> Enumerate(bool withCapabilities = false)
    {

        var handles = new List<IntPtr>();
        Native.MonitorEnumProc cb = (IntPtr hm, IntPtr hdc, ref Native.RECT r, IntPtr d) =>
        {
            handles.Add(hm);
            return true;
        };
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
        GC.KeepAlive(cb);

        var list = new List<Monitor>();

        foreach (var hm in handles)
        {
            var mi = new Native.MONITORINFOEX { cbSize = Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (!Native.GetMonitorInfo(hm, ref mi)) continue;

            var m = new Monitor
            {
                DeviceName = mi.szDevice,
                Rect = mi.rcMonitor,
                IsPrimary = (mi.dwFlags & Native.MONITORINFOF_PRIMARY) != 0,
            };

            var dd = new Native.DISPLAY_DEVICE { cb = Marshal.SizeOf<Native.DISPLAY_DEVICE>() };
            if (Native.EnumDisplayDevices(m.DeviceName, 0, ref dd, Native.EDD_GET_DEVICE_INTERFACE_NAME))
            {
                m.DeviceInterfaceId = dd.DeviceID;
                m.Description = dd.DeviceString;
            }
            uint count = 0;
            if (Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hm, ref count) && count > 0)
            {
                var arr = new Native.PHYSICAL_MONITOR[count];
                if (Native.GetPhysicalMonitorsFromHMONITOR(hm, count, arr))
                {
                    m.PhysicalHandle = arr[0].hPhysicalMonitor;
                    m.HasPhysicalHandle = true;
                    // Release any extras so handles are not leaked on the rare
                    // multi-physical-monitor HMONITOR.
                    for (int i = 1; i < arr.Length; i++) Native.DestroyPhysicalMonitor(arr[i].hPhysicalMonitor);

                    ReadCurrent(m);
                    m.Diagnostic = $"handle=0x{m.PhysicalHandle:X} physicalMonitors={count} " +
                                   $"desc='{arr[0].szDescription}' " +
                                   $"brightness={(m.SupportsBrightness ? "ok" : "refused")} " +
                                   $"contrast={(m.SupportsContrast ? "ok" : "refused")} " +
                                   $"lastError={Marshal.GetLastWin32Error()}";
                    if (m.SupportsBrightness) m.BrightnessBackend = Monitor.Backend.Ddc;
                    if (withCapabilities) m.CapabilityString = ReadCapabilities(m.PhysicalHandle);
                }
            }

            m.Edid = EdidInfo.TryRead(m.DeviceInterfaceId);

            // Capture before anything is applied, so a monitor that already
            // carries an ICC or colorimeter LUT can be restored to it exactly.
            m.BaselineRamp = GammaControl.Capture(m.DeviceName);

            list.Add(m);
        }

        AttachInternalPanels(list);
        AssignPositionLabels(list);
        AssignStableKeys(list);
        return list;
    }

    /// <summary>
    /// Give a laptop's built-in panel a WMI brightness backend, since DDC/CI
    /// does not reach it.
    ///
    /// Runs AFTER the DDC pass, and only when some monitor actually came back
    /// without brightness - which on a desktop is never, so the WMI stack is
    /// not touched at all there. That ordering is not a micro-optimisation: it
    /// is a bug fix. Querying WMI first initialises COM on the calling thread,
    /// and doing so reproducibly made the very first subsequent
    /// GetMonitorBrightness call fail - the primary monitor reported "no
    /// brightness control" on every run while an independent tool read it fine
    /// seconds later. DDC first, WMI only as the fallback it is.
    /// </summary>
    private static void AttachInternalPanels(List<Monitor> list)
    {
        var needing = list.Where(m => !m.SupportsBrightness).ToList();
        if (needing.Count == 0) return;

        var panels = WmiBrightness.Query();
        if (panels.Count == 0) return;

        foreach (var m in needing)
        {
            var hit = MatchInternalPanel(m, panels);
            if (hit == null) continue;

            m.IsInternalPanel = true;
            m.WmiInstanceName = hit.InstanceName;
            m.BrightnessBackend = Monitor.Backend.Wmi;
            m.SupportsBrightness = true;
            m.BrightnessMin = 0;
            m.BrightnessMax = 100;
            m.Brightness = hit.Current;
            // Contrast has no WMI equivalent; an internal panel simply has none.
            m.SupportsContrast = false;
        }
    }

    /// <summary>
    /// Give every attached monitor a key that is unique among them.
    ///
    /// EDID identity alone is not enough. Two of the same model, bought
    /// together, can carry the same product code and a zero serial, and plenty
    /// of monitors ship no text serial descriptor at all - so both would hash to
    /// one key, share a single settings entry, and overwrite each other on every
    /// save. Where identity is weak or duplicated, fall back to the port, which
    /// is what actually tells two identical panels apart.
    /// </summary>
    private static void AssignStableKeys(List<Monitor> list)
    {
        foreach (var m in list)
        {
            m.StableKey = m.Edid is { HasStrongIdentity: true }
                ? "edid|" + m.Edid.IdentityKey
                : "port|" + m.PortInstance;
        }

        foreach (var group in list.GroupBy(m => m.StableKey).Where(g => g.Count() > 1))
            foreach (var m in group)
                m.StableKey = "port|" + m.PortInstance;
    }

    /// <summary>
    /// Refresh brightness and contrast from the monitors. Cheap next to
    /// Enumerate - no capability strings, no EDID.
    /// </summary>
    /// <summary>
    /// Tie a display device to a WMI brightness instance.
    ///
    /// The WMI InstanceName looks like `DISPLAY\LGD0578\4&amp;2f0e5f5b&amp;0&amp;UID8388688_0`
    /// and EnumDisplayDevices gives `\\?\DISPLAY#LGD0578#4&amp;2f0e5f5b&amp;0&amp;UID8388688#{guid}`.
    /// Same device path, different separators - so this is an identity match,
    /// not a guess about which monitor "looks internal".
    /// </summary>
    private static WmiBrightness.Panel MatchInternalPanel(Monitor m, List<WmiBrightness.Panel> panels)
    {
        if (panels.Count == 0) return null;

        var parts = (m.DeviceInterfaceId ?? "").Split('#');
        if (parts.Length < 3) return null;
        string prefix = $@"DISPLAY\{parts[1]}\{parts[2]}";

        foreach (var p in panels)
            if (p.InstanceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return p;

        // A single internal panel that did not match by path is still almost
        // certainly this one if nothing else claimed it, but guessing is how
        // the wrong screen gets adjusted. Leave it unmatched.
        return null;
    }

    public static void ReadCurrent(Monitor m)
    {
        if (m.IsInternalPanel)
        {
            int v = WmiBrightness.Read(m.WmiInstanceName);
            if (v >= 0) { m.Brightness = v; m.SupportsBrightness = true; }
            else m.SupportsBrightness = false;
            return;
        }

        if (!m.HasPhysicalHandle)
        {
            m.SupportsBrightness = m.SupportsContrast = false;
            return;
        }

        // Retry, because a first read straight after opening the handle is not
        // reliable on every panel.
        //
        // This was found the hard way: one monitor reported "no brightness
        // control" on every single run of this app, while a PowerShell script
        // issuing the identical Win32 calls read it correctly seconds before and
        // seconds after. The difference was not the calls - it was the gap
        // between them. An interpreter puts milliseconds between opening the
        // physical monitor handle and the first request; compiled code issues
        // them microseconds apart, and some firmware is not ready that fast. It
        // refuses both brightness and contrast, and sets no error code, so it
        // looks exactly like a monitor that has no DDC support at all.
        //
        // Retrying a few times costs nothing on a healthy panel, since the first
        // attempt succeeds.
        m.SupportsBrightness = ReadFeature(
            (IntPtr h, ref uint a, ref uint b, ref uint c) => Native.GetMonitorBrightness(h, ref a, ref b, ref c),
            m.PhysicalHandle, m.HasPhysicalHandle, out int bMin, out int bCur, out int bMax);
        if (m.SupportsBrightness)
        {
            m.BrightnessMin = bMin;
            m.BrightnessMax = bMax;
            m.Brightness = bCur;
        }

        m.SupportsContrast = ReadFeature(
            (IntPtr h, ref uint a, ref uint b, ref uint c) => Native.GetMonitorContrast(h, ref a, ref b, ref c),
            m.PhysicalHandle, m.HasPhysicalHandle, out int cMin, out int cCur, out int cMax);
        if (m.SupportsContrast)
        {
            m.ContrastMin = cMin;
            m.ContrastMax = cMax;
            m.Contrast = cCur;
        }

        // Note both flags are assigned unconditionally: a monitor that has gone
        // to sleep or switched input stops answering, and leaving a stale "true"
        // behind would show an enabled slider for a value nothing is reporting.
    }

    private delegate bool FeatureReader(IntPtr h, ref uint min, ref uint cur, ref uint max);

    /// <summary>Attempts per read. Three is enough for every panel tested; the first usually wins.</summary>
    private const int ReadAttempts = 3;
    private const int ReadRetryDelayMs = 120;

    private static bool ReadFeature(FeatureReader read, IntPtr handle, bool valid, out int min, out int cur, out int max)
    {
        min = cur = max = 0;
        if (!valid) return false;

        for (int attempt = 0; attempt < ReadAttempts; attempt++)
        {
            if (attempt > 0) Thread.Sleep(ReadRetryDelayMs);

            uint lo = 0, now = 0, hi = 0;
            if (!read(handle, ref lo, ref now, ref hi)) continue;

            // A monitor that answers with an empty range has not really
            // answered; treat it as a failure so the slider is not built around
            // a range of zero.
            if (hi <= lo) continue;

            min = (int)lo;
            cur = (int)now;
            max = (int)hi;
            return true;
        }
        return false;
    }

    private static string ReadCapabilities(IntPtr h)
    {
        try
        {
            uint len = 0;
            if (!Native.GetCapabilitiesStringLength(h, ref len) || len == 0) return "";
            var sb = new StringBuilder((int)len + 2);
            return Native.CapabilitiesRequestAndCapabilitiesReply(h, sb, len) ? sb.ToString() : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Describe each monitor by where it physically sits, because that is how a
    /// person identifies a screen. The device suffix is not a usable identity:
    /// the names carry gaps from past hot-plugging (a four-monitor setup can
    /// enumerate as DISPLAY1, DISPLAY2, DISPLAY3, DISPLAY6) and the suffix need
    /// not match the number Windows paints on screen during Identify.
    /// </summary>
    private static void AssignPositionLabels(List<Monitor> list)
    {
        if (list.Count == 0) return;
        if (list.Count == 1) { list[0].PositionLabel = "only display"; return; }

        // Horizontal band: left third / middle / right third of the virtual desktop.
        int minX = list.Min(m => m.Rect.Left);
        int maxX = list.Max(m => m.Rect.Right);
        int spanX = Math.Max(1, maxX - minX);

        // Vertical: only mention it when a monitor is clearly offset from the pack.
        double avgCentreY = list.Average(m => (m.Rect.Top + m.Rect.Bottom) / 2.0);

        foreach (var m in list)
        {
            double cx = (m.Rect.Left + m.Rect.Right) / 2.0;
            double t = (cx - minX) / spanX;

            string h = t < 0.34 ? "left" : t < 0.67 ? "centre" : "right";

            double cy = (m.Rect.Top + m.Rect.Bottom) / 2.0;
            double dy = cy - avgCentreY;
            // A third of the shortest screen height is a clear vertical offset.
            double threshold = list.Min(x => x.Rect.Height) / 3.0;

            string v = dy < -threshold ? "upper " : dy > threshold ? "lower " : "";

            m.PositionLabel = v + h;

            if (m.Rect.Height > m.Rect.Width) m.PositionLabel += ", portrait";
            if (m.IsPrimary) m.PositionLabel += ", primary";
        }

        // Disambiguate exact duplicates so two labels are never identical.
        foreach (var group in list.GroupBy(m => m.PositionLabel).Where(g => g.Count() > 1))
        {
            int i = 1;
            foreach (var m in group.OrderBy(x => x.Rect.Left).ThenBy(x => x.Rect.Top))
                m.PositionLabel += $" ({i++})";
        }
    }
}
