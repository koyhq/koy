using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Koy.App.Features.Dock;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Dock.Items;
using Koy.Core.Power;
using Koy.Core.Settings;
using Koy.Core.Shell;
using Koy.Core.Tray;
using Koy.Platform.Shell;

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
        public event EventHandler<TrayBalloonNotification>? BalloonReceived { add { } remove { } }
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

        // 10. Pil öğesi (sahte güç verisi): %82 deşarj ve %15 şarjda (düşük eşiğin altında ama şarjda: uyarı rengi yok)
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik-pil-82", renderElement, fullWidth: true, smallTray: true, appCount: 8, battery: new FakePower(82, false));
        Shoot(outDir, prefix, theme, "dockbar-kapsul-pil-82", renderElement, fullWidth: false, smallTray: true, appCount: 8, battery: new FakePower(82, false));
        Shoot(outDir, prefix, theme, "dockbar-tam-genislik-pil-15-sarjda", renderElement, fullWidth: true, smallTray: true, appCount: 8, battery: new FakePower(15, true));
        Shoot(outDir, prefix, theme, "dockbar-kapsul-pil-15-sarjda", renderElement, fullWidth: false, smallTray: true, appCount: 8, battery: new FakePower(15, true));
        Shoot(outDir, prefix, theme, "dockbar-kapsul-pil-15-dusuk", renderElement, fullWidth: false, smallTray: true, appCount: 8, battery: new FakePower(15, false));
        // 9. Malzeme varyantı: Katı / Buzlu / Cam, renkli zeminde, alt ve sol kenar
        RenderMaterials(outDir, prefix, theme, renderElement);

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

    /// <summary>Pil durumları (sahte güç verisi): tam genişlik dock'ta her durum için bir kare. Dosya adı: dockpil-{durum}.</summary>
    public static void RenderBattery(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        foreach (var (name, power) in BatterySnapshots.States)
            Shoot(outDir, prefix, theme, "dockpil-" + name, renderElement, fullWidth: true, smallTray: true, appCount: 8, battery: power);
    }

    /// <summary>
    /// Sürükleme ara kareleri (ek öğe varken: pil): 2. simge 4. simgeye doğru sürükleniyor. Karar gerçek Core yolundan
    /// (<see cref="DockViewModel.EvaluateDrag"/>) geçer. Kareler: orta bant (komşu yerinde, hedef büyümüş ve parlıyor) ve
    /// sağ kenar bölgesi (komşu kaymış). Dosya adı: dockdrag-{tam|kapsul|kapsul-sol}-{bant|kenar}.
    /// </summary>
    public static void RenderDrag(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        foreach (var (label, fullWidth, edge) in new[]
        {
            ("tam", true, DockEdge.Bottom), ("kapsul", false, DockEdge.Bottom), ("kapsul-sol", false, DockEdge.Left)
        })
        foreach (var (state, fraction) in new[] { ("bant", 0.0), ("kenar", 0.3) })
        {
            var vertical = edge is DockEdge.Left or DockEdge.Right;
            Shoot(outDir, prefix, theme, $"dockdrag-{label}-{state}", renderElement, fullWidth, smallTray: true, appCount: 8,
                edge: edge, colorful: vertical, battery: new FakePower(82),
                prepare: (vm, host) => ApplyDrag(vm, host, vertical, from: 1, to: 3, fraction));
        }
    }

    /// <summary>
    /// Tam genişlik dock'un sağ ucu (Windows 11 sistem alanı): pilli ve pilsiz makine, bildirim zili, rahatsız etme, görünür tepsi
    /// simgesi ve üzerine gelme halleri (hap vurgusu). Dosya adı: docksistem-{durum}. Kare dar tutulur (sağ uç görünsün diye).
    /// </summary>
    public static void RenderSystemArea(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        foreach (var (name, battery, dnd, icon, hover) in new (string, FakePower?, bool, bool, string?)[]
        {
            ("pilli", new FakePower(100, ac: true), false, false, null),
            ("pil-pilde", new FakePower(54, time: new TimeSpan(3, 25, 0)), false, false, null),
            ("pilsiz", null, false, false, null),
            ("rahatsiz-etme", new FakePower(100, ac: true), true, false, null),
            ("tepsi-simgesi", new FakePower(100, ac: true), false, true, null),
            ("hover-grup", new FakePower(100, ac: true), false, false, "StatusGroup"),
            ("hover-saat", new FakePower(100, ac: true), false, false, "ClockButton"),
            ("hover-zil", new FakePower(100, ac: true), false, false, "BellButton"),
            ("hover-seritt", new FakePower(100, ac: true), false, false, "DesktopStrip"),
        })
        {
            var power = battery ?? new FakePower(hasBattery: false);
            var status = new DockStatusViewModel(new SnapshotNetwork(), new FakeAudio(), power, new DockFolderSnapshots.FakeShellActionService(),
                new SnapshotFocus(dnd));
            Shoot(outDir, prefix, theme, "docksistem-" + name, renderElement, fullWidth: true, smallTray: false, appCount: 5,
                status: status, tray: new SnapshotTray(icon), width: 760, newTrayDefault: Koy.Core.Tray.TrayIconVisibility.Shown,
                prepare: hover is null ? null : (vm, host) => ForceHover(host, hover));
        }
    }

    private static void ForceHover(DependencyObject root, string name)
    {
        var target = FindNamed(root, name);
        if (target is not null)
            target.Tag = "hover";
    }

    private static FrameworkElement? FindNamed(DependencyObject root, string name)
    {
        if (root is FrameworkElement { Name: var n } fe && n == name)
            return fe;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindNamed(VisualTreeHelper.GetChild(root, i), name) is { } found)
                return found;
        }
        return null;
    }

    private sealed class SnapshotNetwork : Koy.Core.Network.INetworkService
    {
        public Koy.Core.Network.NetworkStatus Current { get; } = new(Koy.Core.Network.NetworkKind.WiFi, true, 4, "TP-Link_D4A3");
        public event EventHandler<Koy.Core.Network.NetworkStatus>? Changed { add { } remove { } }
        public void Dispose() { }
    }

    private sealed class SnapshotFocus(bool dnd) : Koy.Core.Shell.IFocusAssistService
    {
        public bool IsActive => dnd;
        public bool IsDoNotDisturb => dnd;
        public event EventHandler<bool>? StateChanged { add { } remove { } }
        public void OpenSettings() { }
        public void Dispose() { }
    }

    /// <summary>Tepsi: isteğe bağlı tek görünür simge (düz renkli 16 px kare); taşma penceresi boş.</summary>
    private sealed class SnapshotTray(bool withIcon) : ITrayService
    {
        private static readonly TrayIcon Icon = new(new TrayIconKey(0, 1, Guid.NewGuid()), 0, 1, 0, 1, "Discord", false, 4);
        public event EventHandler? IconsChanged { add { } remove { } }
        public event EventHandler<TrayBalloonNotification>? BalloonReceived { add { } remove { } }
        public IReadOnlyList<TrayIcon> GetVisibleIcons() => withIcon ? [Icon] : [];
        public byte[]? GetIconImage(TrayIconKey key)
        {
            var pixels = new byte[16 * 16 * 4];
            for (var i = 0; i < 16 * 16; i++)
            {
                pixels[i * 4] = 0xF2; pixels[i * 4 + 1] = 0x65; pixels[i * 4 + 2] = 0x58; pixels[i * 4 + 3] = 255;
            }
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        public void SendMouse(TrayIconKey key, TrayMouseAction action, int cursorX, int cursorY) { }
        public void RemoveDeadIcons() { }
        public bool IsRunning => true;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    /// <summary>İkinci simgeyi üçüncü simgenin (to) merkezine + kesir kadar sürüklenmiş gösterir; yaylar beklenmeden son değere oturur.</summary>
    private static void ApplyDrag(DockViewModel vm, Grid host, bool vertical, int from, int to, double fraction)
    {
        var list = FindList(host) ?? throw new InvalidOperationException("DockItemsList bulunamadı");
        var widths = new List<double>();
        for (var i = 0; i < vm.Items.Count; i++)
        {
            var c = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(i)!;
            widths.Add(vertical ? c.ActualHeight : c.ActualWidth);
        }

        var centers = Koy.Core.Dock.DockReorderMath.Centers(widths);
        var offset = centers[to] + fraction * widths[to] - centers[from];
        var dragged = vm.Items[from];
        var plan = vm.EvaluateDrag(dragged, from, offset, widths);
        Console.WriteLine($"  sürükleme: hedef={plan.HoveredIndex} bölge={plan.Zone} birleştir={plan.CanMerge} kayma-hedefi={plan.ShiftTarget}");

        vm.BeginReorder();
        dragged.DragZ = 10;
        dragged.SnapOffset(offset);
        for (var i = 0; i < vm.Items.Count; i++)
        {
            if (i == from)
                continue;
            vm.Items[i].SnapOffset(DockReorderMath.ShiftedIndices(from, plan.ShiftTarget).Contains(i)
                ? DockReorderMath.ShiftFor(i, from, plan.ShiftTarget, widths[from])
                : 0);
        }

        if (plan.CanMerge)
        {
            vm.Items[plan.HoveredIndex].IsTargetGlow = true;
            vm.Items[plan.HoveredIndex].SnapScale(1.1);
        }
    }

    private static ItemsControl? FindList(DependencyObject root)
    {
        if (root is ItemsControl { Name: "DockItemsList" } list)
            return list;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindList(VisualTreeHelper.GetChild(root, i)) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Malzeme varyantı: tam genişlik ve kapsül dock Katı / Buzlu / Cam, renkli bir arka planın üstünde (saydamlık ve iç
    /// kenar çizgisi görünsün diye); alt kenar ve bir dikey kenar (sol). Dosya adı: dockmat-{tam|kapsul}-{kenar}-{malzeme}.
    /// </summary>
    public static void RenderMaterials(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        foreach (var edge in new[] { DockEdge.Bottom, DockEdge.Left })
        foreach (var fullWidth in new[] { true, false })
        foreach (var material in new[] { Koy.Core.Appearance.MaterialLevel.Solid, Koy.Core.Appearance.MaterialLevel.Frosted, Koy.Core.Appearance.MaterialLevel.Glass })
        {
            var name = $"dockmat-{(fullWidth ? "tam" : "kapsul")}-{edge.ToString().ToLowerInvariant()}-{material.ToString().ToLowerInvariant()}";
            Shoot(outDir, prefix, theme, name, renderElement, fullWidth, smallTray: true, appCount: edge == DockEdge.Bottom ? 8 : 6,
                hug: !fullWidth, material: material, edge: edge, colorful: true);
        }
    }

    private static Grid ColorfulBackdrop(double width, double height)
    {
        var grid = new Grid
        {
            Width = width,
            Height = height,
            ClipToBounds = true,
            Background = new LinearGradientBrush(
                [new GradientStop(Color.FromRgb(0xFF, 0x3D, 0x81), 0), new GradientStop(Color.FromRgb(0xFF, 0xB3, 0x47), 0.45),
                 new GradientStop(Color.FromRgb(0x2D, 0xD4, 0xBF), 0.75), new GradientStop(Color.FromRgb(0x3B, 0x82, 0xF6), 1)],
                new Point(0, 0), new Point(1, 1))
        };
        (double X, double Y, double R, string C)[] blobs =
        [
            (0.12, 0.70, 70, "#FFFFFF"), (0.35, 0.40, 55, "#7C3AED"), (0.58, 0.78, 80, "#FFE14D"),
            (0.80, 0.45, 60, "#0B1B3A"), (0.93, 0.80, 50, "#FFFFFF")
        ];
        var canvas = new Canvas();
        foreach (var (x, y, r, c) in blobs)
        {
            var e = new System.Windows.Shapes.Ellipse { Width = r * 2, Height = r * 2, Fill = (Brush)new BrushConverter().ConvertFromString(c)! };
            Canvas.SetLeft(e, width * x - r);
            Canvas.SetTop(e, height * y - r);
            canvas.Children.Add(e);
        }
        grid.Children.Add(canvas);
        return grid;
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

    /// <summary>
    /// Tanıtım sitesi için kapsül dock: simgeler kullanıcının görev çubuğuna sabitlediği gerçek uygulamalardan
    /// (Koy'un kendi <see cref="PinnedAppsService"/> servisi) ve varsa Discord'dan gelir. Sabit yoksa sahte simgeler.
    /// </summary>
    public static void RenderSite(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        string[] order = ["File Explorer", "Dosya Gezgini", "Visual Studio 2022", "Visual Studio Code", "Docker Desktop", "Obsidian", "Google Chrome", "Antigravity"];
        string[] running = ["File Explorer", "Dosya Gezgini", "Visual Studio Code", "Google Chrome", "Discord"];

        var apps = new PinnedAppsService().GetPinnedApps()
            .Where(a => a.IconData is { Length: > 0 })
            .Select(a => (a.Title, a.TargetPath ?? a.ShortcutPath, a.IconData!))
            .OrderBy(a => Array.IndexOf(order, a.Title) is var i and >= 0 ? i : order.Length)
            .ToList();

        var discordRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord");
        var discord = Directory.Exists(discordRoot)
            ? Directory.GetDirectories(discordRoot, "app-*").OrderDescending().Select(d => Path.Combine(d, "Discord.exe")).FirstOrDefault(File.Exists)
            : null;
        if (discord is not null && IconHelper.GetIconBytes(discord) is { Length: > 0 } discordIcon)
            apps.Add(("Discord", discord, discordIcon));

        List<DockEntryViewModel>? entries = null;
        if (apps.Count > 0)
        {
            entries = [];
            foreach (var (title, path, icon) in apps)
            {
                var isRunning = running.Contains(title);
                var model = new DockItem(title, title, path, path, icon, true, isRunning, false, isRunning ? [1] : []);
                var vm = new DockItemViewModel(model, DockFolderSnapshots.SharedController) { ItemSize = 38, IconSize = 28 };
                if (title == "Visual Studio Code") vm.IsActive = true;
                entries.Add(vm);
            }
        }

        Shoot(outDir, prefix, theme, "site-dock", renderElement, fullWidth: false, smallTray: true, appCount: 8, entries: entries, hug: true);
    }

    private static void Shoot(
        string outDir, string prefix, string theme, string name,
        Action<FrameworkElement, double, double, string> renderElement,
        bool fullWidth, bool smallTray, int appCount, int pressedIndex = -1, DockIconAlignment alignment = DockIconAlignment.Center,
        DockStartIcon startIcon = DockStartIcon.Koy, string startIconPath = "", IReadOnlyList<DockEntryViewModel>? entries = null, bool hug = false,
        Koy.Core.Appearance.MaterialLevel? material = null, DockEdge edge = DockEdge.Bottom, bool colorful = false, IPowerService? battery = null,
        Action<DockViewModel, Grid>? prepare = null, DockStatusViewModel? status = null, ITrayService? tray = null, double? width = null,
        Koy.Core.Tray.TrayIconVisibility? newTrayDefault = null)
    {
        var settings = new AppSettings
        {
            Dock = new DockSettings
            {
                Style = fullWidth ? DockStyleMode.FullWidth : DockStyleMode.FloatingCapsule,
                Edge = edge,
                IconAlignment = alignment,
                StartIcon = startIcon,
                StartIconPath = startIconPath,
                SmallTray = smallTray,
                ShowFolders = true,
                ShowRecents = false,
                NewTrayIconVisibility = newTrayDefault ?? Koy.Core.Tray.TrayIconVisibilityPolicy.DefaultNewIcon
            },
            DockItems = battery is null ? new DockItemsSettings() : new DockItemsSettings { Items = [new DockItemEntry { Kind = "Battery", Id = "Battery" }] },
            Appearance = new AppearanceSettings
            {
                Material = material ?? Program.Material
            }
        };

        var vm = new DockViewModel(settings, DockFolderSnapshots.SharedController, new DockFolderSnapshots.FakeShellActionService(), status: status, trayService: tray ?? new FakeTray(),
            acquirePower: battery is null ? null : () => battery, releasePower: () => { });

        // Kurucunun kuyruğa attığı ilk öğe eşitlemesi (boş denetleyici) çalışsın, sonra sahte öğeler eklensin.
        Pump();
        // Pil öğesi gerçek yoldan (ComposeExtras) kurulduysa korunur; sahte öğeler onun önüne eklenir.
        var batteryItem = vm.Items.OfType<DockExtraViewModel>().FirstOrDefault(x => x.IsBattery);
        vm.Items.Clear();
        foreach (var entry in entries ?? BuildEntries(appCount))
            vm.Items.Add(entry);
        if (battery is not null)
            vm.Items.Add(batteryItem ?? throw new InvalidOperationException("Pil öğesi oluşmadı"));

        vm.SetMonitor(Monitor);

        FrameworkElement view = fullWidth ? new DockFullWidth { DataContext = vm } : new DockCapsule { DataContext = vm };
        // Site görüntüsü: kapsül gerçekteki gibi içeriği kadar, ekran genişliğine yayılmaz.
        if (hug)
        {
            if (edge is DockEdge.Left or DockEdge.Right)
            {
                view.VerticalAlignment = VerticalAlignment.Center;
                view.HorizontalAlignment = edge == DockEdge.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            }
            else
            {
                view.HorizontalAlignment = HorizontalAlignment.Center;
            }
            // Serbest yüzen kapsül (pencereye yapışık değil): dört köşe yuvarlak.
            vm.CapsuleCornerRadius = new CornerRadius(18);
            vm.DockMargin = new Thickness(12, 12, 12, 10);
        }

        // Düzen iki geçişte oturur: ilk geçiş orta sütun genişliğini bildirir, simgeler yeniden boyutlanır.
        var vertical = edge is DockEdge.Left or DockEdge.Right;
        var hostWidth = colorful && vertical ? 360 : width ?? Monitor.WidthDip;
        var hostHeight = colorful ? (vertical ? 380 : 150) : double.NaN;
        var host = new Grid { Width = hostWidth, DataContext = vm };
        if (colorful)
        {
            host.Height = hostHeight;
            host.Children.Add(ColorfulBackdrop(hostWidth, hostHeight));
            view.HorizontalAlignment = vertical ? (edge == DockEdge.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right) : HorizontalAlignment.Stretch;
            view.VerticalAlignment = vertical ? VerticalAlignment.Stretch : VerticalAlignment.Bottom;
            if (hug)
            {
                view.HorizontalAlignment = vertical ? view.HorizontalAlignment : HorizontalAlignment.Center;
                if (vertical) view.VerticalAlignment = VerticalAlignment.Center;
            }
        }
        host.Children.Add(view);
        for (var i = 0; i < 2; i++)
        {
            host.Measure(new Size(hostWidth, colorful ? hostHeight : double.PositiveInfinity));
            host.Arrange(new Rect(0, 0, host.DesiredSize.Width, host.DesiredSize.Height));
            host.UpdateLayout();
            Pump();
        }

        if (pressedIndex >= 0 && pressedIndex < vm.Items.Count)
        {
            vm.Items[pressedIndex].SnapPress(true);
            host.UpdateLayout();
        }

        if (prepare is not null)
        {
            prepare(vm, host);
            host.UpdateLayout();
            Pump();
        }

        if (colorful)
        {
            renderElement(host, hostWidth, hostHeight, Path.Combine(outDir, $"{prefix}-{theme}-{name}.png"));
            return;
        }

        renderElement(host, width ?? Monitor.WidthDip, fullWidth ? DockPositionResolver.FullWidthHeightDip : hug ? 120 : 80, Path.Combine(outDir, $"{prefix}-{theme}-{name}.png"));
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
