using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Koy.App.Features.Menu;
using Koy.App.Features.Settings;
using Koy.App.Features.Wizard;
using Koy.App.Resources;
using Koy.Core.Settings;

namespace Koy.UiSnapshots;

/// <summary>
/// Koy'un pencerelerini ekrana çıkarmadan PNG'ye çevirir. Hiçbir Win32 kancası, AppBar ya da görev çubuğu
/// servisi başlatılmaz; pencereler gösterilmez, yalnız ölçülüp çizilir.
/// Kullanım: Koy.UiSnapshots --out &lt;klasör&gt; --prefix once|sonra [--theme dark|light|both] [--scene all|uninstall|centik|dock|dockbar|jumplist|startmenu|eklentiler]
/// <c>--scene jumplist</c>: dock sağ tık menüsünün bölümlü atlama listesi hâli (sentetik veri).
/// <c>--scene startmenu</c>: Başlat düğmesinin sağ tık menüsü (Win+X karşılığı), alt menü açık hâliyle.
/// <c>--scene eklentiler</c>: Eklentiler sayfası (depodaki store/ klasöründen sahte ağla; kurulum, uygulama ve hata durumları).
/// <c>--scene uninstall</c>: yalnız Hakkında sayfası ve kaldırma onay görünümü (kaldırıcı hiçbir zaman çalıştırılmaz).
/// <c>--scene sitedock</c>: tanıtım sitesi için, görev çubuğuna sabitlenmiş gerçek uygulamalarla kapsül dock.
/// <c>--user-theme &lt;yol&gt;</c>: o user-theme.json dosyasını (ör. store/ tema paketinin theme.json'ı) uygulayarak çizer; %AppData% dosyasına dokunulmaz.
/// <c>--transparent</c>: gri zemin çizilmez (siteye doğrudan konacak görüntüler). <c>--scale 2</c>: çözünürlük çarpanı (varsayılan 1,5).
/// </summary>
public static class Program
{
    private static double Scale = 1.5;

    /// <summary>Gri zemin yerine saydam zemin.</summary>
    private static bool Transparent;

    private static readonly string[] PageNames =
        ["genel", "centik", "dock", "ekranlar", "gorunum", "kisayollar", "dikte", "performans", "hakkinda", "gelismis"];

    /// <summary>Görüntülerde kullanılan malzeme (Katı / Buzlu / Cam).</summary>
    internal static Koy.Core.Appearance.MaterialLevel Material { get; private set; } = Koy.Core.Appearance.MaterialLevel.Solid;

    /// <summary>--user-theme ile verilen stil dosyası; null ise kullanıcı stili uygulanmaz (temasız görünüm).</summary>
    internal static string? UserThemePath { get; private set; }

    /// <summary>Çizimin arkasındaki zemin; saydam yüzeyler bunu gösterir.</summary>
    private static Color Backdrop = Color.FromRgb(0x7A, 0x80, 0x8C);

    [STAThread]
    public static int Main(string[] args)
    {
        var outDir = Arg(args, "--out") ?? Path.Combine(Environment.CurrentDirectory, "ui-snapshots");
        var prefix = Arg(args, "--prefix") ?? "snap";
        var themeArg = (Arg(args, "--theme") ?? "dark").ToLowerInvariant();
        var scene = (Arg(args, "--scene") ?? "all").ToLowerInvariant();
        Transparent = args.Contains("--transparent");
        if (Arg(args, "--user-theme") is { Length: > 0 } userTheme)
        {
            UserThemePath = Path.GetFullPath(userTheme);
            Koy.App.Resources.ThemeManager.UserStylePath = UserThemePath;
        }
        if (double.TryParse(Arg(args, "--scale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale) && scale is >= 1 and <= 4)
            Scale = scale;
        // Malzeme (kati | buzlu | cam) ve saydam yüzeyin arkasındaki duvar kağıdı (light | dark; verilmezse orta gri).
        // Duvar kağıdı hiçbir zaman gerçek masaüstünden okunmaz; verilen renk okunabilirlik korumasına da girer.
        Material = Arg(args, "--material") switch
        {
            "buzlu" or "frosted" => Koy.Core.Appearance.MaterialLevel.Frosted,
            "cam" or "glass" => Koy.Core.Appearance.MaterialLevel.Glass,
            _ => Koy.Core.Appearance.MaterialLevel.Solid
        };
        Koy.Core.Appearance.Rgb? wallpaper = (Arg(args, "--wallpaper") ?? "").ToLowerInvariant() switch
        {
            "light" => new Koy.Core.Appearance.Rgb(236, 240, 246),
            "dark" => new Koy.Core.Appearance.Rgb(24, 30, 44),
            _ => null
        };
        if (wallpaper is { } w)
            Backdrop = Color.FromRgb(w.R, w.G, w.B);
        MaterialState.SetWallpaperForPreview(wallpaper);
        Directory.CreateDirectory(outDir);

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SnapshotTheme.Install(app);

        var themes = themeArg == "both" ? new[] { "dark", "light" } : new[] { themeArg };
        foreach (var theme in themes)
        {
            SnapshotTheme.Apply(app, theme);
            if (scene == "uninstall")
                RenderUninstall(outDir, prefix, theme);
            else if (scene == "centik")
                RenderCentik(outDir, prefix, theme);
            else if (scene == "arama")
                SearchSnapshots.Render(outDir, prefix, theme, RenderElement);
            else if (scene == "sitedock")
                DockBarSnapshots.RenderSite(outDir, prefix, theme, RenderElement);
            else if (scene is "dockbar" or "dockbars")
                DockBarSnapshots.Render(outDir, prefix, theme, RenderElement);
            else if (scene is "dock" or "folder" or "folders")
                RenderDockFolders(outDir, prefix, theme);
            else if (scene is "jumplist" or "atlama")
                JumpListSnapshots.Render(outDir, prefix, theme, RenderElement);
            else if (scene is "startmenu" or "baslat")
                StartMenuSnapshots.Render(outDir, prefix, theme, RenderElement);
            else if (scene is "eklentiler" or "extensions")
                ExtensionSnapshots.Render(outDir, prefix, theme, RenderElement);
            else
            {
                RenderAll(outDir, prefix, theme);
                RenderDockFolders(outDir, prefix, theme);
                JumpListSnapshots.Render(outDir, prefix, theme, RenderElement);
                StartMenuSnapshots.Render(outDir, prefix, theme, RenderElement);
                ExtensionSnapshots.Render(outDir, prefix, theme, RenderElement);
            }
        }

        Console.WriteLine($"Bitti: {outDir}");
        return 0;
    }

    private static void RenderDockFolders(string outDir, string prefix, string theme)
    {
        DockFolderSnapshots.Render(outDir, prefix, theme, RenderElement);
    }

    private static void RenderAll(string outDir, string prefix, string theme)
    {
        // Ayarlar geçici bir dosyada: gerçek %AppData%\Koy\settings.json'a dokunulmaz.
        var tempSettings = Path.Combine(Path.GetTempPath(), "koy-uisnap-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new SettingsStore(tempSettings);
        store.Save(new AppSettings { General = new GeneralSettings { FirstRunCompleted = true }, Appearance = new AppearanceSettings { Material = Material } });

        try
        {
            var vm = new KoyWindowViewModel(store);
            var win = new KoyWindow(vm);
            // Sayfanın tamamı görünsün diye kaydırma kapatılır, pencere içeriğe göre uzar.
            if (win.FindName("ContentScrollViewer") is System.Windows.Controls.ScrollViewer sv)
                sv.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;

            // İlk ölçüm sayfa şablonlarını kurar; ardından her sayfa gerçek boyutuyla çizilir.
            ((FrameworkElement)win.Content).Measure(new Size(880, double.PositiveInfinity));
            for (var i = 0; i < PageNames.Length; i++)
            {
                vm.SelectPage(i);
                RenderElement((FrameworkElement)win.Content, 880, 680, Path.Combine(outDir, $"{prefix}-{theme}-koy-{PageNames[i]}.png"));
            }

            // "Yalnız değişenleri göster" filtre görünümü: değişmeyen sayfada boş durum, değişen sayfada gruplu satırlar
            vm.SelectPage(0);
            vm.ShowChangedOnly = true;
            RenderElement((FrameworkElement)win.Content, 880, 680, Path.Combine(outDir, $"{prefix}-{theme}-koy-genel-yalniz-degisenler.png"));

            vm.MusicLyricsEnabled = true;
            vm.NotchOpenBehavior = Koy.Core.Notch.NotchOpenBehavior.Click;
            vm.SelectPage(1);
            RenderElement((FrameworkElement)win.Content, 880, 680, Path.Combine(outDir, $"{prefix}-{theme}-koy-centik-yalniz-degisenler.png"));
            vm.ShowChangedOnly = false;

            var wizardVm = new SetupWizardViewModel(store);
            var wizard = new SetupWizardWindow(wizardVm);
            for (var step = 1; step <= SetupWizardViewModel.LastStep; step++)
            {
                wizardVm.CurrentStep = step;
                RenderElement((FrameworkElement)wizard.Content, 880, 600, Path.Combine(outDir, $"{prefix}-{theme}-sihirbaz-{step}.png"));
            }

            // Güncelleme ve Neler Geldi görünümleri
            vm.HasAvailableUpdate = true;
            vm.AvailableVersionText = string.Format(System.Globalization.CultureInfo.CurrentCulture, Koy.App.Resources.Strings.Get("UpdateAvailableBadge"), "0.2.0");
            RenderElement((FrameworkElement)win.Content, 880, 680, Path.Combine(outDir, $"{prefix}-{theme}-koy-guncelleme-dugme.png"));

            vm.OpenUpdateOverlay();
            vm.UpdateHighlights.Clear();
            vm.UpdateHighlights.Add("Windows 11 24H2 için yeni sistem tepsisi kancası");
            vm.UpdateHighlights.Add("Hızlı başlat için klavye gezintisi ve odak halkası");
            RenderElement((FrameworkElement)win.Content, 880, 680, Path.Combine(outDir, $"{prefix}-{theme}-koy-guncelleme-onay.png"));

            vm.IsDownloadingUpdate = true;
            vm.DownloadProgress = 64;
            RenderElement((FrameworkElement)win.Content, 880, 680, Path.Combine(outDir, $"{prefix}-{theme}-koy-guncelleme-indiriliyor.png"));
            vm.CloseUpdateOverlay();

            vm.OpenWhatsNewOverlay();
            if (vm.WhatsNewHighlights.Count == 0)
            {
                vm.WhatsNewHighlights.Add("Yeni çentik tasarımı ve yay fiziği");
                vm.WhatsNewHighlights.Add("Pano geçmişi ve sabitlenenler sekmesi");
            }
            RenderElement((FrameworkElement)win.Content, 880, 680, Path.Combine(outDir, $"{prefix}-{theme}-koy-neler-geldi.png"));
            // Alt kategoriler (Eklenenler, Düzeltmeler) kaydırınca görünür: notların sonu da çizilir.
            if (win.FindName("WhatsNewCard") is DependencyObject whatsNewCard)
            {
                foreach (var notesScroll in FindAll<System.Windows.Controls.ScrollViewer>(whatsNewCard))
                    notesScroll.ScrollToEnd();
                RenderElement((FrameworkElement)win.Content, 880, 680, Path.Combine(outDir, $"{prefix}-{theme}-koy-neler-geldi-alt.png"));
            }
            vm.CloseWhatsNewOverlay();

            var menu = new SagAltMenuWindow(new SagAltMenuViewModel(store));
            RenderElement((FrameworkElement)menu.Content, 320, 0, Path.Combine(outDir, $"{prefix}-{theme}-sagalt-menu.png"));

            var menuWithUpdate = new SagAltMenuWindow(new SagAltMenuViewModel(store) { HasAvailableUpdate = true, AvailableVersionText = "Yeni sürüm: 0.2.0" });
            RenderElement((FrameworkElement)menuWithUpdate.Content, 320, 0, Path.Combine(outDir, $"{prefix}-{theme}-sagalt-menu-guncelleme.png"));
        }
        finally
        {
            try { File.Delete(tempSettings); } catch { }
        }
    }

    /// <summary>
    /// Çentik H arayüzü (Ana, Odak, Raf, Kontrol ve kapalı çentik) ve ayarlardaki hava durumu bölümü.
    /// Çentik gerçek NotchWindow ve NotchViewModel ile kurulur ama gösterilmez; medya, pil, hava, ses ve radyo
    /// servisleri sahte (CentikFakes.cs), zamanlayıcılar gerçek. Gerçek veriye ve görev çubuğuna dokunulmaz.
    /// </summary>
    private static void RenderCentik(string outDir, string prefix, string theme)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "koy-uisnap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var store = new SettingsStore(Path.Combine(tempDir, "settings.json"));
        store.Save(new AppSettings { General = new GeneralSettings { FirstRunCompleted = true }, Appearance = new AppearanceSettings { Material = Material } });

        try
        {
            string Name(string n) => Path.Combine(outDir, $"{prefix}-{theme}-{n}.png");
            RenderNotchScenes(Name);
            // Ayarlar › Çentik › Hava durumu (örnek arama sonuçlarıyla)
            var kvm = new KoyWindowViewModel(store);
            var win = new KoyWindow(kvm);
            if (win.FindName("ContentScrollViewer") is System.Windows.Controls.ScrollViewer sv)
                sv.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            var root = (FrameworkElement)win.Content;
            root.Measure(new Size(880, double.PositiveInfinity));
            kvm.SelectPage(1);
            RenderElement(root, 880, 680, Name("ayar-centik-hava-bos"));

            kvm.Weather.Query = "Ankara";
            kvm.Weather.Results.Add(new Koy.Core.Weather.GeoPlace("Ankara", "Ankara", "Türkiye", 39.92, 32.85));
            kvm.Weather.Results.Add(new Koy.Core.Weather.GeoPlace("Ankara", null, "Gabon", -1.5, 10.5));
            RenderElement(root, 880, 680, Name("ayar-centik-hava-sonuc"));

            kvm.Weather.SelectPlaceCommand.Execute(kvm.Weather.Results[0]);
            RenderElement(root, 880, 680, Name("ayar-centik-hava-secili"));

            kvm.SelectPage(8);
            RenderElement(root, 880, 680, Name("ayar-hakkinda-atif"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void Flush() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

    private sealed record NotchSet(
        Koy.App.Features.Notch.NotchWindow Window,
        Koy.App.Features.Notch.NotchViewModel Vm,
        Koy.Core.Timers.FocusTimers Timers);

    private static NotchSet MakeNotch(bool music, IReadOnlyList<Koy.Core.Shelf.ShelfItem>? shelf = null, List<string>? tileOrder = null,
        bool locationBlocked = false, bool audioConnected = true, Func<Koy.Core.SystemStats.ISystemStatsService>? statsFactory = null,
        bool brightnessSupported = true, bool wideCover = false, Koy.Core.Notch.NotchStyleMode notchStyle = Koy.Core.Notch.NotchStyleMode.Pinned,
        Koy.Core.Notch.IdleDisplayMode idleMode = Koy.Core.Notch.IdleDisplayMode.ClockAndBattery, FakePower? power = null, string? language = null,
        bool clockShowDate = false)
    {
        var settings = new AppSettings
        {
            General = new GeneralSettings
            {
                FirstRunCompleted = true
            },
            Notch = new NotchSettings
            {
                Style = notchStyle,
                IdleDisplay = idleMode,
                ClockShowDate = clockShowDate,
                ControlCenterLayout = tileOrder is null ? new AppSettings().Notch.ControlCenterLayout : new Koy.Core.Settings.ValueList<string>(tileOrder)
            },
            Appearance = new AppearanceSettings
            {
                Material = Material
            },
            Weather = new WeatherSettings
            {
                LocationSource = Koy.Core.Weather.WeatherLocationSource.City,
                City = "İstanbul, Türkiye",
                Latitude = 41.01,
                Longitude = 28.97
            }
        };
        if (language is not null)
            settings.General.Language = language;

        var controller = new Koy.Core.Notch.NotchController();
        controller.SetEnabledPanels(Koy.Core.Notch.NotchPanels.Order);
        var timers = new Koy.Core.Timers.FocusTimers(Koy.Core.Timers.PomodoroConfig.Default);
        var info = music ? new Koy.Core.Media.MediaInfo("Gece Yolculuğu", "Mor ve Ötesi", "Spotify.exe", true, wideCover ? FakeCover(150, 83) : FakeCover(300, 300)) : null;

        var vm = new Koy.App.Features.Notch.NotchViewModel(
            settings,
            controller,
            // Yakalama başlatılmayan servis: çubuklar aşağıda sahte düzeylerle doldurulur, gerçek ses okunmaz.
            new Koy.App.Features.Music.MusicViewModel(new FakeMedia(info), spectrumAcquire: () => new Koy.Platform.Audio.SpectrumCaptureService(), lyricsProvider: new FakeLyrics()),
            new Koy.App.Features.ControlCenter.ControlCenterViewModel(
                () => new FakeAudio(), () => new FakeRadio(), null, () => new FakeBrightness(brightnessSupported), new FakeFocusAssist(), settings,
                wifiFactory: () => new FakeWifi(locationBlocked),
                bluetoothDevices: new Koy.App.Features.ControlCenter.BluetoothDevicesViewModel(() => new FakeBluetooth(audioConnected), null)),
            new Koy.App.Features.Status.StatusViewModel(power ?? new FakePower(), new FakeWeather(), settings),
            new Koy.App.Features.Focus.FocusViewModel(timers, settings, tasks: MakeTasks()),
            new Koy.App.Features.Shelf.ShelfViewModel(new FakeShelf(shelf ?? [])),
            MakeCalendar(settings),
            new Koy.App.Features.SystemInfo.SystemStatsViewModel(statsFactory ?? (() => new FakeSystemStats()), () => { }),
            MakeQuickLaunch(),
            new Koy.App.Features.ClipboardPanel.ClipboardViewModel(new FakeClipboard()));

        var window = new Koy.App.Features.Notch.NotchWindow { DataContext = vm };
        return new NotchSet(window, vm, timers);
    }

    // Deneme kapağı: 300 px desenli PNG (gerçek kapaklar 300–640 px); keskinlik ve yuvarlak köşe kontrolü için ince çizgiler.
    private static byte[] FakeCover(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                var line = x % 20 == 0 || y % 20 == 0;
                pixels[i] = (byte)(line ? 255 : 80 + x / 3);
                pixels[i + 1] = (byte)(line ? 255 : 40 + y / 3);
                pixels[i + 2] = (byte)(line ? 255 : 140);
                pixels[i + 3] = 255;
            }
        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // Tek kullanımlık geçici dosyalar: gerçek tasks.json / events.json'a dokunulmaz.
    private static string TempFile(string name) => Path.Combine(Path.GetTempPath(), "koy-uisnap-h-" + Guid.NewGuid().ToString("N") + "-" + name);

    private static Koy.App.Features.Focus.TasksViewModel MakeTasks()
    {
        var service = new Koy.Core.Tasks.TaskService(TempFile("tasks.json"));
        var done = service.Add("Sunumu gönder");
        service.Add("Raporu gözden geçir");
        service.Add("Toplantı notlarını yaz", new TimeOnly(16, 0));
        service.Add("Spor", new TimeOnly(18, 30));
        service.SetDone(done!.Id, true);
        return new Koy.App.Features.Focus.TasksViewModel(service);
    }

    private static Koy.App.Features.Calendar.CalendarViewModel MakeCalendar(AppSettings settings)
    {
        var events = new Koy.App.Features.Calendar.SharedEvents(new Koy.Core.Calendar.EventStore(TempFile("events.json")));
        var today = DateOnly.FromDateTime(DateTime.Today);
        events.Book.Add("Takım toplantısı", today, new TimeOnly(14, 0));
        events.Book.Add("Diş randevusu", today, new TimeOnly(17, 30));
        events.Book.Add("Sunum", today.AddDays(2), new TimeOnly(10, 0));
        events.Book.Add("Doğum günü", today.AddDays(5), new TimeOnly(19, 0));
        events.Book.Add("Fatura son gün", today.AddDays(-3), new TimeOnly(12, 0));
        return new Koy.App.Features.Calendar.CalendarViewModel(settings, events);
    }

    private static Koy.App.Features.Home.QuickLaunchViewModel MakeQuickLaunch()
    {
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var service = new Koy.Core.QuickLaunch.QuickLaunchService(
            [Path.Combine(win, "notepad.exe"), Path.Combine(win, "System32", "calc.exe"), Path.Combine(win, "System32", "mspaint.exe"),
             Path.Combine(win, "explorer.exe"), Path.Combine(win, "System32", "cmd.exe")],
            _ => { }, new FakeShellActions());
        return new Koy.App.Features.Home.QuickLaunchViewModel(service);
    }

    // Simgeler arka planda çözülür: kısa süre bekleyip iş parçacığı kuyruğunu boşalt.
    private static void Settle()
    {
        for (var i = 0; i < 8; i++)
        {
            Thread.Sleep(120);
            Flush();
        }
    }

    private static void ShotNotch(NotchSet set, string path, bool expanded)
    {
        var shape = (Koy.App.Features.Notch.NotchShape)set.Window.FindName("Notch");
        if (expanded)
        {
            shape.Width = Koy.Core.Notch.NotchLayout.ExpandedWidth;
            shape.Height = Koy.Core.Notch.NotchLayout.ExpandedHeight;
            shape.CornerRadius = Koy.Core.Notch.NotchStyle.CornerRadius(set.Vm.Settings.Notch.Style, Koy.Core.Notch.NotchSurface.Expanded, shape.Height, 34);
        }
        else
        {
            var peek = set.Vm.State == Koy.Core.Notch.NotchState.Peek;
            shape.Width = peek ? 300 : set.Vm.IsMusicPlaying ? 280 : 236;
            shape.Height = peek ? 38 : 34;
            shape.CornerRadius = Koy.Core.Notch.NotchStyle.CornerRadius(set.Vm.Settings.Notch.Style, Koy.Core.Notch.NotchSurface.Closed, shape.Height, 18);
        }

        Settle();
        RenderElement((FrameworkElement)set.Window.Content, Koy.Core.Notch.NotchLayout.CanvasWidth, Koy.Core.Notch.NotchLayout.CanvasHeight, path);
    }

    private static void RenderNotchScenes(Func<string, string> name)
    {
        var shelfItems = new[]
        {
            new Koy.Core.Shelf.ShelfItem("1", "sunum.pdf", @"C:\Belgeler\sunum.pdf", Koy.Core.Shelf.ShelfItemKind.File, DateTimeOffset.Now),
            new Koy.Core.Shelf.ShelfItem("2", "ekran.png", @"C:\Belgeler\ekran.png", Koy.Core.Shelf.ShelfItemKind.File, DateTimeOffset.Now),
            new Koy.Core.Shelf.ShelfItem("3", "notlar", @"C:\Belgeler\notlar", Koy.Core.Shelf.ShelfItemKind.Directory, DateTimeOffset.Now),
            new Koy.Core.Shelf.ShelfItem("4", "Kopyalanan metin", "Kopyalanan metin", Koy.Core.Shelf.ShelfItemKind.Text, DateTimeOffset.Now)
        };

        // Kapalı çentik: boşta, tarihli, müzik çalarken, zamanlayıcı çalışırken.
        var set = MakeNotch(music: false);
        ShotNotch(set, name("kapali"), expanded: false);
        set.Timers.Toggle(Koy.Core.Timers.FocusKind.Focus);
        Flush();
        ShotNotch(set, name("kapali-zamanlayici"), expanded: false);
        set.Vm.Dispose();

        set = MakeNotch(music: false, clockShowDate: true);
        ShotNotch(set, name("kapali-tarih"), expanded: false);
        set.Vm.Dispose();

        set = MakeNotch(music: true);
        ShotNotch(set, name("kapali-muzik"), expanded: false);
        set.Vm.Dispose();

        // Gizlilik noktaları (kamera ve mikrofon aktif)
        set = MakeNotch(music: false);
        set.Vm.UpdatePrivacyState(new Koy.Core.Privacy.PrivacyState(true, true, "CameraApp", "MicApp"));
        ShotNotch(set, name("kapali-gizlilik"), expanded: false);
        set.Vm.Dispose();

        // Halka pil ve gün/saat (#63): %43, düşük pil, şarjda, pilsiz kasa, zamanlayıcı, Peek, İngilizce.
        var ring = Koy.Core.Notch.IdleDisplayMode.RingClockDay;
        set = MakeNotch(music: false, idleMode: ring, power: new FakePower(43));
        ShotNotch(set, name("halka"), expanded: false);
        set.Vm.Controller.OnMouseEnter();
        Flush();
        ShotNotch(set, name("halka-peek"), expanded: false);
        set.Vm.Controller.OnMouseLeave();
        set.Vm.Controller.OnMouseLeaveTimeout();
        set.Timers.Toggle(Koy.Core.Timers.FocusKind.Focus);
        Flush();
        ShotNotch(set, name("halka-zamanlayici"), expanded: false);
        set.Vm.Dispose();
        set = MakeNotch(music: false, idleMode: ring, power: new FakePower(12));
        ShotNotch(set, name("halka-dusuk"), expanded: false);
        set.Vm.Dispose();
        set = MakeNotch(music: false, idleMode: ring, power: new FakePower(100, charging: true));
        ShotNotch(set, name("halka-sarj"), expanded: false);
        set.Vm.Dispose();
        set = MakeNotch(music: false, idleMode: ring, power: new FakePower(hasBattery: false));
        ShotNotch(set, name("halka-pilsiz"), expanded: false);
        set.Vm.Dispose();
        set = MakeNotch(music: false, idleMode: ring, power: new FakePower(43), language: "en");
        ShotNotch(set, name("halka-en"), expanded: false);
        set.Vm.Dispose();

        // Yüzen ada: kulaksız kapsül (pencerenin 9 DIP kayması canlı pencerede; burada yalnız şekil).
        set = MakeNotch(music: false, notchStyle: Koy.Core.Notch.NotchStyleMode.Floating);
        ShotNotch(set, name("yuzen-kapali"), expanded: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Home);
        ShotNotch(set, name("yuzen-acik"), expanded: true);
        set.Vm.Dispose();

        // Ana: müzik çalıyor / çalmıyor + çalışan zamanlayıcı satırı.
        set = MakeNotch(music: true);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Home);
        set.Vm.Music.SetSpectrumAllowed(true);
        set.Vm.Music.Spectrum.Push([0.3f, 0.55f, 0.8f, 0.45f, 0.9f, 0.7f, 0.35f, 0.6f, 0.85f, 1f, 0.5f, 0.3f, 0.45f, 0.75f, 0.9f, 0.6f, 0.35f, 0.7f, 0.85f, 0.45f, 0.3f, 0.5f, 0.7f, 0.35f]);
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        ShotNotch(set, name("ana-muzik"), expanded: true);
        // Müzik kartına tıklanınca: büyük kart Ana gövdesini kaplar.
        set.Vm.ShowHomePageCommand.Execute(Koy.Core.Notch.NotchHomePage.Home);
        ShotNotch(set, name("ana-muzik-buyuk"), expanded: true);
        set.Vm.Dispose();

        // Müzik kartı stilleri (#66): Albüm kapağı (küçük ve büyük kart) ve Düz.
        foreach (var style in new[] { Koy.Core.Media.MusicCardStyle.Artwork, Koy.Core.Media.MusicCardStyle.Neutral })
        {
            set = MakeNotch(music: true);
            set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Home);
            set.Vm.Music.SetCardStyle(style);
            Flush();
            ShotNotch(set, name("ana-muzik-" + style.ToString().ToLowerInvariant()), expanded: true);
            set.Vm.ShowHomePageCommand.Execute(Koy.Core.Notch.NotchHomePage.Home);
            ShotNotch(set, name("ana-muzik-" + style.ToString().ToLowerInvariant() + "-buyuk"), expanded: true);
            set.Vm.Dispose();
        }

        // Video kapağı (Chrome/YouTube Windows'a ~150×83 verir): kırpılmadan tamamı, arkada bulanık hâli.
        set = MakeNotch(music: true, wideCover: true);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Home);
        ShotNotch(set, name("ana-muzik-video"), expanded: true);
        set.Vm.ShowHomePageCommand.Execute(Koy.Core.Notch.NotchHomePage.Home);
        ShotNotch(set, name("ana-muzik-video-buyuk"), expanded: true);
        set.Vm.Dispose();

        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Home);
        ShotNotch(set, name("ana-bos"), expanded: true);
        set.Timers.Toggle(Koy.Core.Timers.FocusKind.Focus);
        Thread.Sleep(1100);
        Flush();
        ShotNotch(set, name("ana-bos-zamanlayici"), expanded: true);

        // Odak: Odak (çalışıyor), Mola, Sayaç, Kronometre (turlarla).
        set.Vm.Controller.GoToPanel(Koy.Core.Notch.NotchPanel.Focus);
        ShotNotch(set, name("odak"), expanded: true);
        set.Vm.Focus.Tasks!.SetScopeCommand.Execute(Koy.Core.Tasks.TaskScope.Week);
        ShotNotch(set, name("odak-gorevler-hafta"), expanded: true);
        set.Vm.Focus.Tasks!.SetScopeCommand.Execute(Koy.Core.Tasks.TaskScope.Day);
        set.Vm.Focus.SelectTabCommand.Execute(Koy.Core.Timers.FocusKind.Break);
        ShotNotch(set, name("odak-mola"), expanded: true);
        set.Vm.Focus.SelectTabCommand.Execute(Koy.Core.Timers.FocusKind.Countdown);
        ShotNotch(set, name("odak-sayac"), expanded: true);
        set.Vm.Focus.SelectTabCommand.Execute(Koy.Core.Timers.FocusKind.Stopwatch);
        set.Vm.Focus.ToggleCommand.Execute(null);
        Thread.Sleep(1300);
        set.Vm.Focus.LapCommand.Execute(null);
        Thread.Sleep(700);
        set.Vm.Focus.LapCommand.Execute(null);
        Flush();
        ShotNotch(set, name("odak-kronometre"), expanded: true);
        set.Vm.Dispose();

        // Pano, Sistem, Takvim (ay, hafta, gün).
        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Clipboard);
        ShotNotch(set, name("pano"), expanded: true);
        set.Vm.Dispose();

        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.System);
        ShotNotch(set, name("sistem"), expanded: true);
        set.Vm.Dispose();

        // Sekmeden dönüş: servis yeni kurulur, yalnız anlık değerler (RAM) hazırdır; CPU ve hız son bilinen değerle görünür.
        var instant = false;
        set = MakeNotch(music: false, statsFactory: () => new FakeSystemStats(instantOnly: instant));
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.System);
        set.Vm.Controller.GoToPanel(Koy.Core.Notch.NotchPanel.Shelf);
        instant = true;
        set.Vm.Controller.GoToPanel(Koy.Core.Notch.NotchPanel.System);
        ShotNotch(set, name("sistem-geri-donus"), expanded: true);
        set.Vm.Controller.GoToPanel(Koy.Core.Notch.NotchPanel.Home);
        ShotNotch(set, name("ana-geri-donus"), expanded: true);
        set.Vm.Dispose();

        // İlk açılış: hiç oran yokken CPU ve hız "–", RAM hemen dolu.
        set = MakeNotch(music: false, statsFactory: () => new FakeSystemStats(instantOnly: true));
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.System);
        ShotNotch(set, name("sistem-ilk-acilis"), expanded: true);
        set.Vm.Dispose();

        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Calendar);
        ShotNotch(set, name("takvim-ay"), expanded: true);
        set.Vm.Calendar.SetViewCommand.Execute(Koy.Core.Calendar.CalendarViewMode.Week);
        ShotNotch(set, name("takvim-hafta"), expanded: true);
        set.Vm.Calendar.SetViewCommand.Execute(Koy.Core.Calendar.CalendarViewMode.Day);
        ShotNotch(set, name("takvim-gun"), expanded: true);
        set.Vm.Dispose();

        // Raf: dolu ve boş. Kontrol: varsayılan sıra ve kayıtlı özel sıra.
        set = MakeNotch(music: false, shelfItems);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Shelf);
        ShotNotch(set, name("raf"), expanded: true);
        set.Vm.Dispose();

        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Shelf);
        ShotNotch(set, name("raf-bos"), expanded: true);
        set.Vm.Dispose();

        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.ControlCenter);
        Flush();
        ShotNotch(set, name("kontrol"), expanded: true);
        set.Vm.Dispose();

        // Parlaklık yolu yoksa kaydırıcı devre dışı ve neden yazılı.
        set = MakeNotch(music: false, brightnessSupported: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.ControlCenter);
        Flush();
        ShotNotch(set, name("kontrol-parlaklik-yok"), expanded: true);
        set.Vm.Dispose();

        // Kayıtlı özel döşeme sırası korunuyor mu
        set = MakeNotch(music: false, tileOrder: ["donotdisturb", "wifi", "bluetooth"]);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.ControlCenter);
        Flush();
        ShotNotch(set, name("kontrol-ozel-sira"), expanded: true);
        set.Vm.Dispose();

        RenderWirelessScenes(name);
    }

    // Bölünmüş döşeme (ok üstüne gelinmiş / basılmış), Wi-Fi ve Bluetooth listeleri, Ana kulaklık kartı.
    private static void RenderWirelessScenes(Func<string, string> name)
    {
        var set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.ControlCenter);
        Flush();
        ShotNotch(set, name("kontrol-bolunmus"), expanded: true);
        var wifiChevron =FindAll<System.Windows.Controls.Button>((System.Windows.DependencyObject)set.Window.Content)
            .First(b => System.Windows.Automation.AutomationProperties.GetName(b) == Koy.App.Resources.Strings.ControlOpenWifiList);
        ForceState(wifiChevron, "IsMouseOverPropertyKey", typeof(System.Windows.UIElement), true);
        ShotNotch(set, name("kontrol-ok-hover"), expanded: true);
        ForceState(wifiChevron, "IsPressedPropertyKey", typeof(System.Windows.Controls.Primitives.ButtonBase), true);
        ShotNotch(set, name("kontrol-ok-basili"), expanded: true);
        ForceState(wifiChevron, "IsPressedPropertyKey", typeof(System.Windows.Controls.Primitives.ButtonBase), false);
        ForceState(wifiChevron, "IsMouseOverPropertyKey", typeof(System.Windows.UIElement), false);
        set.Vm.Dispose();

        // Wi-Fi listesi: bağlı ağ üstte, kayıtlı / açık / güvenli ağlar, gizli ağ.
        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.ControlCenter);
        set.Vm.ControlCenter.OpenPageCommand.Execute(Koy.Core.Radio.RadioKind.WiFi);
        Flush();
        ShotNotch(set, name("wifi-liste"), expanded: true);

        // Güvenli ve kayıtsız ağa tıklanınca satırın içinde şifre kutusu; bağlanıyor ve hata satırları.
        var rows = set.Vm.ControlCenter.Wifi.Rows;
        var secured = rows.First(r => r.Ssid == "Komşu_5G");
        set.Vm.ControlCenter.Wifi.ActivateCommand.Execute(secured);
        Flush();
        Settle();
        var box = FindAll<System.Windows.Controls.PasswordBox>((System.Windows.DependencyObject)set.Window.Content).FirstOrDefault(p => p.DataContext is Koy.App.Features.ControlCenter.WifiRowViewModel { IsPasswordOpen: true });
        if (box is not null)
            box.Password = "12345678";
        ShotNotch(set, name("wifi-sifre"), expanded: true);
        secured.IsPasswordOpen = false;
        rows.First(r => r.Ssid == "Komşu_5G").IsBusy = true;
        rows.First(r => r.Ssid == "Kafe Misafir").ErrorText = Koy.App.Resources.Strings.Get("WifiErrTimeout");
        ShotNotch(set, name("wifi-baglaniyor-hata"), expanded: true);
        set.Vm.Dispose();

        // Konum izni yok: ağ adları gizli, açık mesaj ve konum ayarları bağlantısı.
        set = MakeNotch(music: false, locationBlocked: true);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.ControlCenter);
        set.Vm.ControlCenter.OpenPageCommand.Execute(Koy.Core.Radio.RadioKind.WiFi);
        Flush();
        ShotNotch(set, name("wifi-izin-yok"), expanded: true);
        set.Vm.Dispose();

        // Bluetooth listesi
        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.ControlCenter);
        set.Vm.ControlCenter.OpenPageCommand.Execute(Koy.Core.Radio.RadioKind.Bluetooth);
        Flush();
        ShotNotch(set, name("bluetooth-liste"), expanded: true);
        set.Vm.Dispose();

        // Ana: kulaklık kartı (bağlı ses cihazı varken) ve kart yokken bugünkü düzen
        set = MakeNotch(music: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Home);
        Flush();
        ShotNotch(set, name("ana-kulaklik"), expanded: true);
        set.Vm.Dispose();

        set = MakeNotch(music: true, audioConnected: false);
        set.Vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Home);
        Flush();
        ShotNotch(set, name("ana-kulaklik-yok"), expanded: true);
        set.Vm.Dispose();
    }

    private static IEnumerable<T> FindAll<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (var nested in FindAll<T>(child))
                yield return nested;
        }
    }

    // Çevrimdışı çizimde fare yok: hover / basılı hâli, salt okunur özelliğin anahtarıyla (yansıma) zorlanır.
    private static void ForceState(System.Windows.UIElement element, string keyName, Type owner, bool value)
    {
        var field = owner.GetField(keyName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (field?.GetValue(null) is System.Windows.DependencyPropertyKey key)
            element.SetValue(key, value);
        else
            Console.WriteLine($"uyarı: {keyName} bulunamadı, hover görüntüsü alınamaz");
    }
    /// <summary>Hakkında sayfası (kaldırma düğmesi kapalı ve açık) ve onay görünümü (ayarlar saklanır / silinir).</summary>
    private static void RenderUninstall(string outDir, string prefix, string theme)
    {
        var tempSettings = Path.Combine(Path.GetTempPath(), "koy-uisnap-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new SettingsStore(tempSettings);
        store.Save(new AppSettings { General = new GeneralSettings { FirstRunCompleted = true }, Appearance = new AppearanceSettings { Material = Material } });

        try
        {
            var vm = new KoyWindowViewModel(store);
            var win = new KoyWindow(vm);
            if (win.FindName("ContentScrollViewer") is System.Windows.Controls.ScrollViewer sv)
                sv.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            var root = (FrameworkElement)win.Content;
            root.Measure(new Size(880, double.PositiveInfinity));
            vm.SelectPage(8);

            string Path_(string name) => Path.Combine(outDir, $"{prefix}-{theme}-{name}.png");

            // Geliştirme çalıştırması: kurulu değil, düğme kapalı.
            RenderElement(root, 880, 680, Path_("hakkinda-devre-disi"));

            // Kurulu sürüm gibi: düğme açık. Gerçek kaldırıcıya hiçbir bağ yok, yalnız görünüm.
            vm.UninstallAvailability = Koy.Core.Uninstall.UninstallAvailability.Available;
            RenderElement(root, 880, 680, Path_("hakkinda"));

            vm.IsUninstallConfirming = true;
            RenderElement(root, 880, 680, Path_("onay"));

            vm.KeepSettingsOnUninstall = false;
            RenderElement(root, 880, 680, Path_("onay-ayarlar-silinecek"));

            vm.UninstallError = Koy.App.Resources.Strings.Get("UninstallFailed");
            RenderElement(root, 880, 680, Path_("onay-hata"));
        }
        finally
        {
            try { File.Delete(tempSettings); } catch { }
        }
    }

    /// <summary>Elemanı ölçer, yerleştirir ve nötr bir zemin üstüne PNG olarak yazar.</summary>
    private static void RenderElement(FrameworkElement el, double width, double minHeight, string path)
    {
        el.Measure(new Size(width, double.PositiveInfinity));
        var height = Math.Max(minHeight, Math.Ceiling(el.DesiredSize.Height));
        el.Measure(new Size(width, height));
        el.Arrange(new Rect(0, 0, width, height));
        el.UpdateLayout();

        const double pad = 16;
        var pxW = (int)Math.Ceiling((width + pad * 2) * Scale);
        var pxH = (int)Math.Ceiling((height + pad * 2) * Scale);

        var shot = new RenderTargetBitmap((int)Math.Ceiling(width * Scale), (int)Math.Ceiling(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        shot.Render(el);

        // Köşeler saydam kalmasın diye orta gri zemin: hem koyu hem açık tema okunur.
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            if (!Transparent)
                dc.DrawRectangle(new SolidColorBrush(Backdrop), null, new Rect(0, 0, pxW, pxH));
            dc.DrawImage(shot, new Rect(pad * Scale, pad * Scale, shot.PixelWidth, shot.PixelHeight));
        }
        var final = new RenderTargetBitmap(pxW, pxH, 96, 96, PixelFormats.Pbgra32);
        final.Render(dv);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(final));
        using var fs = File.Create(path);
        enc.Save(fs);
        Console.WriteLine(path);
    }

    private static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
