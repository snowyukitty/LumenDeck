using LumenDeck;

internal static partial class Program
{
    private static void CheckCapabilities()
    {
        foreach (string text in new[] { null, "", " \r\n", "(model(vcp-like)cmds(01 02))", "vcp(60(11 12)" })
            Assert(Capabilities.ParseVcp(text).Count == 0,
                "missing or unterminated VCP blocks produce no advertised controls");

        var advertised = Capabilities.ParseVcp(
            "(prot(monitor)model(Test)cmds(01 02)vcp(04 10 14(05 08 0B 0C) 60(0F 11 12) D6(01 04 05))mccs_ver(2.2))");
        Assert(advertised.Keys.Order().SequenceEqual(new byte[] { 0x04, 0x10, 0x14, 0x60, 0xD6 }),
            "nested value groups do not become additional advertised control codes");
        Assert(advertised[0x04].Count == 0 && advertised[0x10].Count == 0,
            "bare control codes retain unspecified value lists");
        Assert(advertised[0x14].SequenceEqual(new[] { 5, 8, 11, 12 }),
            "colour preset values stay attached to their own code");
        Assert(advertised[0x60].SequenceEqual(new[] { 15, 17, 18 }),
            "input source values are hexadecimal and separate from colour presets");
        Assert(advertised[0xD6].SequenceEqual(new[] { 1, 4, 5 }),
            "parser preserves power capability data without offering an operation");
        Assert(!advertised.ContainsKey(0x12), "a nested HDMI value does not advertise contrast");

        var mixed = Capabilities.ParseVcp("(VCP(\t62\n60(0f 11)\r\nDC() 87))");
        Assert(mixed.Keys.Order().SequenceEqual(new byte[] { 0x60, 0x62, 0x87, 0xDC }),
            "case and whitespace do not hide advertised control codes");
        Assert(mixed[0x60].SequenceEqual(new[] { 15, 17 }), "lowercase hexadecimal values are accepted");
        Assert(mixed[0xDC].Count == 0 && mixed[0x87].Count == 0,
            "empty groups and final bare codes retain unspecified values");

        var invalid = Capabilities.ParseVcp("vcp(ZZ 100 60(11 invalid 12) bad 62)");
        Assert(invalid.Keys.Order().SequenceEqual(new byte[] { 0x60, 0x62 }),
            "nonhexadecimal and oversized code tokens are ignored");
        Assert(invalid[0x60].SequenceEqual(new[] { 17, 18 }),
            "invalid value tokens do not remove valid values or become codes");

        var input = new VcpFeature
        {
            Definition = VcpCatalog.Find(0x60), Current = 0x07,
            AllowedValues = new() { 0x07, 0x11 }, CurrentIsUnadvertised = true,
        };
        Assert(input.LabelFor(0x07) == "Unknown (0x07) - not advertised by this monitor",
            "an unadvertised current value is not confidently named from the standard table");
        Assert(input.LabelFor(0x11) == "HDMI 1", "advertised known values retain useful catalog names");
        input.CurrentIsUnadvertised = false;
        Assert(input.LabelFor(0x07) == "S-Video 1", "advertised values may use their standard names");
        Assert(VcpCatalog.ValueName(input.Definition, 0xFE) == "254",
            "unknown catalog values remain visible as raw numbers");
        Assert(VcpCatalog.Find(0xD6) == null,
            "advertised power control is excluded from the extra-control catalog");
    }
}
