using Koy.Core.Settings;

namespace Koy.Core.Dock.SystemArea;

/// <summary>Windows 11 "Görev çubuğu hizalaması": Orta (varsayılan) ya da Sol. Windows'ta Sağ yoktur.</summary>
public enum TaskbarAlignment
{
    Center,
    Left
}

/// <summary>Windows 11 "Arama" ayarı: Gizle, Yalnız arama simgesi, Arama simgesi ve etiketi, Arama kutusu.</summary>
public enum TaskbarSearchMode
{
    Hidden,
    IconOnly,
    IconAndLabel,
    Box
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

/// <summary>Windows 11 görev çubuğu öğeleri ayarlarından düzeni etkileyenler (tam genişlikte geçerli). Varsayılanlar Windows'unkiler.</summary>
public readonly record struct TaskbarOptions
{
    public TaskbarOptions()
    {
        Search = TaskbarSearchMode.Box;
        ShowTaskView = true;
        ShowWidgets = true;
        ShowDesktopCorner = true;
    }

    public TaskbarSearchMode Search { get; init; }
    public bool ShowTaskView { get; init; }
    public bool ShowWidgets { get; init; }
    public bool ShowDesktopCorner { get; init; }

    public static TaskbarOptions From(DockSettings dock) => new()
    {
        Search = Enum.IsDefined(dock.TaskbarSearch) ? dock.TaskbarSearch : TaskbarSearchMode.Box,
        ShowTaskView = dock.TaskbarShowTaskView,
        ShowWidgets = dock.TaskbarShowWidgets,
        ShowDesktopCorner = dock.TaskbarShowDesktopCorner
    };
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
    TaskbarSearchMode Search,
    bool ShowTaskView,
    WidgetsPlacement Widgets,
    DockSystemAreaLayout SystemArea)
{
    public bool ShowWidgetsLeading => Widgets == WidgetsPlacement.Leading;

    public bool ShowWidgetsAfterItems => Widgets == WidgetsPlacement.AfterItems;

    public bool ShowSearchBox => ShowSearch && Search == TaskbarSearchMode.Box;

    public bool ShowSearchLabel => ShowSearch && Search == TaskbarSearchMode.IconAndLabel;

    public bool ShowSearchIconOnly => ShowSearch && Search == TaskbarSearchMode.IconOnly;
}

/// <summary>
/// Tam genişlik dock'un Windows 11 görev çubuğu düzeni (saf mantık): bölüm sırası, hizaya göre yerleşim, ölçüler ve
/// Windows'ta bulunmayan Koy özelliklerinin kapatılması. Windows'ta düzenin ayarları Windows'un "Görev çubuğu davranışları/öğeleri"
/// ayarlarıdır (<see cref="TaskbarOptions"/>); hizalama mevcut "simge hizası" ayarından gelir (Sol = Sol, Orta ve Sağ = Orta).
/// Kapsül değişmez.
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

    /// <summary>"Arama simgesi ve etiketi" kipinin genişliği.</summary>
    public const double SearchLabelWidthDip = 84.0;

    /// <summary>Sol hizada uygulamaların sağındaki pencere öğeleri düğmesinin en çok genişliği (simge ve sıcaklık).</summary>
    public const double WidgetsAfterItemsWidthDip = 88.0;

    public static TaskbarAlignment EffectiveAlignment(DockIconAlignment alignment) =>
        alignment == DockIconAlignment.Left ? TaskbarAlignment.Left : TaskbarAlignment.Center;

    /// <param name="isFullWidth">Dock tam genişlik stilinde.</param>
    /// <param name="isVertical">Dock sol ya da sağ kenarda.</param>
    /// <param name="alignment">Ayardaki simge hizası.</param>
    /// <param name="hasTray">Koy'un tepsi penceresi çalışıyor.</param>
    /// <param name="hasBattery">Makinede pil var.</param>
    /// <param name="options">Windows görev çubuğu öğeleri ayarları; verilmezse Windows varsayılanları.</param>
    public static TaskbarLayoutDecision Resolve(
        bool isFullWidth, bool isVertical, DockIconAlignment alignment, bool hasTray, bool hasBattery, TaskbarOptions options = default)
    {
        if (!isFullWidth || isVertical)
            return default;

        // default(TaskbarOptions) sıfır değerlerdir (Windows varsayılanları değil): parametresiz çağrıda varsayılanlar kullanılır.
        if (options == default)
            options = new TaskbarOptions();

        var effective = EffectiveAlignment(alignment);
        return new TaskbarLayoutDecision(
            IsActive: true,
            Alignment: effective,
            ShowStart: true,
            ShowSearch: options.Search != TaskbarSearchMode.Hidden,
            Search: options.Search,
            ShowTaskView: options.ShowTaskView,
            Widgets: !options.ShowWidgets ? WidgetsPlacement.None
                : effective == TaskbarAlignment.Center ? WidgetsPlacement.Leading : WidgetsPlacement.AfterItems,
            SystemArea: DockSystemArea.Resolve(true, false, hasTray, hasBattery, options.ShowDesktopCorner));
    }

    /// <summary>"Ara" düğmesinin genişliği: kutu, simge ve etiket ya da yalnız simge (kenar boşlukları dahil).</summary>
    public static double SearchWidthFor(TaskbarSearchMode mode) => mode switch
    {
        TaskbarSearchMode.Box => SearchWidthDip,
        TaskbarSearchMode.IconAndLabel => SearchLabelWidthDip,
        TaskbarSearchMode.IconOnly => ButtonWidthDip,
        _ => 0.0
    };

    /// <summary>
    /// Uygulama şeridinin yanındaki sabit düğmelerin (Başlat, Ara, Görev görünümü ve sol hizada pencere öğeleri) toplam
    /// genişliği: şerit için kalan yer buna göre daralır.
    /// </summary>
    public static double LeadingWidthDip(TaskbarLayoutDecision layout)
    {
        if (!layout.IsActive)
            return 0.0;

        return (layout.ShowStart ? ButtonWidthDip : 0.0)
               + SearchWidthFor(layout.ShowSearch ? layout.Search : TaskbarSearchMode.Hidden)
               + (layout.ShowTaskView ? ButtonWidthDip : 0.0)
               + (layout.ShowWidgetsAfterItems ? WidgetsAfterItemsWidthDip : 0.0);
    }

    /// <summary>
    /// Windows 11 görev çubuğunda olmayan ya da farklı davranan Koy ayarlarının tam genişlikte sabitlenmiş hâli: simge 24 px
    /// ve 44 DIP yuva, büyütme yok, Windows gösterge dili (çalışan = kısa gri çizgi, etkin = uzun vurgu çizgisi), etiket yok,
    /// pencere başına gösterge yok, Windows Başlat simgesi, İndirilenler ve Son kullanılanlar yok, düğmeler her zaman birleşik,
    /// sol tık Windows davranışı, hizalama Orta/Sol. Bunlar <see cref="DockSettingsMatrix"/>'te "yalnız kapsül" ayarlardır
    /// (ayarlar sayfasında tam genişlikte görünmez). Kullanıcı ayarı değişmez (yeni bir kopya döner).
    /// </summary>
    public static DockSettings ApplyWindowsStyle(DockSettings dock) => dock with
    {
        // Windows görev çubuğunun uzunluk ayarı yok: her zaman ekran genişliği.
        LengthPercent = DockLayoutLimits.MaxLengthPercent,
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
        // "Görev çubuğu düğmelerini birleştir ve etiketleri gizle": yalnız "Her zaman" uygulanır, seçenek gösterilmez.
        GroupingMode = DockGroupingMode.Always,
        LeftClickAction = DockLeftClickAction.MinimizeIfActive,
        IconAlignment = EffectiveAlignment(dock.IconAlignment) == TaskbarAlignment.Left ? DockIconAlignment.Left : DockIconAlignment.Center
    };
}
