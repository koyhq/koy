using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

public class DockPresentationMathTests
{
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(300, 300)]
    [InlineData(2000, 2000)]
    [InlineData(9999, 2000)]
    public void Delay_is_clamped_to_zero_and_two_seconds(int input, int expected) =>
        Assert.Equal(expected, DockPresentationMath.ClampDelay(input));

    [Fact]
    public void Indicator_is_none_without_windows()
    {
        Assert.Equal(0, DockPresentationMath.IndicatorCount(0, DockIndicatorStyle.Dot, onePerWindow: true));
        Assert.Equal(0, DockPresentationMath.IndicatorCount(-1, DockIndicatorStyle.Line, onePerWindow: false));
    }

    [Fact]
    public void One_dot_per_window_is_capped_at_three()
    {
        Assert.Equal(1, DockPresentationMath.IndicatorCount(1, DockIndicatorStyle.Dot, onePerWindow: true));
        Assert.Equal(2, DockPresentationMath.IndicatorCount(2, DockIndicatorStyle.Dot, onePerWindow: true));
        Assert.Equal(3, DockPresentationMath.IndicatorCount(3, DockIndicatorStyle.Dot, onePerWindow: true));
        Assert.Equal(3, DockPresentationMath.IndicatorCount(7, DockIndicatorStyle.Dot, onePerWindow: true));
    }

    [Theory]
    [InlineData(DockIndicatorStyle.Dot)]
    [InlineData(DockIndicatorStyle.Line)]
    [InlineData(DockIndicatorStyle.Highlight)]
    public void Without_one_per_window_a_single_indicator_is_drawn(DockIndicatorStyle style) =>
        Assert.Equal(1, DockPresentationMath.IndicatorCount(5, style, onePerWindow: false));

    [Theory]
    [InlineData(DockIndicatorStyle.Line)]
    [InlineData(DockIndicatorStyle.Highlight)]
    public void Per_window_mode_only_applies_to_dots(DockIndicatorStyle style) =>
        Assert.Equal(1, DockPresentationMath.IndicatorCount(4, style, onePerWindow: true));

    [Theory]
    [InlineData(0, "")]
    [InlineData(-2, "")]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(10, "9+")]
    [InlineData(250, "9+")]
    public void Number_badge_text_caps_at_nine_plus(int count, string expected) =>
        Assert.Equal(expected, DockPresentationMath.BadgeText(count, DockBadgeStyle.Number));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(40, true)]
    public void Dot_only_badge_has_no_text_but_shows_when_there_is_a_count(int count, bool shows)
    {
        Assert.Equal("", DockPresentationMath.BadgeText(count, DockBadgeStyle.DotOnly));
        Assert.Equal(shows, DockPresentationMath.ShowsBadge(count, DockBadgeStyle.DotOnly));
    }

    [Fact]
    public void Number_badge_is_hidden_when_count_is_zero()
    {
        Assert.False(DockPresentationMath.ShowsBadge(0, DockBadgeStyle.Number));
        Assert.True(DockPresentationMath.ShowsBadge(3, DockBadgeStyle.Number));
    }

    [Fact]
    public void Edge_touch_reveals_without_waiting()
    {
        Assert.True(DockPresentationMath.ShouldReveal(DockRevealTrigger.EdgeTouch, atEdge: true, buttonPressed: false,
            shortcutActive: false, edgeHeldFor: TimeSpan.Zero, revealDelayMs: 300));
        Assert.False(DockPresentationMath.ShouldReveal(DockRevealTrigger.EdgeTouch, atEdge: false, buttonPressed: false,
            shortcutActive: false, edgeHeldFor: TimeSpan.Zero, revealDelayMs: 300));
    }

    [Fact]
    public void Edge_hold_waits_for_the_configured_delay()
    {
        bool Hold(TimeSpan held) => DockPresentationMath.ShouldReveal(DockRevealTrigger.EdgeHold, atEdge: true,
            buttonPressed: false, shortcutActive: false, edgeHeldFor: held, revealDelayMs: 300);

        Assert.False(Hold(TimeSpan.FromMilliseconds(299)));
        Assert.True(Hold(TimeSpan.FromMilliseconds(300)));
        Assert.True(Hold(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Edge_hold_delay_is_clamped_before_comparison()
    {
        // Dosyada negatif gecikme: sıfıra kıstırılır, hemen çıkar.
        Assert.True(DockPresentationMath.ShouldReveal(DockRevealTrigger.EdgeHold, atEdge: true, buttonPressed: false,
            shortcutActive: false, edgeHeldFor: TimeSpan.Zero, revealDelayMs: -100));
    }

    [Fact]
    public void Shortcut_only_ignores_the_edge()
    {
        Assert.False(DockPresentationMath.ShouldReveal(DockRevealTrigger.ShortcutOnly, atEdge: true, buttonPressed: false,
            shortcutActive: false, edgeHeldFor: TimeSpan.FromSeconds(5), revealDelayMs: 0));
        Assert.True(DockPresentationMath.ShouldReveal(DockRevealTrigger.ShortcutOnly, atEdge: false, buttonPressed: false,
            shortcutActive: true, edgeHeldFor: TimeSpan.Zero, revealDelayMs: 0));
    }

    [Fact]
    public void Undefined_trigger_behaves_like_edge_hold_so_the_dock_stays_reachable()
    {
        var undefined = (DockRevealTrigger)42;

        Assert.False(DockPresentationMath.ShouldReveal(undefined, atEdge: true, buttonPressed: false,
            shortcutActive: false, edgeHeldFor: TimeSpan.FromMilliseconds(100), revealDelayMs: 300));
        Assert.True(DockPresentationMath.ShouldReveal(undefined, atEdge: true, buttonPressed: false,
            shortcutActive: false, edgeHeldFor: TimeSpan.FromMilliseconds(300), revealDelayMs: 300));
    }

    [Theory]
    [InlineData(DockRevealTrigger.EdgeTouch)]
    [InlineData(DockRevealTrigger.EdgeHold)]
    [InlineData(DockRevealTrigger.ShortcutOnly)]
    public void No_trigger_reveals_while_a_mouse_button_is_held(DockRevealTrigger trigger) =>
        Assert.False(DockPresentationMath.ShouldReveal(trigger, atEdge: true, buttonPressed: true,
            shortcutActive: true, edgeHeldFor: TimeSpan.FromSeconds(5), revealDelayMs: 0));

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, true, false)]
    public void Hidden_edge_line_needs_setting_hidden_dock_and_no_fullscreen(bool line, bool hidden, bool fullscreen, bool expected) =>
        Assert.Equal(expected, DockPresentationMath.ShowsHiddenEdgeLine(line, hidden, fullscreen));

    [Theory]
    [InlineData("#1E90FF", true)]
    [InlineData("#abcdef", true)]
    [InlineData("#ABC", false)]
    [InlineData("1E90FF", false)]
    [InlineData("#GG0000", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Hex_color_must_be_six_digits_with_hash(string? text, bool valid) =>
        Assert.Equal(valid, DockPresentationMath.IsValidHexColor(text));

    [Fact]
    public void Custom_color_with_invalid_text_falls_back_to_accent()
    {
        Assert.Equal(DockColorSource.Accent, DockPresentationMath.EffectiveColorSource(DockColorSource.Custom, "not-a-color"));
        Assert.Equal(DockColorSource.Custom, DockPresentationMath.EffectiveColorSource(DockColorSource.Custom, "#FF8800"));
        Assert.Equal(DockColorSource.Neutral, DockPresentationMath.EffectiveColorSource(DockColorSource.Neutral, ""));
    }

    [Fact]
    public void New_dock_settings_have_defaults_that_keep_current_behavior()
    {
        var dock = new DockSettings();

        Assert.Equal(DockColorSource.Accent, dock.IndicatorColor);
        Assert.Equal(DockColorSource.Accent, dock.BadgeColor);
        Assert.Equal(DockBadgeStyle.Number, dock.BadgeStyle);
        Assert.Equal(DockBadgeCorner.TopRight, dock.BadgeCorner);
        // Bugünkü dock kenarda ~300 ms bekleyince açılır (EdgeRevealTracker); varsayılan bunu değiştirmemeli.
        Assert.Equal(DockRevealTrigger.EdgeHold, dock.RevealTrigger);
        Assert.Equal((int)EdgeRevealTracker.DefaultDelay.TotalMilliseconds, dock.RevealDelayMs);
        Assert.False(dock.HiddenEdgeLine);
        Assert.False(dock.IndicatorOnePerWindow);
    }
}
