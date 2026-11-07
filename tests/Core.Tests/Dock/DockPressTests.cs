using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class DockPressTests
{
    [Fact]
    public void Released_state_changes_nothing()
    {
        Assert.Equal(1.0, DockPress.Scale(0, motionEnabled: true));
        Assert.Equal(1.0, DockPress.Opacity(0));
    }

    [Fact]
    public void Fully_pressed_shrinks_and_darkens()
    {
        Assert.Equal(DockPress.PressedScale, DockPress.Scale(1, motionEnabled: true), 6);
        Assert.Equal(DockPress.PressedOpacity, DockPress.Opacity(1), 6);
        Assert.InRange(DockPress.PressedScale, 0.90, 0.94);
    }

    [Fact]
    public void Reduced_motion_never_changes_the_scale_only_the_color()
    {
        Assert.Equal(1.0, DockPress.Scale(1, motionEnabled: false));
        Assert.True(DockPress.Opacity(1) < 1.0);
    }

    [Fact]
    public void Spring_overshoot_springs_back_above_one_but_never_over_darkens()
    {
        // Bırakınca yay sıfırın altına sarkabilir: ölçek 1'in biraz üstüne çıkar (geri sıçrama), opaklık 1'de kalır.
        Assert.True(DockPress.Scale(-0.1, motionEnabled: true) > 1.0);
        Assert.Equal(1.0, DockPress.Opacity(-0.1));
        Assert.Equal(DockPress.PressedOpacity, DockPress.Opacity(1.3), 6);
    }

    [Fact]
    public void Press_scale_is_independent_of_hover_magnify()
    {
        // Görünen ölçek = hover x basma: ikisi çarpılır, biri öbürünün hedefini değiştirmez.
        var hover = 1.2;
        var combined = hover * DockPress.Scale(1, true);
        Assert.Equal(1.2 * DockPress.PressedScale, combined, 6);
        Assert.Equal(1.2, hover);
    }
}
