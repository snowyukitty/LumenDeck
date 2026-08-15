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
        bool ok = m.UseDdc(
            h => Native.SetVCPFeature(h, VcpCode, (uint)mode), false);
        if (ok) m.PowerMode = mode;
        return ok;
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
