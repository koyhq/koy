using Koy.Core.Dock;
using Koy.Core.Shell;
using Xunit;

namespace Koy.Core.Tests.Dock;

public sealed class DockWindowBehaviorTests
{
    private static readonly WindowRect Left = new(0, 0, 1920, 1080);
    private static readonly WindowRect Right = new(1920, 0, 3840, 1080);

    private static DockWindowEntry Win(long handle, WindowRect bounds) => new((nint)handle, "t" + handle, bounds);

    private static DockItem Item(string id, bool pinned, bool active, params DockWindowEntry[] windows) => new(
        Id: id, Title: id, FilePath: id, TargetPath: id, IconData: null,
        IsPinned: pinned, IsRunning: windows.Length > 0, IsActive: active,
        WindowHandles: windows.Select(w => w.Handle).ToList(),
        ProcessId: windows.Length > 0 ? 7 : 0, IsFlashing: true, BadgeCount: 3, Windows: windows);

    // --- Click policy ---

    [Fact]
    public void MinimizeIfActive_minimizes_when_active_else_activates()
    {
        nint[] handles = [10, 20];

        Assert.Equal(new DockClickPlan(DockClickKind.Minimize, 10),
            DockClickPolicy.DecideLeftClick(DockLeftClickAction.MinimizeIfActive, handles, 20, isActive: true));
        Assert.Equal(new DockClickPlan(DockClickKind.Activate, 10),
            DockClickPolicy.DecideLeftClick(DockLeftClickAction.MinimizeIfActive, handles, 99, isActive: false));
    }

    [Fact]
    public void Undefined_action_falls_back_to_todays_behaviour()
    {
        var plan = DockClickPolicy.DecideLeftClick((DockLeftClickAction)42, [10], 10, isActive: true);

        Assert.Equal(new DockClickPlan(DockClickKind.Minimize, 10), plan);
    }

    [Fact]
    public void BringToFront_never_minimizes()
    {
        var plan = DockClickPolicy.DecideLeftClick(DockLeftClickAction.BringToFront, [10, 20], 10, isActive: true);

        Assert.Equal(new DockClickPlan(DockClickKind.Activate, 10), plan);
    }

    [Fact]
    public void Cycle_moves_to_next_window_and_wraps()
    {
        nint[] handles = [10, 20, 30];

        Assert.Equal(20, DockClickPolicy.DecideLeftClick(DockLeftClickAction.CycleWindows, handles, 10, true).Handle);
        Assert.Equal(10, DockClickPolicy.DecideLeftClick(DockLeftClickAction.CycleWindows, handles, 30, true).Handle);
    }

    [Fact]
    public void Cycle_activates_first_window_when_app_is_not_active()
    {
        var plan = DockClickPolicy.DecideLeftClick(DockLeftClickAction.CycleWindows, [10, 20], 99, isActive: false);

        Assert.Equal(new DockClickPlan(DockClickKind.Activate, 10), plan);
    }

    [Fact]
    public void Cycle_with_inconsistent_foreground_starts_from_first()
    {
        var plan = DockClickPolicy.DecideLeftClick(DockLeftClickAction.CycleWindows, [10, 20], 99, isActive: true);

        Assert.Equal(10, plan.Handle);
    }

    [Fact]
    public void No_windows_means_no_plan()
    {
        foreach (var action in Enum.GetValues<DockLeftClickAction>())
            Assert.Equal(DockClickKind.None, DockClickPolicy.DecideLeftClick(action, [], 0, false).Kind);
    }

    [Fact]
    public void ShowThumbnails_asks_ui_to_open_preview()
    {
        var plan = DockClickPolicy.DecideLeftClick(DockLeftClickAction.ShowThumbnails, [10], 0, false);

        Assert.Equal(new DockClickPlan(DockClickKind.ShowThumbnails, 10), plan);
    }

    // --- Monitor filter ---

    [Fact]
    public void IsOnMonitor_uses_window_center()
    {
        Assert.True(DockMonitorFilter.IsOnMonitor(new WindowRect(1800, 100, 2000, 300), Left));   // center 1900
        Assert.False(DockMonitorFilter.IsOnMonitor(new WindowRect(1800, 100, 2200, 300), Left));  // center 2000
        Assert.True(DockMonitorFilter.IsOnMonitor(new WindowRect(1800, 100, 2200, 300), Right));
    }

    [Fact]
    public void IsOnMonitor_keeps_minimized_windows_everywhere()
    {
        var minimized = new WindowRect(-32000, -32000, -31840, -31972);

        Assert.True(DockMonitorFilter.IsOnMonitor(minimized, Left));
        Assert.True(DockMonitorFilter.IsOnMonitor(minimized, Right));
    }

    [Fact]
    public void Filter_drops_unpinned_item_without_windows_on_monitor()
    {
        var items = new[] { Item("a", pinned: false, active: false, Win(1, new WindowRect(2000, 0, 2400, 400))) };

        Assert.Empty(DockMonitorFilter.Apply(items, Left, 0));
    }

    [Fact]
    public void Filter_keeps_pinned_item_as_not_running()
    {
        var items = new[] { Item("a", pinned: true, active: true, Win(1, new WindowRect(2000, 0, 2400, 400))) };

        var result = DockMonitorFilter.Apply(items, Left, 1);

        var only = Assert.Single(result);
        Assert.True(only.IsPinned);
        Assert.False(only.IsRunning);
        Assert.False(only.IsActive);
        Assert.Empty(only.WindowHandles);
        Assert.Empty(only.Windows!);
        Assert.Equal(0, only.BadgeCount);
    }

    [Fact]
    public void Filter_narrows_mixed_item_and_recomputes_active()
    {
        var onLeft = Win(1, new WindowRect(100, 100, 500, 500));
        var onRight = Win(2, new WindowRect(2000, 100, 2400, 500));
        var items = new[] { Item("a", pinned: false, active: true, onLeft, onRight) };

        var onLeftDock = Assert.Single(DockMonitorFilter.Apply(items, Left, foreground: 2));
        Assert.Equal([(nint)1], onLeftDock.WindowHandles);
        Assert.False(onLeftDock.IsActive);

        var onRightDock = Assert.Single(DockMonitorFilter.Apply(items, Right, foreground: 2));
        Assert.Equal([(nint)2], onRightDock.WindowHandles);
        Assert.True(onRightDock.IsActive);
    }

    [Fact]
    public void Filter_passes_everything_through_when_all_windows_match()
    {
        var items = new[] { Item("a", pinned: false, active: false, Win(1, new WindowRect(100, 100, 500, 500))) };

        var result = DockMonitorFilter.Apply(items, Left, 0);

        Assert.Same(items[0], Assert.Single(result));
    }

    [Fact]
    public void Filter_passes_items_without_window_info()
    {
        var items = new[] { Item("a", pinned: true, active: false) with { Windows = null } };

        Assert.Same(items[0], Assert.Single(DockMonitorFilter.Apply(items, Left, 0)));
    }

    // --- Controller wiring ---

    private sealed class Pins : IPinnedAppsService
    {
        public IReadOnlyList<PinnedApp> GetPinnedApps() => [];
    }

    private sealed class Tracker : IWindowTracker
    {
        public List<OpenWindowInfo> Windows { get; } = [];
        public WindowInfo? CurrentForeground { get; set; }
        public List<nint> Activated { get; } = [];
        public List<nint> Minimized { get; } = [];
        public int ScanCount { get; private set; }

        public event EventHandler<WindowInfo>? ForegroundWindowChanged;
        public event EventHandler? WindowsChanged { add { } remove { } }
        public event EventHandler<nint>? WindowFlashing { add { } remove { } }

        public void RaiseForeground(WindowInfo info)
        {
            CurrentForeground = info;
            ForegroundWindowChanged?.Invoke(this, info);
        }

        public IReadOnlyList<OpenWindowInfo> GetOpenWindows() { ScanCount++; return Windows; }
        public void ActivateWindow(nint handle) => Activated.Add(handle);
        public void MinimizeWindow(nint handle) => Minimized.Add(handle);
        public void CloseWindow(nint handle) { }
        public void TerminateProcess(int processId) { }
        public void Start() { }
        public void Stop() { }
        public bool GetCursorPosition(out int x, out int y) { x = 0; y = 0; return true; }
        public void Dispose() { }
    }

    private sealed class Shell : IShellActionService
    {
        public void Launch(string path, string? arguments = null) { }
        public void SendWinKey() { }
        public void ToggleSystemTray() { }
        public void OpenQuickSettings() { }
        public void OpenNotificationCenter() { }
    }

    private static (DockController Controller, Tracker Tracker) Build(params long[] handles)
    {
        var tracker = new Tracker();
        foreach (var h in handles)
            tracker.Windows.Add(new OpenWindowInfo((nint)h, "w" + h, @"C:\app.exe", 5, new WindowRect(0, 0, 100, 100)));
        var controller = new DockController(new Pins(), tracker, new Shell());
        controller.Refresh();
        return (controller, tracker);
    }

    [Fact]
    public void Default_left_click_matches_previous_behaviour()
    {
        var (controller, tracker) = Build(10, 20);
        Assert.Equal(DockLeftClickAction.MinimizeIfActive, controller.LeftClickAction);

        controller.HandleItemClick(controller.Items[0]);
        Assert.Equal([(nint)10], tracker.Activated);

        tracker.CurrentForeground = new WindowInfo(10, new WindowRect(0, 0, 100, 100));
        controller.Refresh();
        controller.HandleItemClick(controller.Items[0]);
        Assert.Equal([(nint)10], tracker.Minimized);
    }

    [Fact]
    public void Cycle_click_activates_next_window()
    {
        var (controller, tracker) = Build(10, 20);
        controller.LeftClickAction = DockLeftClickAction.CycleWindows;
        tracker.CurrentForeground = new WindowInfo(10, new WindowRect(0, 0, 100, 100));
        controller.Refresh();

        controller.HandleItemClick(controller.Items[0]);

        Assert.Equal([(nint)20], tracker.Activated);
        Assert.Empty(tracker.Minimized);
    }

    [Fact]
    public void ShowThumbnails_falls_back_to_default_when_ui_does_not_handle_it()
    {
        var (controller, tracker) = Build(10);
        controller.LeftClickAction = DockLeftClickAction.ShowThumbnails;

        controller.HandleItemClick(controller.Items[0]);

        Assert.Equal([(nint)10], tracker.Activated);
    }

    [Fact]
    public void Foreground_move_refreshes_only_when_enabled()
    {
        var (controller, tracker) = Build(10);
        tracker.RaiseForeground(new WindowInfo(10, new WindowRect(0, 0, 100, 100)));
        var baseline = tracker.ScanCount;

        // Same window, new bounds: ignored by default (today's behaviour).
        tracker.RaiseForeground(new WindowInfo(10, new WindowRect(50, 0, 150, 100)));
        Assert.Equal(baseline, tracker.ScanCount);

        controller.RefreshOnForegroundMove = true;
        tracker.RaiseForeground(new WindowInfo(10, new WindowRect(90, 0, 190, 100)));
        Assert.Equal(baseline + 1, tracker.ScanCount);
    }
}
