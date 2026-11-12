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
    public ProfilesViewModel Profiles { get; }

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

    // --- GELİŞMİŞ SAYFASI VE CANLI YENİDEN YÜKLEME (issue #69) ---

    /// <summary>Gelişmiş sayfasının dizini; mevcut sayfaların dizinleri değişmesin diye en sona eklendi.</summary>
    public const int AdvancedPageIndex = 9;

    // Diskteki son bilinen ayarlar (yüklenen ya da bu pencerenin yazdığı); canlı yeniden yükleme aynıysa iş yapmaz.
    private AppSettings? _lastKnown;

    public string SettingsFilePath => _store.Path;

    [ObservableProperty]
    private IReadOnlyList<AdvancedSettingRow> _advancedRows = [];

    [ObservableProperty]
    private string _advancedFilter = "";

    partial void OnAdvancedFilterChanged(string value) => RebuildAdvancedRows();

    /// <summary>Dış düzenlenen settings.json okunamadı; son geçerli ayarlar korunuyor.</summary>
    [ObservableProperty]
    private bool _settingsFileInvalid;

    // Kendi arayüzü olan ayarlar Gelişmiş tablosuna girmez: VM özellikleri düz v3 adlarıyla SettingsKeyMap'te.
    private static readonly string[] DedicatedUiKeys =
        SettingsKeyMap.V3ToV4.Values.Select(v => $"{v.Section}.{v.Name}").ToArray();

    private void RebuildAdvancedRows()
    {
        if (SelectedPageIndex != AdvancedPageIndex)
        {
            AdvancedRows = [];
            return;
        }

        var filter = AdvancedFilter.Trim();
        AdvancedRows = AdvancedSettings.List(_store.Load(), DedicatedUiKeys)
            .Where(r => filter.Length == 0 || r.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Select(r => new AdvancedSettingRow(r, ApplyAdvanced, ResetAdvanced))
            .ToList();
    }

    private AdvancedSettingError ApplyAdvanced(string key, string text)
    {
        var error = AdvancedSettings.TrySet(_store.Load(), key, text, out var updated);
        if (error == AdvancedSettingError.None)
            CommitExternalSettings(updated);
        return error;
    }

    private AdvancedSettingError ResetAdvanced(string key)
    {
        var error = AdvancedSettings.Reset(_store.Load(), key, out var updated);
        if (error == AdvancedSettingError.None)
            CommitExternalSettings(updated);
        return error;
    }

    // Gelişmiş tablodan yazılan ayar: kaydedilir, pencere ve özellikler yeni değerle kurulur (alan her şey olabilir).
    // Tablo bir sonraki tura bırakılır; satırın kendi bağlaması sürerken liste değiştirilmez.
    private void CommitExternalSettings(AppSettings updated)
    {
        _store.Save(updated);
        ApplyLoadedSettings();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(RebuildAdvancedRows);
    }

    private void ApplyLoadedSettings()
    {
        LoadFromSettings();
        SettingsSaved?.Invoke();
        if (Application.Current is { } current)
            ThemeManager.Apply(current, _store.Load());
        if (Application.Current is App app)
            app.ReloadAllFeatures();
    }

    /// <summary>
    /// settings.json dışarıdan değişti (Koy penceresi açıkken dosya izleyicisi çağırır). Koy'un kendi kaydı yok sayılır;
    /// dosya okunamıyor ya da tek bir alanı bile tanınmıyorsa yüklenmez, kısa bir uyarı gösterilir.
    /// </summary>
    public void ReloadFromDiskIfChanged()
    {
        if (_store.IsOwnWrite())
            return;

        if (!_store.TryReadStrict(out var disk))
        {
            SettingsFileInvalid = true;
            return;
        }

        SettingsFileInvalid = false;
        if (Equals(disk, _lastKnown))
            return;

        ApplyLoadedSettings();
        RebuildAdvancedRows();
    }

    // --- UYGULA / GERİ AL (issue #69) ---
    // Riskli ayar (bugün yalnız görev çubuğunu gizleme): değişiklik hemen uygulanır, kullanıcı ~15 sn içinde
    // onaylamazsa önceki değere dönülür. Karar Core'da (ApplyRevertPolicy); zamanlayıcı yalnız bekleyen değişiklik
    // varken çalışır, karar verilince durur (AGENTS #5).
    private bool? _revertHideTaskbarTo;
    private DateTimeOffset _appliedAt;
    private System.Windows.Threading.DispatcherTimer? _applyRevertTimer;
    private bool _applyingRevert;

    [ObservableProperty]
    private bool _isApplyRevertPending;

    [ObservableProperty]
    private int _applyRevertSecondsLeft;

    partial void OnHideWindowsTaskbarChanged(bool oldValue, bool newValue)
    {
        if (_isLoading || _applyingRevert)
            return;

        // Bekleyen bir değişiklik varken yeniden değişirse ilk (bilinen iyi) değer korunur.
        _revertHideTaskbarTo ??= oldValue;
        if (_revertHideTaskbarTo == newValue)
        {
            StopApplyRevert();
            return;
        }

        _appliedAt = DateTimeOffset.UtcNow;
        _applyRevertTimer ??= CreateApplyRevertTimer();
        _applyRevertTimer.Start();
        ApplyRevertSecondsLeft = (int)Math.Ceiling(ApplyRevertPolicy.DefaultTimeout.TotalSeconds);
        IsApplyRevertPending = true;
    }

    private System.Windows.Threading.DispatcherTimer CreateApplyRevertTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => DecideApplyRevert(ApplyRevertChoice.None);
        return timer;
    }

    private void DecideApplyRevert(ApplyRevertChoice choice)
    {
        if (_revertHideTaskbarTo is not { } previous)
        {
            StopApplyRevert();
            return;
        }

        var now = DateTimeOffset.UtcNow;
        switch (ApplyRevertPolicy.Decide(_appliedAt, now, choice, ApplyRevertPolicy.DefaultTimeout))
        {
            case ApplyRevertDecision.Wait:
                ApplyRevertSecondsLeft = (int)Math.Ceiling(ApplyRevertPolicy.Remaining(_appliedAt, now, ApplyRevertPolicy.DefaultTimeout).TotalSeconds);
                break;
            case ApplyRevertDecision.Keep:
                StopApplyRevert();
                break;
            case ApplyRevertDecision.Revert:
                StopApplyRevert();
                _applyingRevert = true;
                try
                {
                    // Normal kaydetme ve dock yeniden kurma yolundan geçer (OnPropertyChanged).
                    HideWindowsTaskbar = previous;
                }
                finally
                {
                    _applyingRevert = false;
                }
                break;
        }
    }

    private void StopApplyRevert()
    {
        _applyRevertTimer?.Stop();
        _revertHideTaskbarTo = null;
        IsApplyRevertPending = false;
    }

    [RelayCommand]
    private void ConfirmApplyRevert() => DecideApplyRevert(ApplyRevertChoice.Confirm);

    [RelayCommand]
    private void RevertApplyRevert() => DecideApplyRevert(ApplyRevertChoice.Revert);

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

    // --- ÇENTİK DÜZENİ (issue #78) ---
    public IReadOnlyList<int> NotchDelayOptions { get; } = [0, 150, 300, 500, 800, 1200, 2000];

    [ObservableProperty]
    private int _notchOpenDelayMs;

    [ObservableProperty]
    private int _notchCloseDelayMs = NotchOpenPolicy.DefaultCloseDelayMs;

    [ObservableProperty]
    private NotchTabBarStyle _notchTabBarStyle = NotchTabBarStyle.IconAndLabel;

    /// <summary>Sekme sırası satırları (bütün paneller, görünürlükten bağımsız).</summary>
    public ObservableCollection<NotchTabOrderItem> NotchTabOrder { get; } = [];

    /// <summary>Sıra değişince artar; kaydı ve çentiğin yeniden kurulmasını tetikler.</summary>
    [ObservableProperty]
    private int _notchTabOrderVersion;

    private List<string> _panelOrder = [];

    private void LoadTabOrder(IEnumerable<string>? order)
    {
        _panelOrder = NotchPanels.NormalizeOrder(order).Select(NotchPanels.ToKey).ToList();
        NotchTabOrder.Clear();
        foreach (var panel in NotchPanels.NormalizeOrder(_panelOrder))
            NotchTabOrder.Add(new NotchTabOrderItem(panel, NotchTabOrderItem.LabelOf(panel)));
    }

    [RelayCommand]
    private void MoveTabUp(NotchPanel panel) => MoveTab(panel, -1);

    [RelayCommand]
    private void MoveTabDown(NotchPanel panel) => MoveTab(panel, 1);

    // Dosyadaki (elle yazılmış) gecikme listede yoksa en yakın seçenek gösterilir; kaydedilince o değer yazılır.
    private int NearestDelay(int ms)
    {
        var clamped = NotchOpenPolicy.ClampDelay(ms);
        return NotchDelayOptions.OrderBy(o => Math.Abs(o - clamped)).First();
    }

    private void MoveTab(NotchPanel panel, int step)
    {
        var moved = NotchPanels.Move(_panelOrder, panel, step);
        if (moved.SequenceEqual(_panelOrder))
            return;

        LoadTabOrder(moved);
        NotchTabOrderVersion++;
    }

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

    // --- SİMGE PAKETİ (Görünüm sayfası; etkisi dock uygulama simgelerinde) ---
    [ObservableProperty]
    private IconPack _iconPack = IconPack.Builtin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconPackFolderName))]
    private string _iconPackFolder = "";

    /// <summary>Paket satırında gösterilen klasör adı; seçilmemişse "klasör seçilmedi".</summary>
    public string IconPackFolderName => string.IsNullOrWhiteSpace(IconPackFolder)
        ? Strings.Get("IconPackFolderNone")
        : System.IO.Path.GetFileName(IconPackFolder.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : IconPackFolder;

    /// <summary>
    /// "Klasör" seçilince ya da "Klasör seç" düğmesine basılınca çalışır. Yalnız kullanıcı tıklamasından çağrılır
    /// (ayar yüklenirken dosya penceresi açılmasın). Güvenli olmayan klasör (ağ yolu vb.) kabul edilmez.
    /// </summary>
    [RelayCommand]
    public void PickIconPackFolder()
    {
        if (_isLoading)
            return;

        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.Get("IconPackFolderPickTitle") };
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var accepted = owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();

        if (accepted == true && IconPackFiles.IsSafeFolder(dialog.FolderName))
        {
            // Önce klasör, sonra tür: tek geçerli durum olarak kaydedilir.
            IconPackFolder = dialog.FolderName;
            IconPack = IconPack.Folder;
        }
        else if (!IconPackFiles.IsSafeFolder(IconPackFolder))
        {
            // İptal edildi ve elde geçerli klasör yok: "Klasör" boş kalmasın, yerleşiğe dön.
            IconPack = IconPack.Builtin;
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

    // Animasyon başına anahtarlar (issue #79). Rozet zıplaması anahtarı arayüzde yok: dock rozetinde henüz animasyon yok.
    [ObservableProperty]
    private bool _motionNotchOpenClose = true;

    [ObservableProperty]
    private bool _motionTabTransitions = true;

    [ObservableProperty]
    private bool _motionOsd = true;

    [ObservableProperty]
    private bool _motionDockMagnify = true;

    [ObservableProperty]
    private bool _motionFolderPanel = true;

    /// <summary>
    /// Anahtarlar etkili mi: genel animasyon "Kapalı" ya da Windows "animasyonları azalt" (uy açıkken) varsa hepsi
    /// etkisizdir (MotionPolicy.ScaleFor). Pil tasarrufu anlık durum olduğu için burada sayılmaz.
    /// </summary>
    public bool MotionTogglesActive =>
        MotionPolicy.Scale(AnimationSpeed, RespectReduceMotion, SystemParameters.ClientAreaAnimation, batterySaverReduceMotion: false) > 0;

    public bool MotionTogglesInactive => !MotionTogglesActive;

    partial void OnAnimationSpeedChanged(AnimationSpeed value) => NotifyMotionTogglesActive();

    partial void OnRespectReduceMotionChanged(bool value) => NotifyMotionTogglesActive();

    private void NotifyMotionTogglesActive()
    {
        OnPropertyChanged(nameof(MotionTogglesActive));
        OnPropertyChanged(nameof(MotionTogglesInactive));
    }
    // --- SAAT VE BİRİMLER (issue #77) ---
    [ObservableProperty]
    private Koy.Core.Clock.ClockHourFormat _clockHourFormat = Koy.Core.Clock.ClockHourFormat.Auto;

    [ObservableProperty]
    private bool _clockShowSeconds;

    [ObservableProperty]
    private bool _clockShowWeekNumber;

    [ObservableProperty]
    private Koy.Core.Clock.WeekStartSetting _clockFirstDayOfWeek = Koy.Core.Clock.WeekStartSetting.System;

    /// <summary>Kutudaki metin; geçersizse kaydedilmez, eski değer kalır (bkz. <see cref="ClockCustomDateFormatInvalid"/>).</summary>
    [ObservableProperty]
    private string _clockCustomDateFormat = "";

    [ObservableProperty]
    private bool _clockCustomDateFormatInvalid;

    partial void OnClockCustomDateFormatChanged(string value) =>
        ClockCustomDateFormatInvalid = !string.IsNullOrWhiteSpace(value) && !Koy.Core.Clock.ClockFormatPolicy.IsValidCustomDateFormat(value);

    [ObservableProperty]
    private Koy.Core.Units.TemperatureUnit _temperatureUnit = Koy.Core.Units.TemperatureUnit.Celsius;

    [ObservableProperty]
    private Koy.Core.Units.NetworkSpeedUnit _networkSpeedUnit = Koy.Core.Units.NetworkSpeedUnit.Bytes;

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
        Profiles = new ProfilesViewModel(ProfileService.Instance);
        ProfileService.Instance.Applied += OnProfileApplied;
        Displays = new DisplaysViewModel(_store);
        Weather = new WeatherLocationViewModel(_store);
        UpdateStatus.Changed += OnUpdateStatusChanged;
        OnUpdateStatusChanged();
        LoadFromSettings();
        NotchFeature.NotificationAccessChanged += OnNotificationAccessChanged;
        RefreshNotificationPermission();
    }

    // Profil (bu pencereden ya da bir kuraldan) uygulandı: değerler diskteki yeni ayarlardan okunur, aksi halde bir
    // sonraki kayıt eski değerleri geri yazardı. Ayarlar sıfırlamasıyla aynı yenileme.
    private void OnProfileApplied(Koy.Core.Settings.Profiles.SavedProfile profile)
    {
        LoadFromSettings();
        SettingsSaved?.Invoke();
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
            _lastKnown = s;

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
            DockNewTrayIconVisibility = s.Dock.NewTrayIconVisibility == Koy.Core.Tray.TrayIconVisibility.Hidden
                ? Koy.Core.Tray.TrayIconVisibility.Hidden
                : Koy.Core.Tray.TrayIconVisibility.Overflow;
            LoadTrayApps(s.Dock);
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
            NotchOpenDelayMs = NearestDelay(s.Notch.OpenDelayMs);
            NotchCloseDelayMs = NearestDelay(s.Notch.CloseDelayMs);
            NotchTabBarStyle = s.Notch.TabBarStyle;
            LoadTabOrder(s.Notch.PanelOrder);

            // Eski sürümlerin karışık değerleri ("Koyu", 18 vb.) seçenek değerlerine indirilir.
            Theme = s.Appearance.Theme;
            AccentColor = s.Appearance.AccentColor ?? AccentPalette.DefaultHex;
            UseWindowsAccent = s.Appearance.UseWindowsAccent;
            Material = s.Appearance.Material;
            MaterialReadabilityGuard = s.Appearance.MaterialReadabilityGuard;
            NotchFollowsMaterial = s.Notch.FollowsMaterial;
            CornerRadius = CornerSetting.ToStored(CornerSetting.Resolve(s.Appearance.CornerRadius));
            IconPack = s.Appearance.IconPack;
            IconPackFolder = s.Appearance.IconPackFolder ?? "";
            AnimationSpeed = s.Appearance.AnimationSpeed;
            RespectReduceMotion = s.Appearance.RespectReduceMotion;
            BatterySaverReduceMotion = s.Appearance.BatterySaverReduceMotion;
            var motion = s.Appearance.Motion ?? new MotionSettings();
            MotionNotchOpenClose = motion.NotchOpenClose;
            MotionTabTransitions = motion.TabTransitions;
            MotionOsd = motion.Osd;
            MotionDockMagnify = motion.DockMagnify;
            MotionFolderPanel = motion.FolderPanel;
            ClockHourFormat = s.Notch.ClockHourFormat;
            ClockShowSeconds = s.Notch.ClockShowSeconds;
            ClockShowWeekNumber = s.Notch.ClockShowWeekNumber;
            ClockFirstDayOfWeek = s.Notch.ClockFirstDayOfWeek;
            ClockCustomDateFormat = s.Notch.ClockCustomDateFormat ?? "";
            TemperatureUnit = s.General.TemperatureUnit;
            NetworkSpeedUnit = s.General.NetworkSpeedUnit;
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

    // --- TEPSİ SİMGELERİ (issue #77) ---
    /// <summary>Kaydı olmayan (yeni) tepsi simgelerinin yeri: Overflow = tepsi penceresinde görünür, Hidden = gizli.</summary>
    [ObservableProperty]
    private Koy.Core.Tray.TrayIconVisibility _dockNewTrayIconVisibility = Koy.Core.Tray.TrayIconVisibilityPolicy.DefaultNewIcon;

    /// <summary>Satırlardan biri değişince artar; "Dock" önekiyle kaydı ve dock'un yeniden kurulmasını tetikler.</summary>
    [ObservableProperty]
    private int _dockTrayAppsVersion;

    public System.Collections.ObjectModel.ObservableCollection<TrayAppVisibilityRow> TrayAppRows { get; } = [];

    public bool HasTrayAppRows => TrayAppRows.Count > 0;

    partial void OnDockNewTrayIconVisibilityChanged(Koy.Core.Tray.TrayIconVisibility value)
    {
        foreach (var row in TrayAppRows)
            row.FollowDefault(value != Koy.Core.Tray.TrayIconVisibility.Hidden);
    }

    // Kayıtlı "Shown" değerleri korunur: dock'ta satır içi gösterim gelince anlamı olacak, bugün tepsi penceresinde görünür.
    private ValueDictionary<Koy.Core.Tray.TrayIconVisibility> _savedTrayApps = new();

    private void LoadTrayApps(DockSettings dock)
    {
        _savedTrayApps = dock.TrayAppVisibility ?? new();
        TrayAppRows.Clear();
        foreach (var exe in Koy.Core.Tray.TrayIconVisibilityPolicy.SettingsApps(Koy.App.Features.Dock.Tray.TrayAppNames.Seen, _savedTrayApps.Keys))
        {
            var explicitRecord = _savedTrayApps.TryGetValue(exe, out var saved);
            var visibility = explicitRecord ? saved : dock.NewTrayIconVisibility;
            TrayAppRows.Add(new TrayAppVisibilityRow(exe, visibility != Koy.Core.Tray.TrayIconVisibility.Hidden, explicitRecord, () => DockTrayAppsVersion++));
        }

        OnPropertyChanged(nameof(HasTrayAppRows));
    }

    private ValueDictionary<Koy.Core.Tray.TrayIconVisibility> TrayAppsForSave()
    {
        var result = new ValueDictionary<Koy.Core.Tray.TrayIconVisibility>();
        foreach (var row in TrayAppRows.Where(r => r.IsExplicit))
        {
            result[row.ExeName] = !row.IsShown
                ? Koy.Core.Tray.TrayIconVisibility.Hidden
                : _savedTrayApps.TryGetValue(row.ExeName, out var saved) && saved == Koy.Core.Tray.TrayIconVisibility.Shown
                    ? Koy.Core.Tray.TrayIconVisibility.Shown
                    : Koy.Core.Tray.TrayIconVisibility.Overflow;
        }

        return result;
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

        // Gelişmiş tablosu yalnız sayfası açıkken tutulur; başka sayfaya geçince bırakılır.
        RebuildAdvancedRows();
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_isLoading) return;
        if (e.PropertyName is nameof(SelectedPageIndex) or nameof(MotionTogglesActive) or nameof(MotionTogglesInactive) or nameof(ClockCustomDateFormatInvalid)
            or nameof(IsApplyRevertPending) or nameof(ApplyRevertSecondsLeft) or nameof(AdvancedRows) or nameof(SettingsFileInvalid) or nameof(AdvancedFilter)
            or nameof(HasTrayAppRows)
            or nameof(SettingsSearchQuery) or nameof(IsSettingsSearching) or nameof(SettingsSearchResults)
            or nameof(SettingsSearchNoResult) or nameof(ShowChangedOnly) or nameof(CanResetPage) or nameof(IsPageResetConfirming) or nameof(PageResetDone)
            or nameof(MemoryUsageMb) or nameof(MemoryUsageText) or nameof(MemoryPct) or nameof(MemoryLevel)
            or nameof(IsResetConfirming) or nameof(UninstallAvailability) or nameof(CanUninstall) or nameof(UninstallRowDescription)
            or nameof(StartIconFileName) or nameof(IconPackFolderName) or nameof(IsUninstallConfirming) or nameof(KeepSettingsOnUninstall) or nameof(UninstallError) or nameof(IsCheckingUpdates) or nameof(LastCheckText) or nameof(AboutStatusText) or nameof(CheckButtonText) or nameof(CanCheckUpdates) or nameof(DownloadProgressText))
            return;

        SaveSettings();

        if (Application.Current is not App app)
            return;

        if (e.PropertyName is nameof(Theme) or nameof(AccentColor) or nameof(UseWindowsAccent) or nameof(Material)
            or nameof(MaterialReadabilityGuard) or nameof(NotchFollowsMaterial)
            or nameof(CornerRadius) or nameof(AnimationSpeed) or nameof(RespectReduceMotion) or nameof(BatterySaverReduceMotion)
            or nameof(MotionNotchOpenClose) or nameof(MotionTabTransitions) or nameof(MotionOsd) or nameof(MotionDockMagnify) or nameof(MotionFolderPanel)
            or nameof(UiFontFamily) or nameof(ClockFontFamily) or nameof(BaseFontSize) or nameof(UiFontWeight) or nameof(UiScalePercent))
        {
            // Animasyon anahtarları da buradan (Motion.Refresh) canlı uygulanır; çentik ve dock yeniden kurulmaz.
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
             or nameof(IconPack) or nameof(IconPackFolder)
             or nameof(MaterialReadabilityGuard)
             or nameof(AnimationSpeed) or nameof(RespectReduceMotion));

    private static bool AffectsNotch(string? name) =>
        name is nameof(NotchVisibility) or nameof(NotchHideInFullscreen) or nameof(MusicShowWaveform) or nameof(MusicCardStyle) or nameof(MusicLyricsEnabled)
            or nameof(ShowVolumeOsd) or nameof(ShowBrightnessOsd) or nameof(ShowPrivacyIndicator) or nameof(NotchShowNotifications) or nameof(NotchNotificationsHideBody) or nameof(IdleDisplayMode) or nameof(NotchStyle)
            or nameof(Material) or nameof(MaterialReadabilityGuard) or nameof(NotchFollowsMaterial)
            or nameof(NotchOpenBehavior) or nameof(WheelAction) or nameof(BatterySaverReduceMotion)
            or nameof(NotchOpenDelayMs) or nameof(NotchCloseDelayMs) or nameof(NotchTabBarStyle) or nameof(NotchTabOrderVersion)
            or nameof(WeatherRefreshInterval) or nameof(PomodoroWorkMinutes) or nameof(PomodoroShortBreakMinutes) or nameof(PomodoroLongBreakMinutes)
            or nameof(PanelFocus) or nameof(PanelShelf) or nameof(PanelCalendar)
            or nameof(PanelControlCenter) or nameof(PanelClipboard) or nameof(PanelSystem) or nameof(ClipboardHistoryEnabled)
            or nameof(VoiceHarness) or nameof(VoiceClaudePermission) or nameof(VoiceModel) or nameof(VoiceCustomCommand) or nameof(VoiceWorkingDirectory) or nameof(VoiceTimeoutSeconds)
            or nameof(SearchRetainPrior) or nameof(SearchHistoryMode) or nameof(SearchSourcesVersion)
            or nameof(ClockHourFormat) or nameof(ClockShowSeconds) or nameof(ClockShowWeekNumber) or nameof(ClockFirstDayOfWeek)
            or nameof(ClockCustomDateFormat) or nameof(TemperatureUnit) or nameof(NetworkSpeedUnit);

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
            Profiles.RefreshLanguage();
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
                UpdateChannel = UpdateChannel,
                TemperatureUnit = TemperatureUnit,
                NetworkSpeedUnit = NetworkSpeedUnit
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
                MiddleClickAction = MiddleClickAction,
                NewTrayIconVisibility = DockNewTrayIconVisibility,
                TrayAppVisibility = TrayAppsForSave()
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
                OpenDelayMs = NotchOpenPolicy.ClampDelay(NotchOpenDelayMs),
                CloseDelayMs = NotchOpenPolicy.ClampDelay(NotchCloseDelayMs),
                TabBarStyle = NotchTabBarStyle,
                // Varsayılan sıra boş liste olarak kalır ("yalnız değişenler" onu değişmiş saymasın).
                PanelOrder = _panelOrder.SequenceEqual(NotchPanels.Order.Select(NotchPanels.ToKey)) ? [] : new ValueList<string>(_panelOrder),
                FollowsMaterial = NotchFollowsMaterial,
                WheelAction = WheelAction,
                ClockHourFormat = ClockHourFormat,
                ClockShowSeconds = ClockShowSeconds,
                ClockShowWeekNumber = ClockShowWeekNumber,
                ClockFirstDayOfWeek = ClockFirstDayOfWeek,
                // Geçersiz özel biçim kaydedilmez; boş metin yerleşik biçime döner.
                ClockCustomDateFormat = string.IsNullOrWhiteSpace(ClockCustomDateFormat) ? ""
                    : ClockCustomDateFormatInvalid ? current.Notch.ClockCustomDateFormat : ClockCustomDateFormat,
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
                IconPack = IconPack,
                IconPackFolder = IconPackFolder,
                AnimationSpeed = AnimationSpeed,
                RespectReduceMotion = RespectReduceMotion,
                BatterySaverReduceMotion = BatterySaverReduceMotion,
                Motion = (current.Appearance.Motion ?? new MotionSettings()) with
                {
                    NotchOpenClose = MotionNotchOpenClose,
                    TabTransitions = MotionTabTransitions,
                    Osd = MotionOsd,
                    DockMagnify = MotionDockMagnify,
                    FolderPanel = MotionFolderPanel
                },
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
        _lastKnown = updated;
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    public void RestoreTaskbar()
    {
        TaskbarService.Restore(force: true);
        // Dock çalışıyorsa Koy'un tepsi penceresi de bırakılır (görev çubuğu görünürken Shell_TrayWnd sahipliği alınmaz).
        (Application.Current as App)?.NotifyTaskbarRestoredManually();
    }

    // --- AYAR ARAMASI, YALNIZ DEĞİŞENLER, SAYFA SIFIRLAMA (issue #69) ---
    // Dizin ve satır işaretleme pencerededir (sayfaların kendisinden kurulur); burada yalnız durum ve komutlar var.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsSearching))]
    private string _settingsSearchQuery = "";

    public bool IsSettingsSearching => !string.IsNullOrWhiteSpace(SettingsSearchQuery);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SettingsSearchNoResult))]
    private IReadOnlyList<SettingsSearchResult> _settingsSearchResults = [];

    public bool SettingsSearchNoResult => IsSettingsSearching && SettingsSearchResults.Count == 0;

    partial void OnSettingsSearchQueryChanged(string value) => OnPropertyChanged(nameof(SettingsSearchNoResult));

    [ObservableProperty]
    private bool _showChangedOnly;

    /// <summary>Varsayılandan farklı ayar anahtarları ("Dock.ShowTray"); pencere satırları buna göre işaretler.</summary>
    public IReadOnlyList<string> ChangedSettingKeys() => SettingsChanges.ChangedKeys(_store.Load(), new AppSettings());

    /// <summary>Sayfa dizinine göre sıfırlanacak bölümler. Dock ve Ekranlar yok (görev çubuğu / AppBar etkisi).</summary>
    public static IReadOnlyList<string> ResetSectionsFor(int pageIndex) => pageIndex switch
    {
        0 => [nameof(AppSettings.General)],
        // Hava durumu konumu kullanıcı verisidir, sayfa sıfırlamada korunur.
        1 => [nameof(AppSettings.Notch), nameof(AppSettings.Music), nameof(AppSettings.Focus), nameof(AppSettings.Search), nameof(AppSettings.Clipboard)],
        4 => [nameof(AppSettings.Appearance)],
        5 => [nameof(AppSettings.Shortcuts)],
        6 => [nameof(AppSettings.Voice)],
        _ => [],
    };

    public bool CanResetPage => ResetSectionsFor(SelectedPageIndex).Count > 0;

    [ObservableProperty]
    private bool _isPageResetConfirming;

    /// <summary>Son sıfırlamadan sonra kısa bilgi satırı; sayfa değişince kapanır.</summary>
    [ObservableProperty]
    private bool _pageResetDone;

    partial void OnSelectedPageIndexChanged(int value)
    {
        IsPageResetConfirming = false;
        PageResetDone = false;
        OnPropertyChanged(nameof(CanResetPage));
    }

    [RelayCommand]
    private void RequestPageReset()
    {
        PageResetDone = false;
        IsPageResetConfirming = true;
    }

    [RelayCommand]
    private void CancelPageReset() => IsPageResetConfirming = false;

    [RelayCommand]
    private void ConfirmPageReset()
    {
        IsPageResetConfirming = false;
        var sections = ResetSectionsFor(SelectedPageIndex);
        if (sections.Count == 0)
            return;

        _store.Save(SettingsPageReset.Reset(_store.Load(), sections));
        LoadFromSettings();
        SettingsSaved?.Invoke();
        if (Application.Current is { } current)
            ThemeManager.Apply(current, _store.Load());
        if (Application.Current is App app)
            app.ReloadAllFeatures();
        PageResetDone = true;
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
