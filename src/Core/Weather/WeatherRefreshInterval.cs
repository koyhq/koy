namespace Koy.Core.Weather;

// Enum adları settings.json'a yazılır: bir kez yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Hava durumunu yenileme sıklığı. Eski dosyalarda "30m", "1h", "startup" metniydi; göçte ada çevrilir.</summary>
public enum WeatherRefreshInterval
{
    Every30Minutes,
    Hourly,
    OnStartup
}
