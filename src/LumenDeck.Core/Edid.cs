using Microsoft.Win32;

namespace LumenDeck;

/// <summary>
/// Reads the monitor's own EDID block straight out of the PnP registry key.
///
/// Deliberately not WMI. `WmiMonitorID` would put every monitor's identity
/// behind a WMI query, and a WMI query initialises COM on the calling thread -
/// which is the thing that reproducibly broke the first DDC read afterwards (see
/// MonitorService.AttachInternalPanels). The registry route touches none of
/// that, works before any COM apartment exists, and is faster. The project does
/// carry System.Management, but only for laptop internal-panel brightness, where
/// there is no alternative.
///
/// EDID is also the identity to trust. A monitor's MCCS capability string can
/// carry the wrong model outright: firmware gets copy-pasted across a product
/// family, and panels have been observed reporting a larger sibling's model
/// name while EDID and the physical dimensions prove otherwise. Trust EDID plus
/// physical size over the capability string's model field.
/// </summary>
internal sealed class EdidInfo
{
    public string Manufacturer = "";
    public string ModelName = "";
    public string TextSerial = "";
    public ushort ProductCode;      // EDID bytes 10-11
    public uint BinarySerial;       // EDID bytes 12-15
    public int WidthCm;
    public int HeightCm;
    public int Year;

    public double DiagonalInches =>
        WidthCm > 0 && HeightCm > 0
            ? Math.Sqrt(WidthCm * (double)WidthCm + HeightCm * (double)HeightCm) / 2.54
            : 0;

    public string Display =>
        string.IsNullOrWhiteSpace(ModelName)
            ? (string.IsNullOrWhiteSpace(Manufacturer) ? "" : Manufacturer)
            : (string.IsNullOrWhiteSpace(Manufacturer) ? ModelName : Manufacturer + " " + ModelName);

    /// <summary>
    /// Everything that distinguishes one unit from another of the same model.
    ///
    /// The text descriptors alone are not enough: plenty of monitors ship no
    /// 0xFF serial descriptor at all, so two identical panels would produce the
    /// identical key and silently share one saved setting - the second one
    /// overwriting the first on every save. The numeric product code and the
    /// 32-bit serial in bytes 12-15 are always present and are what actually
    /// separate them.
    /// </summary>
    public string IdentityKey =>
        $"{Manufacturer}|{ProductCode:X4}|{BinarySerial:X8}|{TextSerial}|{ModelName}";

    /// <summary>True when the identity carries something unit-specific, not just a brand.</summary>
    public bool HasStrongIdentity =>
        BinarySerial != 0 || !string.IsNullOrWhiteSpace(TextSerial) ||
        (ProductCode != 0 && !string.IsNullOrWhiteSpace(ModelName));

    /// <summary>
    /// deviceInterfaceId comes from EnumDisplayDevices with
    /// EDD_GET_DEVICE_INTERFACE_NAME and looks like:
    ///   \\?\DISPLAY#VVVMMMM#5&amp;1a2b3c4d&amp;0&amp;UID4353#{guid}
    /// which maps to
    ///   HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY\VVVMMMM\5&amp;1a2b3c4d&amp;0&amp;UID4353
    /// </summary>
    public static EdidInfo TryRead(string deviceInterfaceId)
    {
        try
        {
            if (string.IsNullOrEmpty(deviceInterfaceId)) return null;

            var parts = deviceInterfaceId.Split('#');
            if (parts.Length < 3) return null;

            string hardwareId = parts[1];   // three-letter vendor id + product code
            string instance = parts[2];     // PnP instance, unique per port

            string path = $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{hardwareId}\{instance}\Device Parameters";
            using var key = Registry.LocalMachine.OpenSubKey(path);
            if (key?.GetValue("EDID") is not byte[] edid) return null;

            return IsValid(edid) ? Parse(edid) : null;
        }
        catch
        {
            // A monitor with no readable EDID is a normal outcome, not a fault.
            // The caller falls back to the device name.
            return null;
        }
    }

    /// <summary>
    /// Reject anything that is not really an EDID block.
    ///
    /// Without this, any 128-byte blob that happens to sit in that registry
    /// value is parsed as though it were valid, and whatever bytes land at
    /// offset 54 become a "model name". That does not crash - it silently
    /// invents an identity, which then picks the wrong luminance profile and
    /// keys the wrong saved settings. A wrong answer is worse than no answer,
    /// so an invalid block returns null and the caller falls back.
    /// </summary>
    private static bool IsValid(byte[] e)
    {
        if (e == null || e.Length < 128) return false;

        // Fixed header 00 FF FF FF FF FF FF 00.
        if (e[0] != 0x00 || e[7] != 0x00) return false;
        for (int i = 1; i <= 6; i++) if (e[i] != 0xFF) return false;

        // The base block's 128 bytes must sum to a multiple of 256.
        int sum = 0;
        for (int i = 0; i < 128; i++) sum += e[i];
        return (sum & 0xFF) == 0;
    }

    private static EdidInfo Parse(byte[] e)
    {
        var info = new EdidInfo();

        // Manufacturer: bytes 8-9, three 5-bit letters, big endian, 'A' = 1.
        int m = (e[8] << 8) | e[9];
        char c1 = (char)('A' + ((m >> 10) & 0x1F) - 1);
        char c2 = (char)('A' + ((m >> 5) & 0x1F) - 1);
        char c3 = (char)('A' + (m & 0x1F) - 1);
        if (char.IsLetter(c1) && char.IsLetter(c2) && char.IsLetter(c3))
            info.Manufacturer = $"{c1}{c2}{c3}";

        info.ProductCode = (ushort)(e[10] | (e[11] << 8));
        info.BinarySerial = (uint)(e[12] | (e[13] << 8) | (e[14] << 16) | (e[15] << 24));

        info.Year = 1990 + e[17];
        info.WidthCm = e[21];
        info.HeightCm = e[22];

        // Four 18-byte descriptors at 54, 72, 90, 108. A descriptor whose first
        // three bytes are zero is a text block: byte 3 is the tag and byte 4 is
        // reserved and must also be zero.
        for (int off = 54; off <= 108; off += 18)
        {
            if (off + 18 > e.Length) break;
            if (e[off] != 0 || e[off + 1] != 0 || e[off + 2] != 0 || e[off + 4] != 0) continue;

            string text = ReadText(e, off + 5);
            if (e[off + 3] == 0xFC && info.ModelName.Length == 0) info.ModelName = text;
            else if (e[off + 3] == 0xFF && info.TextSerial.Length == 0) info.TextSerial = text;
        }

        return info;
    }

    private static string ReadText(byte[] e, int start)
    {
        var sb = new System.Text.StringBuilder(13);
        for (int i = start; i < start + 13 && i < e.Length; i++)
        {
            byte b = e[i];
            // 0x0A is the documented terminator; a NUL ends it too. Skipping
            // past either and carrying on would splice trailing padding into
            // the name.
            if (b == 0x0A || b == 0x00) break;
            if (b < 0x20 || b > 0x7E) continue;
            sb.Append((char)b);
        }
        return sb.ToString().Trim();
    }
}
