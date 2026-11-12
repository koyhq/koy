using System.Windows.Threading;
using Koy.Core.Diagnostics;
using Koy.Core.Display;
using Koy.Core.Input;
using Koy.Core.Settings;
using Koy.Platform.Display;
using Koy.Platform.Shell;
using Microsoft.Win32;

namespace Koy.App.Features.Actions;

/// <summary>
/// Ekran köşesi eylemleri. Yoklama yok (AGENTS #5): imleç köşeye girince fare kancası (<see cref="CornerHookService"/>)
/// haber verir, bekleme süresi için yalnız o an tek seferlik bir zamanlayıcı kurulur; köşeden çıkınca durur.
/// Kanca yalnız en az bir köşeye eylem bağlıyken kurulur; hepsi "yok" olunca kanca ve ekran aboneliği bırakılır.
/// Kararlar Core'dadır (<see cref="HotCornerTracker"/>, <see cref="HotCornerMath"/>); burası yalnız zaman ve Win32 okur.
/// Tam ekran uygulama öndeyken ve fare tuşu basılıyken (sürükleme) hiçbir şey tetiklenmez.
/// </summary>
public sealed class HotCornerController : IDisposable
{
    // Zamanlayıcı ile TickCount64 çözünürlüğü farkı yüzünden bekleme bir tık eksik ölçülmesin.
    private static readonly TimeSpan DwellMargin = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly Action<InputAction> _run;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly HotCornerTracker _tracker = new();
    private readonly FullscreenDetector _fullscreen = new();
    private ICornerHookService? _hook;
    private MonitorService? _monitors;
    private DispatcherTimer? _dwell;
    private HotCornersSettings _settings = new();
    private IReadOnlyList<DisplayMonitor> _monitorList = [];
    private CornerHit? _hit;

    public HotCornerController(Action<InputAction> run) => _run = run;

    /// <summary>Kanca kurulu mu (tanılama için).</summary>
    public bool IsRunning => _hook is not null;

    /// <summary>Ayarı uygular: gerekiyorsa kancayı kurar, gerekmiyorsa kaldırır.</summary>
    public void Apply(HotCornersSettings settings)
    {
        _settings = settings;
        StopDwell();
        _tracker.Reset();

        if (CornerActions.IsActive(settings))
            StartTracking();
        else
            StopTracking();
    }

    private void StartTracking()
    {
        if (_hook is not null)
            return;

        _monitors ??= new MonitorService();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _hook = new CornerHookService(action => _dispatcher.BeginInvoke(action));
        _hook.CornerChanged += OnCornerChanged;
        RefreshScreens();
    }

    private void StopTracking()
    {
        StopDwell();
        if (_hook is not null)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _hook.CornerChanged -= OnCornerChanged;
            _hook.Dispose();
            _hook = null;
        }

        _tracker.Reset();
        _hit = null;
        _monitorList = [];
    }

    // SystemEvents kendi iş parçacığından da çağırabilir; ekran listesi arayüz iş parçacığında yenilenir.
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(() => CrashLog.Guard("Ekran köşesi", RefreshScreens));

    private void RefreshScreens()
    {
        if (_hook is null || _monitors is null)
            return;

        _monitorList = _monitors.GetMonitors();
        var rects = _monitorList
            .Select(m => new ScreenRect(m.Bounds.Left, m.Bounds.Top, m.Bounds.Width, m.Bounds.Height))
            .ToList();
        _hook.SetScreens(rects, CornerActions.CornerSizePx);
    }

    private void OnCornerChanged(object? sender, CornerHit? hit) => CrashLog.Guard("Ekran köşesi", () =>
    {
        StopDwell();
        _tracker.Reset();
        _hit = hit;

        // Eylemi olmayan köşe "köşe değil" sayılır.
        if (hit is not { } h || ActionAt(h.Corner) is null)
            return;

        _tracker.Update(h.Corner, false, Environment.TickCount64, _settings.DwellMs);
        StartDwell(TimeSpan.FromMilliseconds(CornerActions.ClampDwell(_settings.DwellMs)) + DwellMargin);
    });

    private void OnDwellElapsed(object? sender, EventArgs e) => CrashLog.Guard("Ekran köşesi", () =>
    {
        StopDwell();
        if (_hit is not { } h || ActionAt(h.Corner) is null)
            return;

        // Tam ekran öndeyse bu girişte hiçbir şey yapılmaz; köşeden çıkıp yeniden girmek gerekir.
        if (_settings.DisableInFullscreen && h.ScreenIndex < _monitorList.Count &&
            _fullscreen.IsFullscreen(_monitorList[h.ScreenIndex].Handle))
            return;

        // Sürükleme köşede bitebilir: tuş bırakılınca bekleme baştan sayılır. Yalnız imleç köşedeyken yeniden kurulur.
        if (CursorHelper.IsAnyMouseButtonDown())
        {
            _tracker.Reset();
            _tracker.Update(h.Corner, false, Environment.TickCount64, _settings.DwellMs);
            StartDwell(TimeSpan.FromMilliseconds(CornerActions.ClampDwell(_settings.DwellMs)) + DwellMargin);
            return;
        }

        var fired = _tracker.Update(h.Corner, false, Environment.TickCount64, _settings.DwellMs);
        if (fired is { } corner && ActionAt(corner) is { } toRun)
            _run(toRun);
        else if (fired is null)
            StartDwell(RetryDelay);
    });

    private InputAction? ActionAt(ScreenCorner corner) =>
        CornerActions.ToInputAction(CornerActions.ActionFor(_settings, corner));

    private void StartDwell(TimeSpan delay)
    {
        _dwell = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = delay };
        _dwell.Tick += OnDwellElapsed;
        _dwell.Start();
    }

    private void StopDwell()
    {
        if (_dwell is null)
            return;

        _dwell.Stop();
        _dwell.Tick -= OnDwellElapsed;
        _dwell = null;
    }

    public void Dispose() => StopTracking();
}
