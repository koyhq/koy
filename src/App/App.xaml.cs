using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Koy.App.Features;
using Koy.App.Features.Dock;
using Koy.App.Features.Menu;
using Koy.App.Features.Notch;
using Koy.App.Features.Settings;
using Koy.App.Features.Shortcuts;
using Koy.App.Features.Wizard;
using Koy.App.Resources;
using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Koy.Core.Shortcuts;
using Koy.Core.Tray;
using Koy.Core.Updates;
using Koy.Platform.Media;
using Koy.Platform.Shell;
using Velopack;

namespace Koy.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack kurulum ve güncelleme kancaları: kaldırma ve güncelleme sürecin ölmesiyle biter,
        // OnExit çalışmayabilir; görev çubuğu bu kancalarda mutlaka geri getirilir. Kaldırma kancası ayrıca
        // başlatma kaydını ve durum dosyalarını siler (30 sn sınırı, onay yok; ayarlara dokunmaz). (hata verse bile süreç düşmesin).
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => AppUninstaller.OnBeforeUninstallHook())
            .OnBeforeUpdateFastCallback(_ => App.SafeRestoreTaskbar())
            .OnAfterUpdateFastCallback(_ => App.SafeRestoreTaskbar())
            .Run();

        // Başka bir Koy çalışıyorsa onun Koy penceresi açılır, bu süreç hiçbir şeye dokunmadan çıkar.
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
            return;

        var app = new App();
        app.InitializeComponent();
        instance.ListenForActivation(() =>
            app.Dispatcher.BeginInvoke(() => KoyWindow.ShowInstance()));
        app.Run();
    }
}

public partial class App : Application
{
    private readonly List<IFeature> _features = [];
    private TrayIconManager? _trayIconManager;
    private ShortcutController? _shortcuts;
    private readonly SharedServices _services = new();

    private static readonly TimeSpan PauseDuration = TimeSpan.FromHours(1);
    private DispatcherTimer? _pauseTimer;

    /// <summary>Genel kısayollar; Kısayollar sayfası kayıt sonuçlarını buradan okur.</summary>
    public ShortcutController? Shortcuts => _shortcuts;

    /// <summary>"1 saat duraklat" etkin mi: çentik ve dock kapalı, Windows görev çubuğu geri gelmiş durumda.</summary>
    public bool IsPaused { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Bellek: GPU ile çizimde sürücü sürece ~135 MB ekliyor (ölçüm: 169 MB → 34 MB private, RTX 4050).
        // Yüzeyler küçük olduğu için yazılımla çizim yeterli. Karşılaştırma için KOY_GPU=1.
        if (Environment.GetEnvironmentVariable("KOY_GPU") != "1")
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        // Görev çubuğu güvenliği: önceki oturum çöktüyse görev çubuğunu geri getir.
        SafeRestoreTaskbar();
        InstallGlobalErrorHandlers();

        var store = new SettingsStore(SettingsStore.DefaultPath);
        var settings = store.Load();

        // Tema ve ortak kaynaklar, ilk pencere (sihirbaz dahil) oluşmadan önce yüklenir.
        ThemeManager.Apply(this, settings);

        // İlk açılış sihirbazı: Ayar henüz tamamlanmadıysa 3 adımlı sihirbazı başlat
        bool isFirstRun = !settings.FirstRunCompleted;
        if (isFirstRun)
        {
            var wizard = new SetupWizardWindow();
            wizard.ShowDialog();
            settings = store.Load();
        }

        // Sihirbaz X ile kapatıldıysa (tamamlanmadıysa) dock açılıp görev çubuğu gizlenmesin.
        settings = FirstRunPolicy.ApplySafeDefaultsIfIncomplete(settings);

        var culture = new System.Globalization.CultureInfo(settings.Language == "en" ? "en-US" : "tr-TR");
        System.Globalization.CultureInfo.CurrentUICulture = culture;
        System.Globalization.CultureInfo.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;

        // Dock Koy logosu tıklandığında Sağ Alt Menüyü aç
        DockViewModel.OpenKoyMenuHandler = () => SagAltMenuWindow.Toggle();

        // Sistem tepsisi simgesi: dock kapalıyken de Koy menüsüne erişilmesini sağlar
        _trayIconManager = new TrayIconManager();
        _trayIconManager.ExplorerRestarted += OnExplorerRestarted;
        // Dock açılacaksa Koy simgesi dock'un sağ ucunda olacağı için bildirim alanı simgesi hiç eklenmez
        // (dock hiç pencere açamazsa özellikler başlayınca RefreshNotificationAreaIcon simgeyi geri getirir).
        _trayIconManager.Start(showIcon: !settings.DockEnabled);

        // Yalnız açık özellikler başlatılır. Dock kapalıyken görev çubuğuna hiç dokunulmaz.
        if (settings.NotchEnabled)
            _features.Add(new NotchFeature(settings, _services));

        if (settings.DockEnabled)
            _features.Add(CreateDockFeature(settings));

        foreach (var feature in _features)
            feature.Start();

        // Güncellemeleri otomatik denetle ayarı: açılıştan sonra arka planda tek seferlik denetim.
        _ = AutoUpdater.RunAsync(settings);

        // Genel kısayollar: çentik/dock açık olmasa da çalışır.
        _shortcuts = new ShortcutController();
        _shortcuts.Triggered += OnShortcutTriggered;
        _shortcuts.Start(settings);

        // İlk kurulumdan hemen sonra ana uygulama doğrudan ekrana çıksın
        if (isFirstRun)
        {
            KoyWindow.ShowInstance();
        }
        else if (UpdateFlowPolicy.ShouldShowWhatsNew(AppInfo.Version, settings.LastSeenVersion, settings.FirstRunCompleted))
        {
            KoyWindow.ShowInstance();
            KoyWindow.Instance.ViewModel.OpenWhatsNewOverlay();
        }
    }

    private DockFeature CreateDockFeature(AppSettings settings)
    {
        var dock = new DockFeature(settings, _services);
        dock.WindowCountChanged += () => CrashLog.Guard("Bildirim alanı simgesi", RefreshNotificationAreaIcon);
        return dock;
    }

    /// <summary>
    /// Koy'un bildirim alanı simgesi yalnız dock penceresi yokken görünür (dock'ta Koy düğmesi var; iki simge olmasın).
    /// Dock kapanınca, duraklatılınca ya da hiçbir ekranda dock açılmayınca simge geri gelir.
    /// </summary>
    private void RefreshNotificationAreaIcon()
    {
        var dockWindows = _features.OfType<DockFeature>().Sum(d => d.WindowCount);
        _trayIconManager?.SetIconVisible(TrayHostPolicy.ShouldShowNotificationAreaIcon(dockWindows));
    }

    /// <summary>Kısayol ayarı değişti: diskteki ayarlarla yeniden kaydeder.</summary>
    public void ApplyShortcuts()
    {
        _shortcuts?.Apply(new SettingsStore(SettingsStore.DefaultPath).Load());
    }

    private void OnShortcutTriggered(ShortcutAction action)
    {
        switch (action)
        {
            case ShortcutAction.ToggleNotch:
                _features.OfType<NotchFeature>().FirstOrDefault()?.ToggleExpanded();
                break;
            case ShortcutAction.OpenShelf:
                _features.OfType<NotchFeature>().FirstOrDefault()?.OpenShelf();
                break;
            case ShortcutAction.ToggleDock:
                ToggleDockEnabled();
                break;
            case ShortcutAction.OpenMenu:
                SagAltMenuWindow.Toggle();
                break;
            case ShortcutAction.PlayPause:
                _ = PlayPauseAsync();
                break;
        }
    }

    /// <summary>Dock'u göster/gizle: sağ alt menüdeki anahtarla aynı etki (ayar da kaydedilir).</summary>
    private void ToggleDockEnabled()
    {
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var current = store.Load();
        UpdateDockState(!current.DockEnabled);
    }

    private async Task PlayPauseAsync()
    {
        try
        {
            if (_services.RunningMediaService is { } running)
            {
                await running.PlayPauseAsync();
                return;
            }

            // Çentik ve dock kapalı: servisi yalnız bu iş için kur, sonra bırak (AGENTS.md #5).
            using var temporary = new MediaService();
            await temporary.StartAsync();
            await temporary.PlayPauseAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Kısayol: oynat/duraklat", ex);
        }
    }

    /// <summary>
    /// Görev çubuğunu geri getirir; hata verse bile çağıranın (çökme işleyicisi) akışını bozmaz.
    /// </summary>
    private void OnExplorerRestarted()
    {
        CrashLog.Guard("Explorer yeniden başladı", () =>
        {
            foreach (var dock in _features.OfType<DockFeature>())
                dock.ReapplyTaskbarHiding();
        });
    }

    internal static void SafeRestoreTaskbar()
    {
        try
        {
            TaskbarService.Restore();
        }
        catch (Exception ex)
        {
            CrashLog.Write("TaskbarService.Restore başarısız", ex);
        }
    }

    /// <summary>
    /// Çökme işleyicilerinde Koy'un tepsi penceresini beklemeden kapatır: görev çubuğu geri geldiği için Windows
    /// tepsisi hemen asıl sahibine döner (süreç yaşamaya devam ediyorsa da). Hata verse bile akışı bozmaz.
    /// </summary>
    private void SafeCloseTrayHost()
    {
        try
        {
            _services.RequestTrayHostClose();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Tepsi penceresi kapatılamadı", ex);
        }
    }

    private void InstallGlobalErrorHandlers()
    {
        // Önce log, sonra görev çubuğu: Restore bir istisna atsa bile kayıt yazılmış olur.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            CrashLog.Write(args.IsTerminating ? "AppDomain Unhandled (süreç sonlanıyor)" : "AppDomain Unhandled", args.ExceptionObject);
            SafeRestoreTaskbar();
            SafeCloseTrayHost();
        };

        DispatcherUnhandledException += (_, args) =>
        {
            CrashLog.Write("Dispatcher Unhandled", args.Exception);
            SafeRestoreTaskbar();
            SafeCloseTrayHost();
        };

        // Gözlenmeyen Task hataları: süreci düşürmez, ama izi kalsın.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write("Unobserved Task", args.Exception);
            args.SetObserved();
        };
    }

    public void UpdateNotchState(bool enabled)
    {
        if (IsPaused)
            return;

        var store = new SettingsStore(SettingsStore.DefaultPath);
        var current = store.Load();
        if (current.NotchEnabled != enabled)
            store.Save(current with { NotchEnabled = enabled });

        if (KoyWindow.IsCreated)
            KoyWindow.SyncFeatureStates(notchEnabled: enabled);

        if (SagAltMenuWindow.IsCreated)
            SagAltMenuWindow.SyncFeatureStates(notchEnabled: enabled);

        var existingNotch = _features.OfType<NotchFeature>().FirstOrDefault();
        if (enabled && existingNotch is null)
        {
            var settings = store.Load();
            var notch = new NotchFeature(settings, _services);
            _features.Add(notch);
            notch.Start();
        }
        else if (!enabled && existingNotch is not null)
        {
            existingNotch.Stop();
            _features.Remove(existingNotch);
        }
    }

    /// <summary>Görev çubuğu elle geri getirildi: çalışan dock Koy'un tepsi penceresini bırakır.</summary>
    public void NotifyTaskbarRestoredManually()
    {
        foreach (var dock in _features.OfType<DockFeature>())
            dock.OnTaskbarRestoredManually();
    }

    public void UpdateDockState(bool enabled)
    {
        if (IsPaused)
            return;

        var store = new SettingsStore(SettingsStore.DefaultPath);
        var current = store.Load();
        if (current.DockEnabled != enabled)
            store.Save(current with { DockEnabled = enabled });

        if (KoyWindow.IsCreated)
            KoyWindow.SyncFeatureStates(dockEnabled: enabled);

        if (SagAltMenuWindow.IsCreated)
            SagAltMenuWindow.SyncFeatureStates(dockEnabled: enabled);

        var existingDock = _features.OfType<DockFeature>().FirstOrDefault();
        if (enabled && existingDock is null)
        {
            var settings = store.Load();
            var dock = CreateDockFeature(settings);
            _features.Add(dock);
            dock.Start();
        }
        else if (!enabled && existingDock is not null)
        {
            existingDock.Stop();
            _features.Remove(existingDock);
            TaskbarService.Restore();
        }
    }

    /// <summary>
    /// Ayarlar değişince dock'u günceller. Çalışan dock yerinde güncellenir (görev çubuğu ve çalışma alanı oynamaz);
    /// yalnız dock yoksa kurulur. Dock'u gerçekten kapatan yol <see cref="UpdateDockState"/>'tir.
    /// </summary>
    public void ReloadDock()
    {
        if (IsPaused)
            return;

        var existingDock = _features.OfType<DockFeature>().FirstOrDefault();
        var settings = new SettingsStore(SettingsStore.DefaultPath).Load();
        if (existingDock is not null && settings.DockEnabled)
        {
            existingDock.ApplySettings(settings);
            return;
        }

        if (existingDock is not null)
        {
            existingDock.Stop();
            _features.Remove(existingDock);
        }

        if (settings.DockEnabled)
        {
            var dock = CreateDockFeature(settings);
            _features.Add(dock);
            dock.Start();
        }
    }

    /// <summary>Çalışan çentiği durdurur, ayarlar açıksa yeni ayarlarla yeniden başlatır.</summary>
    public void ReloadNotch()
    {
        if (IsPaused)
            return;

        var existingNotch = _features.OfType<NotchFeature>().FirstOrDefault();
        if (existingNotch is not null)
        {
            existingNotch.Stop();
            _features.Remove(existingNotch);
        }

        var settings = new SettingsStore(SettingsStore.DefaultPath).Load();
        if (settings.NotchEnabled)
        {
            var notch = new NotchFeature(settings, _services);
            _features.Add(notch);
            notch.Start();
        }
    }

    /// <summary>
    /// Duraklat: çentik ve dock durdurulur (dock Stop ile görev çubuğunu geri getirir), tepsi simgesi kalır.
    /// Bir saat sonra ya da tekrar basılınca özellikler diskteki ayarlara göre yeniden başlar.
    /// Ayar dosyasına dokunulmaz; duraklatma yalnız bu oturum için geçerlidir.
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (IsPaused == paused)
            return;

        if (paused)
        {
            foreach (var feature in _features)
                feature.Stop();
            _features.Clear();
            SafeRestoreTaskbar();

            IsPaused = true;
            _pauseTimer ??= new DispatcherTimer { Interval = PauseDuration };
            _pauseTimer.Tick -= OnPauseTimerTick;
            _pauseTimer.Tick += OnPauseTimerTick;
            _pauseTimer.Stop();
            _pauseTimer.Start();
        }
        else
        {
            IsPaused = false;
            _pauseTimer?.Stop();
            ReloadAllFeatures();
        }
    }

    private void OnPauseTimerTick(object? sender, EventArgs e)
    {
        CrashLog.Guard("Duraklatma sona erdi", () => SetPaused(false));
    }

    /// <summary>Ayarlar sıfırlandığında iki özelliği de diskteki ayarlara göre yeniden kurar.</summary>
    public void ReloadAllFeatures()
    {
        ReloadNotch();
        ReloadDock();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shortcuts?.Dispose();
        _shortcuts = null;

        _trayIconManager?.Dispose();
        _trayIconManager = null;

        foreach (var feature in _features)
            feature.Stop();

        _services.Dispose();

        TaskbarService.Restore();
        AppUninstaller.CleanAfterExit();
        base.OnExit(e);
    }
}
