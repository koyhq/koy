using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Koy.Core.Display;
using Koy.Core.Power;
using Koy.Core.Dock;
using Koy.Core.Settings;
using Koy.Core.Shell;
using Koy.Core.Tray;
using Koy.Core.Visibility;
using Koy.Platform.Display;
using Koy.Platform.Media;
using Koy.Platform.Power;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock özelliğinin yaşam döngüsünü (IFeature) yönetir.
/// Dock kapalıyken hiçbir servisi veya hook'u belleğe yüklemez.
/// Açıldığında Windows görev çubuğunu gizler, kapandığında güvenle geri yükler (AGENTS.md #7 & #12).
/// Çoklu monitör ortamında hedef ekranların her biri için bağımsız bir DockWindow örneği yönetir (M6.2).
/// </summary>
public sealed class DockFeature(AppSettings settings, SharedServices services) : IFeature
{
    private sealed class DockWindowState : IDisposable
    {
        public required DisplayMonitor Monitor { get; set; }
        public required DockWindow Window { get; init; }
        // Ayar değişince yerinde yeniden kurulabilir (pencere ve AppBar kaydı aynı kalır): bkz. ApplySettings.
        public required DockViewModel ViewModel { get; set; }
        public required VisibilityController VisibilityController { get; set; }
        public required EdgeRevealTracker EdgeTracker { get; set; }
        public nint Hwnd { get; set; }

        public void Dispose()
        {
            Window.Close();
            ViewModel.Dispose();
            VisibilityController.Dispose();
        }
    }

    private readonly Dictionary<string, DockWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DockWindowState> _windowStates = new(StringComparer.OrdinalIgnoreCase);

    private OrderedPinnedAppsService? _pinnedAppsService;
    private readonly RecentFilesService _recentFiles = new();   // durumsuz; listeyi yalnız yığın açılınca okur
    private WindowTracker? _windowTracker;
    private ShellActionService? _shellActionService;
    private FullscreenDetector? _fullscreenDetector;
    private uint _lastFlyoutCheckPid;
    private bool _lastFlyoutCheckResult;
    private AppBarService? _appBarService;
    private MonitorService? _monitorService;
    private DisplayEvents? _displayEvents;
    private MediaService? _mediaService;
    private IPowerService? _powerService;
    private TrayHostService? _trayService;   // yalnız görev çubuğu gizliyken dolu (ApplyTrayPolicy)
    private DockController? _controller;
    private DispatcherTimer? _edgePeekTimer;

    /// <summary>Açık dock penceresi sayısı. Dock'ta Koy düğmesi göründüğü için bildirim alanı simgesi buna bakar.</summary>
    public int WindowCount => _windowStates.Count;

    /// <summary>Dock pencere sayısı değişti (açıldı, ekran eklendi/çıkarıldı, durdu).</summary>
    public event Action? WindowCountChanged;

    public void Start()
    {
        if (_windows.Count > 0)
            return;

        // Platform servislerini başlat
        _pinnedAppsService = new OrderedPinnedAppsService(
            new PinnedAppsService(),
            () => settings.DockPinnedOrder,
            SavePinnedOrder,
            TaskbandFavoritesReader.Read);
        _windowTracker = services.AcquireWindowTracker();
        _shellActionService = new ShellActionService();
        _fullscreenDetector = new FullscreenDetector();
        _monitorService = new MonitorService();
        _monitorService.Start();
        _monitorService.MonitorsChanged += OnMonitorsChanged;
        _displayEvents = new DisplayEvents(_monitorService);

        _controller = new DockController(
            _pinnedAppsService,
            _windowTracker,
            _shellActionService,
            _fullscreenDetector,
            refreshDebounce: TimeSpan.FromMilliseconds(120),
            post: action => Application.Current?.Dispatcher.InvokeAsync(action));

        // Yalnızca ReserveSpace (Yer ayır / tam genişlikte "Her zaman göster") modunda masaüstü çalışma alanından (AppBar) yer ayır
        if (EffectiveProximity == DockProximityMode.ReserveSpace)
        {
            _appBarService = new AppBarService();
        }

        // Medya servisi (Tam genişlik Now Playing hapı için); Notch ile paylaşılan tek örnek
        _mediaService = services.AcquireMediaService();

        // Pil tasarrufu için güç durumu; ayar kapalıysa servis kurulmaz.
        if (settings.BatterySaverReduceMotion)
        {
            _powerService = services.AcquirePowerService();
            _powerService.PowerChanged += OnPowerChanged;
            ApplyReduceMotion();
        }

        // Dock, ayarlarda "dock açık" olan her monitörde ayrı bir pencere olarak kurulur.
        var targetMonitors = ResolveTargetMonitors(_monitorService.GetMonitors());

        foreach (var monitor in targetMonitors)
        {
            CreateDockWindow(monitor);
        }

        // Önce tepsi, sonra görev çubuğu: Windows'un tepsisi gizlenmeden Koy'un tepsisi hazır olsun.
        ApplyTrayPolicy();

        // Görev çubuğu yalnız en az bir dock penceresi varken gizlenir (aksi halde hiçbir ekranda görev çubuğu kalmazdı).
        ApplyTaskbarPolicy();

        // 100 ms kenar gözetleme (peek) zamanlayıcısı (yalnız dock gizliyken UI thread'de çalışır)
        _edgePeekTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _edgePeekTimer.Tick += (_, _) => CheckEdgePeek();

        _windowTracker.ForegroundWindowChanged += OnForegroundWindowChanged;
        _windowTracker.WindowsChanged += OnWindowsChanged;

        // İzleyici paylaşımlı: Notch onu önceden başlattıysa ilk ön plan olayı bir daha gelmez, mevcut durumu elle uygula.
        var trackerWasRunning = _windowTracker.IsStarted;
        _windowTracker.Start();
        if (trackerWasRunning && _windowTracker.CurrentForeground is { } currentForeground)
            OnForegroundWindowChanged(this, currentForeground);

        // Hide() sonrası Gezgin görev çubuğunu bir kez daha gösterebiliyor; açılışta hemen yakala.
        EnsureTaskbarHidden();
        _controller.Start();

        UpdateEdgeTimer();
        WindowCountChanged?.Invoke();
    }

    /// <summary>
    /// Sabitli uygulama sırasını ve klasör tanımlarını ayar dosyasına yazar.
    /// Sıra ve klasörler birlikte tek Save çağrısıyla kaydedilir; Koy penceresinin kaydettiği öteki ayarlar ezilmez.
    /// </summary>
    public static void SavePinnedOrder(
        IReadOnlyList<string> order,
        IReadOnlyList<DockFolder>? folders = null,
        AppSettings? inMemorySettings = null)
    {
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var current = store.Load();
        var updated = current with { DockPinnedOrder = [.. order] };
        if (folders is not null)
        {
            updated = updated with { DockFolders = [.. folders] };
            if (inMemorySettings is not null)
                inMemorySettings.DockFolders = [.. folders];
        }
        store.Save(updated);
        if (inMemorySettings is not null)
            inMemorySettings.DockPinnedOrder = [.. order];
    }

    private void SavePinnedOrder(IReadOnlyList<string> order) =>
        SavePinnedOrder(order, null, settings);

    /// <summary>
    /// Explorer yeniden başlayınca yeni görev çubuğu görünür ve varsayılan durumda gelir; gizlemeyi yeniden uygular.
    /// TaskbarService.Hide idempotent olduğundan kullanıcının asıl görev çubuğu durumu korunur.
    /// Yer ayır modunda AppBar kayıtları da Explorer'la birlikte yok olur; çalışma alanı yeniden ayrılır.
    /// </summary>
    public void ReapplyTaskbarHiding()
    {
        if (_windowStates.Count == 0)
            return;

        if (settings.HideWindowsTaskbar)
        {
            TaskbarService.Hide();
            // Yeni Gezgin kendi tepsisini oluşturdu: Koy'unki yeniden öne alınır, uygulamalar simgelerini yeniden kaydeder.
            _trayService?.EnsureFirst();
        }

        if (_appBarService is null)
            return;

        foreach (var (_, state) in _windowStates)
        {
            if (state.Hwnd == 0)
                continue;

            var dpiScale = state.Monitor.DpiScaleY;
            _appBarService.Reregister(state.Hwnd, state.Window.ReservedHeight, dpiScale);
        }
    }

    private bool _taskbarHidden;

    /// <summary>Stile göre geçerli yakınlık modu (tam genişlikte Aşağı yapış/Çekil sadeleşir; bkz. DockProximityMapping).</summary>
    private DockProximityMode EffectiveProximity => DockProximityMapping.Effective(settings.DockStyle, settings.DockProximity);

    private void EnsureTaskbarHidden()
    {
        if (_stopping)
            return;

        if (settings.HideWindowsTaskbar && _windowStates.Count > 0)
        {
            // Görev çubuğu yeniden göründüyse gizlendi: tepsi sahipliği (elle geri getirmede bırakılmıştı) geri alınır.
            if (TaskbarService.EnsureHidden() && _trayReleasedByUser)
                _trayReleasedByUser = false;

            ApplyTrayPolicy();

            // Gezgin görev çubuğunu yeniden gösterip gizleyince kendi penceresini öne alabilir; tepsi geri alınır.
            _trayService?.EnsureFirst();
        }
    }

    // "Windows görev çubuğunu geri getir" düğmesi dock çalışırken basıldı: görev çubuğu görünürken tepsi sahipliği alınmaz.
    private bool _trayReleasedByUser;

    /// <summary>
    /// Kullanıcı görev çubuğunu elle geri getirdi (Dock / Hakkında sayfasındaki kurtarma düğmesi). Görev çubuğu
    /// görünürken Windows'un tepsisine dokunulmaz: Koy'un tepsi penceresi bırakılır. Görev çubuğu dock tarafından
    /// yeniden gizlenirse tepsi geri alınır.
    /// </summary>
    public void OnTaskbarRestoredManually()
    {
        _trayReleasedByUser = true;
        if (_trayService is not null)
            ReleaseTray();
    }

    /// <summary>
    /// Dock penceresi varken Windows görev çubuğunu (birincil ve bütün ikincil monitörlerdeki) gizler; hiç dock
    /// penceresi kalmadıysa (ör. dock her ekranda kapatıldı) geri getirir. Kullanıcı "görev çubuğunu gizle"yi
    /// kapattıysa hiç dokunulmaz.
    /// </summary>
    private void ApplyTaskbarPolicy()
    {
        if (!settings.HideWindowsTaskbar)
            return;

        if (_windowStates.Count > 0)
        {
            TaskbarService.Hide();
            _taskbarHidden = true;
            _trayService?.EnsureFirst();
        }
        else if (_taskbarHidden)
        {
            TaskbarService.Restore();
            _taskbarHidden = false;
        }
    }

    /// <summary>
    /// Koy'un tepsi penceresini yalnız Windows görev çubuğu gerçekten gizliyken (ayar açık ve en az bir dock penceresi
    /// var) kurar; koşul bozulunca (son dock penceresi kapandı, ayar değişti) bırakır. Bütün monitörlerdeki dock'lar
    /// SharedServices'teki aynı örneği paylaşır.
    /// </summary>
    private void ApplyTrayPolicy()
    {
        // Durdurulan dock tepsiyi bir daha almaz. Stop sırasında tepsi bırakılırken arayüz iş parçacığı mesaj
        // işler (tepsi iş parçacığı beklenir); pencere olayları buraya gelip pencereleri henüz silinmemiş dock'a
        // yeni bir tepsi penceresi kurduruyordu.
        if (_stopping)
            return;

        var shouldHost = !_trayReleasedByUser
            && TrayHostPolicy.ShouldHost(settings.HideWindowsTaskbar, settings.DockShowTray, _windowStates.Count);

        var now = DateTime.UtcNow;
        var action = _trayRecovery.Decide(shouldHost, _trayService is not null, _trayService is { IsRunning: true }, now);

        // Ölü tutamaç (tepsi iş parçacığı beklenmedik kapandı) bırakılır: düğme ölü pencereye bağlı görünmez, sayaç dengelenir.
        if (action is TrayHostAction.Release or TrayHostAction.Reacquire)
            ReleaseTray();

        if (action is TrayHostAction.Acquire or TrayHostAction.Reacquire)
        {
            _trayService = services.AcquireTrayHost();
            _trayRecovery.ReportAcquire(_trayService is not null, now);
            if (_trayService is not null)
            {
                _trayService.Exited += OnTrayHostExited;
                foreach (var state in _windowStates.Values)
                    state.ViewModel.SetTrayService(_trayService);
            }
        }

        // Kurulum başarısız ya da bekleme sürüyor: yeni pencere olayı beklemeden bir kez yeniden denenir (zamanlayıcı tek atımlık).
        if (shouldHost && _trayService is null && _trayRecovery.Failures > 0 && _trayRetryTimer is null)
            ScheduleTrayRetry();
    }

    private readonly TrayHostRecovery _trayRecovery = new();
    private DispatcherTimer? _trayRetryTimer;

    // Tepsi iş parçacığı pencereyi biz istemeden kapattı: arayüz iş parçacığında yeniden kurulur (düğme geri gelir).
    private void OnTrayHostExited(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(() =>
        {
            if (ReferenceEquals(sender, _trayService))
                ApplyTrayPolicy();
        }, DispatcherPriority.Background);
    }

    // Başarısız kurulumdan sonra tek atımlık yeniden deneme (bekleme süresi TrayHostRecovery'den); boşta zamanlayıcı yok.
    private void ScheduleTrayRetry()
    {
        var delay = TrayHostRecovery.FirstRetryDelay * Math.Pow(2, Math.Min(_trayRecovery.Failures - 1, 4)) + TimeSpan.FromMilliseconds(100);
        _trayRetryTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = delay };
        _trayRetryTimer.Tick += (_, _) =>
        {
            _trayRetryTimer?.Stop();
            _trayRetryTimer = null;
            ApplyTrayPolicy();
        };
        _trayRetryTimer.Start();
    }

    private void ReleaseTray()
    {
        // Düğme ve açık pencere önce kapanır; sonra Koy'un Shell_TrayWnd penceresi yok edilir ve Windows tepsisi tek başına kalır.
        foreach (var state in _windowStates.Values)
            state.ViewModel.SetTrayService(null);

        if (_trayService is not null)
            _trayService.Exited -= OnTrayHostExited;

        _trayRetryTimer?.Stop();
        _trayRetryTimer = null;
        _trayService = null;
        services.ReleaseTrayHost();
    }

    private IReadOnlyList<DisplayMonitor> ResolveTargetMonitors(IReadOnlyList<DisplayMonitor> monitors) =>
        MonitorPolicy.DockMonitors(MonitorPolicy.Resolve(settings.Monitors, settings.NewScreenDefault, monitors));

    private void CreateDockWindow(DisplayMonitor monitor)
    {
        var visibilityController = new VisibilityController(settings.DockVisibility);
        var viewModel = CreateViewModel(visibilityController);

        var window = new DockWindow { DataContext = viewModel };
        window.SetTargetMonitor(monitor);

        var state = new DockWindowState
        {
            Monitor = monitor,
            Window = window,
            ViewModel = viewModel,
            VisibilityController = visibilityController,
            EdgeTracker = NewEdgeTracker()
        };

        // Dock gizlenince / görününce kenar zamanlayıcısı açılıp kapanır (gizli değilken fare izlenmez).
        viewModel.EdgeWatchChanged += (_, _) => UpdateEdgeTimer();

        window.SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            state.Hwnd = hwnd;
            state.VisibilityController.NotchHandle = hwnd;

            if (_fullscreenDetector is not null)
            {
                _fullscreenDetector.IgnoredHandles.Add(hwnd);
            }

            if (_windowTracker is not null)
            {
                _windowTracker.RegisterShellHook(hwnd);
                window.WmShellHook = _windowTracker.ShellHookMessage;
                window.ShellHookHandler = (wParam, lParam) => _windowTracker.HandleShellHookMessage(wParam, lParam);
            }

            if (_appBarService is not null)
            {
                var dpiScale = state.Monitor.DpiScaleY;
                var height = window.ActualHeight > 0 ? window.ReservedHeight : DockPositionResolver.SnappedHeightDip;
                _appBarService.Register(hwnd, height, dpiScale);
            }

            UpdateDockBounds(window, state.VisibilityController);
        };

        window.SizeChanged += (s, e) =>
        {
            UpdateDockBounds(window, state.VisibilityController);

            if (_appBarService is not null && state.Hwnd != 0 && e.NewSize.Height > 0)
            {
                var dpiScale = state.Monitor.DpiScaleY;
                _appBarService.UpdatePosition(state.Hwnd, window.ReservedHeight, dpiScale);
            }
        };

        window.LocationChanged += (s, e) =>
        {
            UpdateDockBounds(window, state.VisibilityController);
        };

        // DPI değişince DIP yükseklik aynı kalır (SizeChanged gelmez) ama piksel karşılığı değişir: AppBar payı yenilenir.
        window.DpiChanged += (s, e) =>
        {
            UpdateDockBounds(window, state.VisibilityController);
            if (_appBarService is not null && state.Hwnd != 0 && window.ActualHeight > 0)
                _appBarService.UpdatePosition(state.Hwnd, window.ReservedHeight, state.Monitor.DpiScaleY);
        };

        window.DisplayChangeHandler = () =>
        {
            _monitorService?.NotifyDisplayChanged();
        };

        _windowStates[monitor.Id] = state;
        _windows[monitor.Id] = window;

        window.Show();
    }

    private DockViewModel CreateViewModel(VisibilityController visibilityController) =>
        new(settings, _controller!, _shellActionService!, visibilityController, status: null,
            mediaService: _mediaService, trayService: _trayService, recentFiles: _recentFiles)
        {
            ReduceMotion = _reduceMotion
        };

    // Çekil modunun kendi gecikmesi (DockDodgeDelayMs) ViewModel'de işler; çifte beklemesin.
    private EdgeRevealTracker NewEdgeTracker() => new(
        EffectiveProximity == DockProximityMode.Dodge ? TimeSpan.Zero : EdgeRevealTracker.DefaultDelay);

    /// <summary>
    /// Ayarlar değişti: çalışan dock'a YERİNDE uygulanır. Dock'u durdurup başlatmak Windows görev çubuğunu geri getirir,
    /// AppBar kaydını siler ve çalışma alanını oynatırdı (büyütülmüş pencereler sıçrıyordu). Burada pencereler, AppBar
    /// kaydı ve görev çubuğunun gizli durumu korunur; yalnız değişenler (Core: <see cref="DockSettingsDiff"/>) yeniden kurulur.
    /// </summary>
    public void ApplySettings(AppSettings next)
    {
        if (_stopping || _windowStates.Count == 0 && _windows.Count == 0)
            return;

        var diff = DockSettingsDiff.Compare(settings, next);
        settings = next;
        if (diff.IsEmpty)
            return;

        if (diff.ReconcileMonitors && _monitorService is not null)
            OnMonitorsChanged(this, EventArgs.Empty);

        if (diff.ReconcilePower)
            ReconcilePower();

        if (diff.RebuildViewModels)
        {
            foreach (var state in _windowStates.Values)
                RebuildViewModel(state);
        }

        if (diff.ReconcileAppBar)
            ReconcileAppBar();

        if (diff.ReconcileTray || diff.ReconcileTaskbar)
        {
            // Görev çubuğu geri getirilmeden önce Koy'un tepsi penceresi bırakılır; gizlenirken önce tepsi kurulur.
            ApplyTrayPolicy();
            if (settings.HideWindowsTaskbar)
            {
                ApplyTaskbarPolicy();
            }
            else if (_taskbarHidden)
            {
                TaskbarService.Restore();
                _taskbarHidden = false;
            }
        }

        UpdateEdgeTimer();

        if (_windowTracker?.CurrentForeground is { } fg && fg.Handle != 0)
            RecheckForegroundState(fg);
    }

    private void RebuildViewModel(DockWindowState state)
    {
        var oldViewModel = state.ViewModel;
        var oldVisibility = state.VisibilityController;

        var visibility = new VisibilityController(settings.DockVisibility) { NotchHandle = state.Hwnd };
        var viewModel = CreateViewModel(visibility);
        viewModel.EdgeWatchChanged += (_, _) => UpdateEdgeTimer();

        state.VisibilityController = visibility;
        state.ViewModel = viewModel;
        state.EdgeTracker = NewEdgeTracker();

        state.Window.SetViewModel(viewModel);

        oldViewModel.Dispose();
        oldVisibility.Dispose();
    }

    /// <summary>AppBar kaydı yalnız etkin mod "Yer ayır" iken vardır; var olan kayıt silinip yeniden kurulmaz, yalnız payı güncellenir.</summary>
    private void ReconcileAppBar()
    {
        if (EffectiveProximity == DockProximityMode.ReserveSpace)
        {
            _appBarService ??= new AppBarService();
            foreach (var state in _windowStates.Values)
            {
                if (state.Hwnd == 0)
                    continue;

                var height = state.Window.ActualHeight > 0 ? state.Window.ReservedHeight : DockPositionResolver.SnappedHeightDip;
                _appBarService.Register(state.Hwnd, height, state.Monitor.DpiScaleY);   // kayıtlıysa dokunmaz
                _appBarService.UpdatePosition(state.Hwnd, height, state.Monitor.DpiScaleY);
            }
        }
        else if (_appBarService is not null)
        {
            foreach (var state in _windowStates.Values)
            {
                if (state.Hwnd != 0)
                    _appBarService.Unregister(state.Hwnd);
            }

            _appBarService.Dispose();
            _appBarService = null;
        }
    }

    private void ReconcilePower()
    {
        if (settings.BatterySaverReduceMotion && _powerService is null)
        {
            _powerService = services.AcquirePowerService();
            _powerService.PowerChanged += OnPowerChanged;
            ApplyReduceMotion();
        }
        else if (!settings.BatterySaverReduceMotion && _powerService is not null)
        {
            _powerService.PowerChanged -= OnPowerChanged;
            _powerService = null;
            services.ReleasePowerService();
            _reduceMotion = false;
            foreach (var state in _windowStates.Values)
                state.ViewModel.ReduceMotion = false;
        }
    }

    private bool _reduceMotion;

    private void OnPowerChanged(object? sender, PowerChangedEventArgs e)
    {
        // WinRT güç olayları UI dışı thread'den gelir; durum ve pencere nesnelerine UI thread'inde dokunulur.
        Application.Current?.Dispatcher.InvokeAsync(ApplyReduceMotion);
    }

    private void ApplyReduceMotion()
    {
        if (_powerService is null)
            return;

        _reduceMotion = BatterySaverPolicy.ShouldReduceMotion(
            settings.BatterySaverReduceMotion, _powerService.HasBattery, _powerService.Status);
        foreach (var state in _windowStates.Values)
            state.ViewModel.ReduceMotion = _reduceMotion;
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
                if (state.Hwnd != 0)
                {
                    _appBarService?.Unregister(state.Hwnd);
                    _windowTracker?.DeregisterShellHook(state.Hwnd);
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
                UpdateDockBounds(existingState.Window, existingState.VisibilityController);
            }
            else
            {
                CreateDockWindow(monitor);
            }
        }

        // Windows Explorer yeni monitör için ikincil görev çubuğu oluşturmuş olabilir (Shell_SecondaryTrayWnd);
        // Hide() ikincil çubukları da kapsar. Son dock penceresi kapandıysa görev çubuğu geri gelir
        // ve (tepsi önce bırakılır) Koy'un tepsi penceresi de kapanır.
        _trayRecovery.ResetFailures();
        ApplyTrayPolicy();
        ApplyTaskbarPolicy();
        UpdateEdgeTimer();
        WindowCountChanged?.Invoke();
    }

    private void OnWindowsChanged(object? sender, EventArgs e)
    {
        // Bir pencere gösterildiğinde (görev çubuğu dahil) tetiklenir; görev çubuğu yeniden göründüyse gizle.
        EnsureTaskbarHidden();

        if (_windowTracker?.CurrentForeground is { } fg && fg.Handle != 0)
        {
            RecheckForegroundState(fg);
        }
    }

    private static void UpdateDockBounds(DockWindow window, VisibilityController visibilityController)
    {
        if (window.ActualWidth <= 0)
            return;

        var source = PresentationSource.FromVisual(window);
        var dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? (window.TargetMonitor?.DpiScaleX ?? 1.0);
        var dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? (window.TargetMonitor?.DpiScaleY ?? 1.0);
        var left = (int)(window.Left * dpiX);
        var top = (int)(window.Top * dpiY);
        visibilityController.NotchBounds = new WindowRect(
            left,
            top,
            left + (int)(window.ActualWidth * dpiX),
            top + (int)(window.ActualHeight * dpiY));
    }

    private void OnForegroundWindowChanged(object? sender, WindowInfo info)
    {
        var pid = WindowStyles.GetWindowProcessId(info.Handle);

        // Harici bir pencereye geçildiğinde açık context menüyü kapat
        if (pid != (uint)Environment.ProcessId)
        {
            DockContextMenuManager.CloseActive();
        }

        // Gezgin kendi görev çubuğunu öne alırsa yeni tepsi kayıtları Koy'a gelmez; olay anında yoklanır, zamanlayıcı yok.
        _trayService?.EnsureFirst();

        RecheckForegroundState(info);
    }

    private void RecheckForegroundState(WindowInfo info)
    {
        if (_fullscreenDetector is null)
            return;

        if (info.Handle == 0)
        {
            SetShellFlyoutOpen(0);
            return;
        }

        // Koy'un kendi pencereleri (sağ alt menü, Koy penceresi, dock) ön plana gelince dock çekilmez ve son dış
        // pencerenin durumu korunur. Bu denetim burada durur: pencere gösterilince (OnWindowsChanged) de buraya gelinir;
        // önceden yalnız ön plan olayında vardı ve Koy menüsü, açıldığı ekranın dock'unu "üstüne binen pencere" sanıp gizletiyordu.
        var pid = WindowStyles.GetWindowProcessId(info.Handle);
        if (pid == (uint)Environment.ProcessId)
            return;

        var fgMon = WindowStyles.GetWindowMonitor(info.Handle);

        // Başlat / Arama / bildirim merkezi: Windows görev çubuğu gibi dock o ekranda görünür (açılır pencere dock
        // bölgesine taşsa da çekilmez). Öteki ekranlar ve son dış pencere durumu olduğu gibi kalır.
        if (IsShellFlyout(pid))
        {
            SetShellFlyoutOpen(fgMon);
            UpdateEdgeTimer();
            return;
        }

        SetShellFlyoutOpen(0);

        var isMaximized = WindowStyles.IsWindowMaximized(info.Handle);

        foreach (var (_, state) in _windowStates)
        {
            // Pencere bu monitörde değilse bu monitör için büyütülmüş sayılmaz (M6.2: Yan ekrandaki durum burayı etkilemez)
            var onThisMonitor = DockForegroundFilter.AppliesToDock(fgMon, state.Monitor.Handle);
            var monitorMaximized = isMaximized && onThisMonitor;

            // Pencere yalnız kendi ekranının dock'unu etkiler; yan ekrandaki büyütülmüş pencerenin kenar taşması
            // bu dock'u yapışık moda geçirmesin (DockForegroundFilter).
            var bounds = DockForegroundFilter.BoundsForDock(info.Bounds, fgMon, state.Monitor.Handle);

            // Her monitörün kendi tam ekran durumu sorgulanır:
            // Yan ekranda tam ekran uygulama olması bu monitördeki Koy'u gizlemez!
            var isFullscreen = _fullscreenDetector.IsFullscreen(state.Monitor.Handle);
            state.VisibilityController.SetFullscreen(isFullscreen);
            state.VisibilityController.UpdateForegroundWindow(onThisMonitor ? info.Handle : 0, bounds);

            var boundsDip = bounds.IsEmpty
                ? WindowRect.Empty
                : new WindowRect(
                    (int)(bounds.Left / state.Monitor.DpiScaleX),
                    (int)(bounds.Top / state.Monitor.DpiScaleY),
                    (int)(bounds.Right / state.Monitor.DpiScaleX),
                    (int)(bounds.Bottom / state.Monitor.DpiScaleY));

            state.ViewModel.UpdateForegroundWindow(boundsDip, monitorMaximized, isFullscreen);

            if (!isFullscreen && !monitorMaximized && state.Hwnd != 0 && info.Handle != state.Hwnd)
            {
                // Uygulamanın bağlam menüsü açıkken dock kendini öne almaz: menü dock'un arkasında kalırdı
                // (menü kapanınca ön plan yine değişir ve dock öne alınır).
                if (!FullscreenDetector.IsScreenClippingHost(info.Handle) && !PopupMenuProbe.AnyVisibleMenu())
                {
                    WindowStyles.ReassertTopmost(state.Hwnd);
                }
            }
        }

        UpdateEdgeTimer();
    }

    /// <summary>Kabuk açılır penceresi yalnız verilen monitörde açık sayılır; 0 = hiçbirinde.</summary>
    private void SetShellFlyoutOpen(nint monitorHandle)
    {
        foreach (var (_, state) in _windowStates)
            state.ViewModel.SetShellFlyoutOpen(monitorHandle != 0 && state.Monitor.Handle == monitorHandle);
    }

    /// <summary>Ön plan süreci kabuk açılır penceresi mi; süreç adı ön plan değişiminde bir kez okunur, son sonuç saklanır.</summary>
    private bool IsShellFlyout(uint pid)
    {
        if (pid == 0)
            return false;
        if (pid == _lastFlyoutCheckPid)
            return _lastFlyoutCheckResult;

        bool result;
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            result = ShellFlyouts.IsFlyoutProcess(process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            result = false;
        }

        _lastFlyoutCheckPid = pid;
        _lastFlyoutCheckResult = result;
        return result;
    }

    private void UpdateEdgeTimer()
    {
        if (_edgePeekTimer is null || _windowStates.Count == 0)
            return;

        // Dodge/AlwaysHidden/akıllı görünürlük kenarı hep izler; büyütülmüş pencere dock'u gizlediyse
        // (NeedsEdgeWatch) ve kenardan açma açıksa yalnız o sürede izlenir.
        var shouldRun = _windowStates.Values.Any(s =>
            ((EffectiveProximity is DockProximityMode.Dodge or DockProximityMode.AlwaysHidden ||
              settings.DockVisibility != VisibilityMode.Fixed) &&
             EffectiveProximity != DockProximityMode.ReserveSpace) ||
            (settings.DockEdgeReveal && s.ViewModel.NeedsEdgeWatch));

        if (shouldRun)
        {
            if (!_edgePeekTimer.IsEnabled)
                _edgePeekTimer.Start();
        }
        else
        {
            if (_edgePeekTimer.IsEnabled)
                _edgePeekTimer.Stop();
        }
    }

    private void CheckEdgePeek()
    {
        // Savunma: UI thread dışından çağrılırsa (VerifyAccess çökmesi, 4.11 23:18) UI'a taşı.
        if (Application.Current is { } app && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(CheckEdgePeek, DispatcherPriority.Background);
            return;
        }

        if (_windowTracker is null || _windowStates.Count == 0)
            return;

        if (_windowTracker.GetCursorPosition(out var x, out var y))
        {
            var buttonDown = CursorHelper.IsAnyMouseButtonDown();
            var now = TimeSpan.FromMilliseconds(Environment.TickCount64);

            foreach (var (_, state) in _windowStates)
            {
                // Tam ekranda (oyun, F11, tam ekran video) kenar tetiği yoktur; bu monitörün bayrağı da temizlenir.
                if (state.VisibilityController.IsFullscreen)
                {
                    state.EdgeTracker.Reset();
                    state.ViewModel.SetMouseAtBottom(false);
                    continue;
                }

                // Monitör başına: yalnız imlecin bulunduğu ekran. Fare kenarda ~300 ms durunca ve tuş basılı değilken açılır.
                var monitor = state.Monitor;
                var inZone = EdgeRevealTracker.IsInZone(
                    x, y, monitor.Bounds.Left, monitor.Bounds.Right, monitor.Bounds.Bottom,
                    revealed: state.ViewModel.IsVisible, dpiScaleY: monitor.DpiScaleY);

                state.ViewModel.SetMouseAtBottom(state.EdgeTracker.Update(inZone, buttonDown, now));
            }
        }
    }

    public void Stop()
    {
        try
        {
            StopCore();
        }
        finally
        {
            // Güvenlik kuralı: Kapanışta Windows görev çubuğu her durumda geri getirilir (yukarıdaki temizlik hata verse bile).
            TaskbarService.Restore();
        }
    }

    // Stop başladı: tepsi ve görev çubuğu kararları artık uygulanmaz (yeniden giriş korunması).
    private bool _stopping;

    private void StopCore()
    {
        _stopping = true;
        _edgePeekTimer?.Stop();
        _edgePeekTimer = null;
        _trayRetryTimer?.Stop();
        _trayRetryTimer = null;

        // Paylaşılan servisleri yalnız bırak; son kullanıcı bırakınca SharedServices Dispose eder.
        if (_mediaService is not null)
        {
            services.ReleaseMediaService();
            _mediaService = null;
        }

        // Koy'un Shell_TrayWnd penceresi kapanır; uygulamalar yeniden doğrudan Windows tepsisine yazar.
        // Bütün kayıtlar zaten Windows'a da iletildiği için tepside eksik simge kalmaz (TrayHostService açıklaması).
        // Görev çubuğu geri getirilmeden (aşağıdaki Restore) önce yapılır.
        if (_trayService is not null)
            ReleaseTray();

        if (_monitorService is not null)
        {
            _monitorService.MonitorsChanged -= OnMonitorsChanged;
        }

        if (_appBarService is not null)
        {
            foreach (var (_, state) in _windowStates)
            {
                if (state.Hwnd != 0)
                    _appBarService.Unregister(state.Hwnd);
            }
            _appBarService.Dispose();
            _appBarService = null;
        }

        if (_windowTracker is not null)
        {
            foreach (var (_, state) in _windowStates)
            {
                if (state.Hwnd != 0)
                    _windowTracker.DeregisterShellHook(state.Hwnd);
            }
            _windowTracker.ForegroundWindowChanged -= OnForegroundWindowChanged;
            _windowTracker.WindowsChanged -= OnWindowsChanged;
            _windowTracker = null;
            services.ReleaseWindowTracker();
        }

        foreach (var (_, state) in _windowStates)
        {
            state.Dispose();
        }
        _windowStates.Clear();
        _windows.Clear();
        WindowCountChanged?.Invoke();

        _displayEvents?.Dispose();
        _displayEvents = null;
        _monitorService?.Dispose();
        _monitorService = null;

        if (_powerService is not null)
        {
            _powerService.PowerChanged -= OnPowerChanged;
            _powerService = null;
            services.ReleasePowerService();
        }

        _controller?.Dispose();
        _controller = null;

        _fullscreenDetector = null;
        _pinnedAppsService = null;
        _shellActionService = null;
    }
}
