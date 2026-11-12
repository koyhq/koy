using Koy.Core.Appearance;
using Koy.Core.Clock;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Media;
using Koy.Core.Notch;
using Koy.Core.Search;
using Koy.Core.Tray;
using Koy.Core.Units;
using Koy.Core.Visibility;
using Koy.Core.Voice;
using Koy.Core.Weather;

using Koy.Core.Dock.Items;

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

    /// <summary>Sıcaklık birimi (hava durumu). Değer Core'da dönüştürülür, birim adı App'te yazılır (bkz. UnitConversion).</summary>
    public TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Celsius;

    /// <summary>Rüzgâr hızı birimi (hava durumu).</summary>
    public WindSpeedUnit WindSpeedUnit { get; set; } = WindSpeedUnit.KilometersPerHour;

    /// <summary>Ağ hızı birimi: bayt ya da bit.</summary>
    public NetworkSpeedUnit NetworkSpeedUnit { get; set; } = NetworkSpeedUnit.Bytes;
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

    /// <summary>Arayüz simge kaynağı: yerleşik ya da kullanıcı klasörü (bkz. IconPack).</summary>
    public IconPack IconPack { get; set; } = IconPack.Builtin;

    /// <summary>Uygulama başına simge değişimi (anahtar: kısayol adı, değer: doğrulanmış simge yolu). Bkz. AppIconOverrides.</summary>
    public ValueDictionary<string> AppIconOverrides { get; set; } = new();

    public AnimationSpeed AnimationSpeed { get; set; } = AnimationSpeed.Normal;
    public bool RespectReduceMotion { get; set; } = true;
    public bool BatterySaverReduceMotion { get; set; } = true;
    public MaterialLevel Material { get; set; } = MaterialLevel.Solid;

    /// <summary>Animasyon başına açma/kapama. Kapalı bir animasyonun son durumu hiç kare çizilmeden anında uygulanır. Henüz bağlı değil: animasyonlar bu anahtarları okumuyor (bkz. MotionPolicy.ScaleFor).</summary>
    public MotionSettings Motion { get; set; } = new();

    /// <summary>Okunabilirlik koruması: açık duvar kağıdında saydam malzeme kendiliğinden daha opak seviyeye çıkar.</summary>
    public bool MaterialReadabilityGuard { get; set; } = true;

    /// <summary>Arayüz yazı tipi adı. Boşsa ya da yüklü değilse Koy varsayılanı (bkz. FontScalePolicy).</summary>
    public string FontFamily { get; set; } = "";

    /// <summary>Saat ve sayılar için ayrı yazı tipi. Boşsa arayüz yazı tipi kullanılır.</summary>
    public string ClockFontFamily { get; set; } = "";

    /// <summary>Taban yazı boyu (DIP), 10-18 aralığında sınırlanır.</summary>
    public double BaseFontSize { get; set; } = FontScalePolicy.DefaultBaseFontSize;

    /// <summary>Yazı kalınlığı (100-900). Okunurluk için 100'lük adımlarda tutulur.</summary>
    public int FontWeight { get; set; } = FontScalePolicy.DefaultFontWeight;

    /// <summary>Koy'un kendi ölçeği (yüzde, 80-150), monitör DPI'sinin üstüne uygulanır.</summary>
    public int UiScalePercent { get; set; } = FontScalePolicy.DefaultUiScale;
}

/// <summary>Animasyon başına açma/kapama (issue #79). Azaltılmış hareket ve pil tasarrufu hepsini ezer.</summary>
public sealed record MotionSettings
{
    public bool NotchOpenClose { get; set; } = true;
    public bool DockMagnify { get; set; } = true;
    public bool FolderPanel { get; set; } = true;
    public bool Osd { get; set; } = true;
    public bool BadgeBounce { get; set; } = true;
    public bool TabTransitions { get; set; } = true;
}

/// <summary>Dock ayarları. Dock'un kendi yazdığı veriler (sıra, klasörler) <see cref="DockItemsSettings"/> bölümündedir.</summary>
public sealed record DockSettings
{
    /// <summary>Kapalıysa Windows görev çubuğuna hiç dokunulmaz.</summary>
    public bool Enabled { get; set; } = true;

    public bool HideWindowsTaskbar { get; set; } = true;

    /// <summary>Dock'un ekran kenarı. Varsayılan alt (bugünkü davranış). Dikey kenarlarda çizim henüz bağlanmadı (bkz. issue #73).</summary>
    public DockEdge Edge { get; set; } = DockEdge.Bottom;

    /// <summary>Kenara paralel uzunluk yüzdesi (%10-%100). Varsayılan %100. Aralık dışı değerler kenetlenir (<see cref="DockLayoutLimits"/>).</summary>
    public int LengthPercent { get; set; } = DockLayoutLimits.DefaultLengthPercent;

    /// <summary>Dock'un ekran kenarından uzaklığı (DIP). Varsayılan 0.</summary>
    public double EdgeOffsetDip { get; set; } = DockLayoutLimits.DefaultEdgeOffsetDip;

    /// <summary>Dock içi dolgu (DIP). Varsayılan 0.</summary>
    public double InnerPaddingDip { get; set; } = DockLayoutLimits.DefaultInnerPaddingDip;

    /// <summary>Simgeler arası boşluk (DIP, yuva başına). Varsayılan 2.</summary>
    public double IconSpacingDip { get; set; } = DockLayoutLimits.DefaultIconSpacingDip;

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

    /// <summary>Gösterge rengi: Windows vurgu rengi (varsayılan), nötr ya da <see cref="IndicatorCustomColor"/>.</summary>
    public DockColorSource IndicatorColor { get; set; } = DockColorSource.Accent;

    /// <summary>Özel gösterge rengi (#RRGGBB). Geçersizse vurgu rengi kullanılır.</summary>
    public string IndicatorCustomColor { get; set; } = "";

    /// <summary>Nokta kipinde her açık pencere için ayrı bir nokta (en çok üç).</summary>
    public bool IndicatorOnePerWindow { get; set; }

    /// <summary>Rozet rengi (bildirim sayısı vb.).</summary>
    public DockColorSource BadgeColor { get; set; } = DockColorSource.Accent;

    /// <summary>Özel rozet rengi (#RRGGBB). Geçersizse vurgu rengi kullanılır.</summary>
    public string BadgeCustomColor { get; set; } = "";

    public DockBadgeCorner BadgeCorner { get; set; } = DockBadgeCorner.TopRight;
    public DockBadgeStyle BadgeStyle { get; set; } = DockBadgeStyle.Number;

    /// <summary>Gizli dock'un çıkma tetikleyicisi: kenarda beklemek (varsayılan, bugünkü davranış), kenara dokunmak ya da yalnız kısayol.</summary>
    public DockRevealTrigger RevealTrigger { get; set; } = DockRevealTrigger.EdgeHold;

    /// <summary>Kenarda bekleme süresi (ms, EdgeHold iken). Varsayılan bugünkü kenar gecikmesi (<see cref="EdgeRevealTracker.DefaultDelay"/>).</summary>
    public int RevealDelayMs { get; set; } = (int)EdgeRevealTracker.DefaultDelay.TotalMilliseconds;

    /// <summary>Dock gizliyken alt kenarda ince bir çizgi görünsün mü.</summary>
    public bool HiddenEdgeLine { get; set; }

    /// <summary>Dock simgesine orta tık: yeni pencere (varsayılan) ya da kapat.</summary>
    public MiddleClickAction MiddleClickAction { get; set; } = MiddleClickAction.NewWindow;

    /// <summary>Aynı uygulamanın pencereleri tek simgede: hiç (varsayılan), her zaman ya da yalnız yer kalmayınca.</summary>
    public DockGroupingMode GroupingMode { get; set; } = DockGroupingMode.Never;

    /// <summary>Dock'ta gösterilen pencereler: tüm monitörler (varsayılan) ya da yalnız dock'un monitörü.</summary>
    public DockWindowMonitorScope WindowMonitorScope { get; set; } = DockWindowMonitorScope.AllMonitors;

    /// <summary>Dock simgesine sol tık: öne getir (varsayılan) ya da başka bir pencere eylemi.</summary>
    public DockLeftClickAction LeftClickAction { get; set; } = DockLeftClickAction.BringToFront;

    /// <summary>Dock simgelerinin altındaki uygulama adı etiketi: hiç (varsayılan), her zaman ya da üzerine gelince.</summary>
    public DockLabelMode LabelMode { get; set; } = DockLabelMode.Never;

    /// <summary>Yeni bir tepsi simgesinin varsayılan yeri (uygulamanın kendi kaydı yoksa). Varsayılan taşma menüsü.</summary>
    public TrayIconVisibility NewTrayIconVisibility { get; set; } = TrayIconVisibilityPolicy.DefaultNewIcon;

    /// <summary>Uygulama başına tepsi simgesi yeri. Anahtar küçük harfli exe adı; kaydı olmayan simge <see cref="NewTrayIconVisibility"/>'i alır.</summary>
    public ValueDictionary<TrayIconVisibility> TrayAppVisibility { get; set; } = new();
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

    /// <summary>
    /// Dock öğe dizisi (ayırıcı, boşluk, saat, çöp kutusu, özel düğme, uygulama, klasör). Yükleme anında en çok 32 kayıt kalır;
    /// kayıtlar <see cref="Koy.Core.Dock.Items.DockItemRules.Resolve"/> ile doğrulanır. Boşsa dock PinnedOrder'dan türetilir
    /// (kullanıcı sırasını kaybetmez). Henüz hiçbir yerde çizilmez.
    /// </summary>
    public ValueList<DockItemEntry> Items { get; set; } = [];
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

    /// <summary>Boştayken çentikteki saat: biçim (12/24 saat, Auto kültürü izler). Kararlar <see cref="ClockFormatPolicy"/>'de.</summary>
    public ClockHourFormat ClockHourFormat { get; set; } = ClockHourFormat.Auto;

    /// <summary>Saat saniyeyi göstersin.</summary>
    public bool ClockShowSeconds { get; set; }

    /// <summary>Saatin yanında tarih görünsün.</summary>
    public bool ClockShowDate { get; set; }

    /// <summary>Takvimde hafta numarası görünsün.</summary>
    public bool ClockShowWeekNumber { get; set; }

    /// <summary>Takvimde haftanın ilk günü (System: kültürün ilk günü).</summary>
    public WeekStartSetting ClockFirstDayOfWeek { get; set; } = WeekStartSetting.System;

    /// <summary>Boşsa yerleşik tarih biçimi kullanılır; doluysa <see cref="ClockFormatPolicy.IsValidCustomDateFormat"/> geçmelidir.</summary>
    public string ClockCustomDateFormat { get; set; } = "";

    /// <summary>İkinci saatin saat dilimi kimliği (Windows kimliği, örn. "Tokyo Standard Time"). Boşsa ikinci saat kapalı.</summary>
    public string ClockSecondTimeZoneId { get; set; } = "";

    /// <summary>
    /// Canlı arka plan (issue #67): nereye uygulanacağı. Varsayılan None = özellik kapalı, hiçbir şey yüklenmez ve çizilmez.
    /// Yalnız çentik açıkken oynar, kapanınca durur ve bellek bırakılır (bkz. <see cref="LiveBackgroundPlaybackPolicy"/>).
    /// </summary>
    public LiveBackgroundTarget LiveBackgroundTarget { get; set; } = LiveBackgroundTarget.None;

    /// <summary>Canlı arka plan kaynağı: yerleşik ambiyans (varsayılan) ya da yerel dosya.</summary>
    public LiveBackgroundSourceKind LiveBackgroundSource { get; set; } = LiveBackgroundSourceKind.BuiltInAmbient;

    /// <summary>Yerleşik ambiyans önayar kimliği (bkz. <see cref="LiveBackgroundPresets"/>); bilinmeyen kimlik varsayılana düşer.</summary>
    public string LiveBackgroundPreset { get; set; } = LiveBackgroundPresets.DefaultId;

    /// <summary>Yerel dosya yolu (.gif, .mp4, .webm, .webp; yalnız yerel sürücü). Geçerliliği <see cref="LiveBackgroundFileValidator"/> denetler.</summary>
    public string LiveBackgroundFilePath { get; set; } = "";

    /// <summary>Okunabilirlik için koyu perdenin yüzdesi (30–90, bkz. <see cref="LiveBackgroundLimits.ClampScrimPercent"/>).</summary>
    public int LiveBackgroundScrimPercent { get; set; } = LiveBackgroundLimits.DefaultScrimPercent;
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
