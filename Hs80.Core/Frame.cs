namespace Hs80.Core;

public static class Frame
{
    public const byte RouteRoot = 0x08;
    public const byte RouteChild = 0x09;
    public const byte CmdGet = 0x02;
    public const byte CmdSet = 0x01;
    public const int Length = 64;

    public static byte[] GetProperty(byte route, byte prop)
        => Pad(0x02, route, CmdGet, prop);

    public static byte[] GetBatteryEvent(byte route)
        => Pad(0x02, route, CmdGet);

    public static byte[] LedSetMode(byte mode)
        => Pad(0x02, RouteChild, CmdSet, 0x03, 0x00, mode);

    public static byte[] LedOpenHandle()
        => Pad(0x02, RouteChild, 0x0D, 0x00, 0x22, 0x00);

    public static byte[] LedWriteData(byte r0, byte g0, byte b0, byte r1, byte g1, byte b1)
        => Pad(0x02, RouteChild, 0x06, 0x00, 0x08, 0x00, 0x00, 0x00, 0x12, 0x00, r0, g0, b0, r1, g1, b1);

    public static byte[] LedCloseHandle()
        => Pad(0x02, RouteChild, 0x05, 0x00, 0x00);

    public static byte[] SetBrightness(byte lo, byte hi)
        => SetProperty(RouteChild, 0x02, lo, hi);

    public static byte[] SetProperty(byte route, byte prop, params byte[] data)
    {
        var frame = new byte[Length];
        frame[0] = 0x02;
        frame[1] = route;
        frame[2] = CmdSet;
        frame[3] = prop;
        frame[4] = 0x00;
        Array.Copy(data, 0, frame, 5, Math.Min(data.Length, Length - 5));
        return frame;
    }

    private static byte[] Pad(params byte[] head)
    {
        var frame = new byte[Length];
        Array.Copy(head, frame, Math.Min(head.Length, Length));
        return frame;
    }
}
