using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Dock;

/// <summary>Dock öğesinin AppUserModelID'si (atlama listesi bu kimliğe bağlı).</summary>
public class DockItemAppIdTests
{
    private sealed class Pins(params PinnedApp[] apps) : IPinnedAppsService
    {
        public IReadOnlyList<PinnedApp> GetPinnedApps() => apps;
    }

    private sealed class Windows(params OpenWindowInfo[] windows) : IWindowTracker
    {
        public event EventHandler<WindowInfo>? ForegroundWindowChanged { add { } remove { } }
        public event EventHandler? WindowsChanged { add { } remove { } }
        public event EventHandler<nint>? WindowFlashing { add { } remove { } }
        public WindowInfo? CurrentForeground => null;
        public IReadOnlyList<OpenWindowInfo> GetOpenWindows() => windows;
        public void ActivateWindow(nint handle) { }
        public void MinimizeWindow(nint handle) { }
        public void CloseWindow(nint handle) { }
        public void TerminateProcess(int processId) { }
        public void Start() { }
        public void Stop() { }
        public bool GetCursorPosition(out int x, out int y) { x = 0; y = 0; return true; }
        public void Dispose() { }
    }

    private sealed class Shell : IShellActionService
    {
        public void SendWinKey() { }
        public void ToggleSystemTray() { }
        public void OpenQuickSettings() { }
        public void OpenNotificationCenter() { }
        public void Launch(string path, string? arguments = null) { }
    }

    private static readonly WindowRect Rect = new(0, 0, 100, 100);

    [Fact]
    public void Pinned_not_running_uses_the_shortcut_aumid()
    {
        using var controller = new DockController(
            new Pins(new PinnedApp("Code", @"C:\p\Code.lnk", @"C:\a\Code.exe", null, "Vendor.Code")), new Windows(), new Shell());
        controller.Refresh();

        Assert.Equal("Vendor.Code", controller.Items[0].AppUserModelId);
    }

    [Fact]
    public void Running_window_aumid_wins_over_the_pin()
    {
        using var controller = new DockController(
            new Pins(new PinnedApp("Chrome", @"C:\p\Chrome.lnk", @"C:\a\chrome.exe", null, "Chrome")),
            new Windows(new OpenWindowInfo(1, "t", @"C:\a\chrome.exe", 10, Rect, AppUserModelId: "Chrome")),
            new Shell());
        controller.Refresh();

        Assert.Equal("Chrome", controller.Items[0].AppUserModelId);
    }

    [Fact]
    public void Unpinned_window_carries_its_own_aumid()
    {
        using var controller = new DockController(
            new Pins(), new Windows(new OpenWindowInfo(1, "t", @"C:\a\app.exe", 10, Rect, AppUserModelId: "Vendor.App")), new Shell());
        controller.Refresh();

        Assert.Equal("Vendor.App", controller.Items[0].AppUserModelId);
    }

    [Fact]
    public void Window_without_aumid_gets_none_so_only_koys_own_items_show()
    {
        // Klasik uygulama: AUMID yok ve exe yolundan kimlik türetilmez.
        using var controller = new DockController(
            new Pins(), new Windows(new OpenWindowInfo(1, "t", @"C:\a\app.exe", 10, Rect, ClassName: "SomeClass")), new Shell());
        controller.Refresh();

        Assert.Null(controller.Items[0].AppUserModelId);
    }

    [Fact]
    public void Explorer_folder_window_maps_to_the_known_explorer_aumid()
    {
        var explorer = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows", "explorer.exe");
        using var controller = new DockController(
            new Pins(), new Windows(new OpenWindowInfo(1, "t", explorer, 10, Rect, ClassName: "CabinetWClass")), new Shell());
        controller.Refresh();

        Assert.Equal(KnownAppUserModelIds.Explorer, controller.Items[0].AppUserModelId);
    }
}
