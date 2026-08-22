using System;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace Hs80.App;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _batteryItem;
    private readonly Icon _full;
    private readonly Icon _mid;
    private readonly Icon _low;
    private readonly Icon _charge;

    public Action? ShowWindow { get; set; }
    public Action? HideWindow { get; set; }
    public Action? LedOn { get; set; }
    public Action? LedOff { get; set; }
    public Action? ExitApp { get; set; }

    public TrayIcon()
    {
        _full = LoadIcon("tray_full.ico");
        _mid = LoadIcon("tray_mid.ico");
        _low = LoadIcon("tray_low.ico");
        _charge = LoadIcon("tray_charge.ico");
        _icon = new Forms.NotifyIcon { Icon = _mid, Visible = true, Text = "HS80 MAX" };
        _menu = new Forms.ContextMenuStrip();
        _batteryItem = new Forms.ToolStripMenuItem("Battery --") { Enabled = false };
        var ledOn = new Forms.ToolStripMenuItem("LED on");
        ledOn.Click += (_, _) => LedOn?.Invoke();
        var ledOff = new Forms.ToolStripMenuItem("LED off");
        ledOff.Click += (_, _) => LedOff?.Invoke();
        var show = new Forms.ToolStripMenuItem("Show");
        show.Click += (_, _) => ShowWindow?.Invoke();
        var hide = new Forms.ToolStripMenuItem("Hide");
        hide.Click += (_, _) => HideWindow?.Invoke();
        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitApp?.Invoke();
        _menu.Items.Add(_batteryItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(ledOn);
        _menu.Items.Add(ledOff);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(show);
        _menu.Items.Add(hide);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(exit);
        _icon.ContextMenuStrip = _menu;
        _icon.DoubleClick += (_, _) => ShowWindow?.Invoke();
    }

    public void SetBattery(int? pct, int? charge)
    {
        Icon pick = _mid;
        if (charge == 1) pick = _charge;
        else if (pct.HasValue)
        {
            if (pct.Value < 15) pick = _low;
            else if (pct.Value < 50) pick = _mid;
            else pick = _full;
        }
        _icon.Icon = pick;
        _icon.Text = pct.HasValue ? $"HS80 MAX — {pct}%" : "HS80 MAX";
        _batteryItem.Text = pct.HasValue ? $"Battery {pct}%" : "Battery --";
    }

    public void ShowToast(string title, string text)
    {
        _icon.ShowBalloonTip(3000, title, text, Forms.ToolTipIcon.None);
    }

    private static Icon LoadIcon(string name)
    {
        try
        {
            var uri = new Uri("/Assets/" + name, UriKind.Relative);
            using var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            return stream == null ? SystemIcons.Application : new Icon(stream);
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _full.Dispose();
        _mid.Dispose();
        _low.Dispose();
        _charge.Dispose();
    }
}
