using Koy.Core.SystemStats;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Network;
using Koy.Core.Power;
using Koy.Core.Settings;
using Koy.Core.Units;
using Koy.Core.Weather;

namespace Koy.App.Features.Status;

/// <summary>
/// Saat, tarih, pil, ağ ve hava durumu. Ana görünümün durum sütunu ve ayrıntılı Durum paneli bunu paylaşır.
/// Saat zamanlayıcısı yalnız panel görünürken çalışır ve dakika başına bir kez tikler.
/// </summary>
public sealed partial class StatusViewModel : ObservableObject, IDisposable
{
    private readonly IPowerService _powerService;
    private readonly IWeatherService _weatherService;
    private readonly INetworkService? _networkService;
    private readonly IPlaceNameService? _placeNameService;
    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;
    private DispatcherTimer? _clockTimer;
    private bool _isActive;
    private bool _disposed;

    [ObservableProperty]
    private string _currentTime = "";

    [ObservableProperty]
    private string _currentDate = "";

    /// <summary>Kısa tarih ("6 Kas Cum"): ana görünümün durum sütunu için.</summary>
    [ObservableProperty]
    private string _shortDate = "";

    /// <summary>"Perşembe, 6 Kasım": açık çentiğin durum satırında ve boşta Ana kartında saatin altında.</summary>
    [ObservableProperty]
    private string _dayDate = "";

    /// <summary>Açık çentik durum satırında tarihin ardındaki ikinci saat (" · UTC+9 21:00"); ikinci saat kapalıysa boş.</summary>
    [ObservableProperty]
    private string _secondClockSuffix = "";

    /// <summary>Günün dilimine göre selamlama ("Günaydın"): müzik çalmıyorken Ana kartında.</summary>
    [ObservableProperty]
    private string _greeting = "";

    /// <summary>Uzun tarih ("6 Kasım 2026 Cuma").</summary>
    [ObservableProperty]
    private string _fullDate = "";

    [ObservableProperty]
    private bool _hasBattery;

    [ObservableProperty]
    private int _batteryPercent = 100;

    [ObservableProperty]
    private bool _isCharging;

    [ObservableProperty]
    private string _batteryText = "";

    /// <summary>Cihaz şu an pille (şarj olmadan) çalışıyor mu. Pil tasarrufu kararlarında kullanılır.</summary>
    public bool IsOnBattery { get; private set; }

    /// <summary>Hava durumu için bir konum var mı (izin verildi ya da şehir seçildi). Yoksa hava durumu hiç gösterilmez.</summary>
    [ObservableProperty]
    private bool _hasWeatherLocation;

    /// <summary>Hava verisi geldi mi (konum var ama ağ yoksa false: sıcaklık yerine "--" gösterilir).</summary>
    [ObservableProperty]
    private bool _hasWeather;

    [ObservableProperty]
    private string _temperatureText = "";

    /// <summary>Durum satırı için kısa sıcaklık ("14°"); veri yoksa "--°".</summary>
    [ObservableProperty]
    private string _temperatureShort = "--°";

    /// <summary>Rüzgâr ipucu ("Rüzgâr 14 km/sa"); rüzgâr verisi yoksa boş.</summary>
    [ObservableProperty]
    private string _windToolTip = "";

    [ObservableProperty]
    private string _weatherDescription = "";

    [ObservableProperty]
    private string _weatherIconKind = "Cloud";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaceName))]
    private string _city = "";

    /// <summary>
    /// Sıcaklığın üstünde küçük yazılan yer adı: seçilen şehir ya da cihaz konumundan çözülen şehir adı;
    /// ad çözülemediyse (ağ yok, servis yanıt vermedi) "Konumum" (uydurma ad yazılmaz).
    /// </summary>
    public string PlaceName => City.Length > 0 ? City : Strings.Get("WeatherPlaceDevice");

    // --- Ağ ---
    [ObservableProperty]
    private NetworkDisplay _networkDisplay = NetworkDisplay.Disconnected;

    /// <summary>Durum sütunundaki ağ satırı: Wi-Fi adı, "Ethernet", "Bağlı değil"…</summary>
    [ObservableProperty]
    private string _networkText = "";

    /// <summary>Ağ simgesi normal mi (internet var) yoksa üstü çizili mi.</summary>
    [ObservableProperty]
    private bool _networkIsConnected;

    /// <summary>Ağ durumu servisi var mı; yoksa satır hiç çizilmez.</summary>
    public bool HasNetworkInfo => _networkService is not null;

    public StatusViewModel(
        IPowerService powerService,
        IWeatherService weatherService,
        AppSettings settings,
        Dispatcher? dispatcher = null,
        INetworkService? networkService = null,
        IPlaceNameService? placeNameService = null)
    {
        _powerService = powerService;
        _weatherService = weatherService;
        _networkService = networkService;
        _placeNameService = placeNameService;
        _settings = settings;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

        UpdateLocationState();
        UpdateClock();
        UpdatePowerState();
        UpdateWeatherState(_weatherService.CurrentWeather);

        _powerService.PowerChanged += OnPowerChanged;
        _weatherService.WeatherChanged += OnWeatherChanged;

        if (_networkService is not null)
        {
            ApplyNetwork(_networkService.Current);
            _networkService.Changed += OnNetworkChanged;
        }

        // Hava durumu panel açılınca istenir (SetActive): açılıştaki HTTPS isteği TLS yığınıyla
        // birkaç MB tutuyordu ve panel hiç açılmayabilir (AGENTS.md #5).
    }

    public void SetActive(bool active)
    {
        if (_isActive == active)
            return;

        _isActive = active;

        if (_isActive)
        {
            UpdateClock();
            ScheduleNextClockTick();

            // Panel açıldığında hava durumunu tazele (önbellek kontrolü servis içindedir)
            RequestWeather();
        }
        else
        {
            _clockTimer?.Stop();
            _clockTimer = null;
        }
    }

    /// <summary>Konum ayarı değişti (cihaz konumu alındı, şehir seçildi, anahtar kapandı): hava durumunu yeniden kurar.</summary>
    public void OnLocationChanged()
    {
        if (_disposed)
            return;

        UpdateLocationState();
        UpdateWeatherState(_weatherService.CurrentWeather);
        if (_isActive)
            RequestWeather();
    }

    private void UpdateLocationState()
    {
        HasWeatherLocation = WeatherLocationPolicy.HasLocation(_settings);
        City = _settings.Weather.LocationSource switch
        {
            WeatherLocationSource.City => CityShortName(_settings.Weather.City),
            WeatherLocationSource.Device when _devicePlace is not null &&
                _devicePlaceFor == PlaceNameParser.Round(_settings.Weather.Latitude, _settings.Weather.Longitude) => _devicePlace,
            _ => ""
        };
    }

    // Cihaz konumunun çözülen yer adı ve hangi koordinat için çözüldüğü (koordinat değişince eski ad gösterilmez).
    private string? _devicePlace;
    private (double Lat, double Lon) _devicePlaceFor;

    /// <summary>Cihaz konumu kullanılıyorsa yer adını çözer (servis aynı koordinat için tek istek atar).</summary>
    private async void RequestDevicePlaceName()
    {
        if (_placeNameService is null || _settings.Weather.LocationSource != WeatherLocationSource.Device)
            return;

        var coordinate = PlaceNameParser.Round(_settings.Weather.Latitude, _settings.Weather.Longitude);
        if (_devicePlace is not null && _devicePlaceFor == coordinate)
            return;

        string? name;
        try
        {
            name = await _placeNameService.GetPlaceNameAsync(_settings.Weather.Latitude, _settings.Weather.Longitude, _settings.General.Language);
        }
        catch
        {
            return;
        }

        // await sonrası arayüz iş parçacığındayız (SetActive/OnLocationChanged oradan çağrılır).
        if (_disposed || string.IsNullOrEmpty(name) ||
            _settings.Weather.LocationSource != WeatherLocationSource.Device ||
            PlaceNameParser.Round(_settings.Weather.Latitude, _settings.Weather.Longitude) != coordinate)
            return;

        _devicePlace = name;
        _devicePlaceFor = coordinate;
        UpdateLocationState();
    }

    // "Ankara, Türkiye" -> "Ankara": dar sütunda yalnız şehir adı.
    private static string CityShortName(string displayName)
    {
        var comma = displayName.IndexOf(',');
        return comma > 0 ? displayName[..comma] : displayName;
    }

    private void RequestWeather()
    {
        if (!HasWeatherLocation)
            return;

        _ = _weatherService.GetCurrentWeatherAsync(_settings.Weather.Latitude, _settings.Weather.Longitude);
        RequestDevicePlaceName();
    }

    // Saat yalnız HH:mm gösterir: bir sonraki dakika başına kurulan tek atımlık zamanlayıcı, saniyelik tik gerekmez.
    private void ScheduleNextClockTick()
    {
        var now = DateTime.Now;
        var untilNextMinute = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);

        _clockTimer?.Stop();
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = untilNextMinute };
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        UpdateClock();
        if (_isActive)
            ScheduleNextClockTick();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var culture = new CultureInfo(_settings.General.Language == "en" ? "en-US" : "tr-TR");
        // 12/24 saat ayarı (issue #77); saniye yalnız kapalı çentik saatinde, burada dakika hizası korunur.
        CurrentTime = now.ToString(Koy.Core.Clock.ClockFormatPolicy.ResolveTimePattern(_settings.Notch.ClockHourFormat, showSeconds: false, culture), culture);
        CurrentDate = now.ToString("d MMMM dddd", culture);
        ShortDate = now.ToString("d MMM ddd", culture);
        FullDate = now.ToString("d MMMM yyyy dddd", culture);
        DayDate = now.ToString("dddd, d MMMM", culture);
        var pattern = Koy.Core.Clock.ClockFormatPolicy.ResolveTimePattern(_settings.Notch.ClockHourFormat, showSeconds: false, culture);
        SecondClockSuffix = Koy.Core.Clock.ClockFormatPolicy.ComposePeekLine("", null,
            Koy.Core.Clock.SecondClock.Read(_settings.Notch.ClockSecondTimeZoneId, DateTimeOffset.Now, pattern, culture)?.Text);
        Greeting = Strings.Get(Koy.Core.Notch.Greeting.ForHour(now.Hour) switch
        {
            Koy.Core.Notch.DayPart.Morning => "GreetMorning",
            Koy.Core.Notch.DayPart.Afternoon => "GreetAfternoon",
            Koy.Core.Notch.DayPart.Evening => "GreetEvening",
            _ => "GreetNight"
        });
    }

    private void OnPowerChanged(object? sender, PowerChangedEventArgs e)
    {
        _dispatcher.InvokeAsync(() =>
        {
            HasBattery = e.HasBattery;
            BatteryPercent = e.RemainingPercent;
            IsCharging = e.IsCharging;
            SetBatteryStatus(e.Status);
            BatteryText = IsCharging ? $"{SystemFormat.Percent(BatteryPercent, CultureInfo.CurrentCulture)} ({Strings.Charging})" : SystemFormat.Percent(BatteryPercent, CultureInfo.CurrentCulture);
        });
    }

    private void UpdatePowerState()
    {
        HasBattery = _powerService.HasBattery;
        BatteryPercent = _powerService.RemainingPercent;
        IsCharging = _powerService.IsCharging;
        SetBatteryStatus(_powerService.Status);
        BatteryText = IsCharging ? $"{SystemFormat.Percent(BatteryPercent, CultureInfo.CurrentCulture)} ({Strings.Charging})" : SystemFormat.Percent(BatteryPercent, CultureInfo.CurrentCulture);
    }

    private void SetBatteryStatus(BatteryStatus status)
    {
        var onBattery = BatterySaverPolicy.ShouldReduceMotion(true, HasBattery, status);
        if (onBattery == IsOnBattery)
            return;

        IsOnBattery = onBattery;
        OnPropertyChanged(nameof(IsOnBattery));
    }

    private void OnNetworkChanged(object? sender, NetworkStatus status)
    {
        // Olay Windows'un iş parçacığından gelir.
        _dispatcher.InvokeAsync(() =>
        {
            if (!_disposed)
                ApplyNetwork(status);
        });
    }

    private void ApplyNetwork(NetworkStatus status)
    {
        NetworkDisplay = NetworkSummary.Describe(status);
        NetworkIsConnected = NetworkSummary.IsConnected(status);
        NetworkText = NetworkDisplay switch
        {
            NetworkDisplay.WiFi => NetworkSummary.WiFiName(status) ?? Strings.Get("NetworkWiFi"),
            NetworkDisplay.Ethernet => Strings.Get("NetworkEthernetShort"),
            NetworkDisplay.Cellular => Strings.Get("NetworkCellularShort"),
            NetworkDisplay.NoInternet => Strings.Get("NetworkNoInternet"),
            _ => Strings.Get("NetworkDisconnected")
        };
    }

    private void OnWeatherChanged(object? sender, WeatherInfo? weather)
    {
        _dispatcher.InvokeAsync(() => UpdateWeatherState(weather));
    }

    private void UpdateWeatherState(WeatherInfo? weather)
    {
        if (weather is null)
        {
            HasWeather = false;
            TemperatureText = "--" + TemperatureSuffix;
            TemperatureShort = "--°";
            WeatherDescription = Strings.Loading;
            WeatherIconKind = "Cloud";
            WindToolTip = "";
            return;
        }

        HasWeather = true;
        // Hava servisi Celsius verir; seçilen birime Core çevirir, birim harfi Strings'ten (issue #77).
        var degrees = Math.Round(UnitConversion.Temperature(weather.Temperature, _settings.General.TemperatureUnit));
        TemperatureText = $"{degrees}{TemperatureSuffix}";
        TemperatureShort = $"{degrees}°";
        WeatherDescription = Strings.Get(weather.DescriptionKey);
        WeatherIconKind = weather.IconKind;
        var wind = weather.WindSpeedKmh is { } kmh
            ? WindSpeedFormatter.Format(kmh, _settings.General.WindSpeedUnit, CultureInfo.CurrentCulture)
            : null;
        WindToolTip = wind is null ? "" : string.Format(CultureInfo.CurrentCulture, Strings.Get("WeatherWindTooltip"), wind);
    }

    private string TemperatureSuffix => Strings.Get(_settings.General.TemperatureUnit == TemperatureUnit.Fahrenheit ? "UnitFahrenheitSuffix" : "UnitCelsiusSuffix");

    [RelayCommand]
    private async Task RefreshWeatherAsync()
    {
        if (!HasWeatherLocation)
            return;

        await _weatherService.RefreshAsync(_settings.Weather.Latitude, _settings.Weather.Longitude);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SetActive(false);
        _powerService.PowerChanged -= OnPowerChanged;
        _weatherService.WeatherChanged -= OnWeatherChanged;
        if (_networkService is not null)
            _networkService.Changed -= OnNetworkChanged;
    }
}
