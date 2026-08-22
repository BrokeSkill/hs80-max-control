namespace Hs80.Core;

internal static class RateGate
{
    private static readonly object Sync = new();
    private static long _lastTick;
    private static int _minMs = 2000;

    internal static int MinIntervalMs
    {
        get
        {
            lock (Sync) return _minMs;
        }
        set
        {
            lock (Sync) _minMs = value;
        }
    }

    public static void Wait()
    {
        lock (Sync)
        {
            var wait = _minMs - (int)(Environment.TickCount64 - _lastTick);
            if (wait > 0) Thread.Sleep(wait);
            _lastTick = Environment.TickCount64;
        }
    }
}
