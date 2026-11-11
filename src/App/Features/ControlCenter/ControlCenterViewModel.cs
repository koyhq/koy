using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Audio;
using Koy.Core.Bluetooth;
using Koy.Core.Diagnostics;
using Koy.Core.Display;
using Koy.Core.Notch;
using Koy.Core.Radio;
using Koy.Core.Settings;
using Koy.Core.Shell;
using Koy.Core.Wireless;

namespace Koy.App.Features.ControlCenter;

/// <summary>Kontrol sekmesinde açık olan sayfa: ana döşemeler ya da bir ağ / cihaz listesi.</summary>
public enum ControlPage
{
    Main,
    WiFi,
    Bluetooth
}

public sealed partial class ControlCenterViewModel : ObservableObject, IDisposable
{
    private readonly Func<IAudioService> _audioFactory;
    private readonly Func<IRadioService> _radioFactory;
    private readonly Action? _radioRelease;
    private readonly Func<IBrightnessService> _brightnessFactory;
    private readonly IFocusAssistService _focusAssistService;
    private IAudioService? _audioService;
    private IRadioService? _radioService;
    private IBrightnessService? _brightnessService;
    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;
    private bool _brightnessStarted;
    private bool _disposed;

    // Son açma/kapama denemesi reddedildiyse döşeme bunu yazar; radyo durumu değişince silinir.
    private readonly Dictionary<RadioKind, bool> _toggleFailed = new() { [RadioKind.WiFi] = false, [RadioKind.Bluetooth] = false };

    private readonly SettingsStore? _store;

    /// <summary>Döşeme sırası (ayardaki <c>ControlCenterLayout</c>; eksik ya da bozuksa varsayılana tamamlanır).</summary>
    public IReadOnlyList<string> TileOrder { get; private set; }

    /// <summary>Sürükleme bitince yeni sırayı ayara yazar. Disk hatası sırayı bozmaz: bu oturumda geçerli kalır.</summary>
    public void SaveTileOrder(IReadOnlyList<string> order)
    {
        if (ControlCenterTiles.SameOrder(order, TileOrder))
            return;

        TileOrder = order.ToList();
        _settings.Notch.ControlCenterLayout = new ValueList<string>(order);
        try
        {
            var store = _store ?? new SettingsStore(SettingsStore.DefaultPath);
            var current = store.Load();
            store.Save(current with { Notch = current.Notch with { ControlCenterLayout = new ValueList<string>(order) } });
        }
        catch (Exception ex)
        {
            CrashLog.Write("Kontrol merkezi sırası kaydedilemedi", ex);
        }
    }

    public RadioTileViewModel WiFi { get; } = new(RadioKind.WiFi);

    public RadioTileViewModel Bluetooth { get; } = new(RadioKind.Bluetooth);

    /// <summary>Wi-Fi listesi sayfası (servis yalnız sayfa açıkken alınır).</summary>
    public WifiPageViewModel Wifi { get; }

    /// <summary>Bluetooth cihazları: Bluetooth listesi, "Bağlı cihazlar" kartı ve Ana'daki kulaklık kartı ortak kullanır.</summary>
    public BluetoothDevicesViewModel BluetoothDevices { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainPage), nameof(IsWifiPage), nameof(IsBluetoothPage))]
    private ControlPage _page = ControlPage.Main;

    public bool IsMainPage => Page == ControlPage.Main;
    public bool IsWifiPage => Page == ControlPage.WiFi;
    public bool IsBluetoothPage => Page == ControlPage.Bluetooth;

    /// <summary>Bluetooth sayfasında / kartında liste yerine gösterilen açıklama (kapalı, bulunamadı, izin yok, boş).</summary>
    [ObservableProperty]
    private string _bluetoothMessage = string.Empty;

    [ObservableProperty]
    private bool _showBluetoothMessage;

    [ObservableProperty]
    private bool _showBluetoothList;

    /// <summary>"Bağlı cihazlar" kartı görünür mü (Bluetooth radyosu var).</summary>
    [ObservableProperty]
    private bool _showConnectedCard;

    /// <summary>Kartta bağlı cihaz yokken gösterilen yazı ("Bağlı cihaz yok" ya da "Bluetooth kapalı").</summary>
    [ObservableProperty]
    private string _connectedEmptyText = string.Empty;

    [ObservableProperty]
    private bool _showConnectedEmpty;

    [ObservableProperty]
    private float _volume;

    [ObservableProperty]
    private int _volumePercent;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private int _brightness;

    [ObservableProperty]
    private bool _isBrightnessSupported;

    // Kaynak ilk kez belirlenene kadar satır gizli kalır (açılışta sahte "desteklenmiyor" yazısı çıkmasın).
    [ObservableProperty]
    private bool _isBrightnessResolved;

    // Servisler panel ilk açıldığında kurulur (AGENTS.md #5): WMI parlaklık sorgusu, CoreAudio ve
    // radyo erişimi açılışta birkaç MB tutuyor ve panel hiç açılmayabilir. Kurulduktan sonra
    // notch kapanana kadar yaşar ve bu ViewModel tarafından bırakılır. İstisna: radyo servisi
    // (kapalı çentikteki Wi-Fi / Bluetooth simgeleri için) çentik açılırken kurulur ve bütün monitörlerde
    // paylaşılır; radioRelease verilirse paylaşımlı sayaç azaltılır, verilmezse servis burada Dispose edilir.
    public ControlCenterViewModel(
        Func<IAudioService> audioFactory,
        Func<IRadioService> radioFactory,
        Action? radioRelease,
        Func<IBrightnessService> brightnessFactory,
        IFocusAssistService focusAssistService,
        AppSettings settings,
        Dispatcher? dispatcher = null,
        SettingsStore? store = null,
        Func<IWifiService>? wifiFactory = null,
        Action? wifiRelease = null,
        BluetoothDevicesViewModel? bluetoothDevices = null)
    {
        _store = store;
        TileOrder = ControlCenterTiles.Resolve(settings.Notch.ControlCenterLayout);
        _audioFactory = audioFactory;
        _radioFactory = radioFactory;
        _radioRelease = radioRelease;
        _brightnessFactory = brightnessFactory;
        _focusAssistService = focusAssistService;
        _settings = settings;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        Wifi = new WifiPageViewModel(wifiFactory ?? (() => throw new InvalidOperationException("Wi-Fi servisi tanımlı değil")), wifiRelease, () => WiFi.IsOn, _dispatcher);
        BluetoothDevices = bluetoothDevices ?? new BluetoothDevicesViewModel(() => throw new InvalidOperationException("Bluetooth servisi tanımlı değil"), null, _dispatcher);
        BluetoothDevices.PropertyChanged += OnBluetoothDevicesChanged;
        BluetoothDevices.Devices.CollectionChanged += (_, _) => UpdateBluetoothView();
        UpdateBluetoothView();
    }

    private void OnBluetoothDevicesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BluetoothDevicesViewModel.IsReady) or nameof(BluetoothDevicesViewModel.HasConnected))
            UpdateBluetoothView();
    }

    // Bluetooth sayfası ve kart, radyo döşemesinin durumundan ve cihaz listesinden beslenir.
    private void UpdateBluetoothView()
    {
        var radio = Bluetooth.State.Status;
        var state = BluetoothDevices.StateFor(radio);
        ShowBluetoothList = state == BluetoothListState.Ready;
        ShowBluetoothMessage = !ShowBluetoothList;
        BluetoothMessage = state switch
        {
            BluetoothListState.RadioOff => Strings.Get("BluetoothRadioOff"),
            BluetoothListState.NotFound => Strings.Get("BluetoothNotFound"),
            BluetoothListState.NoAccess => Strings.Get("RadioNoAccess"),
            BluetoothListState.Empty => Strings.Get("BluetoothEmpty"),
            _ => Strings.Get("Loading")
        };
        ShowConnectedCard = radio is not (RadioTileStatus.NotFound or RadioTileStatus.NoAccess or RadioTileStatus.Loading);
        ShowConnectedEmpty = !BluetoothDevices.HasConnected;
        ConnectedEmptyText = Strings.Get(state == BluetoothListState.RadioOff ? "BluetoothRadioOff" : "BluetoothNoConnected");
    }

    /// <summary>
    /// Kapalı çentikteki Wi-Fi / Bluetooth simgeleri için radyoları panel açılmadan izlemeye başlar.
    /// Durum Radio.StateChanged olayıyla gelir; zamanlayıcı ya da yoklama yok.
    /// </summary>
    public void StartRadioStatus()
    {
        if (_disposed || _radioService is not null)
            return;

        var created = EnsureRadioService();

        // Paylaşımlı servis başka bir monitörde zaten tarandıysa yeniden taranmaz: durum EnsureRadioService'te alındı.
        if (created && _radioService!.WiFiState == RadioState.Unknown && _radioService.BluetoothState == RadioState.Unknown)
            Observe(_radioService.RefreshAsync(), "ControlCenter radyo izleme");
    }

    // Servis yeni bağlandıysa true. Paylaşımlı servis durumu zaten biliyor olabilir (olay bir daha gelmez),
    // bu yüzden döşemeler mevcut durumdan hemen beslenir.
    private bool EnsureRadioService()
    {
        if (_radioService is not null)
            return false;

        _radioService = _radioFactory();
        _radioService.StateChanged += OnRadioStateChanged;
        UpdateRadioTile(RadioKind.WiFi);
        UpdateRadioTile(RadioKind.Bluetooth);
        return true;
    }

    /// <summary>Rahatsız Etme döşemesinin alt satırı: Odaklanma/sunum modu algılanırsa "Odaklanma açık", yoksa "Ayarlarda aç"
    /// (Windows 11 Rahatsız etmeyin düğmesi okunamadığı için "Kapalı" yazılmaz).</summary>
    [ObservableProperty]
    private string _doNotDisturbStatus = Strings.OpenInSettings;

    private bool _dndWatching;

    private void RefreshDoNotDisturb() =>
        DoNotDisturbStatus = _focusAssistService.IsDoNotDisturb ? Strings.Get("FocusOn") : Strings.OpenInSettings;

    // Olay arka plan iş parçacığından gelir.
    private void OnFocusAssistStateChanged(object? sender, bool active) =>
        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed && _dndWatching)
                RefreshDoNotDisturb();
        });

    private void SetDoNotDisturbActive(bool active)
    {
        if (active)
        {
            // Sekme açılınca tek okuma; Odaklanma oturumu için olay da dinlenir (zamanlayıcı yok).
            RefreshDoNotDisturb();
            if (!_dndWatching)
            {
                _dndWatching = true;
                _focusAssistService.StateChanged += OnFocusAssistStateChanged;
            }

            return;
        }

        if (_dndWatching)
        {
            _dndWatching = false;
            _focusAssistService.StateChanged -= OnFocusAssistStateChanged;
        }
    }

    public void SetActive(bool active)
    {
        if (_disposed)
            return;

        SetDoNotDisturbActive(active);

        // Sekme değişince ya da çentik kapanınca liste sayfaları kapanır; servisler yalnız görünürken çalışır.
        BluetoothDevices.SetViewing("control", active);
        if (!active)
        {
            ClosePage();
            return;
        }

        EnsureRadioService();

        // Her açılışta yeniden taranır: USB Wi-Fi takılıp çıkarılmış ya da radyo izni ayarlardan
        // sonradan verilmiş olabilir. Yalnız tek seferlik sorgu; zamanlayıcı yok.
        Observe(_radioService!.RefreshAsync(), "ControlCenter radyo taraması");

        EnsureAudioService();

        // Ses tekerlekle önceden kurulmuş olabilir; parlaklık yine de ilk açılışta bir kez başlar.
        if (_brightnessStarted)
        {
            // Ekran düzeni çentik kapalıyken değişmiş olabilir; kaynak her açılışta arka planda yeniden belirlenir.
            RefreshBrightness();
            return;
        }

        _brightnessStarted = true;
        Observe(InitializeBrightnessAsync(), "ControlCenter parlaklık başlatma");
    }

    // Fire-and-forget görevlerin istisnası yutulmasın: gözlenir ve günlüğe yazılır.
    private static void Observe(Task task, string source) =>
        _ = task.ContinueWith(
            t => CrashLog.Write(source, t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private void EnsureAudioService()
    {
        if (_audioService is not null || _disposed)
            return;

        _audioService = _audioFactory();
        _audioService.StateChanged += OnAudioStateChanged;
        Volume = _audioService.Volume;
        VolumePercent = (int)Math.Round(Volume * 100);
        IsMuted = _audioService.IsMuted;
    }

    /// <summary>Çentik tekerlek eylemi "Ses" iken: panel açılmadan sistem sesini yüzde olarak artırır ya da azaltır.</summary>
    public void AdjustVolume(int deltaPercent)
    {
        EnsureAudioService();
        if (_audioService is null)
            return;

        var current = (int)Math.Round(_audioService.Volume * 100);
        OnVolumeSliderChanged(Math.Clamp(current + deltaPercent, 0, 100));
    }

    private async Task InitializeBrightnessAsync()
    {
        // WMI sorgusu UI iş parçacığını bekletmesin.
        var service = await Task.Run(_brightnessFactory);
        if (_disposed)
        {
            service.Dispose();
            return;
        }

        _brightnessService = service;
        _brightnessService.BrightnessChanged += OnBrightnessChanged;
        _brightnessService.AvailabilityChanged += OnBrightnessAvailabilityChanged;
        ApplyBrightnessState();
    }

    private void ApplyBrightnessState()
    {
        if (_brightnessService is null)
            return;

        IsBrightnessSupported = _brightnessService.IsSupported;
        Brightness = _brightnessService.Brightness;
        IsBrightnessResolved = true;
    }

    private void OnBrightnessAvailabilityChanged(object? sender, EventArgs e) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (!_disposed)
                ApplyBrightnessState();
        });

    /// <summary>
    /// Parlaklık kaynağını arka planda yeniden belirler (ekran eklendi/çıkarıldı, çentik başka ekrana geçti).
    /// Sonuç AvailabilityChanged ile UI'ye döner; UI iş parçacığı WMI/DDC sorgusunu beklemez.
    /// </summary>
    public void RefreshBrightness()
    {
        var service = _brightnessService;
        if (service is null || _disposed)
            return;

        Observe(Task.Run(service.Refresh), "ControlCenter parlaklık yenileme");
    }

    private void OnAudioStateChanged(object? sender, AudioStateChangedEventArgs e)
    {
        _dispatcher.InvokeAsync(() =>
        {
            Volume = e.Volume;
            VolumePercent = (int)Math.Round(e.Volume * 100);
            IsMuted = e.IsMuted;
        });
    }

    private void OnRadioStateChanged(object? sender, RadioStateChangedEventArgs e)
    {
        _dispatcher.InvokeAsync(() =>
        {
            var tile = GetTile(e.Kind);
            if (tile.State.IsOn != (e.State == RadioState.On))
                _toggleFailed[e.Kind] = false;

            UpdateRadioTile(e.Kind);
            if (e.Kind == RadioKind.WiFi)
                Wifi.OnRadioChanged();
        });
    }

    private RadioTileViewModel GetTile(RadioKind kind) => kind == RadioKind.WiFi ? WiFi : Bluetooth;

    private void UpdateRadioTile(RadioKind kind)
    {
        if (_radioService is null)
            return;

        var state = kind == RadioKind.WiFi ? _radioService.WiFiState : _radioService.BluetoothState;
        GetTile(kind).Apply(RadioTileResolver.Resolve(_radioService.Access, state, _toggleFailed[kind]));
        if (kind == RadioKind.Bluetooth)
            UpdateBluetoothView();
    }

    private void OnBrightnessChanged(object? sender, int e)
    {
        _dispatcher.InvokeAsync(() =>
        {
            Brightness = e;
        });
    }

    /// <summary>
    /// Döşemeye tıklama: radyo değiştirilebiliyorsa açar/kapatır, değiştirilemiyorsa nedenine göre
    /// ilgili Windows ayarını açar (sahte anahtar yok).
    /// </summary>
    [RelayCommand]
    private async Task ActivateRadioAsync(RadioKind kind)
    {
        if (_radioService is null)
            return;

        switch (GetTile(kind).State.Action)
        {
            case RadioTileAction.Toggle:
                var ok = await _radioService.ToggleAsync(kind);
                _toggleFailed[kind] = !ok;
                UpdateRadioTile(kind);
                // Başlık anahtarı tek yönlü bağlı; reddedilen denemede kullanıcının çevirdiği hâli gerçek duruma döndür.
                GetTile(kind).NotifyToggle();
                break;
            case RadioTileAction.OpenSettings:
                _radioService.OpenSettings(kind);
                break;
            case RadioTileAction.OpenPrivacySettings:
                _radioService.OpenPrivacySettings();
                break;
        }
    }

    /// <summary>Döşemenin okuna basınca: Wi-Fi ya da Bluetooth listesi sayfası çentik içinde açılır.</summary>
    [RelayCommand]
    private void OpenPage(RadioKind kind)
    {
        if (_disposed)
            return;

        Page = kind == RadioKind.WiFi ? ControlPage.WiFi : ControlPage.Bluetooth;
        Wifi.SetActive(Page == ControlPage.WiFi);
        UpdateBluetoothView();
    }

    /// <summary>Geri oku: Kontrol'ün ana görünümüne döner ve liste servislerini bırakır.</summary>
    [RelayCommand]
    private void Back() => ClosePage();

    private void ClosePage()
    {
        Wifi.SetActive(false);
        Page = ControlPage.Main;
    }

    /// <summary>
    /// Rahatsız Etme'yi kodla açıp kapatmak yok; döşeme Windows'taki ayarı açar. Alt satır yalnız Odaklanma/sunum modunu gösterir
    /// (<see cref="DoNotDisturbStatus"/>, yalnız sekme görünürken okunur).
    /// </summary>
    [RelayCommand]
    private void OpenDoNotDisturbSettings() => _focusAssistService.OpenSettings();

    [RelayCommand]
    private void ToggleMute()
    {
        _audioService?.ToggleMute();
    }

    public void OnVolumeSliderChanged(double newVolumePercent)
    {
        var normalized = (float)Math.Clamp(newVolumePercent / 100.0, 0.0, 1.0);
        _audioService?.SetVolume(normalized);
        Volume = normalized;
        VolumePercent = (int)Math.Round(newVolumePercent);
    }

    public void OnBrightnessSliderChanged(double newBrightness)
    {
        if (!IsBrightnessSupported)
            return;

        var intBrightness = (int)Math.Clamp(newBrightness, 0, 100);
        // Bloklamaz: servis değeri arka planda uygular, art arda gelenleri birleştirir.
        _brightnessService?.SetBrightness(intBrightness);
        Brightness = intBrightness;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        SetDoNotDisturbActive(false);
        Wifi.Dispose();
        BluetoothDevices.PropertyChanged -= OnBluetoothDevicesChanged;
        BluetoothDevices.Dispose();

        if (_audioService is not null)
        {
            _audioService.StateChanged -= OnAudioStateChanged;
            _audioService.Dispose();
            _audioService = null;
        }

        if (_radioService is not null)
        {
            _radioService.StateChanged -= OnRadioStateChanged;
            if (_radioRelease is not null)
                _radioRelease();
            else
                _radioService.Dispose();
            _radioService = null;
        }

        if (_brightnessService is not null)
        {
            _brightnessService.BrightnessChanged -= OnBrightnessChanged;
            _brightnessService.AvailabilityChanged -= OnBrightnessAvailabilityChanged;
            _brightnessService.Dispose();
            _brightnessService = null;
        }
    }
}
