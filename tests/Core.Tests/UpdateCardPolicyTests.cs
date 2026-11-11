using Koy.Core.Updates;

namespace Koy.Core.Tests;

public class UpdateCardPolicyTests
{
    [Fact]
    public void ShowsFirstTime()
    {
        var policy = new UpdateCardPolicy(new FakeClock());
        Assert.True(policy.ShouldShow(true, "0.3.0", notchExpanded: false));
    }

    [Theory]
    [InlineData(false, "0.3.0")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    public void DoesNotShowWithoutUpdate(bool hasUpdate, string? version)
    {
        var policy = new UpdateCardPolicy(new FakeClock());
        Assert.False(policy.ShouldShow(hasUpdate, version, false));
    }

    [Fact]
    public void DoesNotShowAgainForSameVersion()
    {
        var policy = new UpdateCardPolicy(new FakeClock());
        policy.MarkShown("0.3.0");
        Assert.False(policy.ShouldShow(true, "0.3.0", false));
    }

    [Fact]
    public void DismissedVersionStaysHidden()
    {
        var policy = new UpdateCardPolicy(new FakeClock());
        policy.MarkShown("0.3.0");
        policy.Dismiss("0.3.0");
        Assert.False(policy.ShouldShow(true, "0.3.0", false));
    }

    [Fact]
    public void DifferentVersionShowsAgain()
    {
        var policy = new UpdateCardPolicy(new FakeClock());
        policy.MarkShown("0.3.0");
        policy.Dismiss("0.3.0");
        Assert.True(policy.ShouldShow(true, "0.3.1", false));
    }

    [Fact]
    public void WaitsWhileNotchExpandedThenShows()
    {
        var policy = new UpdateCardPolicy(new FakeClock());
        Assert.False(policy.ShouldShow(true, "0.3.0", notchExpanded: true));
        Assert.True(policy.ShouldShow(true, "0.3.0", notchExpanded: false));
    }

    [Fact]
    public void ExpiresAfterFifteenSeconds()
    {
        var clock = new FakeClock();
        var policy = new UpdateCardPolicy(clock);
        Assert.False(policy.IsExpired());

        policy.MarkShown("0.3.0");
        clock.Advance(TimeSpan.FromSeconds(14));
        Assert.False(policy.IsExpired());

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(policy.IsExpired());
    }
}
