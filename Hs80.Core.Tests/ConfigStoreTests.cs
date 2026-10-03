using System.Text.Json;
using Hs80.Core;

namespace Hs80.Core.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void MicFollowsMute_DefaultsFalse()
    {
        var data = new ConfigData();
        Assert.False(data.MicFollowsMute);
    }

    [Fact]
    public void MicMutedRgb_DefaultsToRed()
    {
        var data = new ConfigData();
        Assert.Equal(new[] { 255, 0, 0 }, data.MicMutedRgb);
    }

    [Fact]
    public void RoundTrip_PreservesNewFields()
    {
        var data = new ConfigData { MicFollowsMute = true, MicMutedRgb = new[] { 10, 20, 30 } };
        var json = JsonSerializer.Serialize(data);
        var loaded = JsonSerializer.Deserialize<ConfigData>(json)!;
        Assert.True(loaded.MicFollowsMute);
        Assert.Equal(new[] { 10, 20, 30 }, loaded.MicMutedRgb);
    }

    [Fact]
    public void OldConfigJson_MissingNewFields_LoadsDefaults()
    {
        const string oldJson = "{\"Presets\":[],\"Bindings\":[],\"LowBatteryThreshold\":20,\"CriticalBatteryThreshold\":10,\"HttpPort\":8765,\"HttpEnabled\":false,\"StartWithWindows\":false}";
        var loaded = JsonSerializer.Deserialize<ConfigData>(oldJson)!;
        Assert.False(loaded.MicFollowsMute);
        Assert.Equal(new[] { 255, 0, 0 }, loaded.MicMutedRgb);
    }
}
