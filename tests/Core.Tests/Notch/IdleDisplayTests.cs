using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

public class IdleDisplayTests
{
    [Theory]
    [InlineData(IdleDisplay.ClockAndBattery, true)]
    [InlineData(IdleDisplay.ClockOnly, false)]
    [InlineData(IdleDisplay.Nothing, false)]
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
    [InlineData(99, true)]
    [InlineData(-1, true)]
    public void ShowPeekDetails_returns_expected_for_each_mode(int mode, bool expected)
    {
        Assert.Equal(expected, IdleDisplay.ShowPeekDetails(mode));
    }
}
