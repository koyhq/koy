using Koy.Core.Dock;

namespace Koy.Core.Settings;

/// <summary>
/// settings.json içeriği (şema v4): bölümlere ayrılmış kayıtlar. Yeni ayar = ilgili bölüm kaydına varsayılanlı alan;
/// eşitlik ve hash kayıtlardan kendiliğinden gelir (bkz. docs/MAP.md › Yeni ayar eklemek).
/// Alan yeniden adlandırılır ya da silinirse <see cref="Version"/> artırılır ve <see cref="SettingsMigration"/> yazılır.
/// Özellikler bilerek <c>set</c>: kaynak üreticili JSON, <c>init</c> özelliklerde dosyada olmayan alanı
/// varsayılan yerine sıfırlıyor (NotchEnabled false olup çentik hiç açılmıyordu).
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// Şema sürümü. 3: monitör başına ayar, eski MonitorTarget göçle taşınır. 4: bölümlere ayrılmış kayıtlar,
    /// sayı ve dile bağlı metin değerleri enum adı olarak yazılır (v3 düz dosyası <see cref="SettingsMigration"/> ile taşınır).
    /// </summary>
    public const int CurrentVersion = 4;

    public int Version { get; set; } = CurrentVersion;
    public GeneralSettings General { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public DockSettings Dock { get; set; } = new();
    public DockItemsSettings DockItems { get; set; } = new();
    public NotchSettings Notch { get; set; } = new();
    public MusicSettings Music { get; set; } = new();
    public DisplaysSettings Displays { get; set; } = new();
    public ShortcutsSettings Shortcuts { get; set; } = new();
    public VoiceSettings Voice { get; set; } = new();
    public SearchSettings Search { get; set; } = new();
    public FocusSettings Focus { get; set; } = new();
    public WeatherSettings Weather { get; set; } = new();
    public ClipboardSettings Clipboard { get; set; } = new();
    public QuickLaunchSettings QuickLaunch { get; set; } = new();

    /// <summary>
    /// Dosyada <c>"Dock": null</c> gibi boş bırakılmış bölümleri (ve bölümlerdeki boş listeleri) varsayılanla doldurur;
    /// böylece okuyanlar null denetimi yapmaz. Yeni bölüm/liste eklenince burası güncellenir.
    /// </summary>
    internal AppSettings FillMissingSections()
    {
        General ??= new();
        Appearance ??= new();
        Appearance.AppIconOverrides ??= new();
        Dock ??= new();
        DockItems ??= new();
        Notch ??= new();
        Music ??= new();
        Displays ??= new();
        Shortcuts ??= new();
        Voice ??= new();
        Search ??= new();
        Focus ??= new();
        Weather ??= new();
        Clipboard ??= new();
        QuickLaunch ??= new();

        DockLayoutLimits.Sanitize(Dock);
        DockItems.PinnedOrder ??= [];
        DockItems.Folders ??= [];
        Notch.EnabledPanels ??= new NotchSettings().EnabledPanels;
        Notch.ControlCenterLayout ??= new NotchSettings().ControlCenterLayout;
        Displays.Monitors ??= new();
        Search.DisabledSources ??= [];
        QuickLaunch.Items ??= [];
        return this;
    }
}
