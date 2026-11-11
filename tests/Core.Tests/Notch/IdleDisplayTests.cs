using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

public class IdleDisplayTests
{
    [Theory]
    [InlineData(IdleDisplay.ClockAndBattery, true)]
    [InlineData(IdleDisplay.ClockOnly, false)]
    [InlineData(IdleDisplay.Nothing, false)]
    [InlineData(IdleDisplay.RingClockDay, true)]
    [InlineData(99, true)] // Bilinmeyen değer varsayılana (saat ve pil) düşer
    [InlineData(-1, true)]
    public void ShowRadio_returns_expected_for_each_mode(int mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowRadio(mode));
    }

    [Theory]
    [InlineData(IdleDisplay.ClockAndBattery, true)]
    [InlineData(IdleDisplay.ClockOnly, true)]
    [InlineData(IdleDisplay.Nothing, false)]
    [InlineData(IdleDisplay.RingClockDay, true)]
    [InlineData(99, true)]
    [InlineData(-1, true)]
    public void ShowClock_returns_expected_for_each_mode(int mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowClock(mode));
    }

    [Theory]
    [InlineData(IdleDisplay.ClockAndBattery, true)]
    [InlineData(IdleDisplay.ClockOnly, false)]
    [InlineData(IdleDisplay.Nothing, false)]
    [InlineData(IdleDisplay.RingClockDay, true)]
    [InlineData(99, true)]
    [InlineData(-1, true)]
    public void ShowBattery_returns_expected_for_each_mode(int mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowBattery(mode));
    }

    [Theory]
    [InlineData(IdleDisplay.ClockAndBattery, true)]
    [InlineData(IdleDisplay.ClockOnly, false)]
    [InlineData(IdleDisplay.Nothing, false)]
    [InlineData(IdleDisplay.RingClockDay, true)]
    [InlineData(99, true)]
    [InlineData(-1, true)]
    public void ShowPeekDetails_returns_expected_for_each_mode(int mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowPeekDetails(mode));
    }

    [Theory]
    [InlineData(IdleDisplay.ClockAndBattery, true)]
    [InlineData(IdleDisplay.ClockOnly, false)]
    [InlineData(IdleDisplay.Nothing, false)]
    [InlineData(IdleDisplay.RingClockDay, false)] // halka modunda simgeler yalnız Peek'te
    [InlineData(99, true)]
    public void ShowRadioWhenCollapsed_hides_icons_in_ring_mode(int mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowRadioWhenCollapsed(mode));
    }

    [Theory]
    [InlineData(IdleDisplay.ClockAndBattery, false)]
    [InlineData(IdleDisplay.ClockOnly, false)]
    [InlineData(IdleDisplay.Nothing, false)]
    [InlineData(IdleDisplay.RingClockDay, true)]
    [InlineData(99, false)]
    public void IsRing_only_for_ring_mode(int mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.IsRing(mode));
    }
}
