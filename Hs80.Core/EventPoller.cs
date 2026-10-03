namespace Hs80.Core;

public class EventPoller : IDisposable
{
    private readonly Hs80Device _dev;
    private readonly BatteryReader _battery;
    private readonly TimeSpan _interval;
    private readonly int _lowThreshold;
    private readonly int _criticalThreshold;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private CancellationTokenSource? _micCts;
    private Task? _micLoop;

    private bool _connected;
    private bool _connectedKnown;
    private double? _pct;
    private bool _lowFired;
    private bool _criticalFired;
    private int? _charge;
    private bool? _mic;

    public event EventHandler? BatteryLow;
    public event EventHandler? BatteryCritical;
    public event EventHandler? ChargingStarted;
    public event EventHandler? ChargeComplete;
    public event EventHandler? DischargingStarted;
    public event EventHandler? HeadsetConnected;
    public event EventHandler? HeadsetDisconnected;
    public event EventHandler? MicMuted;
    public event EventHandler? MicUnmuted;
    public event EventHandler<bool>? MicStateChanged;

    public bool? MicMutedState => _mic;

    public EventPoller(Hs80Device device, int lowThreshold = 20, int criticalThreshold = 10, TimeSpan? interval = null)
    {
        _dev = device;
        _battery = new BatteryReader(device);
        _lowThreshold = lowThreshold;
        _criticalThreshold = criticalThreshold;
        _interval = interval ?? TimeSpan.FromSeconds(2);
    }

    public void Start()
    {
        if (_loop != null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
        _micCts = new CancellationTokenSource();
        _micLoop = Task.Run(() => MicLoopAsync(_micCts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _loop?.GetAwaiter().GetResult();
        }
        catch
        {
        }
        _micCts?.Cancel();
        try
        {
            _micLoop?.GetAwaiter().GetResult();
        }
        catch
        {
        }
        _loop = null;
        _cts?.Dispose();
        _cts = null;
        _micLoop = null;
        _micCts?.Dispose();
        _micCts = null;
    }

    public void Dispose() => Stop();

    public void TickOnce()
    {
        var pct = ReadBatteryPercent();
        if (pct == null)
        {
            if (_connectedKnown && _connected) Fire(HeadsetDisconnected);
            _connected = false;
            _connectedKnown = true;
            _pct = null;
            _charge = null;
            _mic = null;
            _lowFired = false;
            _criticalFired = false;
            return;
        }
        var wasConnected = _connectedKnown && _connected;
        var known = _connectedKnown;
        _connected = true;
        _connectedKnown = true;
        var charge = ReadChargeState();
        if (wasConnected)
        {
            DiffBattery(pct.Value);
            DiffCharge(charge);
        }
        else
        {
            _pct = pct.Value;
            _charge = charge;
            if (known) Fire(HeadsetConnected);
        }
    }

    protected virtual double? ReadBatteryPercent()
    {
        return _battery.ReadLivePercent();
    }

    protected virtual int? ReadChargeState()
    {
        return _battery.ReadChargeState();
    }

    protected virtual bool? ReadMicMuted()
    {
        var r = _dev.Xfer(Frame.GetProperty(Frame.RouteChild, 0xA6), false);
        if (r == null) return null;
        var p = Reply.Parse(r);
        if (p.IsNak || p.Err != 0x00 || p.PropValue.Length == 0) return null;
        return p.PropValue[0] != 0;
    }

    private void DiffBattery(double pct)
    {
        _pct = pct;
        if (pct <= _criticalThreshold && !_criticalFired)
        {
            _criticalFired = true;
            Fire(BatteryCritical);
        }
        else if (pct > _criticalThreshold)
        {
            _criticalFired = false;
        }
        if (pct <= _lowThreshold && !_lowFired)
        {
            _lowFired = true;
            Fire(BatteryLow);
        }
        else if (pct > _lowThreshold)
        {
            _lowFired = false;
        }
    }

    private void DiffCharge(int? charge)
    {
        if (charge == null) return;
        if (charge == _charge) return;
        var prev = _charge;
        _charge = charge;
        if (prev == null) return;
        if (charge == 1) Fire(ChargingStarted);
        else if (charge == 2) Fire(ChargeComplete);
        else if (charge == 0) Fire(DischargingStarted);
    }

    private void DiffMic(bool? mic)
    {
        if (mic == null) return;
        if (mic == _mic) return;
        var prev = _mic;
        _mic = mic;
        MicStateChanged?.Invoke(this, mic.Value);
        if (prev == null) return;
        if (mic.Value) Fire(MicMuted);
        else Fire(MicUnmuted);
    }

    internal void TickMicForTest() => DiffMic(ReadMicMuted());

    private static void Fire(EventHandler? handler)
    {
        handler?.Invoke(null, EventArgs.Empty);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                TickOnce();
                await Task.Delay(_interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private async Task MicLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                DiffMic(ReadMicMuted());
                await Task.Delay(150, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }
}
