using System.Globalization;

namespace Koy.Core.Units;

/// <summary>
/// Rüzgâr hızı metni. Open-Meteo <c>wind_speed_10m</c> değerini varsayılan olarak km/sa verir; girdi km/sa'tir.
/// Birim sembolleri uluslararası olduğundan çeviri gerekmez.
/// </summary>
public static class WindSpeedFormatter
{
    public static string Symbol(WindSpeedUnit unit) => unit switch
    {
        WindSpeedUnit.MetersPerSecond => "m/s",
        WindSpeedUnit.MilesPerHour => "mph",
        WindSpeedUnit.Knots => "kn",
        _ => "km/h",
    };

    /// <summary>"12 km/h"; m/s tek ondalık gösterir ("3.3 m/s"). Geçersiz (NaN/sonsuz/negatif) değerde null.</summary>
    public static string? Format(double kilometersPerHour, WindSpeedUnit unit, CultureInfo culture)
    {
        if (!double.IsFinite(kilometersPerHour) || kilometersPerHour < 0)
            return null;

        var value = UnitConversion.WindSpeed(kilometersPerHour, unit);
        var number = unit == WindSpeedUnit.MetersPerSecond
            ? value.ToString("0.0", culture)
            : Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", culture);
        return number + " " + Symbol(unit);
    }
}
