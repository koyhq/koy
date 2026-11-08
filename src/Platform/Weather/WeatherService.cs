using System.Globalization;
using System.Net.Http;
using Koy.Core.Weather;
using Koy.Platform.Net;

namespace Koy.Platform.Weather;

public sealed class WeatherService : IWeatherService
{
    private readonly TimeSpan _cacheDuration;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    private WeatherInfo? _cachedWeather;
    private double? _cachedLatitude;
    private double? _cachedLongitude;
    private DateTimeOffset _lastFetchTime = DateTimeOffset.MinValue;

    public WeatherInfo? CurrentWeather => _cachedWeather;

    public event EventHandler<WeatherInfo?>? WeatherChanged;

    /// <param name="cacheDuration">Önbellek süresi (ayar: hava durumunu yenileme sıklığı); verilmezse 30 dakika.</param>
    public WeatherService(HttpClient? httpClient = null, TimeProvider? timeProvider = null, TimeSpan? cacheDuration = null)
    {
        _cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(30);
        _httpClient = httpClient ?? AppHttp.Client;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<WeatherInfo?> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var isSameLocation = _cachedLatitude.HasValue &&
                             _cachedLongitude.HasValue &&
                             Math.Abs(_cachedLatitude.Value - latitude) < 0.001 &&
                             Math.Abs(_cachedLongitude.Value - longitude) < 0.001;

        if (_cachedWeather is not null && isSameLocation && now - _lastFetchTime < _cacheDuration)
        {
            return _cachedWeather;
        }

        // Konum değiştiyse eski konumun havası yeni konum gibi gösterilmesin.
        if (!isSameLocation && _cachedWeather is not null)
        {
            _cachedWeather = null;
            WeatherChanged?.Invoke(this, null);
        }

        try
        {
            var latStr = latitude.ToString("F2", CultureInfo.InvariantCulture); // ~1 km: tahmin ızgarası zaten km ölçeğinde, tam konum gitmez
            var lonStr = longitude.ToString("F2", CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current=temperature_2m,weather_code";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return _cachedWeather;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (OpenMeteoParser.ParseCurrent(json) is { } current)
            {
                _cachedWeather = new WeatherInfo(
                    current.Temperature,
                    current.WeatherCode,
                    WeatherCodeMapper.ToDescriptionKey(current.WeatherCode),
                    WeatherCodeMapper.ToIconKind(current.WeatherCode),
                    now);
                _cachedLatitude = latitude;
                _cachedLongitude = longitude;
                _lastFetchTime = now;

                WeatherChanged?.Invoke(this, _cachedWeather);
                return _cachedWeather;
            }
        }
        catch
        {
            // Ağ hatası veya zaman aşımı durumunda önbellekteki değer döner
        }

        return _cachedWeather;
    }

    public async Task RefreshAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        // Önbellek süresini sıfırlayarak taze veri al
        _lastFetchTime = DateTimeOffset.MinValue;
        await GetCurrentWeatherAsync(latitude, longitude, cancellationToken);
    }

    public void Dispose()
    {
    }
}
