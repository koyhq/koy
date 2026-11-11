namespace Koy.Core.Units;

/// <summary>
/// Birim dönüşümleri. Saf sayısal işlem: sonuç seçilen birimde bir sayıdır, birim adı ve ondalık ayıracı App'te yazılır.
/// Ağ hızında bit çarpanı 8'dir; App ondalık ön eki (k, M, G) kullanır, ikili <c>SystemFormat.Speed</c> bayt içindir.
/// </summary>
public static class UnitConversion
{
    private const double KilometersPerMile = 1.609344;
    private const double KilometersPerKnot = 1.852;

    /// <summary>Sıcaklık: girdi Celsius, çıktı seçilen birimde.</summary>
    public static double Temperature(double celsius, TemperatureUnit unit) =>
        unit == TemperatureUnit.Fahrenheit ? celsius * 9.0 / 5.0 + 32.0 : celsius;

    /// <summary>Rüzgâr hızı: girdi km/sa, çıktı seçilen birimde.</summary>
    public static double WindSpeed(double kilometersPerHour, WindSpeedUnit unit) => unit switch
    {
        WindSpeedUnit.MetersPerSecond => kilometersPerHour / 3.6,
        WindSpeedUnit.MilesPerHour => kilometersPerHour / KilometersPerMile,
        WindSpeedUnit.Knots => kilometersPerHour / KilometersPerKnot,
        _ => kilometersPerHour,
    };

    /// <summary>Ağ hızı: girdi bayt/sn, çıktı seçilen birimde (bitte x8).</summary>
    public static double NetworkSpeed(double bytesPerSecond, NetworkSpeedUnit unit) =>
        unit == NetworkSpeedUnit.Bits ? bytesPerSecond * 8.0 : bytesPerSecond;
}
