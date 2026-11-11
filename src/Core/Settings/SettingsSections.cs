using Koy.Core.Appearance;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Media;
using Koy.Core.Notch;
using Koy.Core.Search;
using Koy.Core.Visibility;
using Koy.Core.Voice;
using Koy.Core.Weather;

namespace Koy.Core.Settings;

// Ayar bölümleri (settings.json v4). Hepsi sealed record: eşitlik ve değer kopyası (with) ücretsiz gelir, elle Equals yok.
// Özellikler bilerek { get; set; } ve varsayılanlı: kaynak üreticili JSON, init özelliklerde dosyada olmayan alanı
// varsayılan yerine sıfırlıyor (NotchEnabled false olup çentik hiç açılmıyordu). Bkz. AppSettings.
// JSON adları dosyaya yazılır: yayımlandıktan sonra bir özelliğin ADI değiştirilmez (değişirse sürüm artırılır ve göç yazılır).

/// <summary>Genel: dil, ilk açılış, sürüm bilgisi ve güncelleme.</summary>
public sealed record GeneralSettings
{
    public string Language { get; set; } = AppLanguage.SystemDefault;

    /// <summary>İlk açılış sihirbazının tamamlanıp tamamlanmadığını belirtir.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Kullanıcının gördüğü en son Koy sürümü (güncelleme sonrası 'Neler Geldi' ekranı için).</summary>
    public string LastSeenVersion { get; set; } = "";

    public bool AutoCheckUpdates { get; set; } = true;
    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Stable;
}

/// <summary>Görünüm: tema, vurgu, köşe, hareket ve malzeme.</summary>
public sealed record AppearanceSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public string? AccentColor { get; set; }

    /// <summary>Açıksa vurgu rengi Windows vurgu renginden gelir, AccentColor yok sayılır.</summary>
    public bool UseWindowsAccent { get; set; }

    /// <summary>Koy penceresinin köşe seçimi (bkz. CornerSetting): 6 Keskin, 12 Standart, 17 Yuvarlak. Eski varsayılan 18 Standart sayılır.</summary>
    public double CornerRadius { get; set; } = 12.0;

    public AnimationSpeed AnimationSpeed { get; set; } = AnimationSpeed.Normal;
    public bool RespectReduceMotion { get; set; } = true;
    public bool BatterySaverReduceMotion { get; set; } = true;
    public MaterialLevel Material { get; set; } = MaterialLevel.Solid;

    /// <summary>Okunabilirlik koruması: açık duvar kağıdında saydam malzeme kendiliğinden daha opak seviyeye çıkar.</summary>
    public bool MaterialReadabilityGuard { get; set; } = true;
}

/// <summary>Dock ayarları. Dock'un kendi yazdığı veriler (sıra, klasörler) <see cref="DockItemsSettings"/> bölümündedir.</summary>
public sealed record DockSettings
{
    /// <summary>Kapalıysa Windows görev çubuğuna hiç dokunulmaz.</summary>
    public bool Enabled { get; set; } = true;

    public bool HideWindowsTaskbar { get; set; } = true;

    /// <summary>
    /// Eski görünürlük kipi (Fixed / Smart / Peek). Ayarlar'da ARAYÜZÜ YOK, Proximity yerine geçmedi: yakınlık modu "Yer ayır"
    /// dışındayken VisibilityController.IsVisible ile birlikte kararı daraltır ve kenar izlemeyi açar (DockViewModel,
    /// DockFeature.UpdateEdgeTimer). Varsayılan Fixed'ta etkisizdir; yalnız ayar dosyası elle düzenlenirse görünür bir fark yaratır.
    /// Uyumluluk için silinmez.
    /// </summary>
    public VisibilityMode Visibility { get; set; } = VisibilityMode.Fixed;

    /// <summary>Dock yerleşim stili: Yüzen kapsül (FloatingCapsule) veya Tam Genişlik (FullWidth).</summary>
    public DockStyleMode Style { get; set; } = DockStyleMode.FloatingCapsule;

    /// <summary>Tam genişlik dock'ta ikonların yeri: Left, Center (varsayılan), Right. Kapsülde etkisiz.</summary>
    public DockIconAlignment IconAlignment { get; set; } = DockIconAlignment.Center;

    /// <summary>Pencere yaklaşınca dock davranışı: ReserveSpace (Yer ayır - varsayılan), DockDown (Aşağı yapış), Dodge (Çekil), AlwaysHidden (Hep gizli).</summary>
    public DockProximityMode Proximity { get; set; } = DockProximityMode.ReserveSpace;

    /// <summary>Tam ekran bir uygulama veya oyun devredeyken dock'u otomatik gizle.</summary>
    public bool HideInFullscreen { get; set; } = true;

    /// <summary>
    /// Büyütülmüş pencere dock'u gizlediğinde fare alt kenara (~300 ms, fare tuşu basılı değilken) gelince dock çıkar.
    /// Kapalıysa dock büyütülmüş pencerede gizli kalır. Gerçek tam ekranda (oyun, F11, tam ekran video) hiçbir zaman çıkmaz.
    /// </summary>
    public bool EdgeReveal { get; set; } = true;

    public bool HoverMagnify { get; set; } = true;
    public bool ShowStartButton { get; set; } = true;

    /// <summary>Başlat düğmesinin simgesi: Koy (varsayılan), Windows logosu ya da Custom (<see cref="StartIconPath"/>).</summary>
    public DockStartIcon StartIcon { get; set; } = DockStartIcon.Koy;

    /// <summary>Özel Başlat simgesinin dosya yolu (PNG, JPG, ICO). Boşsa ya da dosya yoksa Koy simgesi gösterilir.</summary>
    public string StartIconPath { get; set; } = "";

    public bool ShowFolders { get; set; } = true;

    /// <summary>Dock'ta son kullanılan dosyaların yığın düğmesi (Windows'un Son Kullanılanlar klasörü, en çok 10 öğe).</summary>
    public bool ShowRecents { get; set; } = true;

    /// <summary>Dock'ta sistem tepsisi düğmesi. Açıkken Koy uygulamaların tepsi simgelerini kendisi toplar.</summary>
    public bool ShowTray { get; set; } = true;

    /// <summary>Küçük tepsi: tepsi düğmesi tam boy simge yerine dar bir ok olarak Koy menüsü düğmesinin sağına geçer.</summary>
    public bool SmallTray { get; set; } = true;

    public bool ShowBadges { get; set; } = true;
    public bool ThumbnailPreview { get; set; } = true;
    public int ThumbnailDelayMs { get; set; } = 400;
    public int DodgeDelayMs { get; set; } = 150;
    public double IconSize { get; set; } = 28.0;
    public DockIndicatorStyle IndicatorStyle { get; set; } = DockIndicatorStyle.Dot;

    /// <summary>Dock simgesine orta tık: yeni pencere (varsayılan) ya da kapat.</summary>
    public MiddleClickAction MiddleClickAction { get; set; } = MiddleClickAction.NewWindow;
}

/// <summary>
/// Dock'un KENDİ yazdığı veriler (sürükleyerek sıralama, klasörler). Ayrı bölüm olmasının sebebi: bunlar değişince dock
/// kendini yeniden kurmamalı (<c>DockSettingsDiff</c> yalnız <see cref="DockSettings"/>'e bakar).
/// </summary>
public sealed record DockItemsSettings
{
    /// <summary>
    /// Dock'taki sabitli uygulamaların sırası (kısayol dosya adları, örn. "Obsidian.lnk"). Boşsa ilk açılışta Windows'un
    /// sırası tohum alınır; sonra dock'ta sürükleyerek değiştirilir. Yeni sabitlenen sona eklenir, kaldırılan düşer.
    /// </summary>
    public ValueList<string> PinnedOrder { get; set; } = [];

    /// <summary>
    /// Dock üzerindeki klasör tanımları. Her klasör kısa bir kimlik (Id), görünen ad (Name)
    /// ve kısayol adlarından (.lnk) oluşan uygulama listesi (Items) içerir.
    /// </summary>
    public ValueList<DockFolder> Folders { get; set; } = [];
}

/// <summary>Çentik ayarları.</summary>
public sealed record NotchSettings
{
    public bool Enabled { get; set; } = true;
    public VisibilityMode Visibility { get; set; } = VisibilityMode.Fixed;

    /// <summary>Çentik biçimi: Pinned (varsayılan, ekran kenarına bitişik, kulaklı) ya da Floating (kulaksız kapsül, üstten 9 DIP boşluk).</summary>
    public NotchStyleMode Style { get; set; } = NotchStyleMode.Pinned;

    /// <summary>Çentiğin açılması: üzerine gelince (varsayılan) ya da tıklayınca.</summary>
    public NotchOpenBehavior OpenBehavior { get; set; } = NotchOpenBehavior.Hover;

    /// <summary>Üzerine gelince açılma gecikmesi (ms, 0–2000). Tıklayınca açılmada kullanılmaz.</summary>
    public int OpenDelayMs { get; set; }

    /// <summary>İmleç çentikten çıkınca kapanma gecikmesi (ms, 0–2000). Varsayılan bugünkü 400 ms.</summary>
    public int CloseDelayMs { get; set; } = NotchOpenPolicy.DefaultCloseDelayMs;

    /// <summary>Sekme çubuğu: yalnız simge (varsayılan), simge ve ad, ya da gizli.</summary>
    public NotchTabBarStyle TabBarStyle { get; set; } = NotchTabBarStyle.IconOnly;

    /// <summary>
    /// Sekmelerin kullanıcı sırası (panel anahtarları). Boşsa ya da eksikse varsayılan sıra kullanılır; görünürlük
    /// yine <see cref="EnabledPanels"/> ile belirlenir. Bkz. <c>NotchPanels.NormalizeOrder</c>.
    /// </summary>
    public ValueList<string> PanelOrder { get; set; } = [];

    /// <summary>Ana sayfada gösterilen kartlar ve sırası. Boş liste: hiçbir kart yok. Bkz. <c>HomeCardLayout.Normalize</c>.</summary>
    public ValueList<HomeCard> HomeCards { get; set; } = new(HomeCardLayout.Default);

    /// <summary>Çentik boştayken (kapalı, müzik çalmıyor) ne gösterilir.</summary>
    public IdleDisplayMode IdleDisplay { get; set; } = IdleDisplayMode.ClockAndBattery;

    /// <summary>Çentik üstünde tekerlek: panel değiştir (varsayılan) ya da ses.</summary>
    public WheelAction WheelAction { get; set; } = WheelAction.SwitchPanel;

    /// <summary>Tam ekran uygulama ya da oyun varken çentik gizlensin.</summary>
    public bool HideInFullscreen { get; set; } = true;

    /// <summary>Açık çentik ve kartlar malzemeyi izlesin; kapalıysa açık çentik hep opak kalır.</summary>
    public bool FollowsMaterial { get; set; } = true;

    /// <summary>Ses değişince çentikte 1,5 sn ses göstergesi çıksın.</summary>
    public bool ShowVolumeOsd { get; set; } = true;

    /// <summary>Parlaklık değişince çentikte 1,5 sn parlaklık göstergesi çıksın.</summary>
    public bool ShowBrightnessOsd { get; set; } = true;

    /// <summary>Kamera ve mikrofon kullanımında çentikte gizlilik noktaları görünsün.</summary>
    public bool ShowPrivacyIndicator { get; set; } = true;

    /// <summary>
    /// Windows bildirimleri çentikte kart olarak görünsün. Yalnız açıkken ve izin verilmişse bildirim listesi yoklanır
    /// (paketsiz süreçte olay yok); kapalıyken hiçbir zamanlayıcı çalışmaz.
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Bildirim kartında yalnız uygulama adı ve başlık görünsün, gövde gizlensin (gizlilik).</summary>
    public bool NotificationsHideBody { get; set; }

    public ValueList<string> EnabledPanels { get; set; } = ["home", "focus", "calendar", "shelf", "clipboard", "system", "controlcenter"];
    public ValueList<string> ControlCenterLayout { get; set; } = ["wifi", "bluetooth", "donotdisturb"];
}

/// <summary>Müzik kartı.</summary>
public sealed record MusicSettings
{
    /// <summary>Müzik çalarken çentikte dalga simgesi görünsün.</summary>
    public bool ShowWaveform { get; set; } = true;

    /// <summary>Müzik kartı stili: Neutral (düz), Tinted (albüm rengi, varsayılan), Artwork (kapak arka plan).</summary>
    public MusicCardStyle CardStyle { get; set; } = MusicCardStyle.Tinted;

    /// <summary>
    /// Ana sayfadaki müzik kartında eşzamanlı sözler. Varsayılan kapalı: açıkken çalan parçanın (tarayıcı videosu dahil) adı
    /// LRCLIB'e gider, bu yüzden kullanıcı kartındaki düğmeyle ya da ayardan kendisi açar. İstek yalnız Ana görünürken, parça başına bir kez.
    /// </summary>
    public bool LyricsEnabled { get; set; }

    /// <summary>Çentik görselleştiricisinin biçimi: çubuklar (varsayılan), dalga ya da noktalar.</summary>
    public NotchVisualizerStyle VisualizerStyle { get; set; } = NotchVisualizerStyle.Bars;

    /// <summary>Görselleştirici rengi: vurgu (varsayılan) ya da albüm kapağı.</summary>
    public NotchVisualizerColorSource VisualizerColorSource { get; set; } = NotchVisualizerColorSource.Accent;

    /// <summary>Görselleştiricideki çubuk sayısı (<see cref="Notch.NotchVisualizer.MinBarCount"/>–<see cref="Notch.NotchVisualizer.MaxBarCount"/>).</summary>
    public int VisualizerBarCount { get; set; } = 5;
}

/// <summary>Ekranlar: monitör başına ayar ve yeni ekran varsayılanı.</summary>
public sealed record DisplaysSettings
{
    /// <summary>
    /// Monitör başına çentik/dock ayarı; anahtar kararlı monitör kimliği. Bağlı olmayan ekranların kaydı da saklanır
    /// (ekran geri takılınca ayarı döner). Sözlük yerinde değiştirilmez, <see cref="MonitorPolicy.Update"/> yenisini üretir.
    /// </summary>
    public ValueDictionary<MonitorSettings> Monitors { get; set; } = new();

    /// <summary>
    /// Daha önce hiç görülmemiş yeni bir ekran bağlanınca çentik/dock durumu. Varsayılan AÇIK
    /// (<see cref="MonitorPolicy.DefaultNewScreenMode"/>): yeni ekran ayrı kurulum istemez.
    /// </summary>
    public NewScreenMode NewScreenDefault { get; set; } = MonitorPolicy.DefaultNewScreenMode;
}

/// <summary>
/// Genel kısayollar, "Ctrl+Alt+N" biçiminde metin; boş metin = atanmamış.
/// Varsayılanlar Windows'un Win+... kısayollarıyla çakışmasın diye Ctrl+Alt+harf (bkz. ShortcutBindings).
/// </summary>
public sealed record ShortcutsSettings
{
    public string ToggleNotch { get; set; } = "Ctrl+Alt+N";
    public string OpenShelf { get; set; } = "Ctrl+Alt+R";
    public string ToggleDock { get; set; } = "Ctrl+Alt+D";
    public string OpenMenu { get; set; } = "Ctrl+Alt+K";
    public string PlayPause { get; set; } = "Ctrl+Alt+Space";
    public string OpenSearch { get; set; } = "Win+Shift+Q";
    public string OpenSearchClipboard { get; set; } = "Alt+Shift+F2";
    public string VoiceCommand { get; set; } = "Ctrl+Alt+V";
}

/// <summary>Sesli komut.</summary>
public sealed record VoiceSettings
{
    public VoiceHarnessType Harness { get; set; } = VoiceHarnessType.Claude;

    /// <summary>Varsayılan salt okunur: dikte yanlış duyulsa da onaysız dosya değişikliği ya da komut çalışmaz.
    /// Otomatik ve Düzenleme kullanıcının açık seçimidir (proje sahibi kararı, 2026-11-11).</summary>
    public VoiceClaudePermission ClaudePermission { get; set; } = VoiceClaudePermission.Default;

    /// <summary>Claude'un modeli (takma ad ya da tam ad); "default" = aracın kendi varsayılanı.</summary>
    public string Model { get; set; } = "default";

    public string CustomCommand { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>Arama paneli.</summary>
public sealed record SearchSettings
{
    /// <summary>Arama paneli yeniden açılınca son sorgu geri gelsin.</summary>
    public bool RetainPrior { get; set; } = true;

    /// <summary>Varsayılan kapalı: geçmiş düz metin dosyasına yazılır, açmak kullanıcının kararı. Ayarda açık değer varsa korunur.</summary>
    public SearchHistoryMode HistoryMode { get; set; } = SearchHistoryMode.Disabled;

    /// <summary>Kapatılan arama kaynaklarının kimlikleri.</summary>
    public ValueList<string> DisabledSources { get; set; } = [];

    /// <summary>Arama paneli odak gidince ve sonuç çalışınca kapanmasın.</summary>
    public bool Pinned { get; set; }
}

/// <summary>Odak (Pomodoro) süreleri.</summary>
public sealed record FocusSettings
{
    /// <summary>Pomodoro çalışma süresi (dakika).</summary>
    public int PomodoroWorkMinutes { get; set; } = 25;

    /// <summary>Pomodoro kısa mola süresi (dakika).</summary>
    public int PomodoroShortBreakMinutes { get; set; } = 5;

    /// <summary>Pomodoro uzun mola süresi (dakika).</summary>
    public int PomodoroLongBreakMinutes { get; set; } = 15;
}

/// <summary>Hava durumu.</summary>
public sealed record WeatherSettings
{
    public WeatherRefreshInterval RefreshInterval { get; set; } = WeatherRefreshInterval.Every30Minutes;

    /// <summary>
    /// Hava durumu koordinatı. Yalnız <see cref="LocationSource"/> "Yok" değilse geçerlidir; eski sürümlerde
    /// burada sabit İstanbul vardı, kaynak "Yok" olduğu için artık okunmaz.
    /// </summary>
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Seçilen şehrin adı; cihaz konumunda boş.</summary>
    public string City { get; set; } = "";

    /// <summary>Koordinatın kaynağı: Yok (hava durumu gösterilmez), Cihaz konumu, Şehir araması.</summary>
    public WeatherLocationSource LocationSource { get; set; } = WeatherLocationSource.None;

    /// <summary>Kullanıcı hava durumu için cihaz konumunu kullanmayı kabul etti (sihirbaz ya da ayarlar).</summary>
    public bool UseLocation { get; set; }

    /// <summary>Koordinatın son güncellendiği an (Unix saniyesi, UTC); günde en fazla bir yenileme için.</summary>
    public long LocationUpdatedUnix { get; set; }
}

/// <summary>Pano geçmişi.</summary>
public sealed record ClipboardSettings
{
    /// <summary>Pano geçmişi (yalnız metin). Kapalıyken pano dinleyicisi hiç kurulmaz.</summary>
    public bool HistoryEnabled { get; set; } = true;
}

/// <summary>Hızlı başlat.</summary>
public sealed record QuickLaunchSettings
{
    /// <summary>Hızlı başlat: uygulama / dosya / klasör yolları, sıralı, en çok 8 (bkz. QuickLaunchService).</summary>
    public ValueList<string> Items { get; set; } = [];
}
