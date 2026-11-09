using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Koy.App.Features.Calendar;
using Koy.App.Features.ClipboardPanel;
using Koy.App.Features.ControlCenter;
using Koy.App.Features.Focus;
using Koy.App.Features.Home;
using Koy.App.Features.Search;
using Koy.App.Features.Music;
using Koy.App.Features.Shelf;
using Koy.App.Features.Status;
using Koy.App.Features.SystemInfo;
using Koy.Core.Bluetooth;
using Koy.Core.Clipboard;
using Koy.Core.Diagnostics;
using Koy.Core.Display;
using Koy.Core.Notch;
using Koy.Core.Power;
using Koy.Core.Privacy;
using Koy.Core.Search;
using Koy.Core.Settings;
using Koy.Core.Timers;
using Koy.Core.Shell;
using Koy.Core.Visibility;
using Koy.Core.Weather;
using Koy.Platform.Audio;
using Koy.Platform.Bluetooth;
using Koy.Platform.Display;
using Koy.Platform.Media;
using Koy.Platform.Network;
using Koy.Platform.Power;
using Koy.Platform.Radio;
using Koy.Platform.Shelf;
using Koy.Platform.Shell;
using Koy.Platform.Weather;

namespace Koy.App.Features.Notch;

public sealed class NotchFeature(AppSettings settings, SharedServices services) : IFeature
{
    private sealed class NotchWindowState : IDisposable
    {
        public required DisplayMonitor Monitor { get; set; }
        public required NotchWindow Window { get; init; }
        public required NotchViewModel ViewModel { get; init; }
        public required VisibilityController VisibilityController { get; init; }
        public required NotchController Controller { get; init; }
        public nint Hwnd { get; set; }

        public void Dispose()
        {
            Window.Close();
            ViewModel.Dispose();
            Controller.Dispose();
            VisibilityController.Dispose();
        }
    }

    private readonly Dictionary<string, NotchWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NotchWindowState> _windowStates = new(StringComparer.OrdinalIgnoreCase);

    // Cihaz konumu "yalnız açılışta, günde en fazla bir kez" sorulur: çentik ayar değişince yeniden kurulsa da
    // süreç başına tek deneme yapılır.
    private static bool _deviceLocationAttempted;

    // Pomodoro, sayaç ve kronometre tek örnekte yaşar: sekme değişse, pencere yeniden kurulsa ya da birden çok
    // monitör olsa da süre aynı yerden akar (görünüme bağlı değil).
    private FocusTimers? _focusTimers;
    private readonly ShellActionService _shellActions = new();

    // Pano geçmişi yalnız ayar açıkken alınır (dinleyici ancak o zaman kurulur); Stop'ta bırakılır. Etkinlik defteri
    // bütün monitörlerde tek örnektir ve ilk Takvim açılışında diskten okunur.
    private IClipboardHistoryService? _clipboardHistory;

    // Uygulama listesi bütün ekranların araması için tek kopya.
    private AppSearchSource? _appSearch;
    private readonly SharedEvents _sharedEvents = new();

    private MediaService? _mediaService;
    private IPowerService? _powerService;
    private readonly BatteryNotificationPolicy _batteryPolicy = new();
    private BluetoothService? _bluetoothService;
    private readonly BluetoothHudPolicy _bluetoothPolicy = new();
    private NetworkService? _networkService;
    private WeatherService? _weatherService;
    // Cihaz konumunun yer adı: bütün ekranların çentikleri tek servisi paylaşır (koordinat başına tek istek).
    private readonly PlaceNameService _placeNameService = new();
    // Kurulması ucuz: dosya ya da ağ işi yalnız kullanıcı sözleri açınca başlar.
    private readonly Koy.Core.Lyrics.LyricsProvider _lyricsProvider = new(
        new Koy.Core.Lyrics.LocalLyricsSource(Koy.Core.Lyrics.LocalLyricsSource.DefaultDirectory),
        new Koy.Core.Lyrics.LyricsCache(Koy.Core.Lyrics.LyricsCache.DefaultDirectory),
        new Koy.Platform.Lyrics.LrclibSource());
    private FocusAssistService? _focusAssistService;
    private ShelfService? _shelfService;
    private WindowTracker? _windowTracker;
    private FullscreenDetector? _fullscreenDetector;
    private MonitorService? _monitorService;
    private DisplayEvents? _displayEvents;
    private DispatcherTimer? _edgePeekTimer;

    // Ses/parlaklık göstergeleri (OSD) için kendi servisleri; ilgili ayar kapalıysa hiç kurulmaz (AGENTS.md #5).
    private AudioService? _osdAudio;
    private BrightnessService? _osdBrightness;
    private IPrivacyMonitorService? _privacyMonitor;
    private bool _stopped;

    public void Start()
    {
        if (_windows.Count > 0)
            return;

        // Platform servisleri başlatılır (paylaşılan tekil servisler)
        _mediaService = services.AcquireMediaService();

        _focusTimers = new FocusTimers(PomodoroConfig.FromMinutes(
            settings.PomodoroWorkMinutes, settings.PomodoroShortBreakMinutes, settings.PomodoroLongBreakMinutes));

        _powerService = services.AcquirePowerService();
        _powerService.PowerChanged += OnPowerChanged;
        _bluetoothService = services.AcquireBluetoothService();
        _bluetoothService.DevicesChanged += OnBluetoothDevicesChanged;
        _bluetoothPolicy.Update(_bluetoothService.Devices);
        _networkService = services.AcquireNetworkService();
        if (settings.ClipboardHistoryEnabled)
            _clipboardHistory = services.AcquireClipboardHistory();
        if (settings.ShowPrivacyIndicator)
        {
            _privacyMonitor = services.AcquirePrivacyMonitor();
            _privacyMonitor.StateChanged += OnPrivacyStateChanged;
        }
        _weatherService = new WeatherService(cacheDuration: WeatherRefreshPolicy.CacheDuration(settings.WeatherRefreshInterval));
        _focusAssistService = new FocusAssistService();
        _shelfService = new ShelfService();

        _windowTracker = services.AcquireWindowTracker();
        _fullscreenDetector = new FullscreenDetector();
        _monitorService = new MonitorService();
        _monitorService.Start();
        _monitorService.MonitorsChanged += OnMonitorsChanged;
        _displayEvents = new DisplayEvents(_monitorService);

        // Çentik, ayarlarda "çentik açık" olan her monitörde ayrı bir pencere olarak kurulur.
        var targetMonitors = ResolveTargetMonitors(_monitorService.GetMonitors());

        foreach (var monitor in targetMonitors)
        {
            CreateNotchWindow(monitor);
        }

        // 100 ms kenar gözetleme (peek) zamanlayıcısı; yalnız gizliyken çalışır (AGENTS.md #5).
        // DispatcherTimer UI thread'de tikler: eskiden ThreadPool zamanlayıcısıydı ve CheckEdgePeek
        // pencere/durum nesnelerine UI dışından dokunup VerifyAccess çökmesine yol açıyordu.
        _edgePeekTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _edgePeekTimer.Tick += (_, _) => CheckEdgePeek();

        StartOsdServices();

        _windowTracker.ForegroundWindowChanged += OnForegroundWindowChanged;

        // İzleyici paylaşımlı: Dock onu önceden başlattıysa ilk ön plan olayı bir daha gelmez, mevcut durumu elle uygula.
        var trackerWasRunning = _windowTracker.IsStarted;
        _windowTracker.Start();
        if (trackerWasRunning && _windowTracker.CurrentForeground is { } currentForeground)
            OnForegroundWindowChanged(this, currentForeground);

        UpdateEdgeTimer();

        if (!_deviceLocationAttempted && WeatherLocationPolicy.ShouldRefreshDeviceLocation(settings, DateTimeOffset.UtcNow))
        {
            _deviceLocationAttempted = true;
            _ = RefreshDeviceLocationAsync();
        }
    }

    /// <summary>
    /// Hava durumu için cihaz konumunu bir kez alır ve ayarlara yazar (en çok günde bir kez, yalnız kullanıcı izin
    /// verdiyse). Ret, zaman aşımı ve hata sessizce geçilir: hava durumu önceki koordinatla ya da hiç gösterilmez.
    /// </summary>
    private async Task RefreshDeviceLocationAsync()
    {
        try
        {
            var result = await new LocationService().GetLocationAsync();
            if (_stopped || !result.IsSuccess)
                return;

            // Diskteki güncel ayara yalnız konum alanları işlenir; sırada başka bir ayar değişikliği olmuş olabilir.
            var store = new SettingsStore(SettingsStore.DefaultPath);
            var updated = WeatherLocationPolicy.ApplyDeviceLocation(store.Load(), result, DateTimeOffset.UtcNow);
            store.Save(updated);

            settings.WeatherLocationSource = updated.WeatherLocationSource;
            settings.WeatherLatitude = updated.WeatherLatitude;
            settings.WeatherLongitude = updated.WeatherLongitude;
            settings.WeatherCity = updated.WeatherCity;
            settings.WeatherLocationUpdatedUnix = updated.WeatherLocationUpdatedUnix;

            foreach (var state in _windowStates.Values)
                state.ViewModel.Status.OnLocationChanged();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Hava durumu: cihaz konumu", ex);
        }
    }

    private IReadOnlyList<DisplayMonitor> ResolveTargetMonitors(IReadOnlyList<DisplayMonitor> monitors) =>
        MonitorPolicy.NotchMonitors(MonitorPolicy.Resolve(settings.Monitors, settings.NewScreenDefault, monitors));

    private void CreateNotchWindow(DisplayMonitor monitor)
    {
        var controller = new NotchController();
        controller.SetEnabledPanels(NotchPanels.ParseEnabled(settings.EnabledPanels, settings.ClipboardHistoryEnabled));
        controller.OpenOnHover = settings.NotchOpenBehavior == 0;

        var visibilityController = new VisibilityController(settings.NotchVisibility)
        {
            HideInFullscreen = settings.NotchHideInFullscreen
        };

        // Müzik kartı çubukları: ses yakalama yalnız kart görünür ve müzik çalarken (MusicViewModel.UpdateSpectrum).
        var musicVm = new MusicViewModel(_mediaService!, spectrumAcquire: () => services.AcquireSpectrumService(), spectrumRelease: services.ReleaseSpectrumService,
            lyricsProvider: _lyricsProvider);
        // Parlaklık, çentiğin o anki ekranına bağlanır; ekran düzeni değişince state.Monitor güncellenir.
        NotchWindowState? stateRef = null;
        var controlCenterVm = new ControlCenterViewModel(
            () => new AudioService(),
            () => services.AcquireRadioService(),
            services.ReleaseRadioService,
            () => new BrightnessService(monitorProvider: () => stateRef?.Monitor ?? monitor),
            _focusAssistService!,
            settings,
            wifiFactory: () => services.AcquireWifiService(),
            wifiRelease: services.ReleaseWifiService,
            bluetoothDevices: new BluetoothDevicesViewModel(() => services.AcquireBluetoothService(), services.ReleaseBluetoothService));
        var statusVm = new StatusViewModel(_powerService!, _weatherService!, settings, networkService: _networkService, placeNameService: _placeNameService);
        var tasksVm = new TasksViewModel(services.Tasks);
        var focusVm = new FocusViewModel(_focusTimers!, settings, tasks: tasksVm);
        var shelfVm = new ShelfViewModel(_shelfService!);
        var calendarVm = new CalendarViewModel(settings, _sharedEvents);
        // Sistem verisi çentik açıkken ve Ana ya da Sistem görünürken alınır, yoksa bırakılır (SystemStatsViewModel.SetActive).
        var systemVm = new SystemStatsViewModel(() => services.AcquireSystemStats(_networkService), services.ReleaseSystemStats);
        var quickLaunchVm = new QuickLaunchViewModel(services.QuickLaunch);
        var clipboardVm = _clipboardHistory is null ? null : new ClipboardViewModel(_clipboardHistory);

        var viewModel = new NotchViewModel(
            settings,
            controller,
            musicVm,
            controlCenterVm,
            statusVm,
            focusVm,
            shelfVm,
            calendarVm,
            systemVm,
            quickLaunchVm,
            clipboardVm,
            visibilityController,
            shellActions: _shellActions);

        if (_privacyMonitor is not null)
            viewModel.UpdatePrivacyState(_privacyMonitor.CurrentState);

        var window = new NotchWindow { DataContext = viewModel };
        window.SetTargetMonitor(monitor);

        var state = new NotchWindowState
        {
            Monitor = monitor,
            Window = window,
            ViewModel = viewModel,
            VisibilityController = visibilityController,
            Controller = controller
        };

        stateRef = state;

        window.SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            state.Hwnd = hwnd;
            visibilityController.NotchHandle = hwnd;
            _fullscreenDetector?.IgnoredHandles.Add(hwnd);
            UpdateNotchBounds(window, visibilityController, monitor);
        };

        window.DisplayChangeHandler = () =>
        {
            _monitorService?.NotifyDisplayChanged();
        };

        visibilityController.Changed += OnVisibilityChanged;

        _windowStates[monitor.Id] = state;
        _windows[monitor.Id] = window;

        AttachOsd(viewModel);

        window.Show();
        UpdateNotchBounds(window, visibilityController, monitor);
    }

    private void StartOsdServices()
    {
        if (settings.ShowVolumeOsd)
        {
            _osdAudio = new AudioService();
            foreach (var state in _windowStates.Values)
                state.ViewModel.Osd.Attach(_osdAudio);
        }

        if (settings.ShowBrightnessOsd)
            _ = StartBrightnessOsdAsync();
    }

    private async Task StartBrightnessOsdAsync()
    {
        // WMI sorgusu ve olay izleyicisi UI thread'ini bekletmesin.
        var service = await Task.Run(() => new BrightnessService(watchExternalChanges: true));
        if (_stopped || !service.IsSupported)
        {
            // Masaüstü bilgisayarda parlaklık yok: servis ve izleyici bırakılır.
            service.Dispose();
            return;
        }

        _osdBrightness = service;
        foreach (var state in _windowStates.Values)
            state.ViewModel.Osd.Attach(_osdBrightness);
    }

    private void AttachOsd(NotchViewModel viewModel)
    {
        if (_osdAudio is not null)
            viewModel.Osd.Attach(_osdAudio);
        if (_osdBrightness is not null)
            viewModel.Osd.Attach(_osdBrightness);
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        if (Application.Current is not null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.InvokeAsync(() => OnMonitorsChanged(sender, e));
            return;
        }

        if (_monitorService is null) return;

        var targetMonitors = ResolveTargetMonitors(_monitorService.GetMonitors());
        var targetKeys = new HashSet<string>(targetMonitors.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);

        // 1. Hedefte olmayan veya bağlantısı kesilen monitörlerin pencerelerini kapat
        var toRemove = _windows.Keys.Where(k => !targetKeys.Contains(k)).ToList();
        foreach (var key in toRemove)
        {
            if (_windowStates.Remove(key, out var state))
            {
                state.VisibilityController.Changed -= OnVisibilityChanged;
                if (state.Hwnd != 0)
                {
                    _fullscreenDetector?.IgnoredHandles.Remove(state.Hwnd);
                }
                _windows.Remove(key);
                state.Dispose();
            }
        }

        // 2. Yeni monitörler için pencere oluştur veya mevcutların konumunu güncelle
        foreach (var monitor in targetMonitors)
        {
            if (_windowStates.TryGetValue(monitor.Id, out var existingState))
            {
                existingState.Monitor = monitor;
                existingState.Window.SetTargetMonitor(monitor);
                // Ekran eklendi/çıkarıldı: parlaklık kaynağı (dahili/harici) yeniden belirlenir.
                existingState.ViewModel.ControlCenter.RefreshBrightness();
                UpdateNotchBounds(existingState.Window, existingState.VisibilityController, monitor);
            }
            else
            {
                CreateNotchWindow(monitor);
            }
        }

        UpdateEdgeTimer();
    }

    private static void UpdateNotchBounds(NotchWindow window, VisibilityController visibilityController, DisplayMonitor monitor)
    {
        var source = PresentationSource.FromVisual(window);
        var dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? monitor.DpiScaleX;
        var dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? monitor.DpiScaleY;

        var monitorBounds = monitor.Bounds;
        var notchWidth = NotchLayout.ExpandedWidth * dpiX;
        var notchHeight = 140 * dpiY;
        var monitorWidth = monitorBounds.Right - monitorBounds.Left;
        var left = (int)(monitorBounds.Left + (monitorWidth - notchWidth) / 2);
        var right = (int)(left + notchWidth);

        visibilityController.NotchBounds = new WindowRect(left, monitorBounds.Top, right, (int)(monitorBounds.Top + notchHeight));
    }

    private void OnForegroundWindowChanged(object? sender, WindowInfo info)
    {
        if (_fullscreenDetector is null)
            return;

        foreach (var (_, state) in _windowStates)
        {
            // Her monitörün kendi tam ekran durumu sorgulanır:
            // Yan ekranda tam ekran uygulama olması bu monitördeki Koy'u gizlemez!
            var isFullscreen = _fullscreenDetector.IsFullscreen(state.Monitor.Handle);
            state.VisibilityController.SetFullscreen(isFullscreen);
            state.VisibilityController.UpdateForegroundWindow(info.Handle, info.Bounds);

            // Tam ekranda gizlenmiyorsa (ayar kapalı) çentik tam ekran pencerenin üstünde kalmaya çalışır.
            if ((!isFullscreen || !settings.NotchHideInFullscreen) && state.Hwnd != 0 && info.Handle != state.Hwnd)
            {
                if (!FullscreenDetector.IsScreenClippingHost(info.Handle))
                {
                    WindowStyles.ReassertTopmost(state.Hwnd);
                }
            }
        }

        UpdateEdgeTimer();
    }

    private void OnVisibilityChanged(object? sender, VisibilityResult result)
    {
        UpdateEdgeTimer();
    }

    private void UpdateEdgeTimer()
    {
        if (_edgePeekTimer is null || _windowStates.Count == 0)
            return;

        var anyNeedsPeek = _windowStates.Values.Any(s =>
            (!s.VisibilityController.IsVisible || s.VisibilityController.IsMouseAtEdge) &&
            !s.VisibilityController.IsFullscreen &&
            s.VisibilityController.Mode != VisibilityMode.Fixed);

        if (anyNeedsPeek)
        {
            if (!_edgePeekTimer.IsEnabled)
                _edgePeekTimer.Start();
        }
        else if (_edgePeekTimer.IsEnabled)
        {
            _edgePeekTimer.Stop();
        }
    }

    private void CheckEdgePeek()
    {
        if (_windowTracker is null || _windowStates.Count == 0)
            return;

        if (_windowTracker.GetCursorPosition(out var x, out var y))
        {
            foreach (var (_, state) in _windowStates)
            {
                var vc = state.VisibilityController;
                if (vc.IsFullscreen)
                    continue;

                var monitor = state.Monitor;
                var top = monitor.Bounds.Top;
                var left = monitor.Bounds.Left;
                var right = monitor.Bounds.Right;

                // İmleç bu monitör sınırları içinde mi?
                if (x >= left && x <= right)
                {
                    var bounds = vc.NotchBounds;
                    var minX = bounds.Left - 30;
                    var maxX = bounds.Right + 30;
                    var isAtEdge = y >= top && y <= top + 2 && (bounds.IsEmpty || (x >= minX && x <= maxX));

                    if (isAtEdge)
                    {
                        vc.SetMouseAtEdge(true);
                    }
                    else if (vc.IsMouseAtEdge && !vc.IsMouseOver)
                    {
                        vc.SetMouseAtEdge(false);
                    }
                }
            }
        }
    }

    /// <summary>Kısayol: fare hangi ekrandaysa onun çentiğini açar, açıksa kapatır.</summary>
    public void ToggleExpanded()
    {
        var state = PickStateForShortcut();
        if (state is null)
            return;

        var controller = state.Controller;
        if (controller.State == NotchState.Expanded)
            controller.OnClick();
        else
            controller.Expand();
    }

    /// <summary>Kısayol: fare hangi ekrandaysa orada aramayı açar, açıksa kapatır. Tam ekranda hiçbir şey yapmaz.</summary>
    /// <param name="useClipboard">Panodaki metinle aç.</param>
    public void ToggleSearch(bool useClipboard = false)
    {
        var state = PickStateForShortcut();
        if (state is null || _windowTracker is null)
            return;

        var query = useClipboard ? ReadClipboardText() : null;
        if (state.ViewModel.IsSearchOpen && query is null)
        {
            state.ViewModel.CloseSearch(restoreFocus: true);
            return;
        }

        if (_fullscreenDetector?.IsFullscreen(state.Monitor.Handle) == true)
            return;

        foreach (var other in _windowStates.Values.Where(s => !ReferenceEquals(s, state)))
            other.ViewModel.CloseSearch(restoreFocus: false);

        if (state.ViewModel.IsSearchOpen)
            state.ViewModel.CloseSearch(restoreFocus: false);

        var previous = _windowTracker.CurrentForeground?.Handle ?? 0;
        state.ViewModel.OpenSearch(() => CreateSearch(), previous, query);
        state.Window.FocusSearch();
    }

    private SearchViewModel CreateSearch()
    {
        _appSearch ??= SearchFactory.CreateAppSource(_shellActions);
        return SearchFactory.Create(settings, _appSearch, _windowTracker!, _clipboardHistory, Application.Current.Dispatcher, SaveSearchPinned);
    }

    private void SaveSearchPinned(bool pinned)
    {
        settings.SearchPinned = pinned;
        var store = new SettingsStore(SettingsStore.DefaultPath);
        store.Save(store.Load() with { SearchPinned = pinned });
    }

    private static string? ReadClipboardText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
    }

    /// <summary>Kısayol: Raf panelini açar (panel kapalıysa da).</summary>
    public void OpenShelf()
    {
        PickStateForShortcut()?.ViewModel.ExpandToShelf();
    }

    private NotchWindowState? PickStateForShortcut()
    {
        if (_windowStates.Count == 0)
            return null;

        if (_windowTracker is not null && _windowTracker.GetCursorPosition(out var x, out var y))
        {
            foreach (var state in _windowStates.Values)
            {
                var b = state.Monitor.Bounds;
                if (x >= b.Left && x < b.Right && y >= b.Top && y < b.Bottom)
                    return state;
            }
        }

        return _windowStates.Values.First();
    }

    private void OnPowerChanged(object? sender, PowerChangedEventArgs e)
    {
        var alert = _batteryPolicy.Update(e);
        if (alert.Kind == BatteryAlertKind.None)
            return;

        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            foreach (var (_, state) in _windowStates)
            {
                state.ViewModel.Osd.ShowBattery(alert.Kind, alert.Percent);
            }
        });
    }

    private void OnBluetoothDevicesChanged(object? sender, EventArgs e)
    {
        if (_bluetoothService is null)
            return;

        var events = _bluetoothPolicy.Update(_bluetoothService.Devices);
        if (events.Count == 0)
            return;

        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            foreach (var evt in events)
            {
                foreach (var (_, state) in _windowStates)
                {
                    state.ViewModel.Osd.ShowBluetoothDevice(evt.Name, evt.Connected, evt.BatteryPercent);
                }
            }
        });
    }

    public void Stop()
    {
        _stopped = true;
        _edgePeekTimer?.Stop();
        _edgePeekTimer = null;

        if (_monitorService is not null)
        {
            _monitorService.MonitorsChanged -= OnMonitorsChanged;
        }

        if (_windowTracker is not null)
        {
            _windowTracker.ForegroundWindowChanged -= OnForegroundWindowChanged;
            _windowTracker = null;
            services.ReleaseWindowTracker();
        }

        foreach (var (_, state) in _windowStates)
        {
            state.VisibilityController.Changed -= OnVisibilityChanged;
            state.Dispose();
        }
        _windowStates.Clear();
        _windows.Clear();

        // Pencerelerin Osd görünüm modelleri yukarıda Dispose edildi (olay bağları çözüldü); servisler artık bırakılır.
        _osdAudio?.Dispose();
        _osdAudio = null;
        _osdBrightness?.Dispose();
        _osdBrightness = null;

        _displayEvents?.Dispose();
        _displayEvents = null;
        _monitorService?.Dispose();
        _monitorService = null;

        _fullscreenDetector = null;

        // Paylaşılan medya servisi: yalnız bırak (Dock hâlâ kullanıyor olabilir).
        if (_mediaService is not null)
        {
            services.ReleaseMediaService();
            _mediaService = null;
        }

        if (_powerService is not null)
        {
            _powerService.PowerChanged -= OnPowerChanged;
            _powerService = null;
            services.ReleasePowerService();
        }

        if (_bluetoothService is not null)
        {
            _bluetoothService.DevicesChanged -= OnBluetoothDevicesChanged;
            _bluetoothService = null;
            services.ReleaseBluetoothService();
        }

        if (_networkService is not null)
        {
            _networkService = null;
            services.ReleaseNetworkService();
        }

        _weatherService?.Dispose();
        _weatherService = null;

        _focusAssistService?.Dispose();
        _focusAssistService = null;

        _shelfService?.Dispose();
        _shelfService = null;

        _focusTimers?.Dispose();
        _focusTimers = null;

        // Pencereler yukarıda Dispose edildi (olay abonelikleri kalktı); pano dinleyicisi şimdi bırakılır.
        if (_clipboardHistory is not null)
        {
            _clipboardHistory = null;
            services.ReleaseClipboardHistory();
        }

        if (_privacyMonitor is not null)
        {
            _privacyMonitor.StateChanged -= OnPrivacyStateChanged;
            _privacyMonitor = null;
            services.ReleasePrivacyMonitor();
        }
    }

    private void OnPrivacyStateChanged(PrivacyState state)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            foreach (var s in _windowStates.Values)
                s.ViewModel.UpdatePrivacyState(state);
        });
    }
}
