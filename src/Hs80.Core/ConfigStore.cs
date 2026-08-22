using System.Text.Json;

namespace Hs80.Core;

public sealed class ColorPreset
{
    public string Name { get; set; } = "";
    public int[] EarcupsRgb { get; set; } = new int[3];
    public int[] MicRgb { get; set; } = new int[3];
}

public sealed class ConfigData
{
    public List<ColorPreset> Presets { get; set; } = new();
    public List<EventBinding> Bindings { get; set; } = new();
    public int LowBatteryThreshold { get; set; } = 20;
    public int CriticalBatteryThreshold { get; set; } = 10;
    public int HttpPort { get; set; } = 8765;
    public bool HttpEnabled { get; set; }
    public bool StartWithWindows { get; set; }
}

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _filePath;

    public ConfigStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "hs80-app", "config.json");
    }

    public string FilePath => _filePath;

    public ConfigData Data { get; set; } = new();

    public void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            Data = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(_filePath), JsonOpts) ?? new ConfigData();
        }
        catch
        {
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(Data, JsonOpts));
    }
}
