using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

public class ClockTickTests
{
    [Fact]
    public void Waits_until_just_after_the_next_minute()
    {
        var now = new DateTime(2026, 11, 11, 19, 21, 45, 250);

        Assert.Equal(TimeSpan.FromMilliseconds(14_750) + ClockTick.Margin, ClockTick.DelayToNextMinute(now));
    }

    [Fact]
    public void Wakes_into_the_next_minute()
    {
        var now = new DateTime(2026, 11, 11, 19, 21, 7, 900);

        Assert.Equal(22, now.Add(ClockTick.DelayToNextMinute(now)).Minute);
    }

    [Fact]
    public void Full_minute_wait_at_the_start_of_a_minute()
    {
        var now = new DateTime(2026, 11, 11, 19, 21, 0);

        Assert.Equal(TimeSpan.FromMinutes(1) + ClockTick.Margin, ClockTick.DelayToNextMinute(now));
    }

    [Fact]
    public void Never_shorter_than_the_minimum_delay()
    {
        var now = new DateTime(2026, 11, 11, 19, 21, 59, 990);

        Assert.Equal(ClockTick.MinimumDelay, ClockTick.DelayToNextMinute(now));
    }
}
