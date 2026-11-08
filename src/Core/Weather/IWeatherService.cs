namespace Koy.Core.Weather;

public sealed record WeatherInfo(
    double Temperature,
    int WeatherCode,
    string DescriptionKey,
    string IconKind,
    DateTimeOffset FetchedAt);

/// <summary>
/// Open-Meteo hava durumu sözleşmesi.
/// 30 dk önbellekleme ve tek HttpClient kullanımı şarttır.
/// </summary>
public interface IWeatherService : IDisposable
{
    WeatherInfo? CurrentWeather { get; }
    event EventHandler<WeatherInfo?>? WeatherChanged;

    Task<WeatherInfo?> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    Task RefreshAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
