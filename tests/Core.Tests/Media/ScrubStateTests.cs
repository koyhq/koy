using Koy.Core.Media;

namespace Koy.Core.Tests;

public class ScrubStateTests
{
    private static readonly TimeSpan Song = TimeSpan.FromMinutes(4);

    [Fact]
    public void Begin_fails_for_unknown_or_live_duration()
    {
        var s = new ScrubState();
        Assert.False(s.Begin(TimeSpan.Zero, 0.5));
        Assert.False(s.IsActive);
        Assert.Null(s.End());
    }

    [Fact]
    public void Move_tracks_preview_without_ending()
    {
        var s = new ScrubState();
        Assert.True(s.Begin(Song, 0.1));
        s.Move(0.5);
        Assert.True(s.IsActive);
        Assert.Equal(0.5, s.Fraction);
        Assert.Equal(TimeSpan.FromMinutes(2), s.PreviewPosition);
        Assert.Equal("2:00", s.Text);
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(7, 1)]
    [InlineData(double.NaN, 0)]
    public void Fraction_is_clamped(double input, double expected)
    {
        var s = new ScrubState();
        s.Begin(Song, 0.5);
        s.Move(input);
        Assert.Equal(expected, s.Fraction);
    }

    [Fact]
    public void End_returns_single_target_then_resets()
    {
        var s = new ScrubState();
        s.Begin(Song, 0.25);
        s.Move(0.75);
        Assert.Equal(TimeSpan.FromMinutes(3), s.End());
        Assert.False(s.IsActive);
        Assert.Null(s.End());
        Assert.Equal(string.Empty, s.Text);
    }

    [Fact]
    public void Cancel_discards_without_target()
    {
        var s = new ScrubState();
        s.Begin(Song, 0.25);
        s.Move(0.9);
        s.Cancel();
        Assert.False(s.IsActive);
        Assert.Null(s.End());
    }

    [Fact]
    public void Move_before_begin_is_ignored()
    {
        var s = new ScrubState();
        s.Move(0.5);
        Assert.False(s.IsActive);
        Assert.Equal(0, s.Fraction);
    }

    [Theory]
    [InlineData(83, "1:23")]
    [InlineData(5, "0:05")]
    [InlineData(3725, "1:02:05")]
    public void FormatTime_matches_player_labels(int seconds, string expected) =>
        Assert.Equal(expected, ScrubState.FormatTime(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(60, 5, 65)]
    [InlineData(2, -5, 0)]
    [InlineData(238, 5, 240)]
    public void StepTarget_clamps_to_track(int pos, int delta, int expected) =>
        Assert.Equal(TimeSpan.FromSeconds(expected), ScrubState.StepTarget(TimeSpan.FromSeconds(pos), Song, TimeSpan.FromSeconds(delta)));

    [Fact]
    public void StepTarget_is_null_when_live() =>
        Assert.Null(ScrubState.StepTarget(TimeSpan.FromSeconds(3), TimeSpan.Zero, ScrubState.KeyStep));

    [Fact]
    public void FractionOf_handles_zero_duration() =>
        Assert.Equal(0, ScrubState.FractionOf(TimeSpan.FromSeconds(3), TimeSpan.Zero));
}
