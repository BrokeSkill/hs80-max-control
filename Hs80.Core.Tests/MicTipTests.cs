using Hs80.Core;

namespace Hs80.Core.Tests;

public class MicTipTests
{
    private static readonly int[] Live = { 0, 0, 0 };
    private static readonly int[] Muted = { 255, 0, 0 };

    [Fact]
    public void FollowOff_ReturnsLiveColor_RegardlessOfMuteState()
    {
        var result = MicTip.Resolve(followMute: false, muted: true, liveRgb: Live, mutedRgb: Muted);
        Assert.Equal(Live, result);
    }

    [Fact]
    public void FollowOn_Muted_ReturnsMutedColor()
    {
        var result = MicTip.Resolve(followMute: true, muted: true, liveRgb: Live, mutedRgb: Muted);
        Assert.Equal(Muted, result);
    }

    [Fact]
    public void FollowOn_Unmuted_ReturnsLiveColor()
    {
        var result = MicTip.Resolve(followMute: true, muted: false, liveRgb: Live, mutedRgb: Muted);
        Assert.Equal(Live, result);
    }

    [Fact]
    public void FollowOn_UnknownState_ReturnsLiveColor()
    {
        var result = MicTip.Resolve(followMute: true, muted: null, liveRgb: Live, mutedRgb: Muted);
        Assert.Equal(Live, result);
    }
}
