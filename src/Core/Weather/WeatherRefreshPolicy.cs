namespace Koy.Core.Weather;

/// <summary>
/// "Hava durumunu yenileme sıklığı" ayarını (<see cref="WeatherRefreshInterval"/>) önbellek süresine çevirir.
/// Önbellek süresi dolmadan panel açılsa da yeni istek atılmaz; "startup" oturum boyunca yalnız ilk açılışta çeker
/// (elle yenile düğmesi yine çalışır).
/// </summary>
public static class WeatherRefreshPolicy
{
    public static TimeSpan CacheDuration(WeatherRefreshInterval setting) => setting switch
    {
        WeatherRefreshInterval.Hourly => TimeSpan.FromHours(1),
        WeatherRefreshInterval.OnStartup => TimeSpan.MaxValue,
        _ => TimeSpan.FromMinutes(30)
    };
}
