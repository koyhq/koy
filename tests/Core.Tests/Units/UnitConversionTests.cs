using Koy.Core.Units;

namespace Koy.Core.Tests.Units;

public class UnitConversionTests
{
    [Theory]
    [InlineData(0, 32)]
    [InlineData(100, 212)]
    [InlineData(-40, -40)]
    public void Celsius_converts_to_fahrenheit(double celsius, double expected)
    {
        Assert.Equal(expected, UnitConversion.Temperature(celsius, TemperatureUnit.Fahrenheit), 6);
        Assert.Equal(celsius, UnitConversion.Temperature(celsius, TemperatureUnit.Celsius), 6);
    }

    [Theory]
    [InlineData(WindSpeedUnit.KilometersPerHour, 36.0, 36.0)]
    [InlineData(WindSpeedUnit.MetersPerSecond, 36.0, 10.0)]
    [InlineData(WindSpeedUnit.MilesPerHour, 160.9344, 100.0)]
    [InlineData(WindSpeedUnit.Knots, 1.852, 1.0)]
    public void Wind_speed_converts_from_kilometers_per_hour(WindSpeedUnit unit, double kmh, double expected)
        => Assert.Equal(expected, UnitConversion.WindSpeed(kmh, unit), 6);

    [Fact]
    public void Network_speed_in_bits_is_eight_times_bytes()
    {
        Assert.Equal(8.0, UnitConversion.NetworkSpeed(1, NetworkSpeedUnit.Bits), 6);
        Assert.Equal(1_000_000.0 * 8, UnitConversion.NetworkSpeed(1_000_000, NetworkSpeedUnit.Bits), 6);
    }

    [Fact]
    public void Network_speed_in_bytes_is_unchanged()
        => Assert.Equal(512.0, UnitConversion.NetworkSpeed(512, NetworkSpeedUnit.Bytes), 6);
}
