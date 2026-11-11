using Koy.Core.Notch;
using Koy.Core.Settings;
using Koy.Core.Visibility;

namespace Koy.Core.Tests;

public class NotchBehaviorSettingsTests
{
    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, true, true)]
    [InlineData(IdleDisplayMode.ClockOnly, true, false)]
    [InlineData(IdleDisplayMode.Nothing, false, false)]
    [InlineData(IdleDisplayMode.RingClockDay, true, true)] // halka pil ve gün/saat
    public void IdleDisplay_maps_mode_to_clock_and_battery(IdleDisplayMode mode, bool clock, bool battery)
    {
        Assert.Equal(clock, IdleDisplay.ShowClock(mode));
        Assert.Equal(battery, IdleDisplay.ShowBattery(mode));
    }

    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, true)]
    [InlineData(IdleDisplayMode.ClockOnly, false)]
    [InlineData(IdleDisplayMode.Nothing, false)]
    [InlineData(IdleDisplayMode.RingClockDay, true)] // halka modunda Peek'te
    public void IdleDisplay_shows_radio_icons_only_in_clock_and_battery_mode(IdleDisplayMode mode, bool radio)
    {
        Assert.Equal(radio, IdleDisplay.ShowRadio(mode));
    }

    [Fact]
    public void Hover_open_behavior_expands_immediately_on_mouse_enter()
    {
        using var controller = new NotchController { OpenOnHover = true };

        controller.OnMouseEnter();

        Assert.Equal(NotchState.Expanded, controller.State);
    }

    [Fact]
    public void Hover_open_behavior_collapses_after_mouse_leave_timeout()
    {
        using var controller = new NotchController { OpenOnHover = true };
        controller.OnMouseEnter();

        controller.OnMouseLeave();
        controller.OnMouseLeaveTimeout();

        Assert.Equal(NotchState.Collapsed, controller.State);
    }

    [Fact]
    public void Click_open_behavior_only_peeks_on_hover()
    {
        using var controller = new NotchController { OpenOnHover = false };

        controller.OnMouseEnter();
        Assert.Equal(NotchState.Peek, controller.State);

        controller.OnClick();
        Assert.Equal(NotchState.Expanded, controller.State);
    }

    [Fact]
    public void Default_open_behavior_setting_is_hover()
    {
        Assert.Equal(NotchOpenBehavior.Hover, new AppSettings().NotchOpenBehavior);
    }

    [Fact]
    public void Saved_click_choice_is_kept()
    {
        var path = Path.Combine(Path.GetTempPath(), "koy-open-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new AppSettings { NotchOpenBehavior = NotchOpenBehavior.Click });
            Assert.Equal(NotchOpenBehavior.Click, store.Load().NotchOpenBehavior);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Fullscreen_hides_by_default()
    {
        using var visibility = new VisibilityController(VisibilityMode.Fixed);

        visibility.SetFullscreen(true);

        Assert.False(visibility.IsVisible);
    }

    [Fact]
    public void Fullscreen_keeps_notch_visible_when_hiding_is_turned_off()
    {
        using var visibility = new VisibilityController(VisibilityMode.Fixed) { HideInFullscreen = false };

        visibility.SetFullscreen(true);

        Assert.True(visibility.IsVisible);
    }

    [Fact]
    public void Turning_hiding_off_while_fullscreen_shows_the_notch_again()
    {
        using var visibility = new VisibilityController(VisibilityMode.Fixed);
        visibility.SetFullscreen(true);
        Assert.False(visibility.IsVisible);

        visibility.HideInFullscreen = false;

        Assert.True(visibility.IsVisible);
    }

    [Fact]
    public void Behavior_settings_round_trip_through_the_store()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-notch-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            var settings = new AppSettings { NotchHideInFullscreen = false, MusicShowWaveform = false, IdleDisplayMode = IdleDisplayMode.Nothing };

            store.Save(settings);
            var loaded = store.Load();

            Assert.False(loaded.NotchHideInFullscreen);
            Assert.False(loaded.MusicShowWaveform);
            Assert.Equal(IdleDisplayMode.Nothing, loaded.IdleDisplayMode);
            Assert.Equal(settings, loaded);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
