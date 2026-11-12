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
/// Ekran köşesi eylemleri. Uygulamada hiçbir fare kancası yok; bu yüzden imleç konumu
/// <see cref="CornerActions.PollIntervalMs"/> aralıklı hafif bir yoklamayla okunur. Yoklama yalnız en az bir köşeye
/// eylem bağlıyken çalışır, hepsi "yok" olunca zamanlayıcı durur ve ekran değişikliği aboneliği bırakılır.
/// Kararlar Core'dadır (<see cref="HotCornerTracker"/>, <see cref="HotCornerMath"/>); burası yalnız zaman ve Win32 okur.
/// Tam ekran uygulama öndeyken ve fare tuşu basılıyken (sürükleme) hiçbir şey tetiklenmez.
/// </summary>
public sealed class HotCornerController : IDisposable
{
    private readonly Action<InputAction> _run;
    private readonly HotCornerTracker _tracker = new();
    private readonly FullscreenDetector _fullscreen = new();
    private MonitorService? _monitors;
    private DispatcherTimer? _timer;
    private HotCornersSettings _settings = new();
    private IReadOnlyList<DisplayMonitor> _monitorList = [];
    private IReadOnlyList<ScreenRect> _rects = [];
    private volatile bool _monitorsStale = true;
    private bool _subscribed;

    public HotCornerController(Action<InputAction> run) => _run = run;

    /// <summary>Çalışan bir zamanlayıcı var mı (testler ve tanılama için).</summary>
    public bool IsRunning => _timer is not null;

    /// <summary>Ayarı uygular: gerekiyorsa yoklamayı başlatır, gerekmiyorsa durdurur.</summary>
    public void Apply(HotCornersSettings settings)
    {
        _settings = settings;

        if (CornerActions.IsActive(settings))
            StartPolling();
        else
            StopPolling();
    }

    private void StartPolling()
    {
        if (_timer is not null)
            return;

        _monitors ??= new MonitorService();
        _monitorsStale = true;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _subscribed = true;

        _tracker.Reset();
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(100, CornerActions.PollIntervalMs))
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void StopPolling()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }

        if (_subscribed)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _subscribed = false;
        }

        _tracker.Reset();
        _monitorList = [];
        _rects = [];
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => _monitorsStale = true;

    private void OnTick(object? sender, EventArgs e) => CrashLog.Guard("Ekran köşesi", Tick);

    private void Tick()
    {
        if (!CursorHelper.GetPosition(out var x, out var y))
        {
            _tracker.Reset();
            return;
        }

        if (_monitorsStale && _monitors is not null)
        {
            _monitorList = _monitors.GetMonitors();
            _rects = _monitorList
                .Select(m => new ScreenRect(m.Bounds.Left, m.Bounds.Top, m.Bounds.Width, m.Bounds.Height))
                .ToList();
            _monitorsStale = false;
        }

        var hit = HotCornerMath.HitTest(x, y, _rects, CornerActions.CornerSizePx);
        var action = hit is { } h ? CornerActions.ToInputAction(CornerActions.ActionFor(_settings, h.Corner)) : null;

        // Eylemi olmayan köşe "köşe değil" sayılır. Pahalı denetimler (tuş durumu, tam ekran) yalnız köşedeyken çalışır.
        var suppressed = false;
        if (hit is { } found && action is not null)
        {
            suppressed = CursorHelper.IsAnyMouseButtonDown() ||
                         (_settings.DisableInFullscreen && _fullscreen.IsFullscreen(_monitorList[found.ScreenIndex].Handle));
        }

        var fired = _tracker.Update(action is null ? null : hit?.Corner, suppressed, Environment.TickCount64, _settings.DwellMs);
        if (fired is { } corner && CornerActions.ToInputAction(CornerActions.ActionFor(_settings, corner)) is { } toRun)
            _run(toRun);
    }

    public void Dispose() => StopPolling();
}
