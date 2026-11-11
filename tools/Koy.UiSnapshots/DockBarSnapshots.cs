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
/// küçülmesi), küçük tepsi oku, tam boy tepsi, basma durumu. Dock yüzeyi temadan bağımsız koyudur; sahne iki temada
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

        // 6. Basma durumu (tam genişlik ve kapsül): ikinci öğe basılı, simge %92'ye çöker ve koyulaşır
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik-basili", renderElement, fullWidth: true, smallTray: true, appCount: 8, pressedIndex: 1);
        Shoot(outDir, prefix, theme, "dockbar-kapsul-basili", renderElement, fullWidth: false, smallTray: true, appCount: 8, pressedIndex: 1);

        // 7. Tam genişlikte ikon hizası: ikonlar Başlat'ın ya da sağ düğmelerin yanına yanaşır, arada boşluk kalmaz
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik-sola", renderElement, fullWidth: true, smallTray: true, appCount: 8, alignment: DockIconAlignment.Left);
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik-saga", renderElement, fullWidth: true, smallTray: true, appCount: 8, alignment: DockIconAlignment.Right);

        // 8. Başlat simgesi: varsayılan Koy simgesi yukarıdaki bütün çekimlerde görünür; burada Windows logosu, özel dosya ve bozuk dosya
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik-baslat-windows", renderElement, fullWidth: true, smallTray: true, appCount: 8, startIcon: DockStartIcon.Windows);
        Shoot(outDir, prefix, theme, "dockbar-kapsul-baslat-windows", renderElement, fullWidth: false, smallTray: true, appCount: 8, startIcon: DockStartIcon.Windows);

        var dir = Path.Combine(Path.GetTempPath(), "koy-snap-baslat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var custom = Path.Combine(dir, "ozel.png");
            WriteSamplePng(custom);
            Shoot(outDir, prefix, theme, "dockbar-tam-genislik-baslat-ozel", renderElement, fullWidth: true, smallTray: true, appCount: 8, startIcon: DockStartIcon.Custom, startIconPath: custom);
            Shoot(outDir, prefix, theme, "dockbar-kapsul-baslat-ozel", renderElement, fullWidth: false, smallTray: true, appCount: 8, startIcon: DockStartIcon.Custom, startIconPath: custom);

            // Bozuk dosya (PNG uzantılı ama içeriği değil) ve olmayan dosya: ikisi de Koy simgesine düşmeli.
            var corrupt = Path.Combine(dir, "bozuk.png");
            File.WriteAllText(corrupt, "bu bir png degil");
            Shoot(outDir, prefix, theme, "dockbar-kapsul-baslat-bozuk", renderElement, fullWidth: false, smallTray: true, appCount: 8, startIcon: DockStartIcon.Custom, startIconPath: corrupt);
            Shoot(outDir, prefix, theme, "dockbar-kapsul-baslat-yok", renderElement, fullWidth: false, smallTray: true, appCount: 8, startIcon: DockStartIcon.Custom, startIconPath: Path.Combine(dir, "yok.png"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Özel simge örneği: turuncu yuvarlak karo, ortasında beyaz yıldız benzeri halka.</summary>
    private static void WriteSamplePng(string path)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00)), null, new Rect(0, 0, 128, 128), 32, 32);
            dc.DrawEllipse(null, new Pen(Brushes.White, 14), new Point(64, 64), 30, 30);
        }

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void Shoot(
        string outDir, string prefix, string theme, string name,
        Action<FrameworkElement, double, double, string> renderElement,
        bool fullWidth, bool smallTray, int appCount, int pressedIndex = -1, DockIconAlignment alignment = DockIconAlignment.Center,
        DockStartIcon startIcon = DockStartIcon.Koy, string startIconPath = "")
    {
        var settings = new AppSettings
        {
            Dock = new DockSettings
            {
                Style = fullWidth ? DockStyleMode.FullWidth : DockStyleMode.FloatingCapsule,
                IconAlignment = alignment,
                StartIcon = startIcon,
                StartIconPath = startIconPath,
                SmallTray = smallTray,
                ShowFolders = true,
                ShowRecents = false
            },
            Appearance = new AppearanceSettings
            {
                Material = Program.Material
            }
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

        if (pressedIndex >= 0 && pressedIndex < vm.Items.Count)
        {
            vm.Items[pressedIndex].SnapPress(true);
            host.UpdateLayout();
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
