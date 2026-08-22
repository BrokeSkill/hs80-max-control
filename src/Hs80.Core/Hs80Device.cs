using HidSharp;
using HidSharp.Reports;

namespace Hs80.Core;

public sealed class Hs80Device : IDisposable
{
    public const int Vid = 0x1B1C;
    public const int Pid = 0x0A97;
    public const ushort UsagePage = 0xFF42;
    private const string InterfaceMarker = "mi_04";
    private const string CollectionMarker = "col01";
    private const int MinWriteGapMs = 50;

    private HidStream? _stream;
    private int _off;
    private long _lastWrite;
    private readonly object _xferLock = new();

    public bool IsOpen => _stream != null;
    public string DevicePath { get; private set; } = "";

    public static HidDevice? Find()
    {
        var exact = DeviceList.Local.GetHidDevices(Vid, Pid).FirstOrDefault(IsControlInterface);
        if (exact != null) return exact;
        return DeviceList.Local.GetHidDevices(null, null, null, null).FirstOrDefault(IsControlInterface);
    }

    public void Open(HidDevice device)
    {
        if (device == null) throw new ArgumentNullException(nameof(device));
        _stream = device.Open();
        _stream.ReadTimeout = 500;
        DevicePath = device.DevicePath;
        _off = DetectOffset();
    }

    public void Close()
    {
        _stream?.Dispose();
        _stream = null;
    }

    public void Dispose() => Close();

    public byte[]? Xfer(byte[] request, bool expectBattery = false)
    {
        if (_stream == null) throw new InvalidOperationException("device not open");
        lock (_xferLock)
        {
            ThrottleWrite();
            var buf = new byte[64];
            Array.Copy(request, buf, Math.Min(request.Length, 64));
            _stream.Write(buf);
            _lastWrite = Environment.TickCount64;
            var sub = request.Length > 2 ? request[2] : (byte)0;
            var wantErr = expectBattery ? (byte)0x05 : (byte)0x00;
            byte[]? first = null;
            for (var i = 0; i < 6; i++)
            {
                var r = new byte[64];
                int n;
                try
                {
                    n = _stream.Read(r);
                }
                catch
                {
                    break;
                }
                if (n <= 0) break;
                first ??= (byte[])r.Clone();
                if (ByteAt(r, 2) == sub && ByteAt(r, 3) == wantErr) return r;
            }
            return first;
        }
    }

    private static bool IsControlInterface(HidDevice d)
    {
        if (!d.DevicePath.Contains(InterfaceMarker, StringComparison.OrdinalIgnoreCase)) return false;
        if (!d.DevicePath.Contains(CollectionMarker, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var desc = d.GetReportDescriptor();
            var stack = new Stack<DescriptorItem>();
            foreach (var item in desc.DeviceItems) stack.Push(item);
            while (stack.Count > 0)
            {
                var item = stack.Pop();
                if (item is DeviceItem di)
                {
                    foreach (var v in di.Usages.GetAllValues())
                    {
                        if ((v >> 16) == UsagePage) return true;
                    }
                }
                else if (item is DescriptorCollectionItem dci)
                {
                    foreach (var v in dci.Usages.GetAllValues())
                    {
                        if ((v >> 16) == UsagePage) return true;
                    }
                }
                foreach (var child in item.ChildItems) stack.Push(child);
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    private int DetectOffset()
    {
        var probe = new byte[64];
        probe[0] = 0x02;
        probe[1] = 0x08;
        probe[2] = 0x02;
        _stream!.Write(probe);
        var rep = new byte[64];
        int n;
        try
        {
            n = _stream.Read(rep);
        }
        catch
        {
            n = 0;
        }
        if (n > 3 && rep[3] == 0x05) return 0;
        if (n > 2 && rep[2] == 0x05) return 1;
        return 0;
    }

    private byte ByteAt(byte[] r, int i)
    {
        var idx = i + _off;
        return idx >= 0 && idx < r.Length ? r[idx] : (byte)0;
    }

    private void ThrottleWrite()
    {
        var wait = MinWriteGapMs - (int)(Environment.TickCount64 - _lastWrite);
        if (wait > 0) Thread.Sleep(wait);
    }
}
