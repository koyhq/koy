using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

public class IdleDisplayTests
{
    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, true)]
    [InlineData(IdleDisplayMode.ClockOnly, false)]
    [InlineData(IdleDisplayMode.Nothing, false)]
    [InlineData(IdleDisplayMode.RingClockDay, true)]
    public void ShowRadio_returns_expected_for_each_mode(IdleDisplayMode mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowRadio(mode));
    }

    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, true)]
    [InlineData(IdleDisplayMode.ClockOnly, true)]
    [InlineData(IdleDisplayMode.Nothing, false)]
    [InlineData(IdleDisplayMode.RingClockDay, true)]
    public void ShowClock_returns_expected_for_each_mode(IdleDisplayMode mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowClock(mode));
    }

    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, true)]
    [InlineData(IdleDisplayMode.ClockOnly, false)]
    [InlineData(IdleDisplayMode.Nothing, false)]
    [InlineData(IdleDisplayMode.RingClockDay, true)]
    public void ShowBattery_returns_expected_for_each_mode(IdleDisplayMode mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowBattery(mode));
    }

    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, true)]
    [InlineData(IdleDisplayMode.ClockOnly, false)]
    [InlineData(IdleDisplayMode.Nothing, false)]
    [InlineData(IdleDisplayMode.RingClockDay, true)]
    public void ShowPeekDetails_returns_expected_for_each_mode(IdleDisplayMode mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowPeekDetails(mode));
    }

    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, true)]
    [InlineData(IdleDisplayMode.ClockOnly, false)]
    [InlineData(IdleDisplayMode.Nothing, false)]
    [InlineData(IdleDisplayMode.RingClockDay, false)] // halka modunda simgeler yalnız Peek'te
    public void ShowRadioWhenCollapsed_hides_icons_in_ring_mode(IdleDisplayMode mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowRadioWhenCollapsed(mode));
    }

    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, false, false)]
    [InlineData(IdleDisplayMode.ClockAndBattery, true, true)]
    [InlineData(IdleDisplayMode.ClockOnly, true, false)]
    [InlineData(IdleDisplayMode.Nothing, true, false)]
    [InlineData(IdleDisplayMode.RingClockDay, true, false)] // halka modunda tarih yalnız Peek'te
    public void ShowCollapsedDate_respects_mode_and_setting(IdleDisplayMode mode, bool clockShowDate, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowCollapsedDate(mode, clockShowDate));
    }

    [Theory]
    [InlineData(IdleDisplayMode.ClockAndBattery, false)]
    [InlineData(IdleDisplayMode.ClockOnly, false)]
    [InlineData(IdleDisplayMode.Nothing, false)]
    [InlineData(IdleDisplayMode.RingClockDay, true)]
    public void IsRing_only_for_ring_mode(IdleDisplayMode mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.IsRing(mode));
    }
}
