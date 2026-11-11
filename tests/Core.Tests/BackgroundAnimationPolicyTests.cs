using Koy.Core.Background;

namespace Koy.Core.Tests;

public class BackgroundAnimationPolicyTests
{
    private static readonly BackgroundPlaybackState Expanded = new(IsNotchIdle: false, BatterySaver: false, Fullscreen: false);

    [Theory]
    [InlineData(BackgroundKind.None)]
    [InlineData(BackgroundKind.StillImage)]
    public void Static_kinds_never_animate(BackgroundKind kind)
    {
        var options = new BackgroundAnimationOptions(AnimateWhenIdle: true, AnimateOnBattery: true);

        Assert.False(BackgroundAnimationPolicy.ShouldAnimate(kind, Expanded, options));
    }

    [Theory]
    [InlineData(BackgroundKind.AnimatedImage)]
    [InlineData(BackgroundKind.Video)]
    public void Animated_kinds_animate_when_expanded_on_ac(BackgroundKind kind)
    {
        Assert.True(BackgroundAnimationPolicy.ShouldAnimate(kind, Expanded));
    }

    [Theory]
    [InlineData(BackgroundKind.AnimatedImage)]
    [InlineData(BackgroundKind.Video)]
    public void Idle_notch_does_not_animate_by_default(BackgroundKind kind)
    {
        var idle = Expanded with { IsNotchIdle = true };

        Assert.False(BackgroundAnimationPolicy.ShouldAnimate(kind, idle));
    }

    [Theory]
    [InlineData(BackgroundKind.AnimatedImage)]
    [InlineData(BackgroundKind.Video)]
    public void Battery_saver_does_not_animate_by_default(BackgroundKind kind)
    {
        var battery = Expanded with { BatterySaver = true };

        Assert.False(BackgroundAnimationPolicy.ShouldAnimate(kind, battery));
    }

    [Fact]
    public void Idle_opt_in_allows_animation_while_idle()
    {
        var idle = Expanded with { IsNotchIdle = true };
        var options = new BackgroundAnimationOptions(AnimateWhenIdle: true);

        Assert.True(BackgroundAnimationPolicy.ShouldAnimate(BackgroundKind.Video, idle, options));
    }

    [Fact]
    public void Battery_opt_in_allows_animation_on_battery()
    {
        var battery = Expanded with { BatterySaver = true };
        var options = new BackgroundAnimationOptions(AnimateOnBattery: true);

        Assert.True(BackgroundAnimationPolicy.ShouldAnimate(BackgroundKind.AnimatedImage, battery, options));
    }

    [Fact]
    public void Idle_opt_in_does_not_override_battery_without_battery_opt_in()
    {
        var idleOnBattery = new BackgroundPlaybackState(IsNotchIdle: true, BatterySaver: true, Fullscreen: false);
        var options = new BackgroundAnimationOptions(AnimateWhenIdle: true);

        Assert.False(BackgroundAnimationPolicy.ShouldAnimate(BackgroundKind.Video, idleOnBattery, options));
    }

    [Fact]
    public void Fullscreen_stops_animation_even_with_all_opt_ins()
    {
        var fullscreen = new BackgroundPlaybackState(IsNotchIdle: false, BatterySaver: false, Fullscreen: true);
        var options = new BackgroundAnimationOptions(AnimateWhenIdle: true, AnimateOnBattery: true);

        Assert.False(BackgroundAnimationPolicy.ShouldAnimate(BackgroundKind.Video, fullscreen, options));
    }

    [Fact]
    public void Default_options_are_both_off()
    {
        var options = new BackgroundAnimationOptions();

        Assert.False(options.AnimateWhenIdle);
        Assert.False(options.AnimateOnBattery);
    }
}
