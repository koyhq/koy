using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

/// <summary>Çentik hedef boyutları: eski NotchViewModel.UpdateTargetSize sayılarıyla birebir aynı olmalı.</summary>
public class NotchSizePolicyTests
{
    [Theory]
    [InlineData(NotchState.Collapsed, false, 236, 34, 18)]
    [InlineData(NotchState.Collapsed, true, 280, 34, 18)]
    [InlineData(NotchState.Peek, false, 300, 38, 20)]
    [InlineData(NotchState.Peek, true, 300, 38, 20)]
    public void Closed_states_have_fixed_sizes(NotchState state, bool musicPlaying, double width, double height, double radius)
    {
        var size = NotchSizePolicy.Resolve(state, osdActive: false, osdIsCard: false, musicPlaying);

        Assert.Equal((width, height, radius), (size.Width, size.Height, size.PinnedRadius));
        Assert.Equal(NotchSurface.Closed, size.Surface);
    }

    [Fact]
    public void Expanded_uses_the_fixed_layout_canvas_regardless_of_music()
    {
        var size = NotchSizePolicy.Resolve(NotchState.Expanded, false, false, isMusicPlaying: true);

        Assert.Equal(NotchLayout.ExpandedWidth, size.Width);
        Assert.Equal(NotchLayout.ExpandedHeight, size.Height);
        Assert.Equal(34, size.PinnedRadius);
        Assert.Equal(NotchSurface.Expanded, size.Surface);
    }

    [Theory]
    [InlineData(NotchState.Collapsed)]
    [InlineData(NotchState.Peek)]
    [InlineData(NotchState.Expanded)]
    public void Osd_overrides_the_state(NotchState state)
    {
        var osd = NotchSizePolicy.Resolve(state, osdActive: true, osdIsCard: false, isMusicPlaying: true);
        var card = NotchSizePolicy.Resolve(state, osdActive: true, osdIsCard: true, isMusicPlaying: false);

        Assert.Equal((260d, 40d, 20d, NotchSurface.Closed), (osd.Width, osd.Height, osd.PinnedRadius, osd.Surface));
        Assert.Equal((NotchLayout.CardWidth, NotchLayout.CardHeight, 28d, NotchSurface.Card), (card.Width, card.Height, card.PinnedRadius, card.Surface));
    }

    [Fact]
    public void Pinned_style_keeps_the_table_radius_and_floating_uses_surface_rules()
    {
        var closed = NotchSizePolicy.Resolve(NotchState.Collapsed, false, false, false);
        var expanded = NotchSizePolicy.Resolve(NotchState.Expanded, false, false, false);
        var card = NotchSizePolicy.Resolve(NotchState.Collapsed, true, true, false);

        Assert.Equal(18, NotchSizePolicy.CornerRadius(NotchStyleMode.Pinned, closed));
        Assert.Equal(34, NotchSizePolicy.CornerRadius(NotchStyleMode.Pinned, expanded));
        Assert.Equal(17, NotchSizePolicy.CornerRadius(NotchStyleMode.Floating, closed));
        Assert.Equal(NotchStyle.FloatingExpandedRadius, NotchSizePolicy.CornerRadius(NotchStyleMode.Floating, expanded));
        Assert.Equal(NotchStyle.FloatingCardRadius, NotchSizePolicy.CornerRadius(NotchStyleMode.Floating, card));
    }
}
