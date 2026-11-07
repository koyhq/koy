using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class SnapDebounceTests
{
    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void First_state_is_adopted_immediately()
    {
        var d = new SnapDebounce(Ms(150));
        Assert.True(d.Update(true, Ms(0)));
        Assert.False(d.IsPending);
    }

    [Fact]
    public void Brief_flip_that_returns_is_ignored()
    {
        var d = new SnapDebounce(Ms(150));
        d.Update(false, Ms(0));

        Assert.False(d.Update(true, Ms(10)));   // Alt+Tab ara durağı
        Assert.True(d.IsPending);
        Assert.False(d.Update(false, Ms(60)));  // geri döndü
        Assert.False(d.IsPending);
    }

    [Fact]
    public void Sustained_change_is_applied_after_the_delay()
    {
        var d = new SnapDebounce(Ms(150));
        d.Update(false, Ms(0));

        Assert.False(d.Update(true, Ms(10)));
        Assert.False(d.Update(true, Ms(100)));
        Assert.True(d.Update(true, Ms(170)));
        Assert.False(d.IsPending);
    }

    [Fact]
    public void Remaining_counts_down_and_is_zero_without_pending()
    {
        var d = new SnapDebounce(Ms(150));
        d.Update(false, Ms(0));
        Assert.Equal(TimeSpan.Zero, d.Remaining(Ms(5)));

        d.Update(true, Ms(10));
        Assert.Equal(Ms(100), d.Remaining(Ms(60)));
        Assert.Equal(TimeSpan.Zero, d.Remaining(Ms(500)));
    }

    [Fact]
    public void Reset_jumps_without_waiting()
    {
        var d = new SnapDebounce(Ms(150));
        d.Update(false, Ms(0));
        d.Update(true, Ms(10));

        d.Reset(true);

        Assert.True(d.Effective);
        Assert.False(d.IsPending);
    }
}
