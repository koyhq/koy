namespace Koy.Core.Dock;

// Bu dosyadaki enum adları settings.json'a yazılır: bir kez yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Çalışan uygulama göstergesi: nokta (varsayılan) ya da çizgi.</summary>
public enum DockIndicatorStyle
{
    Dot,
    Line,
    Highlight
}

/// <summary>Dock simgesine orta tık: uygulamanın yeni penceresini aç (varsayılan) ya da pencereyi kapat.</summary>
public enum MiddleClickAction
{
    NewWindow,
    Close
}

/// <summary>Aynı uygulamanın pencereleri dock'ta tek simgede toplansın mı.</summary>
public enum DockGroupingMode
{
    /// <summary>Her pencere ayrı simge (bugünkü davranış).</summary>
    Never,

    /// <summary>Aynı süreç adına sahip pencereler her zaman tek simgede.</summary>
    Always,

    /// <summary>Pencere sayısı dock'un yer sayısını aşınca gruplanır, aksi halde her pencere ayrı simge.</summary>
    WhenSpaceRunsOut
}

/// <summary>Dock'ta hangi monitörün pencereleri gösterilsin.</summary>
public enum DockWindowMonitorScope
{
    /// <summary>Bütün monitörlerin pencereleri (varsayılan).</summary>
    AllMonitors,

    /// <summary>Yalnız dock'un bulunduğu monitördeki pencereler.</summary>
    CurrentMonitor
}

/// <summary>Dock simgesine sol tık: pencereyi öne getir (varsayılan) ya da başka bir eylem.</summary>
public enum DockLeftClickAction
{
    BringToFront,
    MinimizeIfActive,
    CycleWindows,
    ShowThumbnails
}

/// <summary>Dock simgelerinin altındaki uygulama adı etiketi: hiç (varsayılan), her zaman ya da yalnız üzerine gelince.</summary>
public enum DockLabelMode
{
    Never,
    Always,
    OnHover
}
