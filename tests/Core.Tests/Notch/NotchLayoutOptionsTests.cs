using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

/// <summary>Çentik düzeni seçenekleri (issue #78): gecikmeli açılma, kapanma gecikmesi, sekme sırası, sürükleyince açılma.</summary>
public class NotchLayoutOptionsTests
{
    private static RefreshDebouncerTests.ManualTimeProvider Time() => new();

    [Fact]
    public void Hover_open_with_delay_peeks_first_and_opens_when_the_timer_fires()
    {
        var time = Time();
        using var controller = new NotchController(time) { OpenOnHover = true, OpenDelayMs = 300 };

        controller.OnMouseEnter();
        Assert.Equal(NotchState.Peek, controller.State);

        time.FireLast();
        Assert.Equal(NotchState.Expanded, controller.State);
    }

    [Fact]
    public void Leaving_before_the_open_delay_cancels_opening()
    {
        var time = Time();
        using var controller = new NotchController(time) { OpenOnHover = true, OpenDelayMs = 300 };

        controller.OnMouseEnter();
        var openTimer = time.Timers[^1];
        controller.OnMouseLeave();
        controller.OnHoverOpenTimeout(); // geç kalan zamanlayıcı geri çağrısı da açmamalı

        Assert.NotEqual(NotchState.Expanded, controller.State);
        Assert.Contains(openTimer, time.Timers);
    }

    [Fact]
    public void Zero_open_delay_keeps_todays_immediate_open()
    {
        var time = Time();
        using var controller = new NotchController(time) { OpenOnHover = true };

        controller.OnMouseEnter();

        Assert.Equal(NotchState.Expanded, controller.State);
        Assert.Empty(time.Timers);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(250, 250)]
    [InlineData(99999, NotchOpenPolicy.MaxDelayMs)]
    public void Delays_are_clamped(int requested, int expected)
    {
        using var controller = new NotchController { OpenDelayMs = requested, CloseDelayMs = requested };
        Assert.Equal(expected, controller.OpenDelayMs);
        Assert.Equal(expected, controller.CloseDelayMs);
    }

    [Fact]
    public void Close_delay_default_is_todays_400_ms() =>
        Assert.Equal(NotchOpenPolicy.DefaultCloseDelayMs, new NotchController().CloseDelayMs);

    [Fact]
    public void Enabled_panels_follow_the_user_order()
    {
        var panels = NotchPanels.ParseEnabled(["home", "focus", "shelf", "system"], true, ["system", "shelf"]);

        Assert.Equal([NotchPanel.System, NotchPanel.Shelf, NotchPanel.Home, NotchPanel.Focus], panels);
    }

    [Fact]
    public void Empty_order_keeps_the_default_order()
    {
        var keys = new[] { "home", "focus", "calendar" };
        Assert.Equal(NotchPanels.ParseEnabled(keys), NotchPanels.ParseEnabled(keys, true, []));
    }

    [Fact]
    public void Move_shifts_one_step_and_stops_at_the_ends()
    {
        var up = NotchPanels.Move([], NotchPanel.Focus, -1);
        Assert.Equal("focus", up[0]);
        Assert.Equal("home", up[1]);

        var top = NotchPanels.Move(up, NotchPanel.Focus, -1);
        Assert.Equal(up, top);

        var down = NotchPanels.Move([], NotchPanel.ControlCenter, 1);
        Assert.Equal("controlcenter", down[^1]);
    }

    [Fact]
    public void Temporary_panel_is_placed_by_the_user_order()
    {
        using var controller = new NotchController();
        controller.SetPanelOrder(["shelf", "home", "focus"]);
        controller.SetEnabledPanels([NotchPanel.Home, NotchPanel.Focus]);

        controller.Expand(NotchPanel.Shelf);

        Assert.Equal([NotchPanel.Shelf, NotchPanel.Home, NotchPanel.Focus], controller.EnabledPanels);
    }

    [Theory]
    [InlineData(NotchOpenBehavior.Hover, 0, 500, true)]
    [InlineData(NotchOpenBehavior.Click, 0, 500, true)]
    [InlineData(NotchOpenBehavior.DragOver, 100, 500, false)]
    [InlineData(NotchOpenBehavior.DragOver, 500, 500, true)]
    [InlineData(NotchOpenBehavior.DragOver, 0, 0, true)]
    public void Drag_opens_immediately_except_in_drag_over_mode_with_a_delay(NotchOpenBehavior behavior, double elapsed, int delay, bool expected) =>
        Assert.Equal(expected, NotchOpenPolicy.DragShouldOpen(behavior, elapsed, delay));
}
