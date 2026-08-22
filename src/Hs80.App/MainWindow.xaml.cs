using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Hs80.Audio;
using Hs80.Core;
using Hs80.Http;
using Hs80.App.Controls;
using Microsoft.Win32;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Color = System.Windows.Media.Color;
using Frame = Hs80.Core.Frame;
using Point = System.Windows.Point;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Hs80.App;

public partial class MainWindow : Window
{
    private const string LastMarker = "__last";
    private const string HwMarker = "__hw";

    private static readonly SolidColorBrush GoodBrush = new(Color.FromRgb(0x2E, 0xCC, 0x71));
    private static readonly SolidColorBrush BadBrush = new(Color.FromRgb(0xE7, 0x4C, 0x3C));
    private static readonly SolidColorBrush DimBrush = new(Color.FromRgb(0x44, 0x44, 0x44));

    private readonly List<EventCatalogEntry> _catalog = Hs80.Core.EventCatalog.All.ToList();
    private readonly DispatcherTimer _tick;
    private bool _reading;
    private bool _exiting;
    private bool _editorLoading;
    private TrayIcon? _tray;
    private GlobalSystemMediaTransportControlsSessionManager? _smtcMgr;
    private GlobalSystemMediaTransportControlsSession? _smtcSession;
    private LedHttpServer? _http;

    public MainWindow()
    {
        InitializeComponent();
        LoadLastColors();
        BuildEventList();
        WireEditorEvents();
        WirePoller();
        SetupTray();
        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _tick.Tick += OnTick;
        _tick.Start();
        _ = InitSmtcAsync();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreLastColorToHw();
        RunHwConfirm();
        EventList.SelectedIndex = 0;
        HttpPortBox.Text = App.Config.Data.HttpPort.ToString();
        HttpToggle.IsChecked = App.Config.Data.HttpEnabled;
        StartupCheck.IsChecked = RunKey.IsSet();
        if (App.Config.Data.HttpEnabled)
        {
            StartHttp();
        }
    }

    private void StartHttp()
    {
        if (App.Led == null || App.Battery == null || _http?.Running == true) return;
        var port = App.Config.Data.HttpPort;
        var server = new LedHttpServer(App.Led, App.Battery, App.History, port);
        try
        {
            server.Start();
            _http = server;
            DevStateText.Text = $"HTTP :{port}";
        }
        catch
        {
            DevStateText.Text = "HTTP FAILED";
        }
    }

    private void StopHttp()
    {
        _http?.Stop();
        _http = null;
        DevStateText.Text = App.Device == null ? "DONGLE OFFLINE" : "DONGLE OK";
    }

    private void OnStartupToggle(object sender, RoutedEventArgs e)
    {
        var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (exe == null) return;
        if (StartupCheck.IsChecked == true)
        {
            RunKey.Set(exe);
            App.Config.Data.StartWithWindows = true;
        }
        else
        {
            RunKey.Remove();
            App.Config.Data.StartWithWindows = false;
        }
        App.Config.Save();
    }

    private void OnHttpToggle(object sender, RoutedEventArgs e)
    {
        if (HttpToggle.IsChecked == true)
        {
            if (int.TryParse(HttpPortBox.Text, out var port) && port is > 0 and < 65536)
            {
                App.Config.Data.HttpPort = port;
            }
            App.Config.Data.HttpEnabled = true;
            App.Config.Save();
            StartHttp();
        }
        else
        {
            App.Config.Data.HttpEnabled = false;
            App.Config.Save();
            StopHttp();
        }
    }

    private void OnHttpPortChanged(object sender, TextChangedEventArgs e)
    {
        if (!int.TryParse(HttpPortBox.Text, out var port) || port is <= 0 or >= 65536) return;
        if (_http?.Running == true && port != App.Config.Data.HttpPort)
        {
            App.Config.Data.HttpPort = port;
            App.Config.Save();
            StopHttp();
            StartHttp();
        }
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        if (_exiting) return;
        e.Cancel = true;
        Hide();
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnLinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
        }
        e.Handled = true;
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void LoadLastColors()
    {
        var last = App.Config.Data.Presets.FirstOrDefault(p => p.Name == LastMarker);
        if (last == null) return;
        EarcupsPicker.SelectedColor = RgbToColor(last.EarcupsRgb);
        MicPicker.SelectedColor = RgbToColor(last.MicRgb);
    }

    private void RestoreLastColorToHw()
    {
        var last = App.Config.Data.Presets.FirstOrDefault(p => p.Name == LastMarker);
        if (last == null || App.Led == null) return;
        ApplyLed(last.EarcupsRgb, last.MicRgb);
    }

    private (int[] ear, int[] mic) CurrentColors()
    {
        var e = EarcupsPicker.SelectedColor;
        var m = MicPicker.SelectedColor;
        return (new[] { (int)e.R, (int)e.G, (int)e.B }, new[] { (int)m.R, (int)m.G, (int)m.B });
    }

    private void ApplyLed(int[] ear, int[] mic)
    {
        if (ear.Length < 3 || mic.Length < 3) return;
        var ok = App.Led?.SetColor((byte)ear[0], (byte)ear[1], (byte)ear[2], (byte)mic[0], (byte)mic[1], (byte)mic[2]) ?? false;
        if (ok) StoreLast(ear, mic);
    }

    private void StoreLast(int[] ear, int[] mic)
    {
        var presets = App.Config.Data.Presets;
        var last = presets.FirstOrDefault(p => p.Name == LastMarker);
        if (last == null)
        {
            presets.Add(new ColorPreset { Name = LastMarker, EarcupsRgb = ear, MicRgb = mic });
        }
        else
        {
            last.EarcupsRgb = ear;
            last.MicRgb = mic;
        }
        App.Config.Save();
    }

    private void ApplyBrightness()
    {
        App.Led?.SetBrightness((int)BrightnessSlider.Value);
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        ApplyStatus.Text = "APPLYING\u2026";
        var ear = EarcupsPicker.SelectedColor;
        var mic = MicPicker.SelectedColor;
        var brightness = (int)BrightnessSlider.Value;
        _ = Task.Run(() =>
        {
            var ok = App.Led?.SetColor((byte)ear.R, (byte)ear.G, (byte)ear.B, (byte)mic.R, (byte)mic.G, (byte)mic.B) ?? false;
            var b = App.Led?.SetBrightness(brightness) ?? false;
            Dispatcher.InvokeAsync(() =>
            {
                ApplyStatus.Text = !ok ? "FAILED" : "APPLIED";
                if (ok) StoreLast(new[] { (int)ear.R, (int)ear.G, (int)ear.B }, new[] { (int)mic.R, (int)mic.G, (int)mic.B });
            });
        });
    }

    private void OnBrightnessValue(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        BrightnessValue.Text = ((int)e.NewValue).ToString();
    }

    private void OnBrightnessDrag(object sender, DragCompletedEventArgs e)
    {
        ApplyBrightness();
    }

    private void BuildEventList()
    {
        EventList.ItemsSource = _catalog;
        EventSearch.TextChanged += (_, _) => ApplyEventFilter();
    }

    private void ApplyEventFilter()
    {
        var q = EventSearch.Text.Trim();
        EventList.ItemsSource = q.Length == 0
            ? _catalog
            : _catalog
                .Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || c.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || c.TriggerCondition.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    private void WireEditorEvents()
    {
        AudioPresetBox.SelectionChanged += (_, _) => SaveBinding();
        CustomAudioPath.TextChanged += (_, _) => SaveBinding();
        ToastCheck.Checked += (_, _) => SaveBinding();
        ToastCheck.Unchecked += (_, _) => SaveBinding();
        Led0Enable.Checked += (_, _) => OnLedToggleChanged();
        Led0Enable.Unchecked += (_, _) => OnLedToggleChanged();
        Led1Enable.Checked += (_, _) => OnLedToggleChanged();
        Led1Enable.Unchecked += (_, _) => OnLedToggleChanged();
        BindingLed0.ColorCommitted += () => SaveBinding();
        BindingLed1.ColorCommitted += () => SaveBinding();
        VolumeSlider.ValueChanged += (_, _) =>
        {
            VolumeValue.Text = ((int)VolumeSlider.Value) + "%";
            SaveBinding();
        };
    }

    private void OnAudioPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveBinding();
    }

    private void OnCustomPathChanged(object sender, TextChangedEventArgs e)
    {
        SaveBinding();
    }

    private void OnToastChanged(object sender, RoutedEventArgs e)
    {
        SaveBinding();
    }

    private void OnLedToggleChanged(object sender, RoutedEventArgs e)
    {
        OnLedToggleChanged();
    }

    private void OnLedToggleChanged()
    {
        BindingLed0.IsEnabled = Led0Enable.IsChecked == true;
        BindingLed1.IsEnabled = Led1Enable.IsChecked == true;
        SaveBinding();
    }

    private void OnEventSelected(object sender, SelectionChangedEventArgs e)
    {
        if (EventList.SelectedItem is not EventCatalogEntry entry) return;
        var binding = App.Config.Data.Bindings.FirstOrDefault(b => b.Event == entry.Kind);
        var action = binding?.Action;
        _editorLoading = true;
        BindingTitle.Text = entry.Name;
        BindingDesc.Text = entry.Description;
        AudioPresetBox.SelectedIndex = action == null ? 0 : (int)action.AudioPreset;
        CustomAudioPath.Text = action?.CustomAudioPath ?? "";
        ToastCheck.IsChecked = action?.Toast ?? false;
        VolumeSlider.Value = (action?.Volume ?? 0.6) * 100;
        VolumeValue.Text = ((int)VolumeSlider.Value) + "%";
        Led0Enable.IsChecked = action?.LedColorEarcups != null;
        BindingLed0.IsEnabled = Led0Enable.IsChecked == true;
        if (action?.LedColorEarcups != null) BindingLed0.SelectedColor = RgbToColor(action.LedColorEarcups);
        Led1Enable.IsChecked = action?.LedColorMic != null;
        BindingLed1.IsEnabled = Led1Enable.IsChecked == true;
        if (action?.LedColorMic != null) BindingLed1.SelectedColor = RgbToColor(action.LedColorMic);
        BindingEditor.IsEnabled = true;
        _editorLoading = false;
    }

    private void SaveBinding()
    {
        if (_editorLoading) return;
        if (EventList.SelectedItem is not EventCatalogEntry entry) return;
        var bindings = App.Config.Data.Bindings;
        var binding = bindings.FirstOrDefault(b => b.Event == entry.Kind);
        if (binding == null)
        {
            binding = new EventBinding { Event = entry.Kind, Action = new BindingAction() };
            bindings.Add(binding);
        }
        var a = binding.Action;
        a.AudioPreset = AudioPresetBox.SelectedIndex > 0 ? (AudioPreset)AudioPresetBox.SelectedIndex : AudioPreset.None;
        a.Volume = VolumeSlider.Value / 100.0;
        var custom = CustomAudioPath.Text.Trim();
        a.CustomAudioPath = custom.Length == 0 ? null : custom;
        a.Toast = ToastCheck.IsChecked == true;
        a.LedColorEarcups = Led0Enable.IsChecked == true ? ColorToRgb(BindingLed0.SelectedColor) : null;
        a.LedColorMic = Led1Enable.IsChecked == true ? ColorToRgb(BindingLed1.SelectedColor) : null;
        App.Config.Save();
    }

    private void OnBrowseAudio(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Audio files|*.wav;*.mp3|WAV files|*.wav|MP3 files|*.mp3",
            Title = "Choose warning sound",
        };
        if (dlg.ShowDialog(this) == true)
        {
            CustomAudioPath.Text = dlg.FileName;
            SaveBinding();
        }
    }

    private void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        string? path = null;
        var mp3 = false;
        if (AudioPresetBox.SelectedIndex > 0)
        {
            path = ResolvePresetPath((AudioPreset)AudioPresetBox.SelectedIndex);
            mp3 = path != null && path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
        }
        else if (CustomAudioPath.Text.Trim().Length > 0)
        {
            path = CustomAudioPath.Text.Trim();
            mp3 = path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
        }
        if (path == null || !File.Exists(path)) return;
        if (mp3) App.Audio.PlayMp3(path, VolumeSlider.Value / 100.0);
        else App.Audio.PlayWave(path, VolumeSlider.Value / 100.0);
    }

    private void WirePoller()
    {
        var poller = App.Poller;
        if (poller == null) return;
        poller.BatteryLow += (_, _) => FireEvent(EventKind.BatteryLow);
        poller.BatteryCritical += (_, _) => FireEvent(EventKind.BatteryCritical);
        poller.ChargingStarted += (_, _) => FireEvent(EventKind.ChargingStarted);
        poller.ChargeComplete += (_, _) => FireEvent(EventKind.ChargeComplete);
        poller.DischargingStarted += (_, _) => FireEvent(EventKind.DischargingStarted);
        poller.HeadsetConnected += (_, _) => FireEvent(EventKind.HeadsetConnected);
        poller.HeadsetDisconnected += (_, _) => FireEvent(EventKind.HeadsetDisconnected);
        poller.MicMuted += (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                MicDot.Fill = BadBrush;
                MicText.Text = "MIC MUTED";
            });
            FireEvent(EventKind.MicMuted);
        };
        poller.MicUnmuted += (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                MicDot.Fill = GoodBrush;
                MicText.Text = "MIC LIVE";
            });
            FireEvent(EventKind.MicUnmuted);
        };
        poller.Start();
    }

    private void FireEvent(EventKind kind)
    {
        Dispatcher.InvokeAsync(() => RunBinding(kind));
    }

    private void RunBinding(EventKind kind)
    {
        if (kind == EventKind.HeadsetConnected)
        {
            App.Led?.ReapplyLastColor();
            if (!IsHwConfirmed()) RunHwConfirm();
        }
        var binding = App.Config.Data.Bindings.FirstOrDefault(b => b.Event == kind);
        if (binding == null) return;
        var a = binding.Action;
        string? path = null;
        var mp3 = false;
        if (a.AudioPreset != AudioPreset.None)
        {
            path = ResolvePresetPath(a.AudioPreset);
            mp3 = path != null && path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
        }
        else if (!string.IsNullOrEmpty(a.CustomAudioPath))
        {
            path = a.CustomAudioPath;
            mp3 = path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
        }
        if (path != null && File.Exists(path))
        {
            var vol = a.Volume;
            if (mp3) App.Audio.PlayMp3(path, vol);
            else App.Audio.PlayWave(path, vol);
        }
        if (a.Toast)
        {
            var entry = _catalog.FirstOrDefault(c => c.Kind == kind);
            _tray?.ShowToast(entry?.Name ?? kind.ToString(), entry?.TriggerCondition ?? "");
        }
        if (a.LedColorEarcups != null || a.LedColorMic != null)
        {
            ApplyEventLed(a);
        }
    }

    private void ApplyEventLed(BindingAction a)
    {
        var ear = a.LedColorEarcups ?? LastRgb(EarcupsPicker.SelectedColor);
        var mic = a.LedColorMic ?? LastRgb(MicPicker.SelectedColor);
        ApplyLed(ear, mic);
    }

    private static int[] LastRgb(Color c)
    {
        return new[] { (int)c.R, (int)c.G, (int)c.B };
    }

    private static string ResolvePresetPath(AudioPreset preset)
    {
        return PresetAudio.Paths.TryGetValue(preset.ToString(), out var rel)
            ? Path.Combine(AppContext.BaseDirectory, rel)
            : "";
    }

    private bool IsHwConfirmed()
    {
        return App.Config.Data.Presets.Any(p => p.Name == HwMarker);
    }

    private void RunHwConfirm()
    {
        if (App.Led == null || IsHwConfirmed()) return;
        try
        {
            if (!ConfirmStep(0xF5, 0xA6, 0x23, 0, 0, 0,
                "Earcup check",
                "Both earcups should glow amber.",
                "LED0 drives both earcups with one shared color.")) return;
            if (!ConfirmStep(0, 0, 0, 0x2E, 0xCC, 0x71,
                "Mic tip check",
                "The mic tip should glow green.",
                "LED1 drives only the mic tip.")) return;
            App.Config.Data.Presets.Add(new ColorPreset { Name = HwMarker, EarcupsRgb = new[] { 1, 0, 0 }, MicRgb = new[] { 1, 0, 0 } });
            App.Config.Save();
        }
        finally
        {
            RestoreLastColorToHw();
        }
    }

    private bool ConfirmStep(byte r0, byte g0, byte b0, byte r1, byte g1, byte b1, string title, string detail, string hint)
    {
        App.Led?.SetColor(r0, g0, b0, r1, g1, b1);
        var dlg = new ConfirmDialog(title, detail, hint) { Owner = this };
        return dlg.ShowDialog() == true;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_reading) return;
        if (App.Device == null || App.Battery == null)
        {
            SetOfflineDisplay();
            return;
        }
        _reading = true;
        _ = Task.Run(() =>
        {
            double? pct = null;
            int? charge = null;
            bool? mic = null;
            bool? link = null;
            try
            {
                var m = App.Device.Xfer(Frame.GetProperty(Frame.RouteChild, 0xA6), false);
                if (m != null)
                {
                    var p = Reply.Parse(m);
                    if (!p.IsNak && p.Err == 0x00 && p.PropValue.Length > 0) mic = p.PropValue[0] != 0;
                }
                var l = App.Device.Xfer(Frame.GetProperty(Frame.RouteRoot, 0x36), false);
                if (l != null)
                {
                    var p = Reply.Parse(l);
                    if (!p.IsNak && p.Err == 0x00 && p.PropValue.Length > 0) link = (p.PropValue[0] & 0x02) != 0;
                }
                pct = App.Battery.ReadLivePercent();
                charge = App.Battery.ReadChargeState();
            }
            catch
            {
            }
            Dispatcher.InvokeAsync(() =>
            {
                _reading = false;
                ApplyDisplay(pct, charge, mic, link);
            });
        });
    }

    private void SetOfflineDisplay()
    {
        BatteryPercent.Text = "--%";
        ChargeState.Text = "--";
        EtaText.Text = "ETA --";
        MicDot.Fill = DimBrush;
        MicText.Text = "MIC --";
        LinkDot.Fill = DimBrush;
        LinkText.Text = "LINK --";
        DevStateText.Text = App.Device == null ? "DONGLE OFFLINE" : "SCANNING";
        _tray?.SetBattery(null, null);
    }

    private void ApplyDisplay(double? pct, int? charge, bool? mic, bool? link)
    {
        if (pct.HasValue)
        {
            BatteryPercent.Text = $"{pct.Value:0}%";
            App.History.Add(DateTime.UtcNow, pct.Value);
            UpdateSparkline();
            var eta = charge == 1 ? App.History.ChargeEta(pct.Value) : App.History.LinearEta(pct.Value);
            EtaText.Text = eta.HasValue ? "ETA " + FormatEta(eta.Value) : "ETA --";
        }
        else
        {
            BatteryPercent.Text = "--%";
            EtaText.Text = "ETA --";
        }
        ChargeState.Text = charge switch
        {
            0 => "DISCHARGING",
            1 => "CHARGING",
            2 => "FULL",
            _ => charge == 3 ? "ERROR" : "--",
        };
        MicDot.Fill = mic == null ? DimBrush : mic.Value ? BadBrush : GoodBrush;
        MicText.Text = mic == null ? "MIC --" : mic.Value ? "MIC MUTED" : "MIC LIVE";
        LinkDot.Fill = link == null ? DimBrush : link.Value ? GoodBrush : DimBrush;
        LinkText.Text = link == null ? "LINK --" : link.Value ? "LINK UP" : "LINK DOWN";
        DevStateText.Text = pct == null ? "SCANNING" : "DONGLE OK";
        _tray?.SetBattery(pct.HasValue ? (int)Math.Round(pct.Value) : null, charge);
    }

    private void UpdateSparkline()
    {
        var samples = App.History.Samples;
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var recent = samples.Where(s => s.Timestamp >= cutoff).ToList();
        if (recent.Count < 2) return;
        var w = SparklinePath.ActualWidth;
        var h = SparklinePath.ActualHeight;
        if (w <= 0 || h <= 0) return;
        var stride = Math.Max(1, recent.Count / 240);
        var points = new List<Point>(recent.Count / stride + 1);
        for (var i = 0; i < recent.Count; i += stride)
        {
            var s = recent[i];
            var x = w * (i / (double)recent.Count);
            var y = h - h * (Math.Clamp(s.Percent, 0, 100) / 100.0);
            points.Add(new Point(x, y));
        }
        SparklinePath.Points = new PointCollection(points);
    }

    private static string FormatEta(double minutes)
    {
        var m = (int)Math.Round(minutes);
        if (m < 60) return $"{m}m";
        return $"{m / 60}h {m % 60}m";
    }

    private static Color RgbToColor(int[] rgb)
    {
        if (rgb == null || rgb.Length < 3) return Color.FromRgb(245, 166, 35);
        return Color.FromRgb((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]);
    }

    private static int[] ColorToRgb(Color c)
    {
        return new[] { (int)c.R, (int)c.G, (int)c.B };
    }

    private void SetupTray()
    {
        _tray = new TrayIcon();
        _tray.ShowWindow = () =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };
        _tray.HideWindow = () => Hide();
        _tray.LedOn = () =>
        {
            var c = CurrentColors();
            ApplyLed(c.ear, c.mic);
        };
        _tray.LedOff = () => App.Led?.SetColor(0, 0, 0, 0, 0, 0);
        _tray.ExitApp = () =>
        {
            _exiting = true;
            App.Current.Shutdown();
        };
    }

    private async Task InitSmtcAsync()
    {
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _smtcMgr = mgr;
            mgr.CurrentSessionChanged += OnSmtcSessionChanged;
            AttachSession(mgr.GetCurrentSession());
        }
        catch
        {
            SetEmptyMediaState();
        }
    }

    private void OnSmtcSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        AttachSession(sender.GetCurrentSession());
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_smtcSession != null) _smtcSession.MediaPropertiesChanged -= OnSmtcPropsChanged;
        _smtcSession = session;
        if (session == null)
        {
            SetEmptyMediaState();
            return;
        }
        session.MediaPropertiesChanged += OnSmtcPropsChanged;
        _ = RefreshMediaAsync();
    }

    private void OnSmtcPropsChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshMediaAsync();
    }

    private async Task RefreshMediaAsync()
    {
        try
        {
            if (_smtcSession == null)
            {
                SetEmptyMediaState();
                return;
            }
            var props = await _smtcSession.TryGetMediaPropertiesAsync();
            if (props == null || string.IsNullOrEmpty(props.Title))
            {
                SetEmptyMediaState();
                return;
            }
            NowTitle.Text = props.Title;
            NowArtist.Text = props.Artist ?? "";
            NowAlbum.Text = string.IsNullOrEmpty(props.AlbumTitle) ? "" : props.AlbumTitle;
            if (props.Thumbnail != null)
            {
                var bytes = await LoadThumbBytesAsync(props.Thumbnail);
                var img = bytes == null ? null : BytesToImage(bytes);
                if (img != null)
                {
                    ThumbImage.Source = img;
                    ThumbImage.Visibility = Visibility.Visible;
                    ThumbFallback.Visibility = Visibility.Collapsed;
                    return;
                }
            }
            ThumbImage.Visibility = Visibility.Collapsed;
            ThumbFallback.Visibility = Visibility.Visible;
        }
        catch
        {
            SetEmptyMediaState();
        }
    }

    private void SetEmptyMediaState()
    {
        NowTitle.Text = "Nothing playing";
        NowArtist.Text = "Waiting for a media session";
        NowAlbum.Text = "";
        ThumbImage.Visibility = Visibility.Collapsed;
        ThumbFallback.Visibility = Visibility.Visible;
    }

    private static async Task<byte[]?> LoadThumbBytesAsync(IRandomAccessStreamReference thumb)
    {
        try
        {
            using var stream = await thumb.OpenReadAsync();
            var len = (int)stream.Size;
            var reader = new DataReader(stream);
            await reader.LoadAsync((uint)len);
            var bytes = new byte[len];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? BytesToImage(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }
}
