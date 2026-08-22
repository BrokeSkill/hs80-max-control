using Microsoft.Win32;

namespace Hs80.Core;

public static class RunKey
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "hs80-app";

    public static void Set(string exePath)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Run key requires Windows.");
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(ValueName, Quote(exePath));
    }

    public static void Remove()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
        key?.DeleteValue(ValueName, false);
    }

    public static bool IsSet()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) != null;
    }

    private static string Quote(string path)
        => path.Contains(' ') ? "\"" + path + "\"" : path;
}
