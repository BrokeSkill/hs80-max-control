using System.Text.Json;

namespace Hs80.Core;

public sealed class BatteryHistory
{
    public readonly record struct Sample(DateTime Timestamp, double Percent);

    private readonly List<Sample> _samples = new();
    private readonly int _capacity;
    private readonly string _filePath;
    private DateTime _lastSave = DateTime.MinValue;

    public BatteryHistory(string? filePath = null, int capacity = 20000)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "hs80-app", "battery-history.json");
        _capacity = Math.Max(1, capacity);
        Load();
    }

    public string FilePath => _filePath;

    public IReadOnlyList<Sample> Samples => _samples;

    public void Add(DateTime timestamp, double percent)
    {
        _samples.Add(new Sample(timestamp.ToUniversalTime(), percent));
        while (_samples.Count > _capacity) _samples.RemoveAt(0);
        if ((DateTime.UtcNow - _lastSave).TotalSeconds >= 60) Save();
    }

    public void Save()
    {
        _lastSave = DateTime.UtcNow;
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var payload = new
        {
            samples = _samples.Select(s => new { t = s.Timestamp.ToString("o"), p = s.Percent }).ToArray()
        };
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(payload));
        File.Move(tmp, _filePath, true);
    }

    public void Load()
    {
        _samples.Clear();
        if (!File.Exists(_filePath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_filePath));
            if (!doc.RootElement.TryGetProperty("samples", out var arr)) return;
            foreach (var el in arr.EnumerateArray())
            {
                if (!el.TryGetProperty("t", out var t) || !el.TryGetProperty("p", out var p)) continue;
                if (t.ValueKind != JsonValueKind.String || !DateTime.TryParse(t.GetString(), out var ts)) continue;
                _samples.Add(new Sample(ts.ToUniversalTime(), p.GetDouble()));
            }
        }
        catch
        {
        }
    }

    public double? LinearEta(double percent)
        => EtaEstimator.LinearEta(_samples, percent);

    public double? ChargeEta(double percent)
        => EtaEstimator.ChargeEta(_samples, percent);
}

public static class EtaEstimator
{
    private const int WindowMinutes = 30;
    private const int MinSpanMinutes = 5;

    public static double? LinearEta(IReadOnlyList<BatteryHistory.Sample> samples, double percent)
    {
        var slope = FitSlope(Directional(samples, rising: false));
        if (slope == null || slope.Value >= 0) return null;
        var eta = percent / -slope.Value;
        return eta > 0 && !double.IsInfinity(eta) ? eta : null;
    }

    public static double? ChargeEta(IReadOnlyList<BatteryHistory.Sample> samples, double percent)
    {
        var slope = FitSlope(Directional(samples, rising: true));
        if (slope == null || slope.Value <= 0) return null;
        var eta = (100.0 - percent) / slope.Value;
        return eta > 0 && !double.IsInfinity(eta) ? eta : null;
    }

    private static IReadOnlyList<BatteryHistory.Sample> Directional(
        IReadOnlyList<BatteryHistory.Sample> samples, bool rising)
    {
        if (samples == null || samples.Count < 2) return samples ?? Array.Empty<BatteryHistory.Sample>();
        var cutoff = samples[^1].Timestamp.AddMinutes(-WindowMinutes);
        var window = samples.Where(s => s.Timestamp >= cutoff).ToList();
        var filtered = new List<BatteryHistory.Sample>(window.Count);
        for (var i = 1; i < window.Count; i++)
        {
            var d = window[i].Percent - window[i - 1].Percent;
            if (rising ? d > 0 : d < 0) filtered.Add(window[i]);
        }
        return filtered;
    }

    private static double? FitSlope(IReadOnlyList<BatteryHistory.Sample> samples)
    {
        if (samples == null || samples.Count < 2) return null;
        var first = samples[0].Timestamp;
        var span = (samples[^1].Timestamp - first).TotalMinutes;
        if (span < MinSpanMinutes) return null;
        double sx = 0, sy = 0, sxx = 0, sxy = 0;
        var n = 0.0;
        foreach (var s in samples)
        {
            var x = (s.Timestamp - first).TotalMinutes;
            sx += x;
            sy += s.Percent;
            sxx += x * x;
            sxy += x * s.Percent;
            n++;
        }
        var denom = n * sxx - sx * sx;
        if (Math.Abs(denom) < 1e-9) return null;
        return (n * sxy - sx * sy) / denom;
    }
}
