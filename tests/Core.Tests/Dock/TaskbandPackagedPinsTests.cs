using System.Text;
using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Dock;

public class TaskbandPackagedPinsTests
{
    private static readonly WindowRect Bounds = new(0, 0, 800, 600);

    [Fact]
    public void ExtractAumids_returns_empty_for_null_or_short_input()
    {
        Assert.Empty(TaskbandPackagedPins.ExtractAumids(null));
        Assert.Empty(TaskbandPackagedPins.ExtractAumids([]));
        Assert.Empty(TaskbandPackagedPins.ExtractAumids([1, 2, 3, 4]));
    }

    [Theory]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", true)]
    [InlineData("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", true)]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", true)]
    [InlineData("windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel", true)]
    [InlineData("Visual Studio Code.lnk", false)]
    [InlineData("Microsoft.Windows.Explorer", false)]
    [InlineData("NoUnderscore!App", false)]
    [InlineData("Pkg_short!App", false)]
    [InlineData("Pkg_123456789012i!App", false)] // 'i' Crockford Base32'de yok
    [InlineData("Pkg_zpdnekdrzrea0!", false)]
    [InlineData("_zpdnekdrzrea0!App", false)]
    [InlineData(".BadPkg_zpdnekdrzrea0!App", false)]
    public void IsPackagedAumid_validates_format(string value, bool expected)
    {
        Assert.Equal(expected, TaskbandPackagedPins.IsPackagedAumid(value));
    }

    [Fact]
    public void ExtractAumids_extracts_distinct_packaged_apps_in_taskband_order_across_alignments()
    {
        // Gerçek Taskband\Favorites akışını taklit eder: çift ve tek hizalamada UTF-16LE dizgiler, tekrarlar ve .lnk girdileri.
        using var ms = new MemoryStream();
        WriteUtf16WithNulls(ms, "Visual Studio Code.lnk");
        WriteUtf16WithNulls(ms, "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify");
        WriteUtf16WithNulls(ms, @"Assets\Square44x44Logo.png");
        WriteUtf16WithNulls(ms, "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"); // tekrar
        ms.WriteByte(0x01); // tek bayt kaydırarak hizalama 1'i sına
        WriteUtf16WithNulls(ms, "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App");
        WriteUtf16WithNulls(ms, "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

        var aumids = TaskbandPackagedPins.ExtractAumids(ms.ToArray());

        Assert.Equal(
            [
                "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
                "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App",
                "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"
            ],
            aumids);
    }

    [Fact]
    public void SeedFromTaskband_preserves_order_for_mixed_lnk_and_packaged_pins()
    {
        using var ms = new MemoryStream();
        WriteUtf16WithNulls(ms, "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify");
        WriteUtf16WithNulls(ms, "Google Chrome.lnk");
        WriteUtf16WithNulls(ms, "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App");

        var folderOrder = new[]
        {
            "Google Chrome.lnk",
            "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App",
            "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"
        };

        var seeded = PinnedOrder.SeedFromTaskband(ms.ToArray(), folderOrder);

        Assert.Equal(
            [
                "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
                "Google Chrome.lnk",
                "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App"
            ],
            seeded);
    }

    [Fact]
    public void Packaged_pin_matches_running_uwp_window_by_aumid_and_not_unrelated_title()
    {
        const string aumid = "Microsoft.WindowsStore_8wekyb3d8bbwe!App";
        var shellPath = TaskbandPackagedPins.AppsFolderPath(aumid);
        var pin = new PinnedApp("Microsoft Store", shellPath, shellPath, null, aumid);

        var storeWindow = new OpenWindowInfo(1, "Microsoft Store", shellPath, 42, Bounds, null, aumid, "ApplicationFrameWindow");
        var unrelatedWindow = new OpenWindowInfo(2, "Microsoft Belgesi - Not Defteri", @"C:\Windows\notepad.exe", 99, Bounds, null, null, "Notepad");

        Assert.True(PinnedAppMatcher.IsMatch(pin, storeWindow));
        Assert.False(PinnedAppMatcher.IsMatch(pin, unrelatedWindow));
    }

    [Fact]
    public void Clicking_not_running_packaged_pin_launches_shell_apps_folder_path()
    {
        const string aumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        var shellPath = TaskbandPackagedPins.AppsFolderPath(aumid);
        var launched = new List<string>();

        using var controller = new DockController(
            new FakePins(new PinnedApp("Calculator", shellPath, shellPath, null, aumid)),
            new FakeWindows(),
            new FakeShell(launched));

        controller.Refresh();
        Assert.Single(controller.Items);
        Assert.Equal(aumid, controller.Items[0].AppUserModelId);

        controller.HandleItemClick(controller.Items[0]);

        Assert.Equal([shellPath], launched);
    }

    private static void WriteUtf16WithNulls(MemoryStream ms, string text)
    {
        ms.Write([0x00, 0x00]);
        var bytes = Encoding.Unicode.GetBytes(text);
        ms.Write(bytes);
        ms.Write([0x00, 0x00]);
    }

    private sealed class FakePins(params PinnedApp[] apps) : IPinnedAppsService
    {
        public IReadOnlyList<PinnedApp> GetPinnedApps() => apps;
    }

    private sealed class FakeWindows : IWindowTracker
    {
        public event EventHandler<WindowInfo>? ForegroundWindowChanged { add { } remove { } }
        public event EventHandler? WindowsChanged { add { } remove { } }
        public event EventHandler<nint>? WindowFlashing { add { } remove { } }
        public WindowInfo? CurrentForeground => null;
        public IReadOnlyList<OpenWindowInfo> GetOpenWindows() => [];
        public void ActivateWindow(nint handle) { }
        public void MinimizeWindow(nint handle) { }
        public void CloseWindow(nint handle) { }
        public void TerminateProcess(int processId) { }
        public void Start() { }
        public void Stop() { }
        public bool GetCursorPosition(out int x, out int y) { x = 0; y = 0; return true; }
        public void Dispose() { }
    }

    private sealed class FakeShell(List<string> launched) : IShellActionService
    {
        public void SendWinKey() { }
        public void ToggleSystemTray() { }
        public void OpenQuickSettings() { }
        public void OpenNotificationCenter() { }
        public void Launch(string path, string? arguments = null) => launched.Add(path);
    }
}
