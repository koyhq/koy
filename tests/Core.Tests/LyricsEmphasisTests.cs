using Koy.Core.Lyrics;

namespace Koy.Core.Tests;

public class LyricsEmphasisTests
{
    [Fact]
    public void Active_line_is_full()
    {
        Assert.Equal(1, LyricsEmphasis.OpacityAt(0));
        Assert.Equal(1, LyricsEmphasis.ScaleAt(0));
    }

    [Fact]
    public void Lines_fade_and_shrink_with_distance()
    {
        Assert.Equal(0.6, LyricsEmphasis.OpacityAt(1), 6);
        Assert.Equal(0.4, LyricsEmphasis.OpacityAt(-2), 6);
        Assert.Equal(0.28, LyricsEmphasis.OpacityAt(7), 6);
        Assert.Equal(0.9, LyricsEmphasis.ScaleAt(5), 6);
    }

    [Fact]
    public void Fractional_distance_blends_between_steps()
    {
        var o = LyricsEmphasis.OpacityAt(0.5);
        Assert.InRange(o, 0.6, 1);
        Assert.Equal(0.8, o, 6);
    }

    [Fact]
    public void Invalid_distance_is_faintest() => Assert.Equal(0.28, LyricsEmphasis.OpacityAt(double.NaN), 6);
}
