using Hs80.Core;

namespace Hs80.Core.Tests;

file sealed class FakePoller : EventPoller
{
    public bool? NextMuted;

    public FakePoller() : base(null!)
    {
    }

    protected override bool? ReadMicMuted() => NextMuted;

    public void Tick() => TickMicForTest();
}

public class EventPollerMicTests
{
    [Fact]
    public void MicStateChanged_FiresOnFirstRead()
    {
        var poller = new FakePoller { NextMuted = false };
        bool? received = null;
        var fired = false;
        poller.MicStateChanged += (_, muted) => { fired = true; received = muted; };

        poller.Tick();

        Assert.True(fired);
        Assert.False(received);
    }

    [Fact]
    public void MicStateChanged_FiresOnChange()
    {
        var poller = new FakePoller { NextMuted = false };
        poller.Tick();

        var fireCount = 0;
        bool? last = null;
        poller.MicStateChanged += (_, muted) => { fireCount++; last = muted; };

        poller.NextMuted = true;
        poller.Tick();

        Assert.Equal(1, fireCount);
        Assert.True(last);
    }

    [Fact]
    public void MicStateChanged_DoesNotFireOnRepeat()
    {
        var poller = new FakePoller { NextMuted = true };
        poller.Tick();

        var fireCount = 0;
        poller.MicStateChanged += (_, _) => fireCount++;

        poller.Tick();
        poller.Tick();

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void MicMuted_DoesNotFireOnFirstRead()
    {
        var poller = new FakePoller { NextMuted = true };
        var fired = false;
        poller.MicMuted += (_, _) => fired = true;

        poller.Tick();

        Assert.False(fired);
    }

    [Fact]
    public void MicMuted_FiresWhenTransitioningToMuted()
    {
        var poller = new FakePoller { NextMuted = false };
        poller.Tick();

        var fired = false;
        poller.MicMuted += (_, _) => fired = true;
        poller.NextMuted = true;
        poller.Tick();

        Assert.True(fired);
    }
}
