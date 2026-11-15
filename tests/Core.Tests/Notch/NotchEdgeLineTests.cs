using Koy.Core.Display;
using Koy.Core.Notch;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Notch;

public class NotchEdgeLineTests
{
    private static DisplayMonitor Monitor(WindowRect bounds, double dpi) =>
        new("id", "Ekran", bounds, bounds, IsPrimary: true, DpiScaleX: dpi, DpiScaleY: dpi);

    [Fact]
    public void Spans_full_monitor_width_at_top_edge()
    {
        var rect = NotchEdgeLine.Bounds(Monitor(new WindowRect(0, 0, 1920, 1080), 1.0), NotchEdgeLine.DefaultThickness);

        Assert.Equal(new WindowRect(0, 0, 1920, 2), rect);
    }

    [Fact]
    public void Thickness_scales_with_dpi_and_follows_monitor_offset()
    {
        var rect = NotchEdgeLine.Bounds(Monitor(new WindowRect(-2560, -200, 0, 1240), 1.5), 2);

        Assert.Equal(new WindowRect(-2560, -200, 0, -197), rect);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(20, 20)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(99, 20)]
    public void Thickness_is_clamped_to_range(int requested, int expectedHeight) =>
        Assert.Equal(expectedHeight, NotchEdgeLine.Bounds(Monitor(new WindowRect(0, 0, 100, 100), 1.0), requested).Height);

    [Fact]
    public void Thin_line_never_collapses_below_one_pixel() =>
        Assert.Equal(1, NotchEdgeLine.Bounds(Monitor(new WindowRect(0, 0, 100, 100), 0.5), 1).Height);

    [Theory]
    [InlineData(NotchEdgeLineColor.NotchSurface, "KoyNotchSurface")]
    [InlineData(NotchEdgeLineColor.Accent, "KoyAccent")]
    public void Color_reads_theme_resource(NotchEdgeLineColor color, string key) =>
        Assert.Equal(key, NotchEdgeLine.ResourceKey(color));

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void Hides_only_when_notch_hides_in_fullscreen(bool fullscreen, bool hideInFullscreen, bool expected) =>
        Assert.Equal(expected, NotchEdgeLine.IsVisible(fullscreen, hideInFullscreen));
}
