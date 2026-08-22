namespace Hs80.Core;

public class BatteryReader
{
    private readonly Hs80Device _dev;

    public BatteryReader(Hs80Device device)
    {
        _dev = device;
    }

    public double? ReadLivePercent()
    {
        RateGate.Wait();
        var r = Exchange(Frame.GetProperty(Frame.RouteChild, 0x0F), false);
        if (r == null) return null;
        var p = Reply.Parse(r);
        if (p.IsNak || p.Err != 0x00 || p.PropValue.Length < 2) return null;
        return (p.PropValue[0] | (p.PropValue[1] << 8)) / 10.0;
    }

    public int? ReadChargeState()
    {
        RateGate.Wait();
        var r = Exchange(Frame.GetProperty(Frame.RouteChild, 0x10), false);
        if (r == null) return null;
        var p = Reply.Parse(r);
        if (p.IsNak || p.Err != 0x00 || p.PropValue.Length == 0) return null;
        return p.PropValue[0];
    }

    protected virtual byte[]? Exchange(byte[] frame, bool expectBattery)
    {
        return _dev.Xfer(frame, expectBattery);
    }
}
