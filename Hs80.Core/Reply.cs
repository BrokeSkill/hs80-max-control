namespace Hs80.Core;

public sealed class Reply
{
    public byte Route { get; }
    public byte Cmd { get; }
    public byte Err { get; }
    public bool IsNak { get; }
    public byte EventType { get; }
    public byte? BatteryPercent { get; }
    public byte[] PropValue { get; }

    private Reply(byte route, byte cmd, byte err, bool isNak, byte eventType, byte? batteryPercent, byte[] propValue)
    {
        Route = route;
        Cmd = cmd;
        Err = err;
        IsNak = isNak;
        EventType = eventType;
        BatteryPercent = batteryPercent;
        PropValue = propValue;
    }

    public static Reply Parse(byte[] frame)
    {
        var b = frame ?? Array.Empty<byte>();
        if (b.Length >= 2 && b[0] == 0x01 && b[1] == 0xF0)
        {
            return new Reply(b[1], b.Length > 2 ? b[2] : (byte)0, 0, true, 0, null, Array.Empty<byte>());
        }
        var route = b.Length > 1 ? b[1] : (byte)0;
        var cmd = b.Length > 2 ? b[2] : (byte)0;
        var err = b.Length > 3 ? b[3] : (byte)0;
        var pct = err == 0x05 && b.Length > 4 ? b[4] : (byte?)null;
        var prop = new byte[b.Length > 4 ? b.Length - 4 : 0];
        if (prop.Length > 0) Array.Copy(b, 4, prop, 0, prop.Length);
        return new Reply(route, cmd, err, false, err, pct, prop);
    }
}
