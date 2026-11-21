using Koy.Core.Updates;

namespace Koy.Core.Tests.Updates;

public class UpdateCheckScheduleTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0);

    [Fact]
    public void Never_checked_is_due() => Assert.True(UpdateCheckSchedule.IsDue(null, false, Now));

    [Fact]
    public void Recent_success_is_not_due() =>
        Assert.False(UpdateCheckSchedule.IsDue(Now - TimeSpan.FromHours(5), false, Now));

    [Fact]
    public void Success_older_than_interval_is_due() =>
        Assert.True(UpdateCheckSchedule.IsDue(Now - UpdateCheckSchedule.Interval, false, Now));

    [Fact]
    public void Failure_retries_sooner_than_interval()
    {
        Assert.False(UpdateCheckSchedule.IsDue(Now - TimeSpan.FromMinutes(10), true, Now));
        Assert.True(UpdateCheckSchedule.IsDue(Now - UpdateCheckSchedule.RetryAfterFailure, true, Now));
    }

    [Fact]
    public void Clock_moved_back_is_due() =>
        Assert.True(UpdateCheckSchedule.IsDue(Now + TimeSpan.FromHours(1), false, Now));
}
