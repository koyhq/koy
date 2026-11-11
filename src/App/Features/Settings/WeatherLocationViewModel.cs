using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Koy.Core.Weather;
using Koy.Platform.Weather;

namespace Koy.App.Features.Settings;

/// <summary>
/// Ayarlar › Çentik › Hava durumu: "konumu kullan" anahtarı ve yedek olarak şehir arama. Kararlar
/// <see cref="WeatherLocationPolicy"/>'de (Core, testli); bu sınıf yalnız arayüzü, servis çağrılarını ve kaydı bağlar.
/// Ağ ve konum servisleri yalnız kullanıcı bir şey yapınca çağrılır (sayfa açılınca istek atılmaz).
/// </summary>
public sealed partial class WeatherLocationViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly ILocationService _location;
    private readonly IGeocodingService _geocoding;
    private bool _loading;

    [ObservableProperty]
    private bool _useLocation;

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "";

    /// <summary>Konum izni reddedildi: Windows konum ayarlarına giden bağlantı gösterilir.</summary>
    [ObservableProperty]
    private bool _showLocationSettingsLink;

    [ObservableProperty]
    private bool _hasSearched;

    [ObservableProperty]
    private bool _hasLocation;

    public ObservableCollection<GeoPlace> Results { get; } = [];

    public bool NoResults => HasSearched && Results.Count == 0 && !IsBusy;

    public WeatherLocationViewModel(SettingsStore store, ILocationService? location = null, IGeocodingService? geocoding = null)
    {
        _store = store;
        _location = location ?? new LocationService();
        _geocoding = geocoding ?? new GeocodingService();
    }

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            UseLocation = settings.Weather.UseLocation;
            HasLocation = WeatherLocationPolicy.HasLocation(settings);
            StatusText = DescribeStatus(settings);
            ShowLocationSettingsLink = false;
        }
        finally
        {
            _loading = false;
        }
    }

    private static string DescribeStatus(AppSettings s)
    {
        if (!WeatherLocationPolicy.HasLocation(s))
            return Strings.Get("WeatherStatusNone");

        return s.Weather.LocationSource == WeatherLocationSource.Device
            ? Strings.Get("WeatherStatusDevice")
            : string.Format(Strings.Get("WeatherStatusCity"), s.Weather.City);
    }

    partial void OnUseLocationChanged(bool value)
    {
        if (_loading)
            return;

        var updated = WeatherLocationPolicy.SetUseLocation(_store.Load(), value);
        _store.Save(updated);
        HasLocation = WeatherLocationPolicy.HasLocation(updated);
        ShowLocationSettingsLink = false;
        StatusText = DescribeStatus(updated);
        ApplyToRunningNotch();

        if (value)
            _ = FetchDeviceLocationAsync();
    }

    // İzin sorusu (Windows tarafında çıkarsa) ve tek seferlik düşük doğruluklu sorgu; kullanıcı anahtarı açınca.
    private async Task FetchDeviceLocationAsync()
    {
        IsBusy = true;
        StatusText = Strings.Get("WeatherStatusWorking");
        try
        {
            var result = await _location.GetLocationAsync();

            // Bu arada anahtar kapatıldıysa sonuç yazılmaz.
            var current = _store.Load();
            if (!current.Weather.UseLocation)
                return;

            var updated = WeatherLocationPolicy.ApplyDeviceLocation(current, result, DateTimeOffset.UtcNow);
            if (result.IsSuccess)
                _store.Save(updated);

            HasLocation = WeatherLocationPolicy.HasLocation(updated);
            ShowLocationSettingsLink = result.Status == LocationStatus.Denied;
            StatusText = result.Status switch
            {
                LocationStatus.Success => DescribeStatus(updated),
                LocationStatus.Denied => Strings.Get("WeatherStatusDenied"),
                _ => Strings.Get("WeatherStatusFailed")
            };
            ApplyToRunningNotch();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Ayarlar: cihaz konumu", ex);
            StatusText = Strings.Get("WeatherStatusFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        HasSearched = false;
        Results.Clear();
        try
        {
            var language = _store.Load().General.Language;
            foreach (var place in await _geocoding.SearchAsync(Query, language))
                Results.Add(place);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Ayarlar: şehir arama", ex);
        }
        finally
        {
            HasSearched = true;
            IsBusy = false;
            OnPropertyChanged(nameof(NoResults));
        }
    }

    [RelayCommand]
    private void SelectPlace(GeoPlace place)
    {
        var updated = WeatherLocationPolicy.ApplyCity(_store.Load(), place, DateTimeOffset.UtcNow);
        _store.Save(updated);

        _loading = true;
        UseLocation = false;
        _loading = false;

        HasLocation = true;
        ShowLocationSettingsLink = false;
        StatusText = DescribeStatus(updated);
        Results.Clear();
        HasSearched = false;
        Query = "";
        OnPropertyChanged(nameof(NoResults));
        ApplyToRunningNotch();
    }

    [RelayCommand]
    private void ClearLocation()
    {
        var updated = WeatherLocationPolicy.Cleared(WeatherLocationPolicy.SetUseLocation(_store.Load(), false));
        _store.Save(updated);

        _loading = true;
        UseLocation = false;
        _loading = false;

        HasLocation = false;
        ShowLocationSettingsLink = false;
        StatusText = DescribeStatus(updated);
        ApplyToRunningNotch();
    }

    [RelayCommand]
    private void OpenLocationSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "ms-settings:privacy-location", UseShellExecute = true });
        }
        catch
        {
            // Ayarlar sayfası açılamadıysa yapılacak başka bir şey yok.
        }
    }

    // Çentik koordinatı ayarı kurulurken okur; değişince yeni ayarla yeniden kurulur.
    private static void ApplyToRunningNotch() => (Application.Current as App)?.ReloadNotch();

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(NoResults));

    partial void OnHasSearchedChanged(bool value) => OnPropertyChanged(nameof(NoResults));
}
