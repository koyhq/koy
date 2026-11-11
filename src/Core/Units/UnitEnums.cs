namespace Koy.Core.Units;

// Bu dosyadaki enum adları settings.json'a yazılır: yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Sıcaklık birimi.</summary>
public enum TemperatureUnit
{
    Celsius,
    Fahrenheit
}

/// <summary>Rüzgâr hızı birimi.</summary>
public enum WindSpeedUnit
{
    KilometersPerHour,
    MetersPerSecond,
    MilesPerHour,
    Knots
}

/// <summary>Ağ hızı birimi: bayt (B/sn) ya da bit (b/sn).</summary>
public enum NetworkSpeedUnit
{
    Bytes,
    Bits
}
