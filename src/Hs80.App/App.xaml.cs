using System.IO;
using System.Windows;
using Hs80.Audio;
using Hs80.Core;

namespace Hs80.App;

public partial class App : System.Windows.Application
{
    public static ConfigStore Config = new();
    public static BatteryHistory History = new();
    public static AudioPlayer Audio = new();
    public static Hs80Device? Device;
    public static LedController? Led;
    public static BatteryReader? Battery;
    public static EventPoller? Poller;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        Config.Load();
        EnsureAssets();
        SeedBindings();
        TryOpenDevice();
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        Poller?.Stop();
        Led?.RestoreHwMode();
        History.Save();
        Config.Save();
        Device?.Dispose();
    }

    private static void TryOpenDevice()
    {
        try
        {
            var hid = Hs80Device.Find();
            if (hid == null) return;
            var dev = new Hs80Device();
            dev.Open(hid);
            Device = dev;
            Led = new LedController(dev);
            Battery = new BatteryReader(dev);
            Poller = new EventPoller(dev, Config.Data.LowBatteryThreshold, Config.Data.CriticalBatteryThreshold);
        }
        catch
        {
            Device?.Dispose();
            Device = null;
            Led = null;
            Battery = null;
            Poller = null;
        }
    }

    private static void EnsureAssets()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Assets");
            Directory.CreateDirectory(dir);
            foreach (var pair in PresetAudio.Paths)
            {
                var path = Path.Combine(AppContext.BaseDirectory, pair.Value);
                if (File.Exists(path)) continue;
                var uri = new Uri("/Assets/" + Path.GetFileName(pair.Value), UriKind.Relative);
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info == null) continue;
                using var src = info.Stream;
                using var dst = File.Create(path);
                src.CopyTo(dst);
            }
        }
        catch
        {
        }
    }

    private static void SeedBindings()
    {
        var list = Config.Data.Bindings;
        var defaults = new[]
        {
            new EventBinding { Event = EventKind.BatteryLow, Action = new BindingAction { AudioPreset = AudioPreset.LowBattery, Toast = true } },
            new EventBinding { Event = EventKind.BatteryCritical, Action = new BindingAction { AudioPreset = AudioPreset.LowBattery, Toast = true } },
            new EventBinding { Event = EventKind.ChargingStarted, Action = new BindingAction { Toast = true } },
            new EventBinding { Event = EventKind.ChargeComplete, Action = new BindingAction { Toast = true } },
            new EventBinding { Event = EventKind.HeadsetDisconnected, Action = new BindingAction { AudioPreset = AudioPreset.Disconnect, Toast = true } },
            new EventBinding { Event = EventKind.HeadsetConnected, Action = new BindingAction { AudioPreset = AudioPreset.Connect, Toast = true } },
            new EventBinding { Event = EventKind.MicMuted, Action = new BindingAction { AudioPreset = AudioPreset.MicMute, Toast = false } },
            new EventBinding { Event = EventKind.MicUnmuted, Action = new BindingAction { AudioPreset = AudioPreset.MicUnmute, Toast = false } },
        };
        var added = false;
        foreach (var d in defaults)
        {
            if (list.Any(b => b.Event == d.Event)) continue;
            list.Add(d);
            added = true;
        }
        if (added) Config.Save();
    }
}
