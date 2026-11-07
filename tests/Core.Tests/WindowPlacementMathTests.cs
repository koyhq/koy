using Koy.Core.Display;
using Koy.Core.Shell;
using Xunit;

namespace Koy.Core.Tests;

public class WindowPlacementMathTests
{
    // Ana ekran %100, sağdaki ikinci ekran %150 ve farklı çözünürlükte; ikincisi sanal masaüstünde x=1920'den başlar.
    private static readonly DisplayMonitor Main = new("a", "Ana", new WindowRect(0, 0, 1920, 1080), new WindowRect(0, 0, 1920, 1040), true, 1.0, 1.0);
    private static readonly DisplayMonitor Side = new("b", "Yan", new WindowRect(1920, -200, 5760, 1960), new WindowRect(1920, -200, 5760, 1920), false, 1.5, 1.5);

    [Fact]
    public void Notch_is_centered_on_its_own_monitor_with_its_own_scale()
    {
        var rect = WindowPlacementMath.TopCenter(Side, 500, 260);

        Assert.Equal(750, rect.Width);   // 500 DIP * 1.5
        Assert.Equal(390, rect.Height);  // 260 DIP * 1.5
        Assert.Equal(1920 + (3840 - 750) / 2, rect.Left);
        Assert.Equal(-200, rect.Top);
    }

    [Fact]
    public void Center_places_the_window_in_the_middle_of_its_own_monitor()
    {
        var rect = WindowPlacementMath.Center(Side, 200, 100);

        Assert.Equal(300, rect.Width);
        Assert.Equal(150, rect.Height);
        Assert.Equal(1920 + (3840 - 300) / 2, rect.Left);
        Assert.Equal(-200 + (2160 - 150) / 2, rect.Top);
    }

    [Fact]
    public void Notch_on_main_monitor_uses_scale_one()
    {
        var rect = WindowPlacementMath.TopCenter(Main, 500, 260);

        Assert.Equal(new WindowRect(710, 0, 1210, 260), rect);
    }

    [Fact]
    public void Capsule_dock_sits_at_bottom_center_with_scaled_margin()
    {
        var rect = WindowPlacementMath.BottomCenter(Side, 600, 60, 10);

        Assert.Equal(1960 - 15, rect.Bottom);
        Assert.Equal(90, rect.Height);
        Assert.Equal(900, rect.Width);
        Assert.Equal(1920 + (3840 - 900) / 2, rect.Left);
    }

    [Fact]
    public void Capsule_dock_never_exceeds_the_monitor_width()
    {
        var rect = WindowPlacementMath.BottomCenter(Main, 5000, 60, 0);

        Assert.Equal(1920, rect.Width);
        Assert.Equal(0, rect.Left);
    }

    [Fact]
    public void Full_width_dock_spans_the_monitor_and_hugs_the_bottom()
    {
        var rect = WindowPlacementMath.BottomFullWidth(Side, Koy.Core.Dock.DockPositionResolver.FullWidthHeightDip);

        Assert.Equal(1920, rect.Left);
        Assert.Equal(5760, rect.Right);
        Assert.Equal(1960, rect.Bottom);
        Assert.Equal(72, rect.Height);   // 48 DIP * 1.5
    }

    [Theory]
    [InlineData(1.0, 48)]
    [InlineData(1.25, 60)]
    [InlineData(1.5, 72)]
    [InlineData(1.75, 84)]
    [InlineData(2.0, 96)]
    [InlineData(1.1, 53)]     // 52.8 -> 53
    [InlineData(1.05, 50)]    // 50.4 -> 50
    public void Reserved_pixels_equal_the_window_height_at_every_scale(double scale, int expected)
    {
        var monitor = new DisplayMonitor("m", "M", new WindowRect(0, 0, 2560, 1440), new WindowRect(0, 0, 2560, 1440), true, scale, scale);
        var window = WindowPlacementMath.BottomFullWidth(monitor, Koy.Core.Dock.DockPositionResolver.FullWidthHeightDip);

        Assert.Equal(expected, window.Height);
        // AppBar aynı fonksiyondan geçer: ayrılan pay pencere yüksekliğine tam eşittir.
        Assert.Equal(window.Height, WindowPlacementMath.ToPixels(Koy.Core.Dock.DockPositionResolver.FullWidthHeightDip, scale));
    }

    [Theory]
    [InlineData(46, 1.75, 81)]   // 80.5 yukarı yuvarlanır (eski kesme 80, banker 80 verirdi)
    [InlineData(46, 1.25, 58)]   // 57.5
    [InlineData(48, 0, 48)]      // geçersiz ölçek 1 sayılır
    public void ToPixels_rounds_half_up_and_guards_invalid_scale(double dip, double scale, int expected)
        => Assert.Equal(expected, WindowPlacementMath.ToPixels(dip, scale));
}
