using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Koy.App.Features.Calendar;
using Koy.App.Features.ClipboardPanel;
using Koy.App.Features.ControlCenter;
using Koy.App.Features.Focus;
using Koy.App.Features.Home;
using Koy.App.Features.Search;
using Koy.App.Features.Settings;
using Koy.App.Features.Music;
using Koy.App.Features.Shelf;
using Koy.App.Features.Status;
using Koy.App.Features.SystemInfo;
using Koy.App.Features.Notch.OSD;
using Koy.App.Resources;
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
using Koy.Core.Notifications;
using Koy.Core.Updates;
using Koy.Core.Shell;
using Koy.Core.Visibility;
using Koy.Core.Voice;
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
using Koy.Platform.Voice;
using Koy.Platform.Weather;

namespace Koy.App.Features.Notch;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Kaynaklar IFeature.Stop'ta bırakılır (AGENTS.md #12).")]
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
    // Kurulması ucuz: dosya ya da ağ işi yalnız Ana sayfa görünürken başlar.
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
    private VoiceSessionController? _voiceController;
    private bool _stopped;

    public void Start()
    {
        if (_windows.Count > 0)
            return;

        // Platform servisleri başlatılır (paylaşılan tekil servisler)
        _mediaService = services.AcquireMediaService();

        _focusTimers = new FocusTimers(PomodoroConfig.FromMinutes(
            settings.Focus.PomodoroWorkMinutes, settings.Focus.PomodoroShortBreakMinutes, settings.Focus.PomodoroLongBreakMinutes));

        _powerService = services.AcquirePowerService();
        _powerService.PowerChanged += OnPowerChanged;
        _bluetoothService = services.AcquireBluetoothService();
        _bluetoothService.DevicesChanged += OnBluetoothDevicesChanged;
        _bluetoothPolicy.Update(_bluetoothService.Devices);
        _networkService = services.AcquireNetworkService();
        if (settings.Clipboard.HistoryEnabled)
            _clipboardHistory = services.AcquireClipboardHistory();
        if (settings.Notch.ShowPrivacyIndicator)
        {
            _privacyMonitor = services.AcquirePrivacyMonitor();
            _privacyMonitor.StateChanged += OnPrivacyStateChanged;
        }
        _weatherService = new WeatherService(cacheDuration: WeatherRefreshPolicy.CacheDuration(settings.Weather.RefreshInterval));
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
        StartNotifications();

        UpdateStatus.Changed += OnUpdateStatusChanged;
        // Özellik yeniden kurulduğunda (ayar değişimi) denetim sonucu zaten gelmiş olabilir.
        Application.Current?.Dispatcher.InvokeAsync(TryShowUpdateCard);

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

            settings.Weather.LocationSource = updated.Weather.LocationSource;
            settings.Weather.Latitude = updated.Weather.Latitude;
            settings.Weather.Longitude = updated.Weather.Longitude;
            settings.Weather.City = updated.Weather.City;
            settings.Weather.LocationUpdatedUnix = updated.Weather.LocationUpdatedUnix;

            foreach (var state in _windowStates.Values)
                state.ViewModel.Status.OnLocationChanged();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Hava durumu: cihaz konumu", ex);
        }
    }

    private IReadOnlyList<DisplayMonitor> ResolveTargetMonitors(IReadOnlyList<DisplayMonitor> monitors) =>
        MonitorPolicy.NotchMonitors(MonitorPolicy.Resolve(settings.Displays.Monitors, settings.Displays.NewScreenDefault, monitors));

    private void CreateNotchWindow(DisplayMonitor monitor)
    {
        var controller = new NotchController
        {
            OpenOnHover = settings.Notch.OpenBehavior == NotchOpenBehavior.Hover,
            // Açılma / kapanma gecikmesi ve sekme sırası (issue #78).
            OpenDelayMs = settings.Notch.OpenDelayMs,
            CloseDelayMs = settings.Notch.CloseDelayMs,
        };
        controller.SetPanelOrder(settings.Notch.PanelOrder);
        controller.SetEnabledPanels(NotchPanels.ParseEnabled(settings.Notch.EnabledPanels, settings.Clipboard.HistoryEnabled, settings.Notch.PanelOrder));

        var visibilityController = new VisibilityController(settings.Notch.Visibility)
        {
            HideInFullscreen = settings.Notch.HideInFullscreen
        };

        // Müzik kartı çubukları: ses yakalama yalnız kart görünür ve müzik çalarken (MusicViewModel.UpdateSpectrum).
        var musicVm = new MusicViewModel(_mediaService!, spectrumAcquire: () => services.AcquireSpectrumService(), spectrumRelease: services.ReleaseSpectrumService,
            lyricsProvider: _lyricsProvider, cardStyle: settings.Music.CardStyle,
            visualizerStyle: settings.Music.VisualizerStyle, visualizerColorSource: settings.Music.VisualizerColorSource,
            visualizerBarCount: settings.Music.VisualizerBarCount);
        musicVm.Lyrics.EnabledToggled += SaveLyricsEnabled;
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
        var systemVm = new SystemStatsViewModel(() => services.AcquireSystemStats(_networkService), services.ReleaseSystemStats,
            networkUnit: settings.General.NetworkSpeedUnit);
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
            UpdateNotchBounds(window, visibilityController, monitor, NotchStyle.TopOffsetDip(settings.Notch.Style));
        };

        window.DisplayChangeHandler = () =>
        {
            _monitorService?.NotifyDisplayChanged();
        };

        window.VoiceConfirmRequested = ConfirmVoiceCommand;
        window.VoiceCancelRequested = CancelVoiceCommand;
        window.VoiceReplyRequested = OpenVoiceReply;

        visibilityController.Changed += OnVisibilityChanged;

        _windowStates[monitor.Id] = state;
        _windows[monitor.Id] = window;

        AttachOsd(viewModel);

        window.Show();
        UpdateNotchBounds(window, visibilityController, monitor, NotchStyle.TopOffsetDip(settings.Notch.Style));
    }

    private void StartOsdServices()
    {
        if (settings.Notch.ShowVolumeOsd)
        {
            _osdAudio = new AudioService();
            foreach (var state in _windowStates.Values)
                state.ViewModel.Osd.Attach(_osdAudio);
        }

        if (settings.Notch.ShowBrightnessOsd)
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
                UpdateNotchBounds(existingState.Window, existingState.VisibilityController, monitor, NotchStyle.TopOffsetDip(settings.Notch.Style));
            }
            else
            {
                CreateNotchWindow(monitor);
            }
        }

        UpdateEdgeTimer();
    }

    private static void UpdateNotchBounds(NotchWindow window, VisibilityController visibilityController, DisplayMonitor monitor, double topOffsetDip)
    {
        var source = PresentationSource.FromVisual(window);
        var dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? monitor.DpiScaleX;
        var dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? monitor.DpiScaleY;

        var monitorBounds = monitor.Bounds;
        var notchWidth = NotchLayout.ExpandedWidth * dpiX;
        var notchHeight = 140 * dpiY;
        var topOffset = WindowPlacementMath.ToPixels(topOffsetDip, monitor.DpiScaleY);
        var monitorWidth = monitorBounds.Right - monitorBounds.Left;
        var left = (int)(monitorBounds.Left + (monitorWidth - notchWidth) / 2);
        var right = (int)(left + notchWidth);

        visibilityController.NotchBounds = new WindowRect(left, monitorBounds.Top + topOffset, right, (int)(monitorBounds.Top + topOffset + notchHeight));
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
            if ((!isFullscreen || !settings.Notch.HideInFullscreen) && state.Hwnd != 0 && info.Handle != state.Hwnd)
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
                    // Yüzen adada ekran kenarı ile ada arasındaki 9 DIP boşluk da kenar bölgesidir.
                    var edgeBottom = top + NotchStyle.EdgeTriggerBottomPixels(settings.Notch.Style, monitor.DpiScaleY);
                    var isAtEdge = y >= top && y <= edgeBottom && (bounds.IsEmpty || (x >= minX && x <= maxX));

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

    // Karttaki düğme ayarı değiştirir: paylaşılan ayar nesnesi (diğer ekranların çentikleri de okur) ve diskteki dosya.
    private void SaveLyricsEnabled(bool enabled)
    {
        settings.Music.LyricsEnabled = enabled;
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var current = store.Load();
        store.Save(current with { Music = current.Music with { LyricsEnabled = enabled } });
    }

    private void SaveSearchPinned(bool pinned)
    {
        settings.Search.Pinned = pinned;
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var current = store.Load();
        store.Save(current with { Search = current.Search with { Pinned = pinned } });
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

    /// <summary>Kısayol: komut kutusunu açar; kutuda metin varsa çalıştırır, boşsa kapatır; çalışırken iptal eder.</summary>
    public void ToggleVoiceCommand()
    {
        EnsureVoiceController();
        _ = _voiceController?.TriggerShortcutAsync();
    }

    public void ConfirmVoiceCommand()
    {
        _ = _voiceController?.ConfirmAsync();
    }

    public void CancelVoiceCommand()
    {
        _voiceController?.Cancel();
    }

    private void EnsureVoiceController()
    {
        if (_voiceController is not null)
            return;

        _voiceController = new VoiceSessionController(new ProcessHarnessRunnerService(), () => new VoiceSessionOptions(
            HarnessType: settings.Voice.Harness,
            WorkingDirectory: settings.Voice.WorkingDirectory,
            TimeoutSeconds: settings.Voice.TimeoutSeconds,
            CustomExecutable: settings.Voice.CustomCommand,
            ClaudePermission: settings.Voice.ClaudePermission,
            Model: settings.Voice.Model));

        _voiceController.StateChanged += state => Application.Current?.Dispatcher.InvokeAsync(() => UpdateVoiceOsd(state));
    }

    // Core yalnız durum ve hata kodu verir; metin burada seçili dilde yazılır.
    private string VoiceStatusText(VoiceSessionController controller, VoiceState state) => state switch
    {
        VoiceState.Editing => Strings.Get("VoiceEditing"),
        VoiceState.Running => string.Format(CultureInfo.CurrentCulture, Strings.Get("VoiceRunning"), VoiceHarnessName(settings.Voice.Harness)),
        VoiceState.Completed => Strings.Get("VoiceCompleted"),
        VoiceState.Failed => VoiceFailureText(controller),
        _ => string.Empty
    };

    private static string VoiceFailureText(VoiceSessionController controller)
    {
        // Araç kendi hata metnini verdiyse en açıklayıcısı odur (aracın dilinde gelir). Bazı araçlar hatayı stdout'a yazar
        // (ör. claude: "Not logged in · Please run /login"), bu yüzden stderr boşsa stdout'a bakılır.
        if (controller.Failure == VoiceFailure.ExitCode && controller.LastResult is { } result
            && (FirstLine(result.Error) ?? FirstLine(result.Output)) is { } toolLine)
            return toolLine;

        var failure = controller.Failure == VoiceFailure.None ? VoiceFailure.Unexpected : controller.Failure;
        return string.Format(CultureInfo.CurrentCulture, Strings.Get("VoiceError" + failure), controller.FailureDetail);
    }

    private static string? FirstLine(string? text) =>
        text?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() is { Length: > 0 } line
            ? line
            : null;

    private static readonly string VoiceReplyPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Koy", "voice-reply.txt");

    // Yanıtın tamamı çentiğe sığmaz: dosyaya yazılır, çentiğe tıklayınca varsayılan metin düzenleyicide açılır.
    private bool SaveVoiceReply(string prompt, HarnessExecutionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Output) && string.IsNullOrWhiteSpace(result.Error))
            return false;

        try
        {
            var text = new System.Text.StringBuilder()
                .Append("> ").AppendLine(prompt)
                .Append('(').Append(VoiceHarnessName(settings.Voice.Harness)).Append(", ")
                .Append(result.ExitCode.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(result.Duration.TotalSeconds.ToString("F1", CultureInfo.CurrentCulture)).AppendLine(" s)")
                .AppendLine()
                .AppendLine(result.Output);
            if (!string.IsNullOrWhiteSpace(result.Error))
                text.AppendLine().AppendLine("[stderr]").AppendLine(result.Error);

            Directory.CreateDirectory(Path.GetDirectoryName(VoiceReplyPath)!);
            File.WriteAllText(VoiceReplyPath, text.ToString());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLog.Write("Sesli komut yanıtı", ex);
            return false;
        }
    }

    // Yanıtın tamamı çentiğin altındaki kartta okunur; dosya yedek olarak kalır.
    private void ShowVoiceReplyCard(NotchWindowState target, string prompt, HarnessExecutionResult result)
    {
        var body = string.IsNullOrWhiteSpace(result.Error)
            ? result.Output
            : string.IsNullOrWhiteSpace(result.Output) ? result.Error : result.Output + Environment.NewLine + Environment.NewLine + result.Error;
        var info = string.Format(CultureInfo.CurrentCulture, "{0} · {1:F1} s", VoiceHarnessName(settings.Voice.Harness), result.Duration.TotalSeconds);
        var window = target.Window;
        VoiceReplyWindow.ShowReply(window.Left + window.Width / 2, window.Top + 72, prompt, info, body, OpenVoiceReplyFile);
    }

    private void OpenVoiceReply()
    {
        // Kart açıksa çentiğe tıklamak ona odak verir; yoksa yanıt dosyası açılır.
        if (VoiceReplyWindow.FocusCurrent())
            return;

        OpenVoiceReplyFile();
    }

    private void OpenVoiceReplyFile()
    {
        if (!File.Exists(VoiceReplyPath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(VoiceReplyPath) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            CrashLog.Write("Sesli komut yanıtını açma", ex);
        }

        foreach (var (_, windowState) in _windowStates)
        {
            if (windowState.ViewModel.Osd.Type == OsdType.Voice)
                windowState.ViewModel.Osd.Dismiss();
        }
    }

    private static string VoiceHarnessName(VoiceHarnessType harness) => harness switch
    {
        VoiceHarnessType.Agy => "Antigravity",
        VoiceHarnessType.Custom => Strings.Get("VoiceHarnessCustom"),
        _ => harness.ToString()
    };

    private nint _voicePriorForeground;
    private NotchWindowState? _voiceWindow;
    private CancellationTokenSource? _voiceTypingCts;

    // UI iş parçacığında çalışır. Oturum boyunca aynı çentik kullanılır (kutu, odak ve metin tek yerde).
    private void UpdateVoiceOsd(VoiceState state)
    {
        if (_voiceController is null)
            return;

        if (state == VoiceState.Editing)
        {
            VoiceReplyWindow.CloseCurrent();
            _voicePriorForeground = WindowStyles.GetForeground();
            _voiceWindow = PickStateForShortcut();
        }

        if (_voiceWindow is not { } target)
            return;

        if (state == VoiceState.Idle)
        {
            StopVoiceTyping();
            target.ViewModel.Osd.VoiceInputChanged -= OnVoiceInputChanged;
            if (target.ViewModel.Osd.Type == OsdType.Voice)
                target.ViewModel.Osd.Dismiss();
            target.Window.ReleaseVoiceConfirmation(_voicePriorForeground);
            _voicePriorForeground = 0;
            _voiceWindow = null;
            return;
        }

        var text = VoiceStatusText(_voiceController, state);
        var prompt = state == VoiceState.Editing ? string.Empty : _voiceController.Prompt;
        string? detail = state == VoiceState.Failed ? text : null;
        var hasReply = false;
        if (state is VoiceState.Completed or VoiceState.Failed && _voiceController.LastResult is { } result
            && SaveVoiceReply(_voiceController.Prompt, result))
        {
            hasReply = true;
            ShowVoiceReplyCard(target, _voiceController.Prompt, result);
            if (state == VoiceState.Completed)
            {
                // Çentikte yanıtın ilk satırı; tamamı tıklayınca açılan dosyada.
                prompt = FirstLine(result.Output) ?? Strings.Get("VoiceCompleted");
                text = Strings.Get("VoiceReplyOpen");
                detail = result.Output.Length > 600 ? result.Output[..600] + "…" : result.Output;
            }
        }

        target.ViewModel.Osd.ShowVoice(state, text, prompt, detail, linger: hasReply);

        if (state == VoiceState.Editing)
        {
            target.ViewModel.Osd.VoiceInputChanged -= OnVoiceInputChanged;
            target.ViewModel.Osd.VoiceInputChanged += OnVoiceInputChanged;
            target.Window.FocusVoiceConfirmation();
            StartVoiceTyping();
            return;
        }

        // Kutu kapandı: sesle yazma durur, odak kullanıcının önceki penceresine döner.
        StopVoiceTyping();
        target.ViewModel.Osd.VoiceInputChanged -= OnVoiceInputChanged;
        target.Window.ReleaseVoiceConfirmation(_voicePriorForeground);
        if (state is VoiceState.Completed or VoiceState.Failed)
        {
            _voicePriorForeground = 0;
            _voiceWindow = null;
        }
    }

    private void OnVoiceInputChanged(string text) => _voiceController?.UpdatePrompt(text);

    // Odak kutuya geçtikten ve kısayol tuşları bırakıldıktan sonra Windows sesle yazma (Win+H) açılır.
    private void StartVoiceTyping()
    {
        StopVoiceTyping();
        var cts = new CancellationTokenSource();
        _voiceTypingCts = cts;
        _ = OpenVoiceTypingAsync(cts.Token);
    }

    private async Task OpenVoiceTypingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(120, cancellationToken);
            if (_voiceController?.State != VoiceState.Editing)
                return;

            await VoiceTyping.OpenAsync(TimeSpan.FromSeconds(2), cancellationToken);

            // Sesle yazma paneli belirince çentik odağı kaybedebiliyor; odak kutuya geri verilir ki Enter/Esc gelsin.
            // Panel kutuyu izlemeye devam eder (odaktaki metin kutusuna yazar).
            foreach (var delay in new[] { 400, 900 })
            {
                await Task.Delay(delay, cancellationToken);
                if (_voiceController?.State != VoiceState.Editing || _voiceWindow is not { } target)
                    return;
                target.Window.FocusVoiceConfirmation();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CrashLog.Write("Sesle yazma (Win+H)", ex);
        }
    }

    private void StopVoiceTyping()
    {
        _voiceTypingCts?.Cancel();
        _voiceTypingCts?.Dispose();
        _voiceTypingCts = null;
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

    // Güncelleme kartı kararı oturum boyunca korunur: çentik yeniden kurulsa da aynı sürüm ikinci kez çıkmaz.
    private static readonly UpdateCardPolicy s_updateCardPolicy = new();
    private OsdIndicatorViewModel? _cardOsd;
    private NotchViewModel? _cardWaitViewModel;
    private string? _cardVersion;

    private void OnUpdateStatusChanged()
    {
        // Denetim arka plan iş parçacığından bildirir.
        Application.Current?.Dispatcher.InvokeAsync(TryShowUpdateCard);
    }

    /// <summary>Yeni sürüm kartını yalnız birincil ekranın çentiğinde gösterir; çentik açıksa kapanınca dener.</summary>
    private void TryShowUpdateCard()
    {
        if (_stopped || _windowStates.Count == 0)
            return;

        var target = _windowStates.Values.FirstOrDefault(s => s.Monitor.IsPrimary) ?? _windowStates.Values.First();
        var version = UpdateStatus.AvailableVersion;
        var viewModel = target.ViewModel;
        var expanded = viewModel.State == NotchState.Expanded;

        if (!s_updateCardPolicy.ShouldShow(UpdateStatus.HasAvailableUpdate, version, expanded))
        {
            // Çentik açık olduğu için bekleyen kart: kapanınca yeniden sorulur (OSD'nin CanShow kalıbı).
            if (expanded && UpdateStatus.HasAvailableUpdate && !string.IsNullOrWhiteSpace(version)
                && !ReferenceEquals(_cardWaitViewModel, viewModel))
            {
                StopWaitingForCollapse();
                _cardWaitViewModel = viewModel;
                viewModel.PropertyChanged += OnCardWaitViewModelChanged;
            }

            return;
        }

        StopWaitingForCollapse();
        var card = new NotchCard(
            NotchCardKind.Update,
            string.Format(CultureInfo.CurrentCulture, Strings.Get("UpdateCardTitle"), version),
            Strings.Get("UpdateCardSubtitle"),
            Strings.Get("UpdateCardAction"));

        DetachCardOsd();
        _cardOsd = viewModel.Osd;
        _cardOsd.PrimaryActionRequested += OnUpdateCardAction;
        _cardOsd.DismissRequested += OnUpdateCardDismissed;

        if (_cardOsd.ShowCard(card, UpdateCardPolicy.AutoDismissAfter))
        {
            _cardVersion = version;
            s_updateCardPolicy.MarkShown(version!);
        }
        else
        {
            DetachCardOsd();
        }
    }

    private void OnCardWaitViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotchViewModel.State) && sender is NotchViewModel { State: not NotchState.Expanded })
        {
            StopWaitingForCollapse();
            TryShowUpdateCard();
        }
    }

    private void StopWaitingForCollapse()
    {
        if (_cardWaitViewModel is null)
            return;

        _cardWaitViewModel.PropertyChanged -= OnCardWaitViewModelChanged;
        _cardWaitViewModel = null;
    }

    private void DetachCardOsd()
    {
        if (_cardOsd is null)
            return;

        _cardOsd.PrimaryActionRequested -= OnUpdateCardAction;
        _cardOsd.DismissRequested -= OnUpdateCardDismissed;
        _cardOsd = null;
    }

    // Onay, indirme ve görev çubuğunu geri getirip yeniden başlatma (AGENTS #7) KoyWindowViewModel.StartUpdateAsync içinde; kart yalnız çağırır.
    private void OnUpdateCardAction(NotchCard card)
    {
        // Aynı gösterge bildirim kartlarının da sahibi: yalnız güncelleme kartı bu eylemi tetikler.
        if (card.Kind != NotchCardKind.Update)
            return;

        KoyWindow.ShowInstance();
        KoyWindow.Instance.ViewModel.OpenUpdateOverlay();
    }

    private void OnUpdateCardDismissed(NotchCard card)
    {
        if (card.Kind == NotchCardKind.Update && _cardVersion is not null)
            s_updateCardPolicy.Dismiss(_cardVersion);
    }

    // ---- Windows bildirimleri çentikte (#64) ----
    // Paketsiz süreçte olay yok; dinleyici bildirim listesini yoklar. Yalnız ayar açıkken, en az bir çentik varken ve
    // izin Allowed iken kurulur; Stop'ta zamanlayıcı sökülür (AGENTS #5). Kart yalnız birincil ekranın çentiğinde çıkar.

    /// <summary>Bildirim izni durumu değişti (istek sonuçlandı); ayar sayfası satırı yeniler. Arka plan iş parçacığından gelebilir.</summary>
    public static event Action? NotificationAccessChanged;

    private INotificationListener? _notificationListener;
    private readonly NotificationQueue _notificationQueue = new();
    private OsdIndicatorViewModel? _notificationOsd;
    private NotchCard? _notificationCard;
    private NotchViewModel? _notificationWaitViewModel;

    private void StartNotifications()
    {
        if (!settings.Notch.ShowNotifications || _windowStates.Count == 0 || _notificationListener is not null)
            return;

        var listener = services.AcquireNotificationListener();
        _notificationListener = listener;
        _ = EnableNotificationsAsync(listener);
    }

    private async Task EnableNotificationsAsync(INotificationListener listener)
    {
        try
        {
            var status = listener.AccessStatus;
            // Windows izin diyaloğu yalnız durum belirsizken ve ayar açıkken çıkar.
            if (status == NotificationAccessStatus.Unspecified)
                status = await listener.RequestAccessAsync();

            NotificationAccessChanged?.Invoke();

            if (status != NotificationAccessStatus.Allowed || _stopped || !ReferenceEquals(_notificationListener, listener))
                return;

            listener.NotificationArrived += OnNotificationArrived;
            listener.Start();
        }
        catch
        {
            // İzin okunamadı: bildirim kartı yok, çentiğin geri kalanı etkilenmez.
        }
    }

    private void OnNotificationArrived(NotificationItem item)
    {
        // Yoklama thread pool'da çalışır; kuyruğa ve çentiğe yalnız UI iş parçacığından dokunulur.
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (_stopped || _notificationListener is null)
                return;

            var doNotDisturb = _focusAssistService?.IsDoNotDisturb ?? false;
            if (_notificationQueue.Enqueue(item, doNotDisturb))
                PumpNotifications();
        });
    }

    /// <summary>Sıradaki bildirimi gösterir; kart ya da başka bir gösterge sürüyorsa, çentik açıksa bekler.</summary>
    private void PumpNotifications()
    {
        if (_stopped || _windowStates.Count == 0 || _notificationQueue.Current is not null)
            return;

        var target = _windowStates.Values.FirstOrDefault(s => s.Monitor.IsPrimary) ?? _windowStates.Values.First();
        var viewModel = target.ViewModel;
        HookNotificationOsd(viewModel.Osd);

        // Başka gösterge (ses, güncelleme kartı…) sürüyor: Dismissed olayı yeniden dener.
        if (viewModel.Osd.IsActive)
            return;

        if (viewModel.State == NotchState.Expanded)
        {
            if (_notificationQueue.PendingCount > 0 && !ReferenceEquals(_notificationWaitViewModel, viewModel))
            {
                StopWaitingForNotchCollapse();
                _notificationWaitViewModel = viewModel;
                viewModel.PropertyChanged += OnNotificationWaitViewModelChanged;
            }

            return;
        }

        StopWaitingForNotchCollapse();
        var item = _notificationQueue.TryTakeNext(notchExpanded: false);
        if (item is null)
            return;

        var card = NotificationPresentation.ToCard(item, DateTimeOffset.Now, settings.Notch.NotificationsHideBody, GetRelativeTimeLabels());
        _notificationCard = card;
        if (!viewModel.Osd.ShowCard(card, NotificationQueue.CardDuration))
        {
            _notificationCard = null;
            _notificationQueue.Complete();
        }
    }

    private static RelativeTimeLabels GetRelativeTimeLabels() => new(
        Strings.Get("NotificationTimeNow"),
        Strings.Get("NotificationTimeMinutesAgo"),
        Strings.Get("NotificationTimeHoursAgo"));

    private void HookNotificationOsd(OsdIndicatorViewModel osd)
    {
        if (ReferenceEquals(_notificationOsd, osd))
            return;

        UnhookNotificationOsd();
        _notificationOsd = osd;
        osd.Dismissed += OnNotificationOsdDismissed;
        osd.Activated += OnNotificationOsdActivated;
    }

    private void UnhookNotificationOsd()
    {
        if (_notificationOsd is null)
            return;

        _notificationOsd.Dismissed -= OnNotificationOsdDismissed;
        _notificationOsd.Activated -= OnNotificationOsdActivated;
        _notificationOsd = null;
        FinishNotificationCard();
    }

    private void FinishNotificationCard()
    {
        _notificationCard = null;
        _notificationQueue.Complete();
    }

    // Süre doldu, × ile kapandı ya da çentik açıldı: kart bitti, sıradaki bir sonraki turda gelir (kapanış animasyonu başlasın).
    private void OnNotificationOsdDismissed(object? sender, EventArgs e)
    {
        FinishNotificationCard();
        Application.Current?.Dispatcher.InvokeAsync(PumpNotifications, DispatcherPriority.Background);
    }

    // Başka bir gösterge (ses, güncelleme kartı) bildirim kartının yerini aldı: kart düştü, sıradaki o gösterge kapanınca gelir.
    private void OnNotificationOsdActivated(object? sender, EventArgs e)
    {
        if (_notificationQueue.Current is null || _notificationCard is null)
            return;

        if (sender is OsdIndicatorViewModel { Type: OsdType.Card, Card: { } shown } && ReferenceEquals(shown, _notificationCard))
            return;

        FinishNotificationCard();
    }

    private void OnNotificationWaitViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotchViewModel.State) && sender is NotchViewModel { State: not NotchState.Expanded })
        {
            StopWaitingForNotchCollapse();
            PumpNotifications();
        }
    }

    private void StopWaitingForNotchCollapse()
    {
        if (_notificationWaitViewModel is null)
            return;

        _notificationWaitViewModel.PropertyChanged -= OnNotificationWaitViewModelChanged;
        _notificationWaitViewModel = null;
    }

    private void StopNotifications()
    {
        StopWaitingForNotchCollapse();
        UnhookNotificationOsd();
        _notificationQueue.Clear();

        if (_notificationListener is null)
            return;

        _notificationListener.NotificationArrived -= OnNotificationArrived;
        _notificationListener = null;
        services.ReleaseNotificationListener();
    }

    public void Stop()
    {
        _stopped = true;
        UpdateStatus.Changed -= OnUpdateStatusChanged;
        StopWaitingForCollapse();
        DetachCardOsd();
        StopNotifications();
        VoiceReplyWindow.CloseCurrent();
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
        StopVoiceTyping();
        _voiceController?.Dispose();
        _voiceController = null;
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
