using System.Globalization;
using Koy.Core.Units;
using Koy.Core.Weather;

namespace Koy.Core.Tests.Units;

public class WindSpeedFormatterTests
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(WindSpeedUnit.KilometersPerHour, "km/h")]
    [InlineData(WindSpeedUnit.MetersPerSecond, "m/s")]
    [InlineData(WindSpeedUnit.MilesPerHour, "mph")]
    [InlineData(WindSpeedUnit.Knots, "kn")]
    public void Every_unit_has_a_symbol(WindSpeedUnit unit, string symbol)
    {
        Assert.Equal(symbol, WindSpeedFormatter.Symbol(unit));
    }

    [Theory]
    [InlineData(18, WindSpeedUnit.KilometersPerHour, "18 km/h")]
    [InlineData(18.4, WindSpeedUnit.KilometersPerHour, "18 km/h")]
    [InlineData(18.5, WindSpeedUnit.KilometersPerHour, "19 km/h")]
    [InlineData(36, WindSpeedUnit.MetersPerSecond, "10.0 m/s")]
    [InlineData(12, WindSpeedUnit.MetersPerSecond, "3.3 m/s")]
    [InlineData(160.9344, WindSpeedUnit.MilesPerHour, "100 mph")]
    [InlineData(0, WindSpeedUnit.Knots, "0 kn")]
    public void Converts_from_kilometers_per_hour_and_formats(double kmh, WindSpeedUnit unit, string expected)
    {
        Assert.Equal(expected, WindSpeedFormatter.Format(kmh, unit, En));
    }

    [Fact]
    public void Uses_the_culture_decimal_separator()
    {
        Assert.Equal("3,3 m/s", WindSpeedFormatter.Format(12, WindSpeedUnit.MetersPerSecond, new CultureInfo("tr-TR")));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void Invalid_values_give_null(double kmh)
    {
        Assert.Null(WindSpeedFormatter.Format(kmh, WindSpeedUnit.KilometersPerHour, En));
    }

    [Fact]
    public void Default_unit_keeps_provider_value()
    {
        Assert.Equal(WindSpeedUnit.KilometersPerHour, new Koy.Core.Settings.GeneralSettings().WindSpeedUnit);
    }
}

public class OpenMeteoWindParserTests
{
    [Fact]
    public void Wind_speed_is_read_from_current()
    {
        var json = """{"current":{"temperature_2m":10,"weather_code":1,"wind_speed_10m":14.4}}""";

        Assert.Equal(14.4, OpenMeteoParser.ParseWindSpeed(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bozuk")]
    [InlineData("{}")]
    [InlineData("""{"current":{"temperature_2m":10}}""")]
    [InlineData("""{"current":{"wind_speed_10m":"fast"}}""")]
    [InlineData("""{"current":{"wind_speed_10m":-3}}""")]
    public void Missing_or_bad_wind_gives_null(string? json)
    {
        Assert.Null(OpenMeteoParser.ParseWindSpeed(json));
    }
}
