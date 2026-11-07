using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class DockFitMathTests
{
    [Fact]
    public void Fits_without_shrinking_when_there_is_room()
        => Assert.Equal(28, DockFitMath.FitIconSize(28, 10, 100, 1920));

    [Fact]
    public void Shrinks_to_the_largest_whole_icon_that_fits()
    {
        // 20 yuva, sabit 100, 1000 genişlik: yuva başına 45 -> simge 45 - 12 = 33 sığar; tercih 28 olduğundan 28.
        Assert.Equal(28, DockFitMath.FitIconSize(28, 20, 100, 1000));
        // 30 yuva: (1000-100)/30 = 30 -> 30 - 12 = 18 -> alt sınır 24'e çekilir.
        Assert.Equal(24, DockFitMath.FitIconSize(28, 30, 100, 1000));
        // 25 yuva: 36 - 12 = 24.
        Assert.Equal(24, DockFitMath.FitIconSize(28, 25, 100, 1000));
        // 24 yuva: 37.5 - 12 = 25.5 -> 25
        Assert.Equal(25, DockFitMath.FitIconSize(28, 24, 100, 1000));
    }

    [Fact]
    public void Never_goes_below_the_minimum_even_when_nothing_fits()
        => Assert.Equal(DockFitMath.MinIconSizeDip, DockFitMath.FitIconSize(32, 200, 100, 800));

    [Fact]
    public void Never_grows_beyond_the_preferred_size()
        => Assert.Equal(32, DockFitMath.FitIconSize(32, 2, 0, 5000));

    [Fact]
    public void Preferred_below_minimum_is_left_alone()
        => Assert.Equal(20, DockFitMath.FitIconSize(20, 100, 0, 500));

    [Theory]
    [InlineData(0, 100, 1000)]
    [InlineData(5, 100, 0)]
    [InlineData(5, 100, double.NaN)]
    [InlineData(5, 100, double.PositiveInfinity)]
    public void Degenerate_input_keeps_the_preferred_size(int slots, double fixedWidth, double available)
        => Assert.Equal(28, DockFitMath.FitIconSize(28, slots, fixedWidth, available));

    [Fact]
    public void Result_actually_fits_when_above_minimum()
    {
        const int slots = 22;
        const double fixedWidth = 120, available = 1100;
        var icon = DockFitMath.FitIconSize(32, slots, fixedWidth, available);
        Assert.True(icon > DockFitMath.MinIconSizeDip);
        Assert.True(fixedWidth + slots * DockFitMath.SlotWidth(icon) <= available);
    }

    [Fact]
    public void ItemSize_and_slot_width_follow_the_icon()
    {
        Assert.Equal(38, DockFitMath.ItemSizeFor(28));
        Assert.Equal(40, DockFitMath.SlotWidth(28));
    }

    [Fact]
    public void Strip_max_width_never_negative()
    {
        Assert.Equal(600, DockFitMath.StripMaxWidth(1000, 400));
        Assert.Equal(0, DockFitMath.StripMaxWidth(100, 400));
    }
}
