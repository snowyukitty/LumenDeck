namespace LumenDeck;

/// <summary>
/// Colour temperature per display, via the GPU gamma ramp.
///
/// This exists because many monitors will not do it themselves. Panels have
/// been measured that advertise the RGB gain codes 0x16/0x18/0x1A and then
/// ignore every write to them: sweeping blue across 0..100 returned the
/// identical value for all thirteen writes, in both directions, while
/// SetVCPFeature reported success every time. On such a panel the only colour
/// presets offered were 6500K, 9300K and two User slots holding the same
/// barely-warm triple - no warm preset existed to select at all.
///
/// Windows Night light is not an alternative either: it is one global switch
/// that tints every attached monitor.
///
/// IMPORTANT - this composes against a captured baseline rather than writing a
/// fresh identity ramp. An earlier version built every ramp from scratch, which
/// meant that "warmth off" did not restore the display, it *flattened* it:
/// whatever LUT an ICC profile, a colorimeter or an accessibility tool had
/// loaded was silently discarded, and a calibrated monitor came back with
/// different greys and black point. Capture first, multiply onto it, and restore
/// the captured copy to turn it off.
///
/// The remaining catch, which the UI states rather than hides: a gamma ramp is
/// GPU state, not monitor state. It is lost on reboot, on a display mode change,
/// on driver restart, and when some exclusive-fullscreen games exit.
/// </summary>
internal static class GammaControl
{
    public const int NeutralKelvin = 6500;
    public const int MinKelvin = 3000;
    public const int MaxKelvin = 6500;

    /// <summary>
    /// Linear white points normalised to 1.0 at 6500K.
    /// Red is 1.0 everywhere on purpose: warming removes green and blue rather
    /// than boosting red, so the panel is never driven brighter than it already
    /// was and the setting cannot fight the brightness slider.
    /// </summary>
    private static readonly (int K, double R, double G, double B)[] WhitePoints =
    {
        (6500, 1.0000, 1.0000, 1.0000),
        (6000, 1.0000, 0.9576, 0.9151),
        (5500, 1.0000, 0.9098, 0.8283),
        (5000, 1.0000, 0.8577, 0.7215),
        (4500, 1.0000, 0.8003, 0.6127),
        (4000, 1.0000, 0.7350, 0.4977),
        (3500, 1.0000, 0.6596, 0.3690),
        (3000, 1.0000, 0.5697, 0.2231),
    };

    public static (double R, double G, double B) Multipliers(int kelvin)
    {
        kelvin = Math.Clamp(kelvin, MinKelvin, MaxKelvin);

        var hi = WhitePoints[0];
        var lo = WhitePoints[^1];
        for (int i = 0; i < WhitePoints.Length - 1; i++)
        {
            if (kelvin <= WhitePoints[i].K && kelvin >= WhitePoints[i + 1].K)
            {
                hi = WhitePoints[i];
                lo = WhitePoints[i + 1];
                break;
            }
        }

        int span = hi.K - lo.K;
        double t = span == 0 ? 0 : (hi.K - kelvin) / (double)span;
        return (hi.R + (lo.R - hi.R) * t,
                hi.G + (lo.G - hi.G) * t,
                hi.B + (lo.B - hi.B) * t);
    }

    /// <summary>
    /// Read the ramp a display is currently using. Call this before applying
    /// anything, so whatever calibration is already loaded can be preserved and
    /// restored. Returns null if the adapter will not answer.
    /// </summary>
    public static ushort[] Capture(string deviceName)
    {
        IntPtr hdc = Native.CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return null;
        try
        {
            var ramp = new ushort[768];
            return Native.GetDeviceGammaRamp(hdc, ramp) ? ramp : null;
        }
        finally
        {
            Native.DeleteDC(hdc);
        }
    }

    /// <summary>A plain 1:1 ramp, used only when a baseline could not be read.</summary>
    public static ushort[] Identity()
    {
        var ramp = new ushort[768];
        for (int i = 0; i < 256; i++)
        {
            ushort v = (ushort)Math.Min(65535, i * 257);
            ramp[i] = ramp[256 + i] = ramp[512 + i] = v;
        }
        return ramp;
    }

    /// <summary>
    /// Apply a colour temperature on top of <paramref name="baseline"/>.
    /// At 6500K this writes the baseline back unchanged, so "off" really is off.
    ///
    /// Returns false if the adapter refused the ramp. Windows rejects ramps it
    /// considers extreme, and that refusal has to reach the user rather than be
    /// swallowed into a UI that claims success.
    /// </summary>
    public static bool Apply(string deviceName, int kelvin, ushort[] baseline)
    {
        baseline ??= Identity();
        if (baseline.Length < 768) baseline = Identity();

        IntPtr hdc = Native.CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return false;

        try
        {
            var (fr, fg, fb) = Multipliers(kelvin);
            var ramp = new ushort[768];
            for (int i = 0; i < 256; i++)
            {
                ramp[i] = Scale(baseline[i] * fr);
                ramp[256 + i] = Scale(baseline[256 + i] * fg);
                ramp[512 + i] = Scale(baseline[512 + i] * fb);
            }
            return Native.SetDeviceGammaRamp(hdc, ramp);
        }
        finally
        {
            Native.DeleteDC(hdc);
        }
    }

    private static ushort Scale(double v)
    {
        int i = (int)Math.Round(v);
        if (i < 0) i = 0;
        if (i > 65535) i = 65535;
        return (ushort)i;
    }
}
