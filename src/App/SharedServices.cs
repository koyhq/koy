using Koy.Core.Tray;
using Koy.Core.Clipboard;
using Koy.Core.Network;
using Koy.Core.QuickLaunch;
using Koy.Core.Settings;
using Koy.Core.SystemStats;
using Koy.Core.Tasks;
using Koy.Platform.Audio;
using Koy.Platform.Bluetooth;
using Koy.Platform.Clipboard;
using Koy.Platform.Media;
using Koy.Platform.Network;
using Koy.Platform.Radio;
using Koy.Platform.Shell;
using Koy.Platform.SystemStats;
using Koy.Platform.Wireless;

namespace Koy.App;

/// <summary>
/// Dock ve Notch'un ortak kullandığı platform servislerinin tek örneği (WinEvent hook'ları ve GSMTC oturumu ikişer kez kurulmasın).
/// Örnek ilk isteyen özellik için oluşur, son kullanan bıraktığında Dispose edilir; böylece özellikler kapalıyken
/// hiçbir şey belleğe yüklenmez (AGENTS.md #5, #12). Yalnız UI thread'inden kullanılır.
/// </summary>
public sealed class SharedServices : IDisposable
{
    private WindowTracker? _windowTracker;
    private int _windowTrackerUsers;
    private MediaService? _mediaService;
    private int _mediaServiceUsers;
    private RadioService? _radioService;
    private int _radioServiceUsers;
    private NetworkService? _networkService;
    private int _networkServiceUsers;
    private WifiService? _wifiService;
    private int _wifiServiceUsers;
    private BluetoothService? _bluetoothService;
    private int _bluetoothServiceUsers;
    private SpectrumCaptureService? _spectrumService;
    private int _spectrumUsers;
    private TrayHostService? _trayHost;
    private readonly TrayHostLease _trayHostLease = new();
    private ClipboardHistoryService? _clipboard;
    private int _clipboardUsers;
    private SystemStatsService? _systemStats;
    private int _systemStatsUsers;
    private TaskService? _tasks;
    private QuickLaunchService? _quickLaunch;
    private bool _disposed;

    public WindowTracker AcquireWindowTracker()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _windowTrackerUsers++;
        return _windowTracker ??= new WindowTracker();
    }

    public void ReleaseWindowTracker()
    {
        if (_windowTrackerUsers == 0)
            return;

        if (--_windowTrackerUsers == 0)
        {
            _windowTracker?.Dispose();
            _windowTracker = null;
        }
    }

    /// <summary>Çalışan medya servisi (yoksa null); kısayol gibi tek seferlik işler yeni servis kurmasın diye.</summary>
    public MediaService? RunningMediaService => _mediaService;

    public MediaService AcquireMediaService()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _mediaServiceUsers++;
        if (_mediaService is null)
        {
            _mediaService = new MediaService();
            _ = _mediaService.StartAsync();
        }

        return _mediaService;
    }

    public void ReleaseMediaService()
    {
        if (_mediaServiceUsers == 0)
            return;

        if (--_mediaServiceUsers == 0)
        {
            _mediaService?.Dispose();
            _mediaService = null;
        }
    }

    /// <summary>
    /// Wi-Fi / Bluetooth radyo servisi. Her monitördeki çentik aynı örneği paylaşır: radyo olay aboneliği ve
    /// WinRT taraması monitör sayısıyla çoğalmaz. Son kullanan bıraktığında Dispose edilir.
    /// </summary>
    public RadioService AcquireRadioService()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _radioServiceUsers++;
        return _radioService ??= new RadioService();
    }

    public void ReleaseRadioService()
    {
        if (_radioServiceUsers == 0)
            return;

        if (--_radioServiceUsers == 0)
        {
            _radioService?.Dispose();
            _radioService = null;
        }
    }

    /// <summary>
    /// Wi-Fi ağ listesi servisi. Yalnız çentikte Wi-Fi listesi sayfası açıkken alınır; sayfa kapanınca bırakılır
    /// ve son kullanan bıraktığında Dispose edilir (adaptör nesneleri tutulmaz).
    /// </summary>
    public WifiService AcquireWifiService()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _wifiServiceUsers++;
        return _wifiService ??= new WifiService();
    }

    public void ReleaseWifiService()
    {
        if (_wifiServiceUsers == 0)
            return;

        if (--_wifiServiceUsers == 0)
        {
            _wifiService?.Dispose();
            _wifiService = null;
        }
    }

    /// <summary>
    /// Bluetooth cihaz listesi servisi (DeviceWatcher). Ana'daki kulaklık kartı, Kontrol'deki "Bağlı cihazlar" ve
    /// Bluetooth listesi için yalnız çentik açıkken ve ilgili sayfa görünürken alınır; son kullanan bırakınca izleyiciler durur.
    /// </summary>
    public BluetoothService AcquireBluetoothService()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _bluetoothServiceUsers++;
        return _bluetoothService ??= new BluetoothService();
    }

    public void ReleaseBluetoothService()
    {
        if (_bluetoothServiceUsers == 0)
            return;

        if (--_bluetoothServiceUsers == 0)
        {
            _bluetoothService?.Dispose();
            _bluetoothService = null;
        }
    }

    /// <summary>
    /// Müzik kartındaki çubuklar için ses yakalama (WASAPI loopback). Yalnız çentik açık, Ana görünür ve müzik
    /// çalarken alınır; son kullanan bırakınca yakalama iş parçacığı durur.
    /// </summary>
    public SpectrumCaptureService AcquireSpectrumService()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _spectrumUsers++;
        var service = _spectrumService ??= new SpectrumCaptureService();
        service.Start();
        return service;
    }

    public void ReleaseSpectrumService()
    {
        if (_spectrumUsers == 0)
            return;

        if (--_spectrumUsers == 0)
        {
            _spectrumService?.Dispose();
            _spectrumService = null;
        }
    }

    /// <summary>
    /// Ağ durumu servisi (olay tabanlı, yoklama yok). Her monitördeki çentik aynı örneği paylaşır; son kullanan
    /// bıraktığında Dispose edilir ve Windows olay aboneliği kalkar.
    /// </summary>
    public NetworkService AcquireNetworkService()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _networkServiceUsers++;
        return _networkService ??= new NetworkService();
    }

    public void ReleaseNetworkService()
    {
        if (_networkServiceUsers == 0)
            return;

        if (--_networkServiceUsers == 0)
        {
            _networkService?.Dispose();
            _networkService = null;
        }
    }

    /// <summary>
    /// Koy'un Shell_TrayWnd penceresi (sistem tepsisi barındırıcısı). Monitör başına ayrı pencere kurulmaz: bütün
    /// dock'lar aynı örneği paylaşır. Pencere kurulamazsa null döner (sayılmaz, bırakmaya gerek yok).
    /// </summary>
    public TrayHostService? AcquireTrayHost()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Pencere beklenmedik kapandıysa (RequestClose sonrası) yenisi kurulur.
        if (_trayHost is { IsRunning: false })
        {
            _trayHost.Dispose();
            _trayHost = null;
        }

        if (_trayHost is null)
        {
            var host = new TrayHostService();
            host.Start();
            if (!host.IsRunning)
            {
                host.Dispose();
                return null;
            }

            _trayHost = host;
        }

        _trayHostLease.Acquire();
        return _trayHost;
    }

    public void ReleaseTrayHost()
    {
        if (_trayHostLease.Release())
        {
            // Önce alan boşaltılır, sonra kapatılır: Dispose tepsi iş parçacığını beklerken arayüz iş parçacığı
            // mesaj işler ve bir olay yeniden AcquireTrayHost çağırabilir. Eski sırada (Dispose, sonra null) o anda
            // kurulan yeni pencere burada unutuluyor, Shell_TrayWnd sınıfını tutmaya devam ediyor ve sonraki
            // her kurulum başarısız oluyordu (dock kapatılıp açılınca tepsi düğmesi kayboluyordu).
            var host = _trayHost;
            _trayHost = null;
            host?.Dispose();
        }
    }

    /// <summary>
    /// Pano geçmişi. Çağıran önce <c>AppSettings.ClipboardHistoryEnabled</c>'a bakmalı: kapalıysa bu metot
    /// hiç çağrılmaz (dinleyici kurulmaz). Servis Start edilmiş döner; son kullanan bırakınca Stop+Dispose olur.
    /// <see cref="IClipboardHistoryService.Changed"/> arka plan iş parçacığından gelir: Dispatcher'a geçirin.
    /// </summary>
    public IClipboardHistoryService AcquireClipboardHistory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _clipboardUsers++;
        if (_clipboard is null)
        {
            _clipboard = new ClipboardHistoryService();
            _clipboard.Start();
        }

        return _clipboard;
    }

    public void ReleaseClipboardHistory()
    {
        if (_clipboardUsers == 0)
            return;

        if (--_clipboardUsers == 0)
        {
            _clipboard?.Dispose();
            _clipboard = null;
        }
    }

    /// <summary>
    /// Sistem verisi örnekleyicisi (CPU, RAM, ağ, Koy belleği), Start edilmiş döner: çağıran yalnız çentik
    /// AÇILIRKEN almalı, KAPANIRKEN <see cref="ReleaseSystemStats"/> ile bırakmalı (son bırakan zamanlayıcıyı söker).
    /// <paramref name="network"/> ağ adı içindir (INetworkService.Current.Name); ilk alan verir.
    /// <see cref="ISystemStatsService.Updated"/> arka plan iş parçacığından gelir: Dispatcher'a geçirin.
    /// </summary>
    public ISystemStatsService AcquireSystemStats(INetworkService? network = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _systemStatsUsers++;
        if (_systemStats is null)
        {
            _systemStats = new SystemStatsService(network is null ? null : () => network.Current.Name);
            _systemStats.Start();
        }

        return _systemStats;
    }

    public void ReleaseSystemStats()
    {
        if (_systemStatsUsers == 0)
            return;

        if (--_systemStatsUsers == 0)
        {
            _systemStats?.Dispose();
            _systemStats = null;
        }
    }

    /// <summary>Odak görev listesi: tek örnek (tasks.json), ilk istendiğinde diskten okunur. UI iş parçacığından kullanın.</summary>
    public ITaskService Tasks
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _tasks ??= new TaskService();
        }
    }

    /// <summary>
    /// Hızlı başlat listesi: tek örnek. Değişiklikler settings.json'a (QuickLaunchItems) yazılır
    /// (uygulamadaki yükle-değiştir-kaydet deseniyle). Başlatma ShellActionService üzerinden.
    /// </summary>
    public IQuickLaunchService QuickLaunch
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_quickLaunch is null)
            {
                var store = new SettingsStore(SettingsStore.DefaultPath);
                _quickLaunch = new QuickLaunchService(
                    store.Load().QuickLaunchItems,
                    items =>
                    {
                        var settings = store.Load();
                        settings.QuickLaunchItems = items.ToList();
                        store.Save(settings);
                    },
                    new ShellActionService());
            }

            return _quickLaunch;
        }
    }

    /// <summary>
    /// Tepsi penceresini beklemeden kapatma isteği (çökme işleyicileri): Windows tepsisi hemen eski haline döner.
    /// Örnek sonra normal yoldan (Release/Dispose) bırakılır.
    /// </summary>
    public void RequestTrayHostClose() => _trayHost?.RequestClose();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _windowTracker?.Dispose();
        _windowTracker = null;
        _mediaService?.Dispose();
        _mediaService = null;
        _radioService?.Dispose();
        _radioService = null;
        _networkService?.Dispose();
        _networkService = null;
        _networkServiceUsers = 0;
        _wifiService?.Dispose();
        _wifiService = null;
        _wifiServiceUsers = 0;
        _bluetoothService?.Dispose();
        _bluetoothService = null;
        _bluetoothServiceUsers = 0;
        _spectrumService?.Dispose();
        _spectrumService = null;
        _spectrumUsers = 0;
        _trayHost?.Dispose();
        _trayHost = null;
        _trayHostLease.Reset();
        _clipboard?.Dispose();
        _clipboard = null;
        _systemStats?.Dispose();
        _systemStats = null;
        _clipboardUsers = 0;
        _systemStatsUsers = 0;
        _windowTrackerUsers = 0;
        _mediaServiceUsers = 0;
        _radioServiceUsers = 0;
    }
}
