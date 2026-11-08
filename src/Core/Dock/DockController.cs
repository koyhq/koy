using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// Sabitlenmiş uygulamalar ve açık pencereleri birleştirip Dock öğelerini yöneten saf mantık denetleyicisi.
/// Hiçbir Windows veya WPF tipi içermez; birim testlerle tamamen sınanabilir.
/// </summary>
public sealed class DockController : IDisposable
{
    private readonly IPinnedAppsService _pinnedAppsService;
    private readonly IWindowTracker _windowTracker;
    private readonly IShellActionService _shellActionService;
    private readonly IFullscreenDetector? _fullscreenDetector;

    private readonly HashSet<nint> _flashingHandles = [];
    private readonly List<string> _stableUnpinnedOrder = [];
    private nint _lastForegroundHandle;
    private List<DockItem> _items = [];
    private bool _isHiddenByFullscreen;
    private readonly RefreshDebouncer? _refreshDebouncer;
    private bool _disposed;

    public event EventHandler? ItemsChanged;
    public event EventHandler<bool>? FullscreenStateChanged;

    public IReadOnlyList<DockItem> Items => _items;

    public bool IsHiddenByFullscreen => _isHiddenByFullscreen;

    public DockController(
        IPinnedAppsService pinnedAppsService,
        IWindowTracker windowTracker,
        IShellActionService shellActionService,
        IFullscreenDetector? fullscreenDetector = null,
        TimeSpan? refreshDebounce = null,
        Action<Action>? post = null,
        TimeProvider? timeProvider = null)
    {
        _pinnedAppsService = pinnedAppsService;
        _windowTracker = windowTracker;
        _shellActionService = shellActionService;
        _fullscreenDetector = fullscreenDetector;

        // Pencere olay fırtınasında (aç/kapa/göster/gizle) her olayda EnumWindows + simge çıkarımı yapılmasın.
        // Verilmezse yenileme eşzamanlıdır (testler ve basit kullanım).
        if (refreshDebounce is { } delay && delay > TimeSpan.Zero)
            _refreshDebouncer = new RefreshDebouncer(Refresh, delay, post, timeProvider);

        _windowTracker.ForegroundWindowChanged += OnForegroundWindowChanged;
        _windowTracker.WindowsChanged += OnWindowsChanged;
        _windowTracker.WindowFlashing += OnWindowFlashing;
    }

    public void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (_disposed) return;

        // Tam yenileme yapılıyor; bekleyen ertelenmiş istek gereksiz kalır.
        _refreshDebouncer?.Cancel();

        CheckFullscreen();

        var pinnedApps = _pinnedAppsService.GetPinnedApps();
        var openWindows = _windowTracker.GetOpenWindows();
        var currentForeground = _windowTracker.CurrentForeground;

        var remainingWindows = new List<OpenWindowInfo>(openWindows);
        var newItems = new List<DockItem>();

        // 1. Sabitli uygulamaları işle ve eşleşen pencereleri bul
        foreach (var pin in pinnedApps)
        {
            var matchingWindows = remainingWindows.FindAll(w => PinnedAppMatcher.IsMatch(pin, w));
            var isRunning = matchingWindows.Count > 0;
            var isActive = matchingWindows.Any(w => currentForeground is not null && w.Handle == currentForeground.Handle);
            var handles = matchingWindows.ConvertAll(w => w.Handle);
            var processId = matchingWindows.Count > 0 ? matchingWindows[0].ProcessId : 0;

            if (isActive)
            {
                foreach (var h in handles)
                    _flashingHandles.Remove(h);
            }

            var isFlashing = handles.Any(h => _flashingHandles.Contains(h));

            foreach (var m in matchingWindows)
                remainingWindows.Remove(m);

            var windowEntries = matchingWindows.ConvertAll(w => new DockWindowEntry(w.Handle, w.Title, w.Bounds));

            newItems.Add(new DockItem(
                Id: pin.ShortcutPath,
                Title: pin.Title,
                FilePath: pin.ShortcutPath,
                TargetPath: pin.TargetPath,
                IconData: pin.IconData,
                IsPinned: true,
                IsRunning: isRunning,
                IsActive: isActive,
                WindowHandles: handles,
                ProcessId: processId,
                IsFlashing: isFlashing,
                BadgeCount: 0,
                Windows: windowEntries,
                // Çalışan pencerenin kimliği öncelikli (ör. Chrome profili); yoksa sabitli kısayolun AUMID'i.
                AppUserModelId: ResolveAppId(matchingWindows) ?? pin.AppUserModelId));
        }

        // 2. Sabitlenmemiş açık pencereleri grupla ve ilk görülme sırasını koru (Alt+Tab zıplamasını engelle)
        var unpinnedGroups = remainingWindows
            .GroupBy(w => !string.IsNullOrEmpty(w.ProcessPath) ? w.ProcessPath : w.ProcessId.ToString())
            .ToList();

        var activeKeys = new HashSet<string>(unpinnedGroups.Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
        _stableUnpinnedOrder.RemoveAll(k => !activeKeys.Contains(k));

        foreach (var group in unpinnedGroups)
        {
            if (!_stableUnpinnedOrder.Contains(group.Key, StringComparer.OrdinalIgnoreCase))
            {
                _stableUnpinnedOrder.Add(group.Key);
            }
        }

        unpinnedGroups.Sort((a, b) =>
        {
            var idxA = _stableUnpinnedOrder.FindIndex(k => string.Equals(k, a.Key, StringComparison.OrdinalIgnoreCase));
            var idxB = _stableUnpinnedOrder.FindIndex(k => string.Equals(k, b.Key, StringComparison.OrdinalIgnoreCase));
            return idxA.CompareTo(idxB);
        });

        foreach (var group in unpinnedGroups)
        {
            var list = group.ToList();
            var first = list[0];
            var isActive = list.Any(w => currentForeground is not null && w.Handle == currentForeground.Handle);
            var handles = list.ConvertAll(w => w.Handle);

            if (isActive)
            {
                foreach (var h in handles)
                    _flashingHandles.Remove(h);
            }

            var isFlashing = handles.Any(h => _flashingHandles.Contains(h));

            var title = !string.IsNullOrWhiteSpace(first.Title)
                ? first.Title
                : Path.GetFileNameWithoutExtension(first.ProcessPath) ?? "App";

            var windowEntries = list.ConvertAll(w => new DockWindowEntry(w.Handle, w.Title, w.Bounds));

            newItems.Add(new DockItem(
                Id: group.Key,
                Title: title,
                FilePath: first.ProcessPath,
                TargetPath: first.ProcessPath,
                IconData: first.IconData,
                IsPinned: false,
                IsRunning: true,
                IsActive: isActive,
                WindowHandles: handles,
                ProcessId: first.ProcessId,
                IsFlashing: isFlashing,
                BadgeCount: 0,
                Windows: windowEntries,
                AppUserModelId: ResolveAppId(list)));
        }

        _items = newItems;
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pencerelerden ilk AppUserModelID (pencerede yoksa bilinen sistem uygulamaları tablosundan). Atlama listesi bu kimliğe
    /// bağlıdır; kimlik yoksa null (exe yolundan kimlik türetilmez).
    /// </summary>
    private static string? ResolveAppId(IEnumerable<OpenWindowInfo> windows)
    {
        foreach (var w in windows)
        {
            var id = !string.IsNullOrEmpty(w.AppUserModelId)
                ? w.AppUserModelId
                : KnownAppUserModelIds.ForWindow(w.ProcessPath, w.ClassName);
            if (!string.IsNullOrEmpty(id))
                return id;
        }

        return null;
    }

    /// <summary>Sabitli uygulamaların sırasını sürükleyerek değiştirebilir miyiz (kaynak sıralamayı destekliyor mu).</summary>
    public bool CanReorderPinned => _pinnedAppsService is IReorderablePinnedApps;

    /// <summary>
    /// Sabitli uygulamayı yeni dizine (yalnız sabitliler arasında sayılan dizin) taşır, sırayı kalıcı yapar ve listeyi yeniler.
    /// </summary>
    public void MovePinned(string itemId, int newPinnedIndex)
    {
        if (_pinnedAppsService is not IReorderablePinnedApps reorderable)
            return;

        var item = _items.Find(i => i.IsPinned && string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            return;

        reorderable.MoveTo(PinnedOrder.IdOf(item.Id), newPinnedIndex);
        Refresh();
    }

    public void HandleItemClick(DockItem item)
    {
        if (!item.IsRunning)
        {
            var target = !string.IsNullOrEmpty(item.TargetPath)
                ? item.TargetPath
                : item.FilePath ?? item.Id;

            _shellActionService.Launch(target);
            return;
        }

        if (item.WindowHandles.Count == 0)
            return;

        foreach (var h in item.WindowHandles)
            _flashingHandles.Remove(h);

        // Halihazırda aktifse küçült, değilse öne getir
        if (item.IsActive)
        {
            _windowTracker.MinimizeWindow(item.WindowHandles[0]);
        }
        else
        {
            _windowTracker.ActivateWindow(item.WindowHandles[0]);
        }
    }

    public void ActivateWindow(nint handle)
    {
        if (handle != 0)
        {
            _flashingHandles.Remove(handle);
            _windowTracker.ActivateWindow(handle);
        }
    }

    public void CloseWindow(nint handle)
    {
        if (handle != 0)
        {
            _flashingHandles.Remove(handle);
            _windowTracker.CloseWindow(handle);
        }
    }

    public void HandleItemMiddleClick(DockItem item)
    {
        var target = !string.IsNullOrEmpty(item.TargetPath)
            ? item.TargetPath
            : item.FilePath ?? item.Id;

        _shellActionService.Launch(target);
    }

    public void HandleItemClose(DockItem item)
    {
        foreach (var handle in item.WindowHandles)
        {
            _windowTracker.CloseWindow(handle);
        }
    }

    public void HandleItemTerminate(DockItem item)
    {
        if (item.ProcessId > 0)
        {
            _windowTracker.TerminateProcess(item.ProcessId);
        }
    }

    public void CheckFullscreen()
    {
        if (_fullscreenDetector is null)
            return;

        var isFullscreen = _fullscreenDetector.IsFullscreen();
        if (_isHiddenByFullscreen != isFullscreen)
        {
            _isHiddenByFullscreen = isFullscreen;
            FullscreenStateChanged?.Invoke(this, isFullscreen);
        }
    }

    private void RequestRefresh()
    {
        if (_refreshDebouncer is null)
            Refresh();
        else
            _refreshDebouncer.Request();
    }

    private void OnWindowFlashing(object? sender, nint handle)
    {
        _flashingHandles.Add(handle);
        RequestRefresh();
    }

    private void OnForegroundWindowChanged(object? sender, WindowInfo e)
    {
        if (e.Handle != _lastForegroundHandle)
        {
            _lastForegroundHandle = e.Handle;
            Refresh();
        }
    }

    private void OnWindowsChanged(object? sender, EventArgs e)
    {
        RequestRefresh();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshDebouncer?.Dispose();

        _windowTracker.ForegroundWindowChanged -= OnForegroundWindowChanged;
        _windowTracker.WindowsChanged -= OnWindowsChanged;
        _windowTracker.WindowFlashing -= OnWindowFlashing;
    }
}
