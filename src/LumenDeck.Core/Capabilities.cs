namespace LumenDeck;

/// <summary>
/// One control a specific monitor actually offers, with its live value.
/// </summary>
internal sealed class VcpFeature
{
    public VcpCatalog.Definition Definition;
    public int Current;
    public int Max;

    /// <summary>
    /// Values this monitor accepts, when it bothered to list them.
    ///
    /// A capability string may write `14(05 08 0B 0C)` - meaning only those four
    /// colour presets exist on this panel - or bare `14`, meaning it supports
    /// the code but will not say which values. Offering the full MCCS list in
    /// the second case is a guess; offering only the listed ones in the first
    /// case is the truth. Empty means "not stated".
    /// </summary>
    public List<int> AllowedValues = new();

    /// <summary>
    /// True when the monitor's current value is not one it advertised. Naming
    /// such a value from the MCCS table produces a confident falsehood, so the
    /// UI labels it as unknown instead.
    /// </summary>
    public bool CurrentIsUnadvertised;

    /// <summary>Label for a value, honest about the ones the monitor never claimed.</summary>
    public string LabelFor(int value)
    {
        if (CurrentIsUnadvertised && value == Current)
            return $"Unknown (0x{value:X2}) - not advertised by this monitor";
        return VcpCatalog.ValueName(Definition, value);
    }

    public byte Code => Definition.Code;
    public string Name => Definition.Name;
}

/// <summary>
/// Parser for the MCCS capability string a monitor returns.
///
/// The string looks like:
///   (prot(monitor)type(LCD)model(XYZ)cmds(01 02 03)vcp(02 04 10 12 14(05 08 0B
///   0C) 16 18 1A 60(0F 11 12) D6(01 04 05))mccs_ver(2.2))
///
/// Two things matter and both are easy to get wrong: the vcp block is nested, so
/// it cannot be found with a naive "up to the next close paren"; and the value
/// lists inside it are the monitor telling you exactly which settings exist,
/// which is more trustworthy than any general table.
/// </summary>
internal static class Capabilities
{
    /// <summary>Every VCP code the string advertises, with its listed values.</summary>
    public static Dictionary<byte, List<int>> ParseVcp(string capabilityString)
    {
        var result = new Dictionary<byte, List<int>>();
        if (string.IsNullOrWhiteSpace(capabilityString)) return result;

        int start = capabilityString.IndexOf("vcp(", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return result;

        // Walk to the matching close paren rather than the first one, because
        // the block contains nested groups.
        int depth = 0, end = -1;
        for (int i = start + 3; i < capabilityString.Length; i++)
        {
            if (capabilityString[i] == '(') depth++;
            else if (capabilityString[i] == ')')
            {
                depth--;
                if (depth == 0) { end = i; break; }
            }
        }
        if (end < 0) return result;

        string body = capabilityString.Substring(start + 4, end - start - 4);

        byte? pending = null;
        int pos = 0;
        while (pos < body.Length)
        {
            char c = body[pos];

            if (char.IsWhiteSpace(c)) { pos++; continue; }

            if (c == '(')
            {
                // Value list belonging to the code just read.
                int close = body.IndexOf(')', pos);
                if (close < 0) break;
                string inner = body.Substring(pos + 1, close - pos - 1);
                if (pending.HasValue)
                {
                    var values = new List<int>();
                    foreach (var tok in inner.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                        if (TryHex(tok, out int v)) values.Add(v);
                    result[pending.Value] = values;
                }
                pending = null;
                pos = close + 1;
                continue;
            }

            // A bare token: the previous code had no value list.
            int tokenEnd = pos;
            while (tokenEnd < body.Length && !char.IsWhiteSpace(body[tokenEnd]) && body[tokenEnd] != '(') tokenEnd++;
            string token = body.Substring(pos, tokenEnd - pos);
            pos = tokenEnd;

            if (pending.HasValue && !result.ContainsKey(pending.Value))
                result[pending.Value] = new List<int>();

            pending = TryHex(token, out int code) && code <= 0xFF ? (byte)code : null;
        }

        if (pending.HasValue && !result.ContainsKey(pending.Value))
            result[pending.Value] = new List<int>();

        return result;
    }

    private static bool TryHex(string s, out int value) =>
        int.TryParse(s, System.Globalization.NumberStyles.HexNumber,
                     System.Globalization.CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Turn a monitor's advertised codes into the controls worth showing, by
    /// intersecting them with the catalog and reading each one's live value.
    ///
    /// A code that is advertised but will not answer a read is dropped rather
    /// than shown as zero - a control that silently does nothing is worse than
    /// no control.
    /// </summary>
    public static List<VcpFeature> Discover(Monitor m)
    {
        var found = new List<VcpFeature>();
        if (!m.HasPhysicalHandle || string.IsNullOrEmpty(m.CapabilityString)) return found;

        var advertised = ParseVcp(m.CapabilityString);

        foreach (var def in VcpCatalog.All)
        {
            if (!advertised.TryGetValue(def.Code, out var allowed)) continue;

            var feature = new VcpFeature { Definition = def, AllowedValues = allowed ?? new List<int>() };

            if (def.Kind == VcpCatalog.Kind.Action)
            {
                // Nothing to read: these are write-only triggers.
                found.Add(feature);
                continue;
            }

            uint cur = 0, max = 0;
            if (!Native.GetVCPFeatureAndVCPFeatureReply(m.PhysicalHandle, def.Code, IntPtr.Zero, ref cur, ref max))
                continue;

            feature.Current = (int)cur;
            feature.Max = (int)max;

            // A continuous control with no range is not a control.
            if (def.Kind == VcpCatalog.Kind.Continuous && feature.Max <= 0) continue;

            // For a select, prefer the monitor's own list; fall back to the
            // catalog's, and if the current value is outside both, include it so
            // the dropdown can still show where the monitor actually is.
            if (def.Kind == VcpCatalog.Kind.Select)
            {
                if (feature.AllowedValues.Count == 0 && def.Values != null)
                    feature.AllowedValues = def.Values.Keys.ToList();

                // A monitor can report a current value it never advertised. One
                // panel here sits on input source 0x07 while listing only
                // DisplayPort and HDMI - and 0x07 is "S-Video 1" in the MCCS
                // table, a connector it does not physically have. Keep the value
                // so the dropdown shows where the monitor actually is, but mark
                // it, because naming it from the standard table would state a
                // confident falsehood.
                if (!feature.AllowedValues.Contains(feature.Current))
                {
                    feature.AllowedValues.Add(feature.Current);
                    feature.CurrentIsUnadvertised = true;
                }
                feature.AllowedValues.Sort();
            }

            found.Add(feature);
            Thread.Sleep(40);   // MCCS pacing between reads
        }

        return found;
    }
}
