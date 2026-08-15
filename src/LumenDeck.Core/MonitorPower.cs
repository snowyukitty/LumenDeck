namespace LumenDeck;

/// <summary>
/// MCCS display-power control (VCP D6).
///
/// Value 04 is the DPMS-off state: the panel and backlight enter their lowest
/// normal power mode while the display remains attached to Windows. Value 01
/// wakes it again. This is different from setting brightness to zero, which
/// leaves the panel electronics and usually the backlight powered.
/// </summary>
internal static class MonitorPower
{
    public const byte VcpCode = 0xD6;
    public const int Unknown = 0x00;
    public const int On = 0x01;
    public const int Off = 0x04;

    private const int WakeAttempts = 4;

    /// <summary>
    /// Hardware for which DPM-off has been observed to cut the DDC receiver.
    /// Keep this deliberately narrow: it is a safety block backed by a real
    /// unit and the model's advertised modes (On, DPM-off, hard-off only), not
    /// a guess based on manufacturer.
    /// </summary>
    public static bool IsKnownWakeUnsafe(Monitor m) =>
        m?.Edid?.Manufacturer.Equals("DEL", StringComparison.OrdinalIgnoreCase) == true &&
        m.Edid.ModelName.Contains("U2518D", StringComparison.OrdinalIgnoreCase);

    public static bool TryRead(Monitor m, out int mode)
    {
        int observed = Unknown;
        bool ok = m != null && m.UseDdc(h =>
        {
            uint current = 0, maximum = 0;
            bool read = Native.GetVCPFeatureAndVCPFeatureReply(
                h, VcpCode, IntPtr.Zero, ref current, ref maximum);
            if (read) observed = (int)current;
            return read;
        }, false);

        if (ok) m.PowerMode = observed;
        mode = observed;
        return ok;
    }

    public static bool Set(Monitor m, int mode)
    {
        if (m == null || (mode != On && mode != Off)) return false;
        if (mode == On) return Wake(m);
        if (IsKnownWakeUnsafe(m)) return false;

        bool sent = Send(m, Off);
        if (sent) m.PowerMode = Off;
        return sent;
    }

    /// <summary>
    /// A successful SetVCPFeature return only means the graphics driver accepted
    /// the request. Retry On and require a DDC read afterwards; otherwise an
    /// asleep receiver produces a convincing but false success.
    /// </summary>
    public static bool Wake(Monitor m)
    {
        if (m == null) return false;
        m.PowerMode = Off; // keep every subsequent toggle pointing toward Wake

        for (int attempt = 0; attempt < WakeAttempts; attempt++)
        {
            Send(m, On);
            Thread.Sleep(250 + attempt * 150);

            if (TryRead(m, out int mode) && mode == On)
            {
                m.PowerMode = On;
                return true;
            }

            // Some monitors accept D6 but refuse to report it. A live standard
            // brightness read at least proves the receiver did not remain cut
            // off after the wake request.
            if (TryReadBrightness(m))
            {
                m.PowerMode = On;
                return true;
            }
        }

        m.PowerMode = Off;
        return false;
    }

    private static bool Send(Monitor m, int mode) =>
        m.UseDdc(h => Native.SetVCPFeature(h, VcpCode, (uint)mode), false);

    private static bool TryReadBrightness(Monitor m)
    {
        uint minimum = 0, current = 0, maximum = 0;
        bool ok = m.UseDdc(
            h => Native.GetMonitorBrightness(h, ref minimum, ref current, ref maximum), false);
        if (!ok) return false;

        m.SupportsBrightness = true;
        m.BrightnessMin = (int)minimum;
        m.Brightness = (int)current;
        m.BrightnessMax = (int)maximum;
        return true;
    }

    /// <summary>
    /// Resolve a toggle from live state when possible. If an off monitor no
    /// longer answers DDC reads, a failed brightness probe or a remembered off
    /// request both make waking it the conservative fallback.
    /// </summary>
    public static int ToggleTarget(Monitor m)
    {
        if (m == null) return On;
        if (TryRead(m, out int current)) return current == On ? Off : On;
        if (m.PowerMode == Off || !m.SupportsBrightness) return On;
        return Off;
    }
}
