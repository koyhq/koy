using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Settings;
using Koy.Core.Shell;
using Koy.Core.Units;
using Koy.Core.Weather;

namespace Koy.App.Features.Dock;

/// <summary>
/// Tam genişlik dock'un pencere öğeleri düğmesi (Windows 11'deki hava durumu: simge, sıcaklık, kısa açıklama). Veri Koy'un
/// mevcut hava servisinden (önbellekli; yenileme aralığı servisin önbellek süresidir), yeni zamanlayıcı yok: dock görünür
/// olunca ve dakikalık saat tikinde bir istek atılır, servis süre dolmadıysa önbellekten döner. Konum yoksa yalnız simge
/// görünür. Tıklayınca Windows pencere öğeleri (Win+W). Servisin sahibi (DockFeature) <paramref name="release"/> ile geri alır.
/// </summary>
public sealed partial class DockWeatherViewModel : ObservableObject, IDisposable
{
    private readonly IWeatherService _weather;
    private readonly AppSettings _settings;
    private readonly IShellActionService _shell;
    private readonly Dispatcher _dispatcher;
    private readonly Action? _release;
    private bool _active;
    private bool _disposed;

    /// <summary>Hava konumu seçilmiş (ya da cihaz konumu çözülmüş): yoksa yalnız simge gösterilir.</summary>
    [ObservableProperty]
    private bool _hasLocation;

    [ObservableProperty]
    private bool _hasWeather;

    /// <summary>"11°C" (birim ayardaki sıcaklık birimidir).</summary>
    [ObservableProperty]
    private string _temperatureText = string.Empty;

    /// <summary>Kısa açıklama ("Çok bulutlu"); veri gelene dek boş.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary><see cref="Koy.App.Features.Status.WeatherGlyph"/> için simge türü.</summary>
    [ObservableProperty]
    private string _weatherIconKind = "Cloud";

    [ObservableProperty]
    private string _tooltip = string.Empty;

    public DockWeatherViewModel(IWeatherService weather, AppSettings settings, IShellActionService shell, Action? release = null, Dispatcher? dispatcher = null)
    {
        _weather = weather;
        _settings = settings;
        _shell = shell;
        _release = release;
        _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        HasLocation = WeatherLocationPolicy.HasLocation(settings);
        _weather.WeatherChanged += OnWeatherChanged;
        Apply(_weather.CurrentWeather);
    }

    /// <summary>Dock görünürken hava istenir (önbellek süresi dolmadıysa ağ isteği atılmaz).</summary>
    public void SetActive(bool active)
    {
        if (_active == active || _disposed)
            return;

        _active = active;
        if (active)
            Request();
    }

    /// <summary>Dakikalık saat tikinde çağrılır: aralık dolduysa servis yeniler, dolmadıysa önbellek döner (yoklama değil, mevcut tik).</summary>
    public void Request()
    {
        if (_disposed || !_active || !HasLocation)
            return;

        _ = _weather.GetCurrentWeatherAsync(_settings.Weather.Latitude, _settings.Weather.Longitude);
    }

    [RelayCommand]
    private void OpenWidgets() => _shell.OpenWidgets();

    private void OnWeatherChanged(object? sender, WeatherInfo? weather) =>
        _dispatcher.InvokeAsync(() => Apply(weather));

    private void Apply(WeatherInfo? weather)
    {
        if (weather is null)
        {
            HasWeather = false;
            TemperatureText = string.Empty;
            Description = string.Empty;
            WeatherIconKind = "Cloud";
            Tooltip = string.Empty;
            return;
        }

        HasWeather = true;
        var suffix = Strings.Get(_settings.General.TemperatureUnit == TemperatureUnit.Fahrenheit ? "UnitFahrenheitSuffix" : "UnitCelsiusSuffix");
        var degrees = Math.Round(UnitConversion.Temperature(weather.Temperature, _settings.General.TemperatureUnit));
        TemperatureText = $"{degrees}{suffix}";
        Description = Strings.Get(weather.DescriptionKey);
        WeatherIconKind = weather.IconKind;
        Tooltip = TemperatureText + ", " + Description;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _weather.WeatherChanged -= OnWeatherChanged;
        _release?.Invoke();
    }
}
