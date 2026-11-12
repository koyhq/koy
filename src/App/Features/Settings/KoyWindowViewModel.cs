using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.Core.Notch;
using Koy.App.Resources;
using Koy.Core.Appearance;
using Koy.Core.Dock;
using Koy.Core.Media;
using Koy.Core.Settings;
using Koy.Core.Uninstall;
using Koy.Core.Updates;
using Koy.Core.Visibility;
using Koy.Core.Weather;
using Koy.Core.Voice;
using Koy.App.Features.Notch;
using Koy.Core.Notifications;
using Koy.Platform.Notifications;
using Koy.Platform.Shell;

namespace Koy.App.Features.Settings;

public sealed partial class KoyWindowViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    public SettingsStore Store => _store;

    /// <summary>Kısayollar sayfası: tuş yakalama, kayıt ve hata durumu.</summary>
    public ShortcutsViewModel Shortcuts { get; }

    /// <summary>Ekranlar sayfası: monitör şeması ve ekran başına ayarlar.</summary>
    public DisplaysViewModel Displays { get; }

    /// <summary>Çentik sayfası › Hava durumu: konum anahtarı ve şehir arama.</summary>
    public WeatherLocationViewModel Weather { get; }

    [ObservableProperty]
    private int _selectedPageIndex; // 0: Genel, 1: Çentik, 2: Dock, 3: Ekranlar, 4: Görünüm, 5: Kısayollar, 6: Dikte, 7: Performans, 8: Hakkında

    // --- GENEL ---
    [ObservableProperty]
    private bool _runAtStartup;

    [ObservableProperty]
    private bool _hideWindowsTaskbar = true;

    [ObservableProperty]
    private string _language = AppLanguage.SystemDefault;

    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    [ObservableProperty]
    private UpdateChannel _updateChannel = UpdateChannel.Stable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastCheckText), nameof(AboutStatusText), nameof(CheckButtonText), nameof(CanCheckUpdates))]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdatesCommand))]
    private bool _isCheckingUpdates;

    /// <summary>Son denetimin gerçek durumu; hiç denetim yapılmadıysa "Henüz denetlenmedi".</summary>
    public string LastCheckText
    {
        get
        {
            if (IsCheckingUpdates)
                return Strings.Get("UpdateChecking");
            return UpdateStatus.LastResult switch
            {
                UpdateCheckResult.Failed => Strings.Get("UpdateCheckFailed"),
                UpdateCheckResult.NotInstalled => Strings.Get("UpdateNotInstalled"),
                UpdateCheckResult.UpdateReady => Strings.Get("UpdateReadyOnExit"),
                _ when UpdateStatus.LastCheck is { } t => string.Format(Strings.Get("UpdateLastCheck"), FormatCheckTime(t, DateTime.Now)),
                _ => Strings.Get("UpdateNeverChecked")
            };
        }
    }

    public string CheckButtonText => Strings.Get(IsCheckingUpdates ? "UpdateChecking" : "CheckNow");
    public bool CanCheckUpdates => !IsCheckingUpdates;

    private static string FormatCheckTime(DateTime t, DateTime now) =>
        t.Date == now.Date ? string.Format(Strings.Get("UpdateToday"), t.ToString("HH:mm")) : t.ToString("d MMM HH:mm");

    // --- ÇENTİK ---
    [ObservableProperty]
    private bool _notchEnabled = true;

    [ObservableProperty]
    private VisibilityMode _notchVisibility = VisibilityMode.Smart;

    [ObservableProperty]
    private bool _notchHideInFullscreen = true;

    [ObservableProperty]
    private IdleDisplayMode _idleDisplayMode;

    [ObservableProperty]
    private NotchStyleMode _notchStyle = NotchStyleMode.Pinned;

    [ObservableProperty]
    private bool _musicShowWaveform = true;

    [ObservableProperty]
    private bool _musicLyricsEnabled;

    [ObservableProperty]
    private MusicCardStyle _musicCardStyle = MusicCardStyle.Tinted;

    [ObservableProperty]
    private bool _showVolumeOsd = true;

    [ObservableProperty]
    private bool _showBrightnessOsd = true;

    [ObservableProperty]
    private bool _showPrivacyIndicator = true;

    /// <summary>Windows bildirimleri çentikte kart olarak görünsün (<c>AppSettings.NotchShowNotifications</c>).</summary>
    [ObservableProperty]
    private bool _notchShowNotifications = true;

    [ObservableProperty]
    private bool _notchNotificationsHideBody;

    /// <summary>Ayar açık ama Windows bildirim izni yok (reddedildi ya da okunamadı): sayfada açıklama satırı çıkar.</summary>
    [ObservableProperty]
    private bool _notificationsPermissionMissing;

    [ObservableProperty]
    private bool _panelFocus = true;

    [ObservableProperty]
    private bool _panelShelf = true;

    [ObservableProperty]
    private bool _panelCalendar = true;

    [ObservableProperty]
    private bool _panelControlCenter = true;

    [ObservableProperty]
    private bool _panelClipboard = true;

    [ObservableProperty]
    private bool _panelSystem = true;

    /// <summary>
    /// Pano geçmişi (<c>AppSettings.ClipboardHistoryEnabled</c>). Kapalıyken pano dinlenmez ve Pano sekmesi görünmez;
    /// değişince çentik yeniden kurulur (servis alınır ya da bırakılır).
    /// </summary>
    [ObservableProperty]
    private bool _clipboardHistoryEnabled = true;

    [ObservableProperty]
    private NotchOpenBehavior _notchOpenBehavior = NotchOpenBehavior.Click;

    // --- DOCK ---
    [ObservableProperty]
    private bool _dockEnabled = true;

    [ObservableProperty]
    private DockStyleMode _dockStyle = DockStyleMode.FloatingCapsule;

    [ObservableProperty]
    private DockIconAlignment _dockIconAlignment = DockIconAlignment.Center;

    [ObservableProperty]
    private DockProximityMode _dockProximity = DockProximityMode.ReserveSpace;

    // Tam genişlikte Aşağı yapış / Çekil sunulmaz: seçiliyse geçerli karşılığına (Yer ayır / Hep gizli) çekilir.
    // Kapsüle dönünce yeni seçim yapılır; ayar dosyası yüklenirken değer bozulmaz.
    partial void OnDockStyleChanged(DockStyleMode value)
    {
        if (_isLoading)
            return;

        DockProximity = DockProximityMapping.Effective(value, DockProximity);
    }

    [ObservableProperty]
    private bool _dockHideInFullscreen = true;

    [ObservableProperty]
    private bool _dockEdgeReveal = true;

    [ObservableProperty]
    private int _dockDodgeDelayMs = 150;

    [ObservableProperty]
    private double _dockIconSize = 28.0;

    [ObservableProperty]
    private bool _dockHoverMagnify = true;

    [ObservableProperty]
    private DockIndicatorStyle _dockIndicatorStyle;

    [ObservableProperty]
    private bool _dockShowBadges = true;

    [ObservableProperty]
    private bool _dockShowStartButton = true;

    [ObservableProperty]
    private DockStartIcon _dockStartIcon = DockStartIcon.Koy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartIconFileName))]
    private string _dockStartIconPath = "";

    /// <summary>Özel simge satırında gösterilen dosya adı; seçilmemişse "görsel seçilmedi".</summary>
    public string StartIconFileName => string.IsNullOrWhiteSpace(DockStartIconPath)
        ? Strings.Get("DockStartIconNoFile")
        : System.IO.Path.GetFileName(DockStartIconPath);

    /// <summary>
    /// "Özel" seçilince ya da "Görsel seç" düğmesine basılınca çalışır. Yalnız kullanıcı tıklamasından çağrılır:
    /// ayar yüklenirken (özellik değişimi) dosya penceresi açılmasın diye bir özellik değişim kancasına bağlanmaz.
    /// </summary>
    [RelayCommand]
    public void PickStartIcon()
    {
        if (_isLoading)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Get("DockStartIconPickTitle"),
            Filter = Strings.Get("DockStartIconPickFilter"),
            CheckFileExists = true
        };

        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var accepted = owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();

        if (accepted == true && DockStartIconResolver.IsSupportedFile(dialog.FileName))
        {
            // Önce yol, sonra tür: tek geçerli durum olarak kaydedilir.
            DockStartIconPath = dialog.FileName;
            DockStartIcon = DockStartIcon.Custom;
        }
        else if (DockStartIconResolver.Resolve(DockStartIcon.Custom, DockStartIconPath).Kind != DockStartIcon.Custom)
        {
            // İptal edildi ve elde geçerli dosya yok: "Özel" boş kalmasın, varsayılana dön.
            DockStartIcon = DockStartIcon.Koy;
        }
    }

    [ObservableProperty]
    private bool _dockShowFolders = true;

    [ObservableProperty]
    private bool _dockShowRecents = true;

    [ObservableProperty]
    private bool _dockShowTray = true;

    [ObservableProperty]
    private bool _dockSmallTray = true;

    [ObservableProperty]
    private bool _dockThumbnailPreview = true;

    [ObservableProperty]
    private int _dockThumbnailDelayMs = 400;

    // --- EKRANLAR ---
    [ObservableProperty]
    private bool _screen1Notch = true;

    [ObservableProperty]
    private bool _screen1Dock = true;

    [ObservableProperty]
    private bool _screen1WindowsOnly;

    [ObservableProperty]
    private int _newScreenDefault; // 0: Çentik ve dock, 1: Yalnız çentik, 2: Hiçbiri

    // --- GÖRÜNÜM ---
    [ObservableProperty]
    private AppTheme _theme = AppTheme.Dark;

    [ObservableProperty]
    private string _accentColor = "#3B82F6";

    [ObservableProperty]
    private bool _useWindowsAccent;

    [ObservableProperty]
    private MaterialLevel _material = MaterialLevel.Solid;

    /// <summary>Okunabilirlik koruması: açık duvar kağıdında saydam malzeme daha opak seviyeye çıkar.</summary>
    [ObservableProperty]
    private bool _materialReadabilityGuard = true;

    /// <summary>Açık çentik malzemeyi izlesin; kapalıysa açık çentik hep opak.</summary>
    [ObservableProperty]
    private bool _notchFollowsMaterial = true;

    [ObservableProperty]
    private double _cornerRadius = 12.0;

    [ObservableProperty]
    private AnimationSpeed _animationSpeed = AnimationSpeed.Normal;

    [ObservableProperty]
    private bool _respectReduceMotion = true;

    // --- YAZI VE KOY ÖLÇEĞİ (issue #71) ---
    /// <summary>Arayüz yazı tipi; boş = Koy varsayılanı.</summary>
    [ObservableProperty]
    private string _uiFontFamily = "";

    /// <summary>Saat yazı tipi; boş = arayüz yazı tipi.</summary>
    [ObservableProperty]
    private string _clockFontFamily = "";

    [ObservableProperty]
    private double _baseFontSize = FontScalePolicy.DefaultBaseFontSize;

    [ObservableProperty]
    private int _uiFontWeight = FontScalePolicy.DefaultFontWeight;

    [ObservableProperty]
    private int _uiScalePercent = FontScalePolicy.DefaultUiScale;

    public IReadOnlyList<double> BaseFontSizeOptions { get; } = [10, 11, 12, 13, 14, 15, 16, 17, 18];

    public IReadOnlyList<int> UiScaleOptions { get; } = [80, 90, 100, 110, 120, 130, 140, 150];

    private IReadOnlyList<FontOption>? _fontOptions;

    /// <summary>Yazı tipi listesi: önce "Varsayılan", sonra yüklü yazı tipleri. Yalnız liste ilk istendiğinde taranır.</summary>
    public IReadOnlyList<FontOption> FontOptions => _fontOptions ??=
        [new FontOption("", Strings.Get("FontDefault"), new System.Windows.Media.FontFamily(FontScalePolicy.DefaultFontFamily)),
         .. ThemeManager.InstalledFonts().Select(f => new FontOption(f, f, new System.Windows.Media.FontFamily(f)))];

    // --- KISAYOLLAR ---
    [ObservableProperty]
    private WheelAction _wheelAction;

    [ObservableProperty]
    private MiddleClickAction _middleClickAction;

    // --- PERFORMANS ---
    [ObservableProperty]
    private double _memoryUsageMb;

    [ObservableProperty]
    private string _memoryUsageText = "–";

    [ObservableProperty]
    private double _memoryPct;

    /// <summary>Boşta private bellek hedefi (MB). Çok ekran / açık panellerle <see cref="MemoryLimitMb"/>'a kadar kabul (AGENTS.md #1).</summary>
    private const double MemoryTargetMb = 150.0;

    private const double MemoryLimitMb = 200.0;

    /// <summary>0: hedef içinde, 1: hedef aşıldı ama kabul sınırı içinde, 2: 200 MB kabul sınırı aşıldı.</summary>
    [ObservableProperty]
    private int _memoryLevel;

    [ObservableProperty]
    private bool _batterySaverReduceMotion = true;

    [ObservableProperty]
    private WeatherRefreshInterval _weatherRefreshInterval = WeatherRefreshInterval.Every30Minutes;

    [ObservableProperty]
    private int _pomodoroWorkMinutes = 25;

    [ObservableProperty]
    private int _pomodoroShortBreakMinutes = 5;

    [ObservableProperty]
    private int _pomodoroLongBreakMinutes = 15;

    /// <summary>Pomodoro süre seçenekleri (dakika). Ayar dosyasına elle yazılan başka değerler de çalışır.</summary>
    public int[] PomodoroWorkOptions { get; } = [15, 20, 25, 30, 45, 50, 60];
    public int[] PomodoroShortBreakOptions { get; } = [3, 5, 10, 15];
    public int[] PomodoroLongBreakOptions { get; } = [10, 15, 20, 30];

    // --- SESLİ KOMUT VE HARNESS ---
    [ObservableProperty]
    private string _voiceHarness = "Claude";

    [ObservableProperty]
    private string _voiceClaudePermission = "Default";

    [ObservableProperty]
    private string _voiceModel = "default";

    [ObservableProperty]
    private string _voiceCustomCommand = "";

    [ObservableProperty]
    private string _voiceWorkingDirectory = "";


    [ObservableProperty]
    private int _voiceTimeoutSeconds = 60;

    public string[] VoiceHarnessOptions { get; } = ["Claude", "Codex", "Agy", "Custom"];

    // --- GÜNCELLEME VE NELER GELDİ ---
    [ObservableProperty]
    private bool _hasAvailableUpdate;

    [ObservableProperty]
    private string _availableVersionText = "";

    [ObservableProperty]
    private bool _isUpdateOverlayOpen;

    [ObservableProperty]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadProgressText))]
    private int _downloadProgress;

    public string DownloadProgressText => string.Format(Strings.Get("UpdateDownloading"), DownloadProgress);

    [ObservableProperty]
    private string _updateModalTitleText = "";

    [ObservableProperty]
    private string _updateModalIntroText = "";

    public ObservableCollection<string> UpdateHighlights { get; } = [];

    [ObservableProperty]
    private bool _isWhatsNewOverlayOpen;

    [ObservableProperty]
    private string _whatsNewTitleText = "";

    public ObservableCollection<string> WhatsNewHighlights { get; } = [];
    public ObservableCollection<ReleaseSection> WhatsNewSections { get; } = [];

    // --- HAKKINDA ---
    /// <summary>Çalışan derlemenin sürümü (AssemblyInformationalVersion; "+commit" eki atılır).</summary>
    public string AppVersion { get; } = AppInfo.Version;

    /// <summary>Hakkında kimlik kartı: sürüm, lisans ve güncelleme durumu (denetim yapılmadıysa "güncel" denmez).</summary>
    public string AboutStatusText
    {
        get
        {
            var line = string.Format(Strings.Get("AboutVersionLine"), AppVersion);
            var state = IsCheckingUpdates ? Strings.Get("UpdateChecking") : UpdateStatus.LastResult switch
            {
                UpdateCheckResult.UpToDate => Strings.Get("UpdateStateUpToDate"),
                UpdateCheckResult.UpdateReady => Strings.Get("UpdateStateReady"),
                UpdateCheckResult.UpdateAvailable => string.Format(Strings.Get("UpdateAvailableBadge"), UpdateStatus.AvailableVersion ?? ""),
                UpdateCheckResult.Failed => Strings.Get("UpdateStateFailed"),
                _ => Strings.Get("UpdateStateUnknown")
            };
            return $"{line} · {state}";
        }
    }

    public event Action? SettingsSaved;

    private bool _isLoading;

    public KoyWindowViewModel(SettingsStore? store = null)
    {
        _store = store ?? new SettingsStore(SettingsStore.DefaultPath);
        Shortcuts = new ShortcutsViewModel(_store);
        Displays = new DisplaysViewModel(_store);
        Weather = new WeatherLocationViewModel(_store);
        UpdateStatus.Changed += OnUpdateStatusChanged;
        OnUpdateStatusChanged();
        LoadFromSettings();
        NotchFeature.NotificationAccessChanged += OnNotificationAccessChanged;
        RefreshNotificationPermission();
    }

    partial void OnNotchShowNotificationsChanged(bool value) => RefreshNotificationPermission();

    // İzin sonucu çentik özelliği istek yaptıktan sonra gelir (arka plan iş parçacığı olabilir).
    private void OnNotificationAccessChanged() =>
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(RefreshNotificationPermission);

    private void RefreshNotificationPermission() =>
        NotificationsPermissionMissing = NotchShowNotifications &&
            WinRtNotificationListener.GetAccessStatus() is NotificationAccessStatus.Denied or NotificationAccessStatus.Unavailable;

    private void OnUpdateStatusChanged()
    {
        HasAvailableUpdate = UpdateStatus.HasAvailableUpdate;
        AvailableVersionText = UpdateStatus.AvailableVersion is { } v
            ? string.Format(Strings.Get("UpdateAvailableBadge"), v)
            : string.Empty;
        OnPropertyChanged(nameof(LastCheckText));
        OnPropertyChanged(nameof(AboutStatusText));
    }

    public void LoadFromSettings()
    {
        _isLoading = true;
        try
        {
            var s = _store.Load();

            RunAtStartup = StartupService.IsEnabled;
            HideWindowsTaskbar = s.Dock.HideWindowsTaskbar;
            Language = s.General.Language;
            AutoCheckUpdates = s.General.AutoCheckUpdates;
            UpdateChannel = s.General.UpdateChannel;

            NotchEnabled = s.Notch.Enabled;
            NotchVisibility = s.Notch.Visibility;
            DockEnabled = s.Dock.Enabled;
            DockStyle = s.Dock.Style;
            DockIconAlignment = s.Dock.IconAlignment;
            DockProximity = s.Dock.Proximity;
            DockHideInFullscreen = s.Dock.HideInFullscreen;
            DockEdgeReveal = s.Dock.EdgeReveal;
            DockHoverMagnify = s.Dock.HoverMagnify;
            DockShowStartButton = s.Dock.ShowStartButton;
            DockStartIcon = s.Dock.StartIcon;
            DockStartIconPath = s.Dock.StartIconPath ?? "";
            DockShowFolders = s.Dock.ShowFolders;
            DockShowRecents = s.Dock.ShowRecents;
            DockShowTray = s.Dock.ShowTray;
            DockSmallTray = s.Dock.SmallTray;
            DockShowBadges = s.Dock.ShowBadges;
            DockThumbnailPreview = s.Dock.ThumbnailPreview;
            DockThumbnailDelayMs = s.Dock.ThumbnailDelayMs;
            DockDodgeDelayMs = s.Dock.DodgeDelayMs;
            DockIconSize = s.Dock.IconSize > 0 ? s.Dock.IconSize : 28.0;
            DockIndicatorStyle = s.Dock.IndicatorStyle;
            // Tam genişlikte seçilemeyen mod ayar ekranında geçerli karşılığıyla görünür (çalışma anındaki eşlemeyle aynı).
            DockProximity = DockProximityMapping.Effective(DockStyle, DockProximity);

            NotchHideInFullscreen = s.Notch.HideInFullscreen;
            ShowVolumeOsd = s.Notch.ShowVolumeOsd;
            ShowBrightnessOsd = s.Notch.ShowBrightnessOsd;
            ShowPrivacyIndicator = s.Notch.ShowPrivacyIndicator;
            NotchShowNotifications = s.Notch.ShowNotifications;
            NotchNotificationsHideBody = s.Notch.NotificationsHideBody;
            MusicShowWaveform = s.Music.ShowWaveform;
            MusicCardStyle = s.Music.CardStyle;
            MusicLyricsEnabled = s.Music.LyricsEnabled;
            IdleDisplayMode = s.Notch.IdleDisplay;
            NotchStyle = s.Notch.Style;
            NotchOpenBehavior = s.Notch.OpenBehavior;

            // Eski sürümlerin karışık değerleri ("Koyu", 18 vb.) seçenek değerlerine indirilir.
            Theme = s.Appearance.Theme;
            AccentColor = s.Appearance.AccentColor ?? AccentPalette.DefaultHex;
            UseWindowsAccent = s.Appearance.UseWindowsAccent;
            Material = s.Appearance.Material;
            MaterialReadabilityGuard = s.Appearance.MaterialReadabilityGuard;
            NotchFollowsMaterial = s.Notch.FollowsMaterial;
            CornerRadius = CornerSetting.ToStored(CornerSetting.Resolve(s.Appearance.CornerRadius));
            AnimationSpeed = s.Appearance.AnimationSpeed;
            RespectReduceMotion = s.Appearance.RespectReduceMotion;
            BatterySaverReduceMotion = s.Appearance.BatterySaverReduceMotion;
            UiFontFamily = s.Appearance.FontFamily ?? "";
            ClockFontFamily = s.Appearance.ClockFontFamily ?? "";
            BaseFontSize = FontScalePolicy.ClampBaseFontSize(s.Appearance.BaseFontSize);
            UiFontWeight = FontScalePolicy.ClampFontWeight(s.Appearance.FontWeight);
            UiScalePercent = FontScalePolicy.ClampUiScale(s.Appearance.UiScalePercent);
            WeatherRefreshInterval = s.Weather.RefreshInterval;
            PomodoroWorkMinutes = s.Focus.PomodoroWorkMinutes;
            PomodoroShortBreakMinutes = s.Focus.PomodoroShortBreakMinutes;
            PomodoroLongBreakMinutes = s.Focus.PomodoroLongBreakMinutes;

            VoiceHarness = s.Voice.Harness.ToString();
            VoiceClaudePermission = s.Voice.ClaudePermission.ToString();
            VoiceModel = string.IsNullOrWhiteSpace(s.Voice.Model) ? "default" : s.Voice.Model;
            VoiceCustomCommand = s.Voice.CustomCommand;
            VoiceWorkingDirectory = s.Voice.WorkingDirectory;
            VoiceTimeoutSeconds = s.Voice.TimeoutSeconds;

            WheelAction = s.Notch.WheelAction;
            MiddleClickAction = s.Dock.MiddleClickAction;
            Shortcuts.Load(s);
            Weather.Load(s);

            var panels = NotchPanels.ParseEnabled(s.Notch.EnabledPanels ?? ["home", "focus", "calendar", "shelf", "clipboard", "system", "controlcenter"]);
            PanelFocus = panels.Contains(NotchPanel.Focus);
            PanelShelf = panels.Contains(NotchPanel.Shelf);
            PanelCalendar = panels.Contains(NotchPanel.Calendar);
            PanelControlCenter = panels.Contains(NotchPanel.ControlCenter);
            PanelClipboard = panels.Contains(NotchPanel.Clipboard);
            PanelSystem = panels.Contains(NotchPanel.System);
            ClipboardHistoryEnabled = s.Clipboard.HistoryEnabled;
            SearchRetainPrior = s.Search.RetainPrior;
            SearchHistoryMode = s.Search.HistoryMode;
            LoadSearchSources(s.Search.DisabledSources);

            RefreshMemory();

        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Dışarıdan (sağ alt menü veya kısayol) değiştirilen durumları ViewModel'e senkronize eder.
    /// <c>_isLoading = true</c> koruması sayesinde ayarları diske tekrar yazmaz ve döngüsel
    /// özellik tetiklemelerini engeller.
    /// </summary>
    public void SyncExternalState(bool? notchEnabled = null, bool? dockEnabled = null)
    {
        _isLoading = true;
        try
        {
            if (notchEnabled.HasValue)
                NotchEnabled = notchEnabled.Value;
            if (dockEnabled.HasValue)
                DockEnabled = dockEnabled.Value;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Diskteki güncel durumları okuyup <see cref="FeatureStateSyncPolicy.Reconcile"/> ile uzlaştırarak
    /// ViewModel'e aktarır. Kapanışta eski bellek durumunun diski ezmesini önlemek için kullanılır.
    /// </summary>
    public void SyncFromDisk()
    {
        var disk = _store.Load();
        var reconciled = FeatureStateSyncPolicy.Reconcile(disk, disk.Notch.Enabled, disk.Dock.Enabled);
        SyncExternalState(reconciled.Notch.Enabled, reconciled.Dock.Enabled);
    }

    public void RefreshMemory()
    {
        try
        {
            // Görev Yöneticisi'yle aynı sayı (özel çalışma kümesi); okunamazsa ayrılmış özel bellek.
            var bytes = Koy.Platform.Diagnostics.ProcessMemory.GetPrivateWorkingSetBytes();
            if (bytes <= 0)
                bytes = Process.GetCurrentProcess().PrivateMemorySize64;
            var memMb = bytes / (1024.0 * 1024.0);
            MemoryUsageMb = Math.Round(memMb, 1);
            MemoryUsageText = $"{MemoryUsageMb:N1} MB / {MemoryTargetMb:N0} MB";
            MemoryPct = Math.Clamp((MemoryUsageMb / MemoryTargetMb) * 100.0, 0, 100);
            MemoryLevel = MemoryUsageMb > MemoryLimitMb ? 2 : MemoryUsageMb > MemoryTargetMb ? 1 : 0;
        }
        catch
        {
            // Ölçülemediyse uydurma değer gösterilmez.
            MemoryUsageMb = 0;
            MemoryUsageText = "–";
            MemoryPct = 0;
            MemoryLevel = 0;
        }
    }

    [ObservableProperty]
    private bool _searchRetainPrior = true;

    [ObservableProperty]
    private Koy.Core.Search.SearchHistoryMode _searchHistoryMode = Koy.Core.Search.SearchHistoryMode.Disabled;

    /// <summary>Kaynak anahtarlarından biri değişince artar; kaydı ve çentiğin yeniden kurulmasını tetikler.</summary>
    [ObservableProperty]
    private int _searchSourcesVersion;

    public System.Collections.ObjectModel.ObservableCollection<SearchSourceToggle> SearchSources { get; } = [];

    private void LoadSearchSources(IReadOnlyCollection<string> disabled)
    {
        SearchSources.Clear();
        foreach (var (id, nameKey) in Koy.App.Features.Search.SearchFactory.SourceCatalog)
            SearchSources.Add(new SearchSourceToggle(id, Koy.App.Resources.Strings.Get(nameKey), !disabled.Contains(id), () => SearchSourcesVersion++));
    }

    [RelayCommand]
    private void ClearSearchHistory() => Koy.App.Features.Search.SearchFactory.ClearHistory();

    [RelayCommand]
    public void SelectPage(int index)
    {
        SelectedPageIndex = index;
        if (index == 7) // Performans
        {
            RefreshMemory();
        }
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_isLoading) return;
        if (e.PropertyName is nameof(SelectedPageIndex) or nameof(MemoryUsageMb) or nameof(MemoryUsageText) or nameof(MemoryPct) or nameof(MemoryLevel)
            or nameof(IsResetConfirming) or nameof(UninstallAvailability) or nameof(CanUninstall) or nameof(UninstallRowDescription)
            or nameof(StartIconFileName) or nameof(IsUninstallConfirming) or nameof(KeepSettingsOnUninstall) or nameof(UninstallError) or nameof(IsCheckingUpdates) or nameof(LastCheckText) or nameof(AboutStatusText) or nameof(CheckButtonText) or nameof(CanCheckUpdates) or nameof(DownloadProgressText))
            return;

        SaveSettings();

        if (Application.Current is not App app)
            return;

        if (e.PropertyName is nameof(Theme) or nameof(AccentColor) or nameof(UseWindowsAccent) or nameof(Material)
            or nameof(MaterialReadabilityGuard) or nameof(NotchFollowsMaterial)
            or nameof(CornerRadius) or nameof(AnimationSpeed) or nameof(RespectReduceMotion) or nameof(BatterySaverReduceMotion)
            or nameof(UiFontFamily) or nameof(ClockFontFamily) or nameof(BaseFontSize) or nameof(UiFontWeight) or nameof(UiScalePercent))
        {
            // Yazı ve ölçek de kaynaklardan (DynamicResource) ve ThemeManager.Applied'dan canlı uygulanır.
            // Görünüm ayarları açık pencerelere anında yansır.
            ThemeManager.Apply(app, _store.Load());
        }

        if (e.PropertyName == nameof(NotchEnabled))
        {
            app.UpdateNotchState(NotchEnabled);
        }
        else if (e.PropertyName == nameof(DockEnabled))
        {
            app.UpdateDockState(DockEnabled);
        }
        else
        {
            // Özellikler ayarı kurulurken okur; seçenek değişince ilgili özellik yeni ayarla yeniden kurulur.
            if (AffectsDock(e.PropertyName))
                app.ReloadDock();
            if (AffectsNotch(e.PropertyName))
                app.ReloadNotch();
        }
    }

    private static bool AffectsDock(string? name) =>
        name is not null && name != nameof(DockEnabled) &&
        (name.StartsWith("Dock", StringComparison.Ordinal) ||
         name is nameof(HideWindowsTaskbar) or nameof(BatterySaverReduceMotion) or nameof(MiddleClickAction) or nameof(Material)
             or nameof(MaterialReadabilityGuard)
             or nameof(AnimationSpeed) or nameof(RespectReduceMotion));

    private static bool AffectsNotch(string? name) =>
        name is nameof(NotchVisibility) or nameof(NotchHideInFullscreen) or nameof(MusicShowWaveform) or nameof(MusicCardStyle) or nameof(MusicLyricsEnabled)
            or nameof(ShowVolumeOsd) or nameof(ShowBrightnessOsd) or nameof(ShowPrivacyIndicator) or nameof(NotchShowNotifications) or nameof(NotchNotificationsHideBody) or nameof(IdleDisplayMode) or nameof(NotchStyle)
            or nameof(Material) or nameof(MaterialReadabilityGuard) or nameof(NotchFollowsMaterial)
            or nameof(NotchOpenBehavior) or nameof(WheelAction) or nameof(BatterySaverReduceMotion)
            or nameof(WeatherRefreshInterval) or nameof(PomodoroWorkMinutes) or nameof(PomodoroShortBreakMinutes) or nameof(PomodoroLongBreakMinutes)
            or nameof(PanelFocus) or nameof(PanelShelf) or nameof(PanelCalendar)
            or nameof(PanelControlCenter) or nameof(PanelClipboard) or nameof(PanelSystem) or nameof(ClipboardHistoryEnabled)
            or nameof(VoiceHarness) or nameof(VoiceClaudePermission) or nameof(VoiceModel) or nameof(VoiceCustomCommand) or nameof(VoiceWorkingDirectory) or nameof(VoiceTimeoutSeconds)
            or nameof(SearchRetainPrior) or nameof(SearchHistoryMode) or nameof(SearchSourcesVersion);

    partial void OnLanguageChanged(string value)
    {
        if (_isLoading) return;
        try
        {
            var culture = new System.Globalization.CultureInfo(value == "en" ? "en-US" : "tr-TR");
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.CurrentUICulture = culture;
            System.Globalization.CultureInfo.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Koy.App.Resources.Loc.Instance.Refresh();

            // Bu metinler Loc'a bağlı değil, burada hesaplanıyor; dil değişince kendileri haber vermeli.
            OnPropertyChanged(nameof(CheckButtonText));
            OnPropertyChanged(nameof(LastCheckText));
            OnPropertyChanged(nameof(AboutStatusText));
            OnPropertyChanged(nameof(UninstallRowDescription));
            OnPropertyChanged(nameof(DownloadProgressText));
            OnPropertyChanged(nameof(StartIconFileName));
        }
        catch { }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        // Kaldırma başladıysa ayar dosyası yeniden oluşmasın (pencereler kapanırken kaydetmeye çalışır).
        if (AppUninstaller.InProgress)
            return;

        StartupService.SetEnabled(RunAtStartup);

        // Ana her zaman açık; sekme sırası sabit (NotchPanels.Order), ayar yalnız hangilerinin açık olduğunu tutar.
        var enabledPanels = new List<string> { NotchPanels.ToKey(NotchPanel.Home) };
        if (PanelFocus) enabledPanels.Add(NotchPanels.ToKey(NotchPanel.Focus));
        if (PanelCalendar) enabledPanels.Add(NotchPanels.ToKey(NotchPanel.Calendar));
        if (PanelShelf) enabledPanels.Add(NotchPanels.ToKey(NotchPanel.Shelf));
        if (PanelClipboard) enabledPanels.Add(NotchPanels.ToKey(NotchPanel.Clipboard));
        if (PanelSystem) enabledPanels.Add(NotchPanels.ToKey(NotchPanel.System));
        if (PanelControlCenter) enabledPanels.Add(NotchPanels.ToKey(NotchPanel.ControlCenter));

        var current = _store.Load();
        var updated = current with
        {
            General = current.General with
            {
                Language = Language,
                AutoCheckUpdates = AutoCheckUpdates,
                UpdateChannel = UpdateChannel
            },
            Dock = current.Dock with
            {
                HideWindowsTaskbar = HideWindowsTaskbar,
                Enabled = DockEnabled,
                Style = DockStyle,
                IconAlignment = DockIconAlignment,
                Proximity = DockProximity,
                HideInFullscreen = DockHideInFullscreen,
                EdgeReveal = DockEdgeReveal,
                HoverMagnify = DockHoverMagnify,
                ShowStartButton = DockShowStartButton,
                StartIcon = DockStartIcon,
                StartIconPath = DockStartIconPath,
                ShowFolders = DockShowFolders,
                ShowRecents = DockShowRecents,
                ShowTray = DockShowTray,
                SmallTray = DockSmallTray,
                ShowBadges = DockShowBadges,
                ThumbnailPreview = DockThumbnailPreview,
                ThumbnailDelayMs = DockThumbnailDelayMs,
                DodgeDelayMs = DockDodgeDelayMs,
                IconSize = DockIconSize,
                IndicatorStyle = DockIndicatorStyle,
                MiddleClickAction = MiddleClickAction
            },
            Notch = current.Notch with
            {
                Enabled = NotchEnabled,
                Visibility = NotchVisibility,
                HideInFullscreen = NotchHideInFullscreen,
                ShowVolumeOsd = ShowVolumeOsd,
                ShowBrightnessOsd = ShowBrightnessOsd,
                ShowPrivacyIndicator = ShowPrivacyIndicator,
                ShowNotifications = NotchShowNotifications,
                NotificationsHideBody = NotchNotificationsHideBody,
                IdleDisplay = IdleDisplayMode,
                Style = NotchStyle,
                OpenBehavior = NotchOpenBehavior,
                FollowsMaterial = NotchFollowsMaterial,
                WheelAction = WheelAction,
                EnabledPanels = new ValueList<string>(enabledPanels)
            },
            Music = current.Music with
            {
                ShowWaveform = MusicShowWaveform,
                CardStyle = MusicCardStyle,
                LyricsEnabled = MusicLyricsEnabled
            },
            Appearance = current.Appearance with
            {
                Theme = Theme,
                AccentColor = AccentColor,
                UseWindowsAccent = UseWindowsAccent,
                Material = Material,
                MaterialReadabilityGuard = MaterialReadabilityGuard,
                CornerRadius = CornerRadius,
                AnimationSpeed = AnimationSpeed,
                RespectReduceMotion = RespectReduceMotion,
                BatterySaverReduceMotion = BatterySaverReduceMotion,
                FontFamily = UiFontFamily ?? "",
                ClockFontFamily = ClockFontFamily ?? "",
                BaseFontSize = FontScalePolicy.ClampBaseFontSize(BaseFontSize),
                FontWeight = FontScalePolicy.ClampFontWeight(UiFontWeight),
                UiScalePercent = FontScalePolicy.ClampUiScale(UiScalePercent)
            },
            Weather = current.Weather with
            {
                RefreshInterval = WeatherRefreshInterval
            },
            Focus = current.Focus with
            {
                PomodoroWorkMinutes = PomodoroWorkMinutes,
                PomodoroShortBreakMinutes = PomodoroShortBreakMinutes,
                PomodoroLongBreakMinutes = PomodoroLongBreakMinutes
            },
            Voice = current.Voice with
            {
                Harness = Enum.TryParse<VoiceHarnessType>(VoiceHarness, out var vh) ? vh : VoiceHarnessType.Claude,
                ClaudePermission = Enum.TryParse<VoiceClaudePermission>(VoiceClaudePermission, out var vp) ? vp : Koy.Core.Voice.VoiceClaudePermission.Default,
                Model = string.IsNullOrWhiteSpace(VoiceModel) ? "default" : VoiceModel,
                CustomCommand = VoiceCustomCommand,
                WorkingDirectory = VoiceWorkingDirectory,
                TimeoutSeconds = VoiceTimeoutSeconds
            },
            Clipboard = current.Clipboard with
            {
                HistoryEnabled = ClipboardHistoryEnabled
            },
            Search = current.Search with
            {
                RetainPrior = SearchRetainPrior,
                HistoryMode = SearchHistoryMode,
                DisabledSources = new ValueList<string>(SearchSources.Where(s => !s.IsEnabled).Select(s => s.Id))
            }
        };

        _store.Save(updated);
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    public void RestoreTaskbar()
    {
        TaskbarService.Restore(force: true);
        // Dock çalışıyorsa Koy'un tepsi penceresi de bırakılır (görev çubuğu görünürken Shell_TrayWnd sahipliği alınmaz).
        (Application.Current as App)?.NotifyTaskbarRestoredManually();
    }

    /// <summary>Sıfırlama iki adımlı: ilk tıklama onay ister (yanlışlıkla silmeyi önler).</summary>
    [ObservableProperty]
    private bool _isResetConfirming;

    [RelayCommand]
    public void RequestReset() => IsResetConfirming = true;

    [RelayCommand]
    public void CancelReset() => IsResetConfirming = false;

    [RelayCommand]
    public void ResetSettings()
    {
        IsResetConfirming = false;
        // Kurulum sihirbazı tamamlandı bilgisi korunur; yoksa sıfırlama sihirbazı yeniden açardı.
        _store.Save(FirstRunPolicy.CreateReset(_store.Load()));
        LoadFromSettings();
        SettingsSaved?.Invoke();
        if (Application.Current is { } current)
            ThemeManager.Apply(current, _store.Load());

        // Çalışan çentik ve dock yeni varsayılanlarla yeniden kurulur.
        if (Application.Current is App app)
            app.ReloadAllFeatures();
    }

    // --- KALDIRMA ---
    /// <summary>Uygulama içinden kaldırma mümkün mü; kurulu olmayan sürümde düğme kapalı kalır.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUninstall), nameof(UninstallRowDescription))]
    private UninstallAvailability _uninstallAvailability = AppUninstaller.GetAvailability();

    public bool CanUninstall => UninstallAvailability == UninstallAvailability.Available;

    public string UninstallRowDescription =>
        Strings.Get(CanUninstall ? "UninstallRowDesc" : "UninstallOnlyInstalled");

    /// <summary>Onay görünümü açık mı.</summary>
    [ObservableProperty]
    private bool _isUninstallConfirming;

    /// <summary>"Ayarlarımı sakla": varsayılan işaretli; kaldırma geri alınamayan tek yönlü iş, güvenli taraf saklamak.</summary>
    [ObservableProperty]
    private bool _keepSettingsOnUninstall = true;

    [ObservableProperty]
    private string _uninstallError = "";

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    public void RequestUninstall()
    {
        UninstallError = "";
        KeepSettingsOnUninstall = true;
        IsUninstallConfirming = true;
    }

    [RelayCommand]
    public void CancelUninstall() => IsUninstallConfirming = false;

    [RelayCommand]
    public void ConfirmUninstall()
    {
        if (!CanUninstall)
            return;

        if (AppUninstaller.Begin(KeepSettingsOnUninstall))
            return; // Koy kapanıyor

        UninstallError = Strings.Get("UninstallFailed");
    }

    partial void OnUninstallAvailabilityChanged(UninstallAvailability value) => RequestUninstallCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    public void CopyDiagnostics()
    {
        RefreshMemory();
        var os = Environment.OSVersion.VersionString;
        var dotNet = Environment.Version.ToString();
        var proc = Process.GetCurrentProcess();

        var text = $"Koy v{AppVersion}\nOS: {os}\n.NET: {dotNet}\nPrivate Memory: {MemoryUsageMb:N1} MB\nProcess ID: {proc.Id}";
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    public async Task CheckUpdatesAsync()
    {
        IsCheckingUpdates = true;
        try
        {
            var mgr = new Velopack.UpdateManager(new Velopack.Sources.GithubSource("https://github.com/koyhq/koy", null, UpdateChannel == UpdateChannel.Preview));
            if (!mgr.IsInstalled)
            {
                // Geliştirme çalıştırmasında denetim yapılamaz; başarı gibi gösterilmez.
                UpdateStatus.Record(UpdateCheckResult.NotInstalled);
                return;
            }

            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion != null)
            {
                UpdateStatus.RecordAvailable(newVersion);
                return;
            }

            UpdateStatus.Record(UpdateCheckResult.UpToDate);
        }
        catch
        {
            UpdateStatus.Record(UpdateCheckResult.Failed);
        }
        finally
        {
            IsCheckingUpdates = false;
            OnPropertyChanged(nameof(LastCheckText));
            OnPropertyChanged(nameof(AboutStatusText));
        }
    }

    [RelayCommand]
    public void OpenUpdateOverlay()
    {
        var version = UpdateStatus.AvailableVersion ?? AppVersion;
        UpdateModalTitleText = Strings.Get("UpdateModalTitle");
        UpdateModalIntroText = string.Format(Strings.Get("UpdateModalIntro"), version);

        // Yeni sürümün notları güncelleme beslemesinden gelir (yayın akışı CHANGELOG bölümünü paket notu yapar).
        // Gömülü CHANGELOG yalnız kurulu sürümü bilir; yeni sürüm orada yoksa eski notları göstermek yerine liste boş kalır.
        var (feedNotes, feedLanguage) = ChangelogParser.SelectFeedNotes(UpdateStatus.AvailableUpdate?.TargetFullRelease?.NotesMarkdown, Language);
        var release = ChangelogParser.ExtractVersion(feedNotes, version, feedLanguage)
                      ?? ChangelogParser.ResolveEmbedded(version, Language);

        UpdateHighlights.Clear();
        if (release is not null && release.Highlights.Count > 0)
        {
            foreach (var h in release.Highlights)
                UpdateHighlights.Add(h);
        }

        // İndirme sürerken (pencere kapatılıp menüden yeniden açıldıysa) durumu sıfırlama: ikinci indirme başlamasın.
        if (!IsDownloadingUpdate)
            DownloadProgress = 0;
        IsUpdateOverlayOpen = true;
    }

    [RelayCommand]
    public void CloseUpdateOverlay()
    {
        if (IsDownloadingUpdate)
            return;
        IsUpdateOverlayOpen = false;
    }

    [RelayCommand]
    public async Task StartUpdateAsync()
    {
        if (UpdateStatus.AvailableUpdate is not { } update)
            return;

        IsDownloadingUpdate = true;
        DownloadProgress = 0;

        try
        {
            var mgr = new Velopack.UpdateManager(new Velopack.Sources.GithubSource("https://github.com/koyhq/koy", null, UpdateChannel == UpdateChannel.Preview));
            var dispatcher = System.Windows.Application.Current.Dispatcher;
            await mgr.DownloadUpdatesAsync(update, progress =>
            {
                // Velopack ilerlemeyi arka plan iş parçacığından bildirebilir.
                dispatcher.BeginInvoke(() => DownloadProgress = progress);
            });

            App.SafeRestoreTaskbar();
            mgr.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            Koy.Core.Diagnostics.CrashLog.Write("Güncelleme indirme", ex);
            IsDownloadingUpdate = false;
        }
    }

    [RelayCommand]
    public void OpenWhatsNewOverlay()
    {
        WhatsNewTitleText = string.Format(Strings.Get("WhatsNewTitle"), AppVersion);
        var release = ChangelogParser.ResolveEmbedded(AppVersion, Language)
                      ?? ChangelogParser.ParseLatest(ChangelogParser.ReadEmbeddedChangelog(Language), Language);

        WhatsNewHighlights.Clear();
        WhatsNewSections.Clear();
        if (release is not null)
        {
            foreach (var h in release.Highlights)
                WhatsNewHighlights.Add(h);
            foreach (var s in release.Sections)
                WhatsNewSections.Add(s);
        }

        IsWhatsNewOverlayOpen = true;
    }

    [RelayCommand]
    public void CloseWhatsNewOverlay()
    {
        IsWhatsNewOverlayOpen = false;
        var current = _store.Load();
        if (current.General.LastSeenVersion != AppVersion)
        {
            _store.Save(current with { General = current.General with { LastSeenVersion = AppVersion } });
        }
    }

    [RelayCommand]
    public void OpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/koyhq/koy",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    /// <summary>Hava durumu verisinin kaynağı (Open-Meteo, CC BY 4.0 atfı).</summary>
    [RelayCommand]
    public void OpenOpenMeteo()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://open-meteo.com/",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    /// <summary>Cihaz konumunun yer adı kaynağı (OpenStreetMap Nominatim, ODbL atfı).</summary>
    [RelayCommand]
    public void OpenOpenStreetMap()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.openstreetmap.org/copyright",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    [RelayCommand]
    public void ExitApp()
    {
        TaskbarService.Restore();
        Application.Current?.Shutdown();
    }
}
