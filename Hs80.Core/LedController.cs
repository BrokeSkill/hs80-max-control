namespace Hs80.Core;

public class LedController
{
    private readonly Hs80Device _dev;
    private (byte R0, byte G0, byte B0, byte R1, byte G1, byte B1)? _last;

    public LedController(Hs80Device device)
    {
        _dev = device;
    }

    public (byte R0, byte G0, byte B0, byte R1, byte G1, byte B1)? LastColor => _last;

    public bool SetColor(byte earcupsR, byte earcupsG, byte earcupsB, byte micR, byte micG, byte micB)
    {
        if (!Write(Frame.LedSetMode(0x02))) return false;
        if (!Write(Frame.LedOpenHandle())) return false;
        if (!Write(Frame.LedWriteData(earcupsR, earcupsG, earcupsB, micR, micG, micB))) return false;
        if (!Write(Frame.LedCloseHandle())) return false;
        _last = (earcupsR, earcupsG, earcupsB, micR, micG, micB);
        return true;
    }

    public bool SetBrightness(int value)
    {
        value = Math.Clamp(value, 0, 1000);
        if (!Write(Frame.LedSetMode(0x02))) return false;
        var ok = Write(Frame.SetBrightness((byte)(value & 0xFF), (byte)((value >> 8) & 0xFF)));
        return ok;
    }

    public bool ReapplyLastColor()
    {
        if (_last == null) return false;
        var c = _last.Value;
        return SetColor(c.R0, c.G0, c.B0, c.R1, c.G1, c.B1);
    }

    public bool RestoreHwMode()
    {
        return Write(Frame.LedSetMode(0x01));
    }

    protected virtual byte[]? Exchange(byte[] frame)
    {
        return _dev.Xfer(frame, false);
    }

    protected virtual bool Write(byte[] frame)
    {
        var r = Exchange(frame);
        if (r == null) return false;
        var p = Reply.Parse(r);
        return !p.IsNak && p.Err == 0x00;
    }
}
