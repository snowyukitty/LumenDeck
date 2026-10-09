namespace LumenDeck;

/// <summary>Narrow transport boundary; fake implementations never need native handles.</summary>
internal interface IDdcTransport
{
    bool CanWrite(Monitor monitor);
    bool Write(Monitor monitor, DdcWorker.Feature feature, byte code, int value);
    bool Read(Monitor monitor, DdcWorker.Feature feature, out int actual);
    bool RestoreBrightness(Monitor monitor, int value);
}

internal sealed class NativeDdcTransport : IDdcTransport
{
    public bool CanWrite(Monitor monitor) => monitor.IsInternalPanel || monitor.HasPhysicalHandle;

    public bool Write(Monitor monitor, DdcWorker.Feature feature, byte code, int value)
    {
        if (monitor.IsInternalPanel)
            return feature == DdcWorker.Feature.Brightness && WmiBrightness.Set(monitor.WmiInstanceName, value);
        return feature switch
        {
            DdcWorker.Feature.Brightness => monitor.UseDdc(h => Native.SetMonitorBrightness(h, (uint)value), false),
            DdcWorker.Feature.Contrast => monitor.UseDdc(h => Native.SetMonitorContrast(h, (uint)value), false),
            DdcWorker.Feature.Vcp => monitor.UseDdc(h => Native.SetVCPFeature(h, code, (uint)value), false),
            _ => false,
        };
    }

    public bool Read(Monitor monitor, DdcWorker.Feature feature, out int actual)
    {
        if (monitor.IsInternalPanel)
        {
            actual = feature == DdcWorker.Feature.Brightness ? WmiBrightness.Read(monitor.WmiInstanceName) : -1;
            return actual >= 0;
        }
        int observed = 0;
        bool read = monitor.UseDdc(h =>
        {
            uint minimum = 0, current = 0, maximum = 0;
            bool ok = feature switch
            {
                DdcWorker.Feature.Brightness => Native.GetMonitorBrightness(h, ref minimum, ref current, ref maximum),
                DdcWorker.Feature.Contrast => Native.GetMonitorContrast(h, ref minimum, ref current, ref maximum),
                _ => false,
            };
            if (ok) observed = (int)current;
            return ok;
        }, false);
        actual = observed;
        return read;
    }

    public bool RestoreBrightness(Monitor monitor, int value)
    {
        if (monitor.IsInternalPanel) return WmiBrightness.Set(monitor.WmiInstanceName, value);
        for (int attempt = 0; attempt < DdcWorker.VerifyAttempts; attempt++)
        {
            if (!Write(monitor, DdcWorker.Feature.Brightness, 0, value)) continue;
            Thread.Sleep(DdcWorker.VerifySettleMilliseconds);
            if (Read(monitor, DdcWorker.Feature.Brightness, out int actual) && actual == value) return true;
        }
        return false;
    }
}
