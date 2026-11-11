using Koy.Core.Notch;
using Xunit;

namespace Koy.Core.Tests;

public class NotchLayoutTests
{
    [Theory]
    [InlineData(NotchState.Collapsed, false, true, false)]
    [InlineData(NotchState.Collapsed, true, true, false)]
    // Parça yokken ipucu boş kalır: boşta yerleşimi açık kalır ki Wi-Fi / Bluetooth simgelerine tıklanabilsin.
    [InlineData(NotchState.Peek, false, true, false)]
    [InlineData(NotchState.Peek, true, false, true)]
    [InlineData(NotchState.Expanded, false, false, false)]
    [InlineData(NotchState.Expanded, true, false, false)]
    public void Layout_choice_depends_on_state_and_track(NotchState state, bool hasTrack, bool collapsed, bool peek)
    {
        Assert.Equal(collapsed, NotchLayout.ShowCollapsedLayout(state, hasTrack));
        Assert.Equal(peek, NotchLayout.ShowPeekLayout(state, hasTrack));
    }

    [Theory]
    [InlineData(NotchState.Collapsed, false, false)]
    [InlineData(NotchState.Collapsed, true, false)]
    [InlineData(NotchState.Peek, false, true)]
    [InlineData(NotchState.Peek, true, false)]
    [InlineData(NotchState.Expanded, false, false)]
    public void Date_and_connections_show_only_when_hovering_without_a_track(NotchState state, bool hasTrack, bool expected)
    {
        Assert.Equal(expected, NotchLayout.ShowPeekDetails(state, hasTrack));
    }

    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, true)]
    [InlineData(IdleDisplayMode.ClockOnly, false)]
    [InlineData(IdleDisplayMode.Nothing, false)]
    public void Peek_details_follow_the_idle_display_setting(IdleDisplayMode mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowPeekDetails(mode));
    }

    [Fact]
    public void Expanded_notch_fits_the_fixed_canvas()
    {
        // Kural #13: pencere sabit tuvalde durur; açık çentik tuvali aşamaz, titreme payı kalır.
        Assert.InRange(NotchLayout.ExpandedWidth, 100, NotchLayout.CanvasWidth);
        Assert.InRange(NotchLayout.ExpandedHeight, 100, NotchLayout.CanvasHeight - 8);
    }
    [Fact]
    public void Hover_in_click_mode_without_track_keeps_idle_layout()
    {
        using var controller = new NotchController { OpenOnHover = false };

        controller.OnMouseEnter();

        Assert.Equal(NotchState.Peek, controller.State);
        Assert.True(NotchLayout.ShowCollapsedLayout(controller.State, hasTrack: false));
        Assert.False(NotchLayout.ShowPeekLayout(controller.State, hasTrack: false));
    }
}
