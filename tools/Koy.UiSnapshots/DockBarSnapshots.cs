using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Koy.App.Features.Dock;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Settings;
using Koy.Core.Shell;
using Koy.Core.Tray;

namespace Koy.UiSnapshots;

/// <summary>
/// Dock çubuklarının gerçek XAML'ini (DockFullWidth, DockCapsule) sahte verilerle çizer: tam genişlik, taşma (simge
/// küçülmesi), küçük tepsi oku, tam boy tepsi. Dock yüzeyi temadan bağımsız koyudur; sahne iki temada
/// da çalışır, görüntüler aynı çıkmalıdır (temaya bağımlılık sızdıysa fark görünür).
/// </summary>
internal static class DockBarSnapshots
{
    private sealed class FakeTray : ITrayService
    {
        public event EventHandler? IconsChanged { add { } remove { } }
        public IReadOnlyList<TrayIcon> GetVisibleIcons() => [];
        public byte[]? GetIconImage(TrayIconKey key) => null;
        public void SendMouse(TrayIconKey key, TrayMouseAction action, int cursorX, int cursorY) { }
        public void RemoveDeadIcons() { }
        public bool IsRunning => true;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private static readonly DisplayMonitor Monitor = new(
        "snap", "Snapshot", new WindowRect(0, 0, 1280, 720), new WindowRect(0, 0, 1280, 720), true);

    public static void Render(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        // 1. Tam genişlik: 8 öğe + klasör, göstergeler (etkin, çalışan, çoklu pencere, dikkat, rozet), küçük tepsi oku
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik", renderElement, fullWidth: true, smallTray: true, appCount: 8);

        // 2. Tam genişlik, tam boy tepsi düğmesi (Küçük tepsi kapalı)
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik-tam-tepsi", renderElement, fullWidth: true, smallTray: false, appCount: 8);

        // 3. Tam genişlik taşma: çok uygulama, simgeler 24 px'e kadar küçülür, orta bölüm yanlara binmez
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik-tasma", renderElement, fullWidth: true, smallTray: true, appCount: 60);

        // 4. Kapsül: küçük tepsi oku Koy düğmesinin sağında
        Shoot(outDir, prefix, theme, "dockbar-kapsul-kucuk-tepsi", renderElement, fullWidth: false, smallTray: true, appCount: 8);

        // 5. Kapsül taşma
        Shoot(outDir, prefix, theme, "dockbar-kapsul-tasma", renderElement, fullWidth: false, smallTray: true, appCount: 60);

    }

    private static void Shoot(
        string outDir, string prefix, string theme, string name,
        Action<FrameworkElement, double, double, string> renderElement,
        bool fullWidth, bool smallTray, int appCount)
    {
        var settings = new AppSettings
        {
            DockStyle = fullWidth ? DockStyleMode.FullWidth : DockStyleMode.FloatingCapsule,
            DockSmallTray = smallTray,
            DockShowFolders = true,
            DockShowRecents = false,
            Material = "Katı"
        };

        var vm = new DockViewModel(settings, DockFolderSnapshots.SharedController, new DockFolderSnapshots.FakeShellActionService(), trayService: new FakeTray());

        // Kurucunun kuyruğa attığı ilk öğe eşitlemesi (boş denetleyici) çalışsın, sonra sahte öğeler eklensin.
        Pump();
        vm.Items.Clear();
        foreach (var entry in BuildEntries(appCount))
            vm.Items.Add(entry);

        vm.SetMonitor(Monitor);

        FrameworkElement view = fullWidth ? new DockFullWidth { DataContext = vm } : new DockCapsule { DataContext = vm };

        // Düzen iki geçişte oturur: ilk geçiş orta sütun genişliğini bildirir, simgeler yeniden boyutlanır.
        var host = new Grid { Width = Monitor.WidthDip, DataContext = vm };
        host.Children.Add(view);
        for (var i = 0; i < 2; i++)
        {
            host.Measure(new Size(Monitor.WidthDip, double.PositiveInfinity));
            host.Arrange(new Rect(0, 0, host.DesiredSize.Width, host.DesiredSize.Height));
            host.UpdateLayout();
            Pump();
        }

        renderElement(host, Monitor.WidthDip, fullWidth ? DockPositionResolver.FullWidthHeightDip : 80, Path.Combine(outDir, $"{prefix}-{theme}-{name}.png"));
    }

    private static List<DockEntryViewModel> BuildEntries(int count)
    {
        var palette = new (string Id, string Title, Color Color, string Letter)[]
        {
            ("chrome", "Google Chrome", Color.FromRgb(0x42, 0x85, 0xF4), "C"),
            ("code", "Visual Studio Code", Color.FromRgb(0x00, 0x98, 0xFF), "V"),
            ("terminal", "Windows Terminal", Color.FromRgb(0x30, 0x30, 0x30), "T"),
            ("spotify", "Spotify", Color.FromRgb(0x1D, 0xB9, 0x54), "S"),
            ("discord", "Discord", Color.FromRgb(0x58, 0x65, 0xF2), "D"),
            ("explorer", "Dosya Gezgini", Color.FromRgb(0xF5, 0xB0, 0x2A), "E"),
            ("notepad", "Not Defteri", Color.FromRgb(0x5B, 0xA8, 0xE0), "N"),
            ("edge", "Microsoft Edge", Color.FromRgb(0x00, 0x78, 0xD7), "M"),
        };

        var entries = new List<DockEntryViewModel>();
        for (var i = 0; i < count; i++)
        {
            var p = palette[i % palette.Length];
            var id = i < palette.Length ? p.Id : p.Id + i;
            var vm = DockFolderSnapshots.CreateAppVm(id, p.Title, p.Color, p.Letter, isRunning: i % 3 != 2);
            if (i == 1) vm.IsActive = true;
            if (i == 2) { vm.IsAttention = true; vm.IsRunning = true; }
            if (i == 3) { vm.IsMultiWindow = true; vm.IsRunning = true; }
            if (i == 4) { vm.BadgeCount = 3; vm.HasBadge = true; vm.IsRunning = true; }
            entries.Add(vm);

            if (i == 5)
            {
                entries.Add(new DockFolderViewModel(
                    new DockFolder("web", "Web", ["chrome", "edge"]),
                    [DockFolderSnapshots.CreateAppVm("chrome", "Google Chrome", palette[0].Color, "C"),
                     DockFolderSnapshots.CreateAppVm("edge", "Microsoft Edge", palette[7].Color, "M")]));
            }
        }

        return entries;
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Thread.Sleep(30);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }
}
