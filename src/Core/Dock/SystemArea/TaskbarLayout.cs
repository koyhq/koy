using Koy.Core.Settings;

namespace Koy.Core.Dock.SystemArea;

/// <summary>Windows 11 "Görev çubuğu hizalaması": Orta (varsayılan) ya da Sol. Windows'ta Sağ yoktur.</summary>
public enum TaskbarAlignment
{
    Center,
    Left
}

/// <summary>Pencere öğeleri (hava durumu) düğmesinin yeri.</summary>
public enum WidgetsPlacement
{
    None,

    /// <summary>Ortada hizada en solda, iki satır (simge, sıcaklık, kısa açıklama).</summary>
    Leading,

    /// <summary>Sol hizada uygulamaların sağında, yalnız simge ve sıcaklık.</summary>
    AfterItems
}

/// <summary>
/// Tam genişlik dock'un bütün düzen kararı (Windows 11 görev çubuğu gibi): sol ve orta bölüm ile sağ uçtaki sistem alanı.
/// Kapsül ve dikey kenar bu düzene girmez (<see cref="IsActive"/> yanlış).
/// </summary>
public readonly record struct TaskbarLayoutDecision(
    bool IsActive,
    TaskbarAlignment Alignment,
    bool ShowStart,
    bool ShowSearch,
    bool ShowTaskView,
    WidgetsPlacement Widgets,
    DockSystemAreaLayout SystemArea)
{
    public bool ShowWidgetsLeading => Widgets == WidgetsPlacement.Leading;

    public bool ShowWidgetsAfterItems => Widgets == WidgetsPlacement.AfterItems;
}

/// <summary>
/// Tam genişlik dock'un Windows 11 görev çubuğu düzeni (saf mantık): bölüm sırası, hizaya göre yerleşim, ölçüler ve
/// Windows'ta bulunmayan Koy özelliklerinin kapatılması. Windows'ta bu düzenin ayarı yoktur; hizalama tek istisnadır
/// (mevcut "simge hizası" ayarı: Sol = Sol, Orta ve Sağ = Orta). Kapsül değişmez.
/// </summary>
public static class TaskbarLayout
{
    /// <summary>Windows 11 uygulama simgesi (DIP).</summary>
    public const double IconSizeDip = 24.0;

    /// <summary>Uygulama yuvası (DIP): simge, kutu ve aralık birlikte. Kutu <see cref="DockFitMath.ItemSizeFor"/>, kalanı aralıktır.</summary>
    public const double SlotDip = 44.0;

    /// <summary>Başlat ve Görev görünümü düğmelerinin genişliği (kenar boşlukları dahil).</summary>
    public const double ButtonWidthDip = 44.0;

    /// <summary>"Ara" kutusunun genişliği (kenar boşlukları dahil), Windows 11'in yaklaşık ölçüsü.</summary>
    public const double SearchWidthDip = 132.0;

    /// <summary>Sol hizada uygulamaların sağındaki pencere öğeleri düğmesinin en çok genişliği (simge ve sıcaklık).</summary>
    public const double WidgetsAfterItemsWidthDip = 88.0;

    public static TaskbarAlignment EffectiveAlignment(DockIconAlignment alignment) =>
        alignment == DockIconAlignment.Left ? TaskbarAlignment.Left : TaskbarAlignment.Center;

    /// <param name="isFullWidth">Dock tam genişlik stilinde.</param>
    /// <param name="isVertical">Dock sol ya da sağ kenarda.</param>
    /// <param name="alignment">Ayardaki simge hizası.</param>
    /// <param name="hasTray">Koy'un tepsi penceresi çalışıyor.</param>
    /// <param name="hasBattery">Makinede pil var.</param>
    public static TaskbarLayoutDecision Resolve(
        bool isFullWidth, bool isVertical, DockIconAlignment alignment, bool hasTray, bool hasBattery)
    {
        if (!isFullWidth || isVertical)
            return default;

        var effective = EffectiveAlignment(alignment);
        return new TaskbarLayoutDecision(
            IsActive: true,
            Alignment: effective,
            ShowStart: true,
            ShowSearch: true,
            ShowTaskView: true,
            Widgets: effective == TaskbarAlignment.Center ? WidgetsPlacement.Leading : WidgetsPlacement.AfterItems,
            SystemArea: DockSystemArea.Resolve(true, false, hasTray, hasBattery));
    }

    /// <summary>
    /// Uygulama şeridinin yanındaki sabit düğmelerin (Başlat, Ara, Görev görünümü ve sol hizada pencere öğeleri) toplam
    /// genişliği: şerit için kalan yer buna göre daralır.
    /// </summary>
    public static double LeadingWidthDip(TaskbarLayoutDecision layout)
    {
        if (!layout.IsActive)
            return 0.0;

        return (layout.ShowStart ? ButtonWidthDip : 0.0)
               + (layout.ShowSearch ? SearchWidthDip : 0.0)
               + (layout.ShowTaskView ? ButtonWidthDip : 0.0)
               + (layout.ShowWidgetsAfterItems ? WidgetsAfterItemsWidthDip : 0.0);
    }

    /// <summary>
    /// Windows 11 görev çubuğunda olmayan ya da farklı davranan Koy ayarlarının tam genişlikte sabitlenmiş hâli: simge 24 px
    /// ve 44 DIP yuva, büyütme yok, Windows gösterge dili (çalışan = kısa gri çizgi, etkin = uzun vurgu çizgisi), etiket yok,
    /// pencere başına gösterge yok, Windows Başlat simgesi, İndirilenler ve Son kullanılanlar yok, hizalama Orta/Sol.
    /// Kullanıcı ayarı değişmez (yeni bir kopya döner); kapsül ayarları yine ayarlardan okunur.
    /// </summary>
    public static DockSettings ApplyWindowsStyle(DockSettings dock) => dock with
    {
        IconSize = IconSizeDip,
        IconSpacingDip = SlotDip - DockFitMath.ItemSizeFor(IconSizeDip),
        HoverMagnify = false,
        IndicatorStyle = DockIndicatorStyle.Dot,
        IndicatorColor = DockColorSource.Accent,
        IndicatorOnePerWindow = false,
        LabelMode = DockLabelMode.Never,
        ShowStartButton = true,
        StartIcon = DockStartIcon.Windows,
        ShowFolders = false,
        ShowRecents = false,
        IconAlignment = EffectiveAlignment(dock.IconAlignment) == TaskbarAlignment.Left ? DockIconAlignment.Left : DockIconAlignment.Center
    };
}
