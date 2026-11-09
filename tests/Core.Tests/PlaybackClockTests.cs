using Koy.Core.Media;

namespace Koy.Core.Tests;

public class PlaybackClockTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static PlaybackTimeline Timeline(double position, DateTimeOffset updated, double start = 0, double end = 200) =>
        new(S(position), S(start), S(end), updated);

    [Fact]
    public void Adds_elapsed_time_while_playing()
    {
        Assert.Equal(S(15), PlaybackClock.PositionAt(Timeline(10, Now.AddSeconds(-5)), true, Now));
    }

    [Fact]
    public void Paused_position_does_not_advance()
    {
        Assert.Equal(S(10), PlaybackClock.PositionAt(Timeline(10, Now.AddSeconds(-5)), false, Now));
    }

    [Fact]
    public void Is_relative_to_start_time()
    {
        Assert.Equal(S(5), PlaybackClock.PositionAt(Timeline(15, Now, start: 10), false, Now));
    }

    [Fact]
    public void Clamps_to_duration()
    {
        Assert.Equal(S(200), PlaybackClock.PositionAt(Timeline(199, Now.AddMinutes(-30)), true, Now));
    }

    [Fact]
    public void Ignores_very_old_timestamps()
    {
        Assert.Equal(S(10), PlaybackClock.PositionAt(Timeline(10, Now.AddHours(-2)), true, Now));
    }

    [Fact]
    public void Ignores_unset_timestamp()
    {
        Assert.Equal(S(10), PlaybackClock.PositionAt(Timeline(10, DateTimeOffset.MinValue), true, Now));
    }

    [Fact]
    public void Negative_duration_is_zero()
    {
        Assert.Equal(TimeSpan.Zero, PlaybackClock.Duration(Timeline(0, Now, start: 50, end: 10)));
    }
}
