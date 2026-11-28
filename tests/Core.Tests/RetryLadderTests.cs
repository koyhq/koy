using Koy.Core.Voice;

namespace Koy.Core.Tests;

public class RetryLadderTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(4, 120)]
    public void Waits_grow_after_each_failure(int failed, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), RetryLadder.NextDelay(failed));
    }

    [Fact]
    public void The_ladder_ends_after_five_attempts()
    {
        Assert.Equal(5, RetryLadder.MaxAttempts);
        Assert.Null(RetryLadder.NextDelay(RetryLadder.MaxAttempts));
    }

    [Fact]
    public void Zero_failures_is_not_a_retry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = RetryLadder.NextDelay(0); });
    }
}
