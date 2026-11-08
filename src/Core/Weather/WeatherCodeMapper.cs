namespace Koy.Core.Weather;

public static class WeatherCodeMapper
{
    /// <summary>WMO hava kodunun çeviri anahtarı; metni arayüz katmanı çözer (Strings.resx).</summary>
    public static string ToDescriptionKey(int code) => code switch
    {
        0 => "WeatherCondClear",
        1 => "WeatherCondMostlyClear",
        2 => "WeatherCondPartlyCloudy",
        3 => "WeatherCondOvercast",
        45 => "WeatherCondFog",
        48 => "WeatherCondRimeFog",
        51 => "WeatherCondLightDrizzle",
        53 => "WeatherCondModerateDrizzle",
        55 => "WeatherCondDenseDrizzle",
        56 or 57 => "WeatherCondFreezingDrizzle",
        61 => "WeatherCondLightRain",
        63 => "WeatherCondRain",
        65 => "WeatherCondHeavyRain",
        66 or 67 => "WeatherCondFreezingRain",
        71 => "WeatherCondLightSnow",
        73 => "WeatherCondSnow",
        75 => "WeatherCondHeavySnow",
        77 => "WeatherCondSnowGrains",
        80 => "WeatherCondLightShowers",
        81 => "WeatherCondShowers",
        82 => "WeatherCondHeavyShowers",
        85 => "WeatherCondLightSnowShowers",
        86 => "WeatherCondHeavySnowShowers",
        95 => "WeatherCondThunderstorm",
        96 or 99 => "WeatherCondHailStorm",
        _ => "WeatherCondUnknown"
    };

    public static string ToIconKind(int code) => code switch
    {
        0 => "Sun",
        1 or 2 => "PartlyCloudy",
        3 => "Cloud",
        45 or 48 => "Fog",
        51 or 53 or 55 or 56 or 57 => "Drizzle",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "Rain",
        71 or 73 or 75 or 77 or 85 or 86 => "Snow",
        95 or 96 or 99 => "Thunderstorm",
        _ => "Cloud"
    };
}
