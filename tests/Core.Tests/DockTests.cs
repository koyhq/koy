using Koy.Core.Dock;
using Koy.Core.Shell;
using Xunit;

namespace Koy.Core.Tests;

public class DockTests
{
    private sealed class FakePinnedAppsService : IPinnedAppsService
    {
        public List<PinnedApp> Apps { get; set; } = [];
        public IReadOnlyList<PinnedApp> GetPinnedApps() => Apps;
    }

    private sealed class FakeWindowTracker : IWindowTracker
    {
        public List<OpenWindowInfo> Windows { get; set; } = [];
        public WindowInfo? CurrentForeground { get; set; }

        public List<nint> Activated { get; } = [];
        public List<nint> Minimized { get; } = [];
        public List<nint> Closed { get; } = [];
        public List<int> Terminated { get; } = [];

        public event EventHandler<WindowInfo>? ForegroundWindowChanged;
        public event EventHandler? WindowsChanged;
        public event EventHandler<nint>? WindowFlashing;

        public IReadOnlyList<OpenWindowInfo> GetOpenWindows() => Windows;

        public void ActivateWindow(nint handle) => Activated.Add(handle);
        public void MinimizeWindow(nint handle) => Minimized.Add(handle);
        public void CloseWindow(nint handle) => Closed.Add(handle);
        public void TerminateProcess(int processId) => Terminated.Add(processId);

        public void TriggerWindowFlashing(nint handle) => WindowFlashing?.Invoke(this, handle);

        public void TriggerForegroundChange(WindowInfo info)
        {
            CurrentForeground = info;
            ForegroundWindowChanged?.Invoke(this, info);
        }

        public void TriggerWindowsChange()
        {
            WindowsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Start() { }
        public void Stop() { }
        public bool GetCursorPosition(out int x, out int y) { x = 0; y = 0; return true; }
        public void Dispose() { }
    }

    private sealed class FakeShellActionService : IShellActionService
    {
        public List<string> Launched { get; } = [];
        public int WinKeyCount { get; set; }
        public int TrayToggleCount { get; set; }

        public void Launch(string path, string? arguments = null) => Launched.Add(path);
        public void SendWinKey() => WinKeyCount++;
        public void ToggleSystemTray() => TrayToggleCount++;
        public void OpenQuickSettings() { }
        public void OpenNotificationCenter() { }
    }

    private sealed class FakeFullscreenDetector : IFullscreenDetector
    {
        public bool Fullscreen { get; set; }
        public bool IsFullscreen() => Fullscreen;
    }

    [Fact]
    public void Refresh_LoadsPinnedAppsInPreservedOrder()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps =
            [
                new PinnedApp("Chrome", @"C:\Pins\Chrome.lnk", @"C:\Program Files\Chrome\chrome.exe", null),
                new PinnedApp("Code", @"C:\Pins\Code.lnk", @"C:\Program Files\VSCode\Code.exe", null)
            ]
        };
        var windowTracker = new FakeWindowTracker();
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.Equal(2, controller.Items.Count);
        Assert.Equal("Chrome", controller.Items[0].Title);
        Assert.True(controller.Items[0].IsPinned);
        Assert.False(controller.Items[0].IsRunning);

        Assert.Equal("Code", controller.Items[1].Title);
        Assert.True(controller.Items[1].IsPinned);
        Assert.False(controller.Items[1].IsRunning);
    }

    [Fact]
    public void Refresh_MatchesRunningWindowToPinnedApp()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps =
            [
                new PinnedApp("Chrome", @"C:\Pins\Chrome.lnk", @"C:\Program Files\Chrome\chrome.exe", null)
            ]
        };
        var windowTracker = new FakeWindowTracker
        {
            Windows =
            [
                new OpenWindowInfo(1001, "Google - Google Chrome", @"C:\Program Files\Chrome\chrome.exe", 1234, new WindowRect(0, 0, 800, 600))
            ]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.Single(controller.Items);
        var item = controller.Items[0];
        Assert.True(item.IsPinned);
        Assert.True(item.IsRunning);
        Assert.Single(item.WindowHandles);
        Assert.Equal(1001, item.WindowHandles[0]);
        Assert.Equal(1234, item.ProcessId);
    }

    [Fact]
    public void Refresh_GroupsMultipleWindowsOfSameApp()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps =
            [
                new PinnedApp("Chrome", @"C:\Pins\Chrome.lnk", @"C:\Program Files\Chrome\chrome.exe", null)
            ]
        };
        var windowTracker = new FakeWindowTracker
        {
            Windows =
            [
                new OpenWindowInfo(1001, "Google - Chrome", @"C:\Program Files\Chrome\chrome.exe", 1234, new WindowRect(0, 0, 800, 600)),
                new OpenWindowInfo(1002, "GitHub - Chrome", @"C:\Program Files\Chrome\chrome.exe", 1234, new WindowRect(0, 0, 800, 600))
            ]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.Single(controller.Items);
        var item = controller.Items[0];
        Assert.Equal(2, item.WindowHandles.Count);
        Assert.Contains((nint)1001, item.WindowHandles);
        Assert.Contains((nint)1002, item.WindowHandles);
    }

    [Fact]
    public void Refresh_AddsUnpinnedRunningAppsAfterPinnedApps()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps =
            [
                new PinnedApp("Chrome", @"C:\Pins\Chrome.lnk", @"C:\Program Files\Chrome\chrome.exe", null)
            ]
        };
        var windowTracker = new FakeWindowTracker
        {
            Windows =
            [
                new OpenWindowInfo(2001, "Untitled - Notepad", @"C:\Windows\System32\notepad.exe", 5678, new WindowRect(0, 0, 600, 400))
            ]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.Equal(2, controller.Items.Count);
        Assert.Equal("Chrome", controller.Items[0].Title);
        Assert.True(controller.Items[0].IsPinned);
        Assert.False(controller.Items[0].IsRunning);

        Assert.Equal("Untitled - Notepad", controller.Items[1].Title);
        Assert.False(controller.Items[1].IsPinned);
        Assert.True(controller.Items[1].IsRunning);
        Assert.Single(controller.Items[1].WindowHandles);
        Assert.Equal(2001, controller.Items[1].WindowHandles[0]);
    }

    [Fact]
    public void Refresh_IdentifiesActiveWindow()
    {
        var pinnedService = new FakePinnedAppsService();
        var windowTracker = new FakeWindowTracker
        {
            Windows =
            [
                new OpenWindowInfo(1001, "Notepad", @"C:\Windows\notepad.exe", 100, new WindowRect(0, 0, 400, 400)),
                new OpenWindowInfo(2001, "Calculator", @"C:\Windows\calc.exe", 200, new WindowRect(0, 0, 400, 400))
            ],
            CurrentForeground = new WindowInfo(2001, new WindowRect(0, 0, 400, 400))
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.Equal(2, controller.Items.Count);
        Assert.False(controller.Items[0].IsActive);
        Assert.True(controller.Items[1].IsActive);
    }

    [Fact]
    public void HandleItemClick_LaunchesWhenNotRunning()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps = [new PinnedApp("Code", @"C:\Pins\Code.lnk", @"C:\VSCode\code.exe", null)]
        };
        var windowTracker = new FakeWindowTracker();
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        controller.HandleItemClick(controller.Items[0]);

        Assert.Single(shell.Launched);
        Assert.Equal(@"C:\VSCode\code.exe", shell.Launched[0]);
    }

    [Fact]
    public void HandleItemClick_ActivatesWhenRunningAndNotActive()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps = [new PinnedApp("Code", @"C:\Pins\Code.lnk", @"C:\VSCode\code.exe", null)]
        };
        var windowTracker = new FakeWindowTracker
        {
            Windows = [new OpenWindowInfo(1001, "Code", @"C:\VSCode\code.exe", 100, new WindowRect(0, 0, 500, 500))],
            CurrentForeground = new WindowInfo(9999, WindowRect.Empty) // another window is active
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.False(controller.Items[0].IsActive);
        controller.HandleItemClick(controller.Items[0]);

        Assert.Single(windowTracker.Activated);
        Assert.Equal(1001, windowTracker.Activated[0]);
        Assert.Empty(windowTracker.Minimized);
    }

    [Fact]
    public void HandleItemClick_MinimizesWhenRunningAndAlreadyActive()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps = [new PinnedApp("Code", @"C:\Pins\Code.lnk", @"C:\VSCode\code.exe", null)]
        };
        var windowTracker = new FakeWindowTracker
        {
            Windows = [new OpenWindowInfo(1001, "Code", @"C:\VSCode\code.exe", 100, new WindowRect(0, 0, 500, 500))],
            CurrentForeground = new WindowInfo(1001, new WindowRect(0, 0, 500, 500)) // Code is active
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.True(controller.Items[0].IsActive);
        controller.HandleItemClick(controller.Items[0]);

        Assert.Single(windowTracker.Minimized);
        Assert.Equal(1001, windowTracker.Minimized[0]);
        Assert.Empty(windowTracker.Activated);
    }

    [Fact]
    public void HandleItemMiddleClick_LaunchesNewInstance()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps = [new PinnedApp("Terminal", @"C:\Pins\wt.lnk", @"C:\Windows\wt.exe", null)]
        };
        var windowTracker = new FakeWindowTracker
        {
            Windows = [new OpenWindowInfo(1001, "Terminal", @"C:\Windows\wt.exe", 50, new WindowRect(0, 0, 500, 500))]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        controller.HandleItemMiddleClick(controller.Items[0]);

        Assert.Single(shell.Launched);
        Assert.Equal(@"C:\Windows\wt.exe", shell.Launched[0]);
    }

    [Fact]
    public void HandleItemClose_ClosesAllWindowsOfItem()
    {
        var pinnedService = new FakePinnedAppsService();
        var windowTracker = new FakeWindowTracker
        {
            Windows =
            [
                new OpenWindowInfo(1001, "Doc 1", @"C:\App\app.exe", 10, new WindowRect(0, 0, 500, 500)),
                new OpenWindowInfo(1002, "Doc 2", @"C:\App\app.exe", 10, new WindowRect(0, 0, 500, 500))
            ]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        controller.HandleItemClose(controller.Items[0]);

        Assert.Equal(2, windowTracker.Closed.Count);
        Assert.Contains((nint)1001, windowTracker.Closed);
        Assert.Contains((nint)1002, windowTracker.Closed);
    }

    [Fact]
    public void HandleItemTerminate_TerminatesProcessId()
    {
        var pinnedService = new FakePinnedAppsService();
        var windowTracker = new FakeWindowTracker
        {
            Windows = [new OpenWindowInfo(1001, "App", @"C:\App\app.exe", 777, new WindowRect(0, 0, 500, 500))]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        controller.HandleItemTerminate(controller.Items[0]);

        Assert.Single(windowTracker.Terminated);
        Assert.Equal(777, windowTracker.Terminated[0]);
    }

    [Fact]
    public void FullscreenDetector_UpdatesIsHiddenByFullscreen()
    {
        var pinnedService = new FakePinnedAppsService();
        var windowTracker = new FakeWindowTracker();
        var shell = new FakeShellActionService();
        var fullscreen = new FakeFullscreenDetector { Fullscreen = true };

        using var controller = new DockController(pinnedService, windowTracker, shell, fullscreen);
        controller.Refresh();

        Assert.True(controller.IsHiddenByFullscreen);

        fullscreen.Fullscreen = false;
        controller.Refresh();

        Assert.False(controller.IsHiddenByFullscreen);
    }

    [Fact]
    public void Refresh_PreservesIconDataForUnpinnedRunningApp()
    {
        var dummyIcon = new byte[] { 0x42, 0x4D, 0x01, 0x02 };
        var pinnedService = new FakePinnedAppsService();
        var windowTracker = new FakeWindowTracker
        {
            Windows =
            [
                new OpenWindowInfo(3001, "Custom App", @"C:\App\custom.exe", 999, new WindowRect(0, 0, 500, 500), dummyIcon)
            ]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.Single(controller.Items);
        Assert.Equal(dummyIcon, controller.Items[0].IconData);
        Assert.False(controller.Items[0].IsPinned);
        Assert.True(controller.Items[0].IsRunning);
    }

    [Fact]
    public void WindowFlashing_SetsIsFlashingOnMatchingDockItem()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps = [new PinnedApp("App", @"C:\Pins\app.lnk", @"C:\app.exe", null)]
        };
        var windowTracker = new FakeWindowTracker
        {
            Windows = [new OpenWindowInfo(1001, "App", @"C:\app.exe", 100, new WindowRect(0, 0, 400, 400))]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.False(controller.Items[0].IsFlashing);

        windowTracker.TriggerWindowFlashing(1001);

        Assert.True(controller.Items[0].IsFlashing);
    }

    [Fact]
    public void HandleItemClick_ClearsFlashingState()
    {
        var pinnedService = new FakePinnedAppsService
        {
            Apps = [new PinnedApp("App", @"C:\Pins\app.lnk", @"C:\app.exe", null)]
        };
        var windowTracker = new FakeWindowTracker
        {
            Windows = [new OpenWindowInfo(1001, "App", @"C:\app.exe", 100, new WindowRect(0, 0, 400, 400))]
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        windowTracker.TriggerWindowFlashing(1001);
        Assert.True(controller.Items[0].IsFlashing);

        controller.HandleItemClick(controller.Items[0]);
        controller.Refresh();

        Assert.False(controller.Items[0].IsFlashing);
    }

    [Fact]
    public void AltTab_PreservesUnpinnedAppsOrder()
    {
        var pinnedService = new FakePinnedAppsService();
        var winA = new OpenWindowInfo(1001, "AppA", @"C:\AppA.exe", 10, new WindowRect(0, 0, 400, 400));
        var winB = new OpenWindowInfo(1002, "AppB", @"C:\AppB.exe", 20, new WindowRect(0, 0, 400, 400));
        var windowTracker = new FakeWindowTracker
        {
            Windows = [winA, winB],
            CurrentForeground = new WindowInfo(1001, new WindowRect(0, 0, 400, 400))
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell);
        controller.Refresh();

        Assert.Equal(2, controller.Items.Count);
        Assert.Equal(@"C:\AppA.exe", controller.Items[0].Id);
        Assert.Equal(@"C:\AppB.exe", controller.Items[1].Id);

        // Alt+Tab: winB comes to top of Z-order in EnumWindows
        windowTracker.Windows = [winB, winA];
        windowTracker.TriggerForegroundChange(new WindowInfo(1002, new WindowRect(0, 0, 400, 400)));

        Assert.Equal(2, controller.Items.Count);
        // Order MUST stay stable: AppA first, AppB second!
        Assert.Equal(@"C:\AppA.exe", controller.Items[0].Id);
        Assert.Equal(@"C:\AppB.exe", controller.Items[1].Id);
        Assert.False(controller.Items[0].IsActive);
        Assert.True(controller.Items[1].IsActive);
    }

    [Fact]
    public void RefreshDebounce_PencereOlaylariniTekYenilemeyeIndirir()
    {
        var pinnedService = new FakePinnedAppsService();
        var windowTracker = new FakeWindowTracker();
        var shell = new FakeShellActionService();
        var time = new RefreshDebouncerTests.ManualTimeProvider();
        using var controller = new DockController(
            pinnedService, windowTracker, shell,
            refreshDebounce: TimeSpan.FromMilliseconds(100), timeProvider: time);

        var refreshCount = 0;
        controller.ItemsChanged += (_, _) => refreshCount++;

        windowTracker.TriggerWindowsChange();
        windowTracker.TriggerWindowsChange();
        windowTracker.TriggerWindowsChange();
        Assert.Equal(0, refreshCount);

        time.FireLast();

        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public void RefreshDebounce_OnPlanDegisimiHemenYenilerVeBekleyeniIptalEder()
    {
        var pinnedService = new FakePinnedAppsService();
        var windowTracker = new FakeWindowTracker();
        var shell = new FakeShellActionService();
        var time = new RefreshDebouncerTests.ManualTimeProvider();
        using var controller = new DockController(
            pinnedService, windowTracker, shell,
            refreshDebounce: TimeSpan.FromMilliseconds(100), timeProvider: time);

        var refreshCount = 0;
        controller.ItemsChanged += (_, _) => refreshCount++;

        windowTracker.TriggerWindowsChange();
        windowTracker.TriggerForegroundChange(new WindowInfo(42, WindowRect.Empty));
        Assert.Equal(1, refreshCount);

        time.FireLast();
        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public void MovePinned_SabitliSirayiDegistirirVeListeyiYeniler()
    {
        var inner = new FakePinnedAppsService
        {
            Apps =
            [
                new PinnedApp("A", @"C:\Pins\A.lnk", null, null),
                new PinnedApp("B", @"C:\Pins\B.lnk", null, null),
                new PinnedApp("C", @"C:\Pins\C.lnk", null, null)
            ]
        };
        List<string> stored = ["A.lnk", "B.lnk", "C.lnk"];
        var ordered = new OrderedPinnedAppsService(inner, () => stored, o => stored = [.. o]);

        using var controller = new DockController(ordered, new FakeWindowTracker(), new FakeShellActionService());
        controller.Refresh();
        Assert.True(controller.CanReorderPinned);

        controller.MovePinned(@"C:\Pins\A.lnk", 2);

        Assert.Equal(["B", "C", "A"], controller.Items.Select(i => i.Title));
        Assert.Equal(["B.lnk", "C.lnk", "A.lnk"], stored);
    }

    [Fact]
    public void MovePinned_SiralanamazKaynaktaVeBilinmeyenOgedeSessizGecer()
    {
        var inner = new FakePinnedAppsService { Apps = [new PinnedApp("A", @"C:\Pins\A.lnk", null, null)] };
        using var controller = new DockController(inner, new FakeWindowTracker(), new FakeShellActionService());
        controller.Refresh();

        Assert.False(controller.CanReorderPinned);
        controller.MovePinned(@"C:\Pins\A.lnk", 0);
        controller.MovePinned(@"C:\Pins\Yok.lnk", 3);

        Assert.Single(controller.Items);
    }

    [Fact]
    public void DockController_GroupingMode_Never_Ayrik_Simgeler_Uretir()
    {
        var pinnedService = new FakePinnedAppsService();
        var win1 = new OpenWindowInfo(1001, "Window 1", @"C:\App.exe", 10, new WindowRect(0, 0, 400, 400));
        var win2 = new OpenWindowInfo(1002, "Window 2", @"C:\App.exe", 10, new WindowRect(0, 0, 400, 400));
        var windowTracker = new FakeWindowTracker
        {
            Windows = [win1, win2],
            CurrentForeground = new WindowInfo(1001, new WindowRect(0, 0, 400, 400))
        };
        var shell = new FakeShellActionService();

        using var controller = new DockController(pinnedService, windowTracker, shell)
        {
            GroupingMode = DockGroupingMode.Never
        };
        controller.Refresh();

        Assert.Equal(2, controller.Items.Count);
        Assert.NotNull(controller.Items[0].Windows);
        Assert.Single(controller.Items[0].Windows!);
        Assert.NotNull(controller.Items[1].Windows);
        Assert.Single(controller.Items[1].Windows!);
    }
}
