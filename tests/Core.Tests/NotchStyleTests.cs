using Koy.Core.Notch;
using Xunit;

namespace Koy.Core.Tests;

public class NotchStyleTests
{
    [Fact]
    public void Pinned_keeps_ears_and_touches_the_screen_edge()
    {
        Assert.Equal(12, NotchStyle.EarRadius(NotchStyleMode.Pinned));
        Assert.Equal(0, NotchStyle.TopOffsetDip(NotchStyleMode.Pinned));
    }

    [Fact]
    public void Floating_has_no_ears_and_sits_nine_dip_below_the_top()
    {
        Assert.Equal(0, NotchStyle.EarRadius(NotchStyleMode.Floating));
        Assert.Equal(9, NotchStyle.TopOffsetDip(NotchStyleMode.Floating));
    }

    [Theory]
    [InlineData(NotchSurface.Closed, 34, 17)]
    [InlineData(NotchSurface.Closed, 38, 19)]
    [InlineData(NotchSurface.Closed, 40, 20)]
    [InlineData(NotchSurface.Expanded, 352, 30)]
    [InlineData(NotchSurface.Card, 72, 26)]
    public void Floating_radius_is_a_capsule_when_closed_and_fixed_when_open(NotchSurface surface, double height, double expected)
        => Assert.Equal(expected, NotchStyle.CornerRadius(NotchStyleMode.Floating, surface, height, 99));

    [Theory]
    [InlineData(NotchSurface.Closed)]
    [InlineData(NotchSurface.Expanded)]
    [InlineData(NotchSurface.Card)]
    public void Pinned_radius_is_the_existing_value(NotchSurface surface)
        => Assert.Equal(21, NotchStyle.CornerRadius(NotchStyleMode.Pinned, surface, 50, 21));

    [Theory]
    [InlineData(NotchStyleMode.Pinned, 1.0, 2)]
    [InlineData(NotchStyleMode.Floating, 1.0, 11)]
    [InlineData(NotchStyleMode.Floating, 1.5, 16)]
    [InlineData(NotchStyleMode.Floating, 2.0, 20)]
    public void Edge_trigger_covers_the_floating_gap(NotchStyleMode style, double scale, int expectedPixels)
        => Assert.Equal(expectedPixels, NotchStyle.EdgeTriggerBottomPixels(style, scale));
}
