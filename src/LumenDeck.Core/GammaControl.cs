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
/// Which baseline a display owns is <see cref="DisplayGamma"/>'s job, not this
/// one's: capturing the current ramp every time is exactly how warmth ends up
/// composed onto warmth.
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
    /// The exponent a display applies to what comes down the cable. sRGB and
    /// every consumer panel are close enough to 2.2 for this purpose.
    ///
    /// This constant is the whole reason warmth used to be far stronger than the
    /// number on the slider - see <see cref="EncodedMultipliers"/>.
    /// </summary>
    private const double DisplayGamma = 2.2;

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

    /// <summary>The white point in LINEAR light. Not what a gamma ramp takes.</summary>
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
    /// The same white point, converted into the domain a gamma ramp actually
    /// works in.
    ///
    /// A gamma ramp holds ENCODED values: the display raises whatever it is
    /// given to roughly the power 2.2 before any light comes out. Scaling a ramp
    /// entry by f therefore scales the emitted light by f^2.2, not by f.
    ///
    /// An earlier version multiplied the encoded ramp by the linear white points
    /// above, which overshot every setting by that exponent and is why every
    /// screen looked far too warm. At the Night preset the
    /// blue channel asks for 0.6345 of full; applied to the encoded ramp that
    /// emits 0.6345^2.2 = 0.37 of full, a white point nearer 3000K than the
    /// 4600K the button claims - a visibly orange screen on every panel,
    /// regardless of make.
    ///
    /// Raising each linear ratio to 1/2.2 first makes the emitted light land on
    /// the requested ratio, because (e * f^(1/2.2))^2.2 = e^2.2 * f.
    /// </summary>
    public static (double R, double G, double B) EncodedMultipliers(int kelvin)
    {
        var (r, g, b) = Multipliers(kelvin);
        return (Encode(r), Encode(g), Encode(b));
    }

    private static double Encode(double linear) =>
        linear <= 0 ? 0 : Math.Pow(linear, 1.0 / DisplayGamma);

    /// <summary>
    /// Read the ramp a display is currently using.
    ///
    /// Note what this cannot tell you: whether the ramp it returns is the
    /// display's own state or something LumenDeck wrote earlier and left behind.
    /// <see cref="DisplayGamma"/> answers that; capturing straight into a
    /// baseline is the bug, not the fix. Returns null if the adapter will not
    /// answer.
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
    /// The ramp that puts <paramref name="kelvin"/> on top of
    /// <paramref name="baseline"/>. At 6500K this is the baseline unchanged, so
    /// "off" really is off.
    /// </summary>
    public static ushort[] Compose(ushort[] baseline, int kelvin)
    {
        if (baseline == null || baseline.Length < 768) baseline = Identity();

        var (fr, fg, fb) = EncodedMultipliers(kelvin);
        var ramp = new ushort[768];
        for (int i = 0; i < 256; i++)
        {
            ramp[i] = Scale(baseline[i] * fr);
            ramp[256 + i] = Scale(baseline[256 + i] * fg);
            ramp[512 + i] = Scale(baseline[512 + i] * fb);
        }
        return ramp;
    }

    /// <summary>
    /// Hand a ramp to the adapter.
    ///
    /// Returns false if it refused. Windows rejects ramps it considers extreme,
    /// and that refusal has to reach the user rather than be swallowed into a UI
    /// that claims success.
    /// </summary>
    public static bool Write(string deviceName, ushort[] ramp)
    {
        if (ramp == null || ramp.Length < 768) return false;

        IntPtr hdc = Native.CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return false;
        try
        {
            return Native.SetDeviceGammaRamp(hdc, ramp);
        }
        finally
        {
            Native.DeleteDC(hdc);
        }
    }

    /// <summary>Compose and write in one step, for callers with no baseline bookkeeping.</summary>
    public static bool Apply(string deviceName, int kelvin, ushort[] baseline) =>
        Write(deviceName, Compose(baseline, kelvin));

    /// <summary>
    /// A cheap fingerprint of a ramp, used to recognise a ramp LumenDeck wrote
    /// when it is read back in a later session. FNV-1a; nothing here is
    /// security-sensitive, it only has to be stable and collision-free enough to
    /// tell one 1.5 KB blob from another.
    /// </summary>
    public static string Signature(ushort[] ramp)
    {
        if (ramp == null) return "";
        ulong h = 14695981039346656037;
        foreach (ushort v in ramp)
        {
            h = (h ^ (byte)v) * 1099511628211;
            h = (h ^ (byte)(v >> 8)) * 1099511628211;
        }
        return h.ToString("x16");
    }

    /// <summary>Tolerated deviation from a straight line, out of 65535. 0.3%.</summary>
    private const double LinearTolerance = 192;

    /// <summary>
    /// Whether this ramp is the identity scaled per channel - which is exactly
    /// and only the shape LumenDeck writes when the display's baseline was
    /// identity, and therefore the shape of a warm ramp left behind by an
    /// earlier session or a crash.
    ///
    /// This is what lets a display that is already tinted be recovered without
    /// any stored record. It is a narrow test on purpose:
    ///
    ///  - every channel must be a straight line through the origin, which a real
    ///    ICC or colorimeter LUT is not (those carry a measured curve, deviating
    ///    from linear by whole percent, far outside the tolerance here);
    ///  - red must be essentially untouched, because warming only ever removes
    ///    green and blue. A ramp that dims red is somebody else's - a screen
    ///    dimmer, an accessibility filter - and must be left alone.
    ///
    /// If it matches, the original baseline is recoverable exactly: it was
    /// identity.
    /// </summary>
    public static bool LooksLikeScaledIdentity(ushort[] ramp)
    {
        if (ramp == null || ramp.Length < 768) return false;

        for (int c = 0; c < 3; c++)
        {
            int o = c * 256;
            double f = ramp[o + 255] / 65535.0;
            if (f > 1.0001) return false;

            for (int i = 0; i < 256; i++)
                if (Math.Abs(ramp[o + i] - i * 257.0 * f) > LinearTolerance) return false;
        }

        return ramp[255] / 65535.0 >= 0.995;
    }

    /// <summary>Whether a ramp is (near enough) the untouched 1:1 ramp.</summary>
    public static bool IsIdentity(ushort[] ramp)
    {
        if (ramp == null || ramp.Length < 768) return false;
        for (int c = 0; c < 3; c++)
            for (int i = 0; i < 256; i++)
                if (Math.Abs(ramp[c * 256 + i] - i * 257.0) > LinearTolerance) return false;
        return true;
    }

    private static ushort Scale(double v)
    {
        int i = (int)Math.Round(v);
        if (i < 0) i = 0;
        if (i > 65535) i = 65535;
        return (ushort)i;
    }
}
