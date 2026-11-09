using Koy.Core.Lyrics;

namespace Koy.Core.Tests;

public class LrcParserTests
{
    private static TimeSpan Ms(int value) => TimeSpan.FromMilliseconds(value);

    [Theory]
    [InlineData("[01:02.5]a", 62500)]
    [InlineData("[01:02.50]a", 62500)]
    [InlineData("[01:02.500]a", 62500)]
    [InlineData("[01:02:50]a", 62500)]
    [InlineData("[01:02]a", 62000)]
    public void Reads_time_stamps(string lrc, int expected)
    {
        var line = Assert.Single(LrcParser.Parse(lrc));

        Assert.Equal(Ms(expected), line.Time);
        Assert.Equal("a", line.Text);
    }

    [Fact]
    public void Sorts_by_time_and_expands_repeated_stamps()
    {
        var lines = LrcParser.Parse("[00:30.00]b\n[00:10.00][00:50.00]chorus\n");

        Assert.Equal(["chorus", "b", "chorus"], lines.Select(l => l.Text));
        Assert.Equal([Ms(10_000), Ms(30_000), Ms(50_000)], lines.Select(l => l.Time));
    }

    [Fact]
    public void Skips_metadata_and_handles_bom_and_crlf()
    {
        var lines = LrcParser.Parse("﻿[ar:Someone]\r\n[ti:Song]\r\nplain text\r\n[00:01.00] hello \r\n");

        var line = Assert.Single(lines);
        Assert.Equal("hello", line.Text);
    }

    [Fact]
    public void Removes_word_level_tags()
    {
        var line = Assert.Single(LrcParser.Parse("[00:01.00]<00:01.00>one <00:01.50>two"));

        Assert.Equal("one two", line.Text);
    }

    [Fact]
    public void Positive_offset_moves_lines_earlier_and_never_below_zero()
    {
        var lines = LrcParser.Parse("[offset:+500]\n[00:00.20]a\n[00:02.00]b");

        Assert.Equal(TimeSpan.Zero, lines[0].Time);
        Assert.Equal(Ms(1500), lines[1].Time);
    }

    [Fact]
    public void Collapses_consecutive_blank_lines()
    {
        var lines = LrcParser.Parse("[00:01.00]a\n[00:02.00]\n[00:03.00]\n[00:04.00]b");

        Assert.Equal(["a", "", "b"], lines.Select(l => l.Text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no stamps here")]
    [InlineData("[00:01.00]\n[00:02.00]")]
    public void Returns_empty_when_there_is_nothing_to_show(string? lrc)
    {
        Assert.Empty(LrcParser.Parse(lrc));
    }
}
