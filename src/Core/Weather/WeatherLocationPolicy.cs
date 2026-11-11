using Koy.Core.Settings;

namespace Koy.Core.Weather;

/// <summary>
/// Hava durumunun hangi koordinatla gösterileceğine ve cihaz konumunun ne zaman yenileneceğine karar verir.
/// Hepsi saf: ayarlar ve zaman girer, yeni ayarlar çıkar; kayıt ve servis çağrıları çağıranın işi.
/// </summary>
public static class WeatherLocationPolicy
{
    /// <summary>Cihaz konumu en sık bu aralıkla yenilenir (yalnız uygulama açılışında bakılır).</summary>
    public static readonly TimeSpan DeviceRefreshInterval = TimeSpan.FromHours(24);

    /// <summary>Hava durumu gösterilebilir mi: kaynak var ve koordinat geçerli (0,0 "boş" sayılır).</summary>
    public static bool HasLocation(AppSettings settings) =>
        settings.Weather.LocationSource != WeatherLocationSource.None &&
        OpenMeteoParser.IsValidCoordinate(settings.Weather.Latitude, settings.Weather.Longitude) &&
        !(settings.Weather.Latitude == 0 && settings.Weather.Longitude == 0);

    /// <summary>
    /// Açılışta cihaz konumu sorulsun mu: kullanıcı izin verdiyse ve koordinat henüz cihazdan alınmamışsa ya da
    /// son alınışının üstünden 24 saat geçtiyse. Saat geriye alınmışsa (gelecekte görünen kayıt) da yenilenir.
    /// </summary>
    public static bool ShouldRefreshDeviceLocation(AppSettings settings, DateTimeOffset now)
    {
        if (!settings.Weather.UseLocation)
            return false;

        if (settings.Weather.LocationSource != WeatherLocationSource.Device || !HasLocation(settings))
            return true;

        var updated = DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, settings.Weather.LocationUpdatedUnix));
        var age = now - updated;
        return age < TimeSpan.Zero || age >= DeviceRefreshInterval;
    }

    /// <summary>Cihaz konumu alındıysa ayarları günceller; hata, ret ya da zaman aşımında olduğu gibi döner.</summary>
    public static AppSettings ApplyDeviceLocation(AppSettings settings, LocationResult result, DateTimeOffset now)
    {
        if (!result.IsSuccess || !OpenMeteoParser.IsValidCoordinate(result.Latitude, result.Longitude))
            return settings;

        return settings with
        {
            Weather = settings.Weather with
            {
                LocationSource = WeatherLocationSource.Device,
                Latitude = result.Latitude,
                Longitude = result.Longitude,
                City = "",
                LocationUpdatedUnix = now.ToUnixTimeSeconds()
            }
        };
    }

    /// <summary>Şehir seçildi: cihaz konumu kullanımı kapanır (açık bir seçim, konumdan önce gelir).</summary>
    public static AppSettings ApplyCity(AppSettings settings, GeoPlace place, DateTimeOffset now) => settings with
    {
        Weather = settings.Weather with
        {
            LocationSource = WeatherLocationSource.City,
            UseLocation = false,
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            City = place.DisplayName,
            LocationUpdatedUnix = now.ToUnixTimeSeconds()
        }
    };

    /// <summary>
    /// "Konumu kullan" anahtarı. Açılınca bir sonraki yenilemede cihaz konumu sorulur (seçili şehir, konum gelene
    /// kadar yedek olarak kalır). Kapanınca cihazdan gelen koordinat silinir; seçili şehir korunur.
    /// </summary>
    public static AppSettings SetUseLocation(AppSettings settings, bool use)
    {
        if (use)
            return settings with { Weather = settings.Weather with { UseLocation = true } };

        var baseSettings = settings.Weather.LocationSource == WeatherLocationSource.Device ? Cleared(settings) : settings;
        return baseSettings with { Weather = baseSettings.Weather with { UseLocation = false } };
    }

    /// <summary>Konum bilgisini siler; hava durumu gösterilmez.</summary>
    public static AppSettings Cleared(AppSettings settings) => settings with
    {
        Weather = settings.Weather with
        {
            LocationSource = WeatherLocationSource.None,
            Latitude = 0,
            Longitude = 0,
            City = "",
            LocationUpdatedUnix = 0
        }
    };
}
