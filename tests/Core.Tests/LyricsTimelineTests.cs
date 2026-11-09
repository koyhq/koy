using Koy.Core.Lyrics;

namespace Koy.Core.Tests;

public class LyricsTimelineTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static readonly LyricLine[] Lines =
    [
        new(S(10), "a"),
        new(S(20), "b"),
        new(S(30), "c")
    ];

    [Theory]
    [InlineData(0, -1)]
    [InlineData(9.8, -1)]
    [InlineData(9.9, 0)]
    [InlineData(19, 0)]
    [InlineData(25, 1)]
    [InlineData(500, 2)]
    public void Finds_active_line_with_lead(double position, int expected)
    {
        Assert.Equal(expected, LyricsTimeline.IndexAt(Lines, S(position)));
    }

    [Fact]
    public void No_lines_means_no_active_line_or_timer()
    {
        Assert.Equal(-1, LyricsTimeline.IndexAt([], S(5)));
        Assert.Null(LyricsTimeline.UntilNextChange([], S(5)));
    }

    [Fact]
    public void Delay_reaches_next_line_minus_lead()
    {
        Assert.Equal(S(20) - LyricsTimeline.Lead - S(15), LyricsTimeline.UntilNextChange(Lines, S(15)));
    }

    [Fact]
    public void No_timer_after_last_line()
    {
        Assert.Null(LyricsTimeline.UntilNextChange(Lines, S(31)));
    }

    [Fact]
    public void Lines_with_same_time_never_produce_a_zero_delay_loop()
    {
        LyricLine[] lines = [new(S(10), "a"), new(S(10), "b"), new(S(20), "c")];

        Assert.Equal(1, LyricsTimeline.IndexAt(lines, S(11)));
        Assert.True(LyricsTimeline.UntilNextChange(lines, S(11)) > TimeSpan.Zero);
    }
}
