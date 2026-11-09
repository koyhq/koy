using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Visibility;
using Koy.Core.Weather;

namespace Koy.Core.Settings;

/// <summary>
/// settings.json içeriği. Yeni alan eklerken varsayılan değer ver ki eski dosyalar bozulmasın.
/// Alan yeniden adlandırılır ya da silinirse <see cref="Version"/> artırılır.
/// Özellikler bilerek <c>set</c>: kaynak üreticili JSON, <c>init</c> özelliklerde dosyada olmayan alanı
/// varsayılan yerine sıfırlıyor (NotchEnabled false olup çentik hiç açılmıyordu).
/// </summary>
public sealed record AppSettings
{
    /// <summary>Şema sürümü. 3: monitör başına ayar (<see cref="Monitors"/>), eski <see cref="MonitorTarget"/> göçle taşınır.</summary>
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;
    public string Language { get; set; } = AppLanguage.SystemDefault;
    public bool NotchEnabled { get; set; } = true;

    /// <summary>Kapalıysa Windows görev çubuğuna hiç dokunulmaz.</summary>
    public bool DockEnabled { get; set; } = true;

    public VisibilityMode NotchVisibility { get; set; } = VisibilityMode.Fixed;
    /// <summary>
    /// Eski görünürlük kipi (Fixed / Smart / Peek). Ayarlar'da ARAYÜZÜ YOK, DockProximity yerine geçmedi: yakınlık modu "Yer ayır"
    /// dışındayken VisibilityController.IsVisible ile birlikte kararı daraltır ve kenar izlemeyi açar (DockViewModel,
    /// DockFeature.UpdateEdgeTimer). Varsayılan Fixed'ta etkisizdir; yalnız ayar dosyası elle düzenlenirse görünür bir fark yaratır.
    /// Uyumluluk için silinmez.
    /// </summary>
    public VisibilityMode DockVisibility { get; set; } = VisibilityMode.Fixed;
    public bool DockHoverMagnify { get; set; } = true;

    /// <summary>Dock yerleşim stili: Yüzen kapsül (FloatingCapsule) veya Tam Genişlik (FullWidth).</summary>
    public DockStyleMode DockStyle { get; set; } = DockStyleMode.FloatingCapsule;

    /// <summary>Tam genişlik dock'ta ikonların yeri: Left, Center (varsayılan), Right. Kapsülde etkisiz.</summary>
    public DockIconAlignment DockIconAlignment { get; set; } = DockIconAlignment.Center;

    /// <summary>Pencere yaklaşınca dock davranışı: ReserveSpace (Yer ayır - varsayılan), DockDown (Aşağı yapış), Dodge (Çekil), AlwaysHidden (Hep gizli).</summary>
    public DockProximityMode DockProximity { get; set; } = DockProximityMode.ReserveSpace;

    /// <summary>Tam ekran bir uygulama veya oyun devredeyken dock'u otomatik gizle.</summary>
    public bool DockHideInFullscreen { get; set; } = true;

    /// <summary>
    /// Büyütülmüş pencere dock'u gizlediğinde fare alt kenara (~300 ms, fare tuşu basılı değilken) gelince dock çıkar.
    /// Kapalıysa dock büyütülmüş pencerede gizli kalır. Gerçek tam ekranda (oyun, F11, tam ekran video) hiçbir zaman çıkmaz.
    /// </summary>
    public bool DockEdgeReveal { get; set; } = true;

    public bool DockShowStartButton { get; set; } = true;
    public bool DockShowFolders { get; set; } = true;

    /// <summary>Dock'ta son kullanılan dosyaların yığın düğmesi (Windows'un Son Kullanılanlar klasörü, en çok 10 öğe).</summary>
    public bool DockShowRecents { get; set; } = true;

    /// <summary>Dock'ta sistem tepsisi düğmesi. Açıkken Koy uygulamaların tepsi simgelerini kendisi toplar.</summary>
    public bool DockShowTray { get; set; } = true;

    /// <summary>Küçük tepsi: tepsi düğmesi tam boy simge yerine dar bir ok olarak Koy menüsü düğmesinin sağına geçer.</summary>
    public bool DockSmallTray { get; set; } = true;
    public bool DockShowBadges { get; set; } = true;

    /// <summary>
    /// Dock'taki sabitli uygulamaların sırası (kısayol dosya adları, örn. "Obsidian.lnk"). Boşsa ilk açılışta Windows'un
    /// sırası tohum alınır; sonra dock'ta sürükleyerek değiştirilir. Yeni sabitlenen sona eklenir, kaldırılan düşer.
    /// </summary>
    public List<string> DockPinnedOrder { get; set; } = [];

    /// <summary>
    /// Dock üzerindeki klasör tanımları. Her klasör kısa bir kimlik (Id), görünen ad (Name)
    /// ve kısayol adlarından (.lnk) oluşan uygulama listesi (Items) içerir.
    /// </summary>
    public List<DockFolder> DockFolders { get; set; } = [];
    public bool DockThumbnailPreview { get; set; } = true;
    public int DockThumbnailDelayMs { get; set; } = 400;
    public int DockDodgeDelayMs { get; set; } = 150;

    /// <summary>Eski (sürüm 2) hedef monitör alanı. Göçten sonra okunmaz; yalnız eski dosyalar bozulmasın diye durur.</summary>
    public string MonitorTarget { get; set; } = "Primary";

    /// <summary>
    /// Monitör başına çentik/dock ayarı; anahtar kararlı monitör kimliği. Bağlı olmayan ekranların kaydı da saklanır
    /// (ekran geri takılınca ayarı döner). Sözlük yerinde değiştirilmez, <see cref="MonitorPolicy.Update"/> yenisini üretir.
    /// </summary>
    public Dictionary<string, MonitorSettings> Monitors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Daha önce hiç görülmemiş yeni bir ekran bağlanınca çentik/dock durumu. Varsayılan AÇIK
    /// (<see cref="MonitorPolicy.DefaultNewScreenMode"/>): yeni ekran ayrı kurulum istemez.
    /// </summary>
    public NewScreenMode NewScreenDefault { get; set; } = MonitorPolicy.DefaultNewScreenMode;
    public string? AccentColor { get; set; }

    /// <summary>Açıksa vurgu rengi Windows vurgu renginden gelir, AccentColor yok sayılır.</summary>
    public bool UseWindowsAccent { get; set; }

    /// <summary>İlk açılış sihirbazının tamamlanıp tamamlanmadığını belirtir.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Kullanıcının gördüğü en son Koy sürümü (güncelleme sonrası 'Neler Geldi' ekranı için).</summary>
    public string LastSeenVersion { get; set; } = "";

    public string Theme { get; set; } = "Dark";
    /// <summary>Koy penceresinin köşe seçimi (bkz. CornerSetting): 6 Keskin, 12 Standart, 17 Yuvarlak. Eski varsayılan 18 Standart sayılır.</summary>
    public double CornerRadius { get; set; } = 12.0;
    public string AnimationSpeed { get; set; } = "Normal";
    public bool RespectReduceMotion { get; set; } = true;
    public bool HideWindowsTaskbar { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public string UpdateChannel { get; set; } = "Kararlı";
    public bool BatterySaverReduceMotion { get; set; } = true;
    public string WeatherRefreshInterval { get; set; } = "30m";
    public double DockIconSize { get; set; } = 28.0;
    public int IdleDisplayMode { get; set; }
    public int NotchOpenBehavior { get; set; } = 1;
    public string Material { get; set; } = "Katı";
    public int WheelAction { get; set; }
    public int MiddleClickAction { get; set; }
    public int DockIndicatorStyle { get; set; }

    /// <summary>
    /// Genel kısayollar, "Ctrl+Alt+N" biçiminde metin; boş metin = atanmamış.
    /// Varsayılanlar Windows'un Win+... kısayollarıyla çakışmasın diye Ctrl+Alt+harf (bkz. ShortcutBindings).
    /// </summary>
    public string ShortcutToggleNotch { get; set; } = "Ctrl+Alt+N";
    public string ShortcutOpenShelf { get; set; } = "Ctrl+Alt+R";
    public string ShortcutToggleDock { get; set; } = "Ctrl+Alt+D";
    public string ShortcutOpenMenu { get; set; } = "Ctrl+Alt+K";
    public string ShortcutPlayPause { get; set; } = "Ctrl+Alt+Space";
    public string ShortcutOpenSearch { get; set; } = "Win+Shift+Q";
    public string ShortcutOpenSearchClipboard { get; set; } = "Alt+Shift+F2";

    /// <summary>Arama paneli yeniden açılınca son sorgu geri gelsin.</summary>
    public bool SearchRetainPrior { get; set; } = true;

    /// <summary>Varsayılan kapalı: geçmiş düz metin dosyasına yazılır, açmak kullanıcının kararı. Ayarda açık değer varsa korunur.</summary>
    public Koy.Core.Search.SearchHistoryMode SearchHistoryMode { get; set; } = Koy.Core.Search.SearchHistoryMode.Disabled;

    /// <summary>Kapatılan arama kaynaklarının kimlikleri.</summary>
    public List<string> SearchDisabledSources { get; set; } = [];

    /// <summary>Arama paneli odak gidince ve sonuç çalışınca kapanmasın.</summary>
    public bool SearchPinned { get; set; }
    /// <summary>Pomodoro uzun mola süresi (dakika).</summary>
    public int PomodoroLongBreakMinutes { get; set; } = 15;
    /// <summary>Pomodoro kısa mola süresi (dakika).</summary>
    public int PomodoroShortBreakMinutes { get; set; } = 5;
    /// <summary>Pomodoro çalışma süresi (dakika).</summary>
    public int PomodoroWorkMinutes { get; set; } = 25;
    /// <summary>Parlaklık değişince çentikte 1,5 sn parlaklık göstergesi çıksın.</summary>
    public bool ShowBrightnessOsd { get; set; } = true;
    /// <summary>Ses değişince çentikte 1,5 sn ses göstergesi çıksın.</summary>
    public bool ShowVolumeOsd { get; set; } = true;
    /// <summary>Müzik çalarken çentikte dalga simgesi görünsün.</summary>
    public bool MusicShowWaveform { get; set; } = true;
    /// <summary>Ana sayfadaki müzik kartında eşzamanlı sözler. İstek yalnız Ana sayfa görünürken, parça başına bir kez gider.</summary>
    public bool MusicLyricsEnabled { get; set; } = true;
    /// <summary>Tam ekran uygulama ya da oyun varken çentik gizlensin.</summary>
    public bool NotchHideInFullscreen { get; set; } = true;
    /// <summary>Kamera ve mikrofon kullanımında çentikte gizlilik noktaları görünsün.</summary>
    public bool ShowPrivacyIndicator { get; set; } = true;

    public List<string> ControlCenterLayout { get; set; } = ["wifi", "bluetooth", "donotdisturb"];
    public List<string> EnabledPanels { get; set; } = ["home", "focus", "calendar", "shelf", "clipboard", "system", "controlcenter"];
    /// <summary>
    /// Hava durumu koordinatı. Yalnız <see cref="WeatherLocationSource"/> "Yok" değilse geçerlidir; eski sürümlerde
    /// burada sabit İstanbul vardı, kaynak "Yok" olduğu için artık okunmaz.
    /// </summary>
    public double WeatherLatitude { get; set; }
    public double WeatherLongitude { get; set; }
    /// <summary>Seçilen şehrin adı; cihaz konumunda boş.</summary>
    public string WeatherCity { get; set; } = "";
    /// <summary>Koordinatın kaynağı: Yok (hava durumu gösterilmez), Cihaz konumu, Şehir araması.</summary>
    public WeatherLocationSource WeatherLocationSource { get; set; } = WeatherLocationSource.None;
    /// <summary>Kullanıcı hava durumu için cihaz konumunu kullanmayı kabul etti (sihirbaz ya da ayarlar).</summary>
    public bool WeatherUseLocation { get; set; }
    /// <summary>Koordinatın son güncellendiği an (Unix saniyesi, UTC); günde en fazla bir yenileme için.</summary>
    public long WeatherLocationUpdatedUnix { get; set; }

    /// <summary>Pano geçmişi (yalnız metin). Kapalıyken pano dinleyicisi hiç kurulmaz.</summary>
    public bool ClipboardHistoryEnabled { get; set; } = true;

    /// <summary>Hızlı başlat: uygulama / dosya / klasör yolları, sıralı, en çok 8 (bkz. QuickLaunchService).</summary>
    public List<string> QuickLaunchItems { get; set; } = [];

    public bool Equals(AppSettings? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Version == other.Version &&
               Language == other.Language &&
               NotchEnabled == other.NotchEnabled &&
               DockEnabled == other.DockEnabled &&
               NotchVisibility == other.NotchVisibility &&
               DockVisibility == other.DockVisibility &&
               DockHoverMagnify == other.DockHoverMagnify &&
               DockStyle == other.DockStyle &&
               DockIconAlignment == other.DockIconAlignment &&
               DockProximity == other.DockProximity &&
               DockHideInFullscreen == other.DockHideInFullscreen &&
               DockEdgeReveal == other.DockEdgeReveal &&
               DockShowStartButton == other.DockShowStartButton &&
               DockShowFolders == other.DockShowFolders &&
               DockShowRecents == other.DockShowRecents &&
               DockShowTray == other.DockShowTray &&
               DockSmallTray == other.DockSmallTray &&
               DockShowBadges == other.DockShowBadges &&
               DockThumbnailPreview == other.DockThumbnailPreview &&
               DockThumbnailDelayMs == other.DockThumbnailDelayMs &&
               DockDodgeDelayMs == other.DockDodgeDelayMs &&
               MonitorTarget == other.MonitorTarget &&
               NewScreenDefault == other.NewScreenDefault &&
               MonitorsEqual(Monitors, other.Monitors) &&
               AccentColor == other.AccentColor &&
               UseWindowsAccent == other.UseWindowsAccent &&
               FirstRunCompleted == other.FirstRunCompleted &&
               LastSeenVersion == other.LastSeenVersion &&
               Theme == other.Theme &&
               CornerRadius.Equals(other.CornerRadius) &&
               AnimationSpeed == other.AnimationSpeed &&
               RespectReduceMotion == other.RespectReduceMotion &&
               HideWindowsTaskbar == other.HideWindowsTaskbar &&
               AutoCheckUpdates == other.AutoCheckUpdates &&
               UpdateChannel == other.UpdateChannel &&
               BatterySaverReduceMotion == other.BatterySaverReduceMotion &&
               WeatherRefreshInterval == other.WeatherRefreshInterval &&
               DockIconSize.Equals(other.DockIconSize) &&
               IdleDisplayMode == other.IdleDisplayMode &&
               NotchOpenBehavior == other.NotchOpenBehavior &&
               Material == other.Material &&
               WheelAction == other.WheelAction &&
               MiddleClickAction == other.MiddleClickAction &&
               DockIndicatorStyle == other.DockIndicatorStyle &&
               ShortcutToggleNotch == other.ShortcutToggleNotch &&
               ShortcutOpenShelf == other.ShortcutOpenShelf &&
               ShortcutToggleDock == other.ShortcutToggleDock &&
               ShortcutOpenMenu == other.ShortcutOpenMenu &&
               ShortcutPlayPause == other.ShortcutPlayPause &&
               ShortcutOpenSearch == other.ShortcutOpenSearch &&
               ShortcutOpenSearchClipboard == other.ShortcutOpenSearchClipboard &&
               SearchRetainPrior == other.SearchRetainPrior &&
               SearchHistoryMode == other.SearchHistoryMode &&
               SearchDisabledSources.SequenceEqual(other.SearchDisabledSources) &&
               SearchPinned == other.SearchPinned &&
               PomodoroLongBreakMinutes == other.PomodoroLongBreakMinutes &&
               PomodoroShortBreakMinutes == other.PomodoroShortBreakMinutes &&
               PomodoroWorkMinutes == other.PomodoroWorkMinutes &&
               ShowBrightnessOsd == other.ShowBrightnessOsd &&
               ShowVolumeOsd == other.ShowVolumeOsd &&
               MusicShowWaveform == other.MusicShowWaveform &&
               MusicLyricsEnabled == other.MusicLyricsEnabled &&
               NotchHideInFullscreen == other.NotchHideInFullscreen &&
               ShowPrivacyIndicator == other.ShowPrivacyIndicator &&
               WeatherLatitude.Equals(other.WeatherLatitude) &&
               WeatherLongitude.Equals(other.WeatherLongitude) &&
               WeatherCity == other.WeatherCity &&
               WeatherLocationSource == other.WeatherLocationSource &&
               WeatherUseLocation == other.WeatherUseLocation &&
               WeatherLocationUpdatedUnix == other.WeatherLocationUpdatedUnix &&
               ClipboardHistoryEnabled == other.ClipboardHistoryEnabled &&
               (QuickLaunchItems == other.QuickLaunchItems || (QuickLaunchItems is not null && other.QuickLaunchItems is not null && QuickLaunchItems.SequenceEqual(other.QuickLaunchItems))) &&
               (ControlCenterLayout == other.ControlCenterLayout || (ControlCenterLayout is not null && other.ControlCenterLayout is not null && ControlCenterLayout.SequenceEqual(other.ControlCenterLayout))) &&
               (EnabledPanels == other.EnabledPanels || (EnabledPanels is not null && other.EnabledPanels is not null && EnabledPanels.SequenceEqual(other.EnabledPanels))) &&
               ListsEqual(DockPinnedOrder, other.DockPinnedOrder) &&
               FoldersEqual(DockFolders, other.DockFolders);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Version);
        hash.Add(Language);
        hash.Add(NotchEnabled);
        hash.Add(DockEnabled);
        hash.Add(NotchVisibility);
        hash.Add(DockVisibility);
        hash.Add(DockHoverMagnify);
        hash.Add(DockStyle);
        hash.Add(DockIconAlignment);
        hash.Add(DockProximity);
        hash.Add(DockHideInFullscreen);
        hash.Add(DockEdgeReveal);
        hash.Add(DockShowStartButton);
        hash.Add(DockShowFolders);
        hash.Add(DockShowRecents);
        hash.Add(DockShowTray);
        hash.Add(DockSmallTray);
        hash.Add(DockShowBadges);
        hash.Add(DockThumbnailPreview);
        hash.Add(DockThumbnailDelayMs);
        hash.Add(DockDodgeDelayMs);
        hash.Add(MonitorTarget);
        hash.Add(NewScreenDefault);
        hash.Add(MonitorsHash(Monitors));
        hash.Add(AccentColor);
        hash.Add(UseWindowsAccent);
        hash.Add(FirstRunCompleted);
        hash.Add(LastSeenVersion);
        hash.Add(Theme);
        hash.Add(CornerRadius);
        hash.Add(AnimationSpeed);
        hash.Add(RespectReduceMotion);
        hash.Add(HideWindowsTaskbar);
        hash.Add(AutoCheckUpdates);
        hash.Add(UpdateChannel);
        hash.Add(BatterySaverReduceMotion);
        hash.Add(WeatherRefreshInterval);
        hash.Add(DockIconSize);
        hash.Add(IdleDisplayMode);
        hash.Add(NotchOpenBehavior);
        hash.Add(Material);
        hash.Add(WheelAction);
        hash.Add(MiddleClickAction);
        hash.Add(DockIndicatorStyle);
        hash.Add(ShortcutToggleNotch);
        hash.Add(ShortcutOpenShelf);
        hash.Add(ShortcutToggleDock);
        hash.Add(ShortcutOpenMenu);
        hash.Add(ShortcutPlayPause);
        hash.Add(ShortcutOpenSearch);
        hash.Add(ShortcutOpenSearchClipboard);
        hash.Add(SearchRetainPrior);
        hash.Add(SearchHistoryMode);
        hash.Add(SearchPinned);
        hash.Add(PomodoroLongBreakMinutes);
        hash.Add(PomodoroShortBreakMinutes);
        hash.Add(PomodoroWorkMinutes);
        hash.Add(ShowBrightnessOsd);
        hash.Add(ShowVolumeOsd);
        hash.Add(MusicShowWaveform);
        hash.Add(MusicLyricsEnabled);
        hash.Add(NotchHideInFullscreen);
        hash.Add(ShowPrivacyIndicator);
        hash.Add(WeatherLatitude);
        hash.Add(WeatherLongitude);
        hash.Add(WeatherCity);
        hash.Add(WeatherLocationSource);
        hash.Add(WeatherUseLocation);
        hash.Add(WeatherLocationUpdatedUnix);
        hash.Add(ClipboardHistoryEnabled);
        if (QuickLaunchItems is not null)
        {
            foreach (var item in QuickLaunchItems)
                hash.Add(item);
        }
        if (ControlCenterLayout is not null)
        {
            foreach (var item in ControlCenterLayout)
                hash.Add(item);
        }
        if (EnabledPanels is not null)
        {
            foreach (var item in EnabledPanels)
                hash.Add(item);
        }
        if (DockPinnedOrder is not null)
        {
            foreach (var item in DockPinnedOrder)
                hash.Add(item);
        }
        if (DockFolders is not null)
        {
            foreach (var folder in DockFolders)
            {
                if (folder is null) continue;
                hash.Add(folder.Id, StringComparer.OrdinalIgnoreCase);
                hash.Add(folder.Name);
                if (folder.Items is not null)
                {
                    foreach (var item in folder.Items)
                        hash.Add(item, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        return hash.ToHashCode();
    }

    private static bool ListsEqual(List<string>? a, List<string>? b)
        => ReferenceEquals(a, b) || ((a ?? []).SequenceEqual(b ?? []));

    private static bool FoldersEqual(List<DockFolder>? a, List<DockFolder>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        a ??= [];
        b ??= [];
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            var fa = a[i];
            var fb = b[i];
            if (fa is null || fb is null)
            {
                if (!ReferenceEquals(fa, fb)) return false;
                continue;
            }
            if (!string.Equals(fa.Id, fb.Id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(fa.Name, fb.Name, StringComparison.Ordinal) ||
                !fa.Items.SequenceEqual(fb.Items, StringComparer.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static bool MonitorsEqual(Dictionary<string, MonitorSettings>? a, Dictionary<string, MonitorSettings>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        a ??= [];
        b ??= [];
        if (a.Count != b.Count) return false;
        foreach (var (key, value) in a)
        {
            var match = b.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key is null || !Equals(value, match.Value)) return false;
        }
        return true;
    }

    // Sıradan bağımsız: sözlük sırası değişse de aynı ayarlar aynı özeti verir.
    private static int MonitorsHash(Dictionary<string, MonitorSettings>? monitors)
    {
        var sum = 0;
        if (monitors is not null)
            foreach (var (key, value) in monitors)
                sum += HashCode.Combine(key.ToLowerInvariant(), value);
        return sum;
    }
}
