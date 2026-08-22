using System.Net;
using System.Text;
using System.Text.Json;
using Hs80.Core;

namespace Hs80.Http;

public sealed class LedHttpServer
{
    private const string LoopbackHost = "127.0.0.1";

    private readonly LedController _led;
    private readonly BatteryReader _battery;
    private readonly BatteryHistory _history;
    private readonly string _prefix;
    private readonly object _sync = new();
    private HttpListener? _listener;
    private Task? _loop;
    private volatile bool _running;
    private int _brightness = 500;

    public LedHttpServer(LedController led, BatteryReader battery, BatteryHistory history, int port = 8765)
        : this(led, battery, history, $"http://{LoopbackHost}:{port}/")
    {
    }

    public LedHttpServer(LedController led, BatteryReader battery, BatteryHistory history, string prefix)
    {
        _led = led;
        _battery = battery;
        _history = history;
        _prefix = prefix.EndsWith('/') ? prefix : prefix + "/";
    }

    public bool Running => _running;

    public void Start()
    {
        lock (_sync)
        {
            if (_running) return;
            ValidateLoopback(_prefix);
            var listener = new HttpListener();
            listener.Prefixes.Add(_prefix);
            listener.Start();
            _listener = listener;
            _running = true;
            _loop = Task.Run(Loop);
        }
    }

    public void Stop()
    {
        HttpListener? listener;
        Task? loop;
        lock (_sync)
        {
            if (!_running) return;
            listener = _listener;
            loop = _loop;
            _running = false;
            _listener = null;
            _loop = null;
        }
        listener?.Stop();
        loop?.Wait(2000);
    }

    private static void ValidateLoopback(string prefix)
    {
        if (!Uri.TryCreate(prefix, UriKind.Absolute, out var uri) || uri.Scheme != "http")
            throw new ArgumentException("prefix must be an absolute http URL", nameof(prefix));
        if (!string.Equals(uri.Host, LoopbackHost, StringComparison.Ordinal))
            throw new ArgumentException($"prefix must bind to {LoopbackHost} only", nameof(prefix));
    }

    private void Loop()
    {
        var listener = _listener;
        while (true)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = listener!.GetContext();
            }
            catch
            {
                return;
            }
            try
            {
                Handle(ctx);
            }
            catch
            {
                TryError(ctx, 500, "internal error");
            }
            finally
            {
                try
                {
                    ctx.Response.Close();
                }
                catch
                {
                }
            }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var method = ctx.Request.HttpMethod;
        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        switch (path)
        {
            case "/api/led" when method == "GET":
                WriteJson(ctx, 200, LedStateJson());
                break;
            case "/api/led" when method == "POST":
                HandleLedPost(ctx);
                break;
            case "/api/battery" when method == "GET":
                WriteJson(ctx, 200, BatteryJson());
                break;
            case "/api/led":
            case "/api/battery":
                WriteError(ctx, 405, "method not allowed");
                break;
            default:
                WriteError(ctx, 404, "not found");
                break;
        }
    }

    private void HandleLedPost(HttpListenerContext ctx)
    {
        string body;
        using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
            body = reader.ReadToEnd();
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                WriteError(ctx, 400, "invalid body");
                return;
            }
            if (root.TryGetProperty("brightness", out var b))
            {
                if (b.ValueKind != JsonValueKind.Number || !b.TryGetInt32(out var v) || v < 0 || v > 1000)
                {
                    WriteError(ctx, 400, "brightness must be an integer 0-1000");
                    return;
                }
                if (!_led.SetBrightness(v))
                {
                    WriteError(ctx, 500, "led write failed");
                    return;
                }
                _brightness = v;
                WriteJson(ctx, 200, LedStateJson());
                return;
            }
            if (!root.TryGetProperty("led", out var led) || led.ValueKind != JsonValueKind.String)
            {
                WriteError(ctx, 400, "body must have led+rgb or brightness");
                return;
            }
            var target = led.GetString();
            if (target is not ("earcups" or "mic" or "both"))
            {
                WriteError(ctx, 400, "led must be earcups, mic or both");
                return;
            }
            if (!root.TryGetProperty("rgb", out var rgb) || rgb.ValueKind != JsonValueKind.Array)
            {
                WriteError(ctx, 400, "rgb must be an array of 3 integers 0-255");
                return;
            }
            var values = new int[3];
            var n = 0;
            foreach (var el in rgb.EnumerateArray())
            {
                if (n >= 3 || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var c) || c < 0 || c > 255)
                {
                    WriteError(ctx, 400, "rgb must be an array of 3 integers 0-255");
                    return;
                }
                values[n++] = c;
            }
            if (n != 3)
            {
                WriteError(ctx, 400, "rgb must be an array of 3 integers 0-255");
                return;
            }
            var cur = _led.LastColor ?? default;
            var (r0, g0, b0, r1, g1, b1) = target switch
            {
                "earcups" => ((byte)values[0], (byte)values[1], (byte)values[2], cur.R1, cur.G1, cur.B1),
                "mic" => (cur.R0, cur.G0, cur.B0, (byte)values[0], (byte)values[1], (byte)values[2]),
                _ => ((byte)values[0], (byte)values[1], (byte)values[2], (byte)values[0], (byte)values[1], (byte)values[2]),
            };
            if (!_led.SetColor(r0, g0, b0, r1, g1, b1))
            {
                WriteError(ctx, 500, "led write failed");
                return;
            }
            WriteJson(ctx, 200, LedStateJson());
        }
        catch (JsonException)
        {
            WriteError(ctx, 400, "invalid json");
        }
    }

    private string LedStateJson()
    {
        var c = _led.LastColor ?? default;
        return JsonSerializer.Serialize(new
        {
            led0 = new { rgb = new[] { (int)c.R0, (int)c.G0, (int)c.B0 } },
            led1 = new { rgb = new[] { (int)c.R1, (int)c.G1, (int)c.B1 } },
            brightness = _brightness,
        });
    }

    private string BatteryJson()
    {
        var percent = _battery.ReadLivePercent();
        var state = _battery.ReadChargeState();
        var charge = state switch
        {
            0 => "discharge",
            1 => "charging",
            2 => "full",
            _ => "error",
        };
        double? eta = null;
        if (percent.HasValue)
        {
            if (state == 1)
                eta = EtaEstimator.ChargeEta(_history.Samples, percent.Value);
            else if (state == 0)
                eta = EtaEstimator.LinearEta(_history.Samples, percent.Value);
        }
        return JsonSerializer.Serialize(new
        {
            percent = percent.HasValue ? Math.Round(percent.Value, 1) : (double?)null,
            charge_state = charge,
            eta_min = eta,
        });
    }

    private static void WriteJson(HttpListenerContext ctx, int status, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    private static void WriteError(HttpListenerContext ctx, int status, string message)
    {
        WriteJson(ctx, status, JsonSerializer.Serialize(new { error = message }));
    }

    private static void TryError(HttpListenerContext ctx, int status, string message)
    {
        try
        {
            WriteError(ctx, status, message);
        }
        catch
        {
        }
    }
}
