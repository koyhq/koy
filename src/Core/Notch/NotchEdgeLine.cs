using Koy.Core.Display;
using Koy.Core.Shell;

namespace Koy.Core.Notch;

/// <summary>Üst kenar çizgisinin rengi.</summary>
public enum NotchEdgeLineColor
{
    /// <summary>Kapalı çentikle aynı zemin (KoyNotchSurface, varsayılan siyah): çentik çizginin devamı gibi görünür.</summary>
    NotchSurface,
    /// <summary>Seçili vurgu rengi (KoyAccent).</summary>
    Accent
}

/// <summary>
/// Çentiğin iki yanından ekranın üst kenarı boyunca uzanan çizgi. Çizgi ayrı, tıklama geçiren ince bir penceredir ve
/// çentiğin hemen altında durur: çentik kendi genişliği kadarını örter, gizlenince çizginin içine çekilir.
/// </summary>
public static class NotchEdgeLine
{
    /// <summary>En ince çizgi (DIP).</summary>
    public const int MinThickness = 1;

    /// <summary>En kalın çizgi (DIP). Kapalı çentik 34 DIP; çizgi onun yarısından az kalır ki çentik çizgiden ayrı okunsun.</summary>
    public const int MaxThickness = 20;

    /// <summary>Varsayılan kalınlık (DIP).</summary>
    public const int DefaultThickness = 2;

    /// <summary>Ayar dosyasından ya da profilden gelen kalınlığı geçerli aralığa çeker.</summary>
    public static int ClampThickness(int thickness) => Math.Clamp(thickness, MinThickness, MaxThickness);

    /// <summary>Rengin okunacağı tema kaynağı.</summary>
    public static string ResourceKey(NotchEdgeLineColor color) =>
        color == NotchEdgeLineColor.Accent ? "KoyAccent" : "KoyNotchSurface";

    /// <summary>
    /// Çizgi penceresinin piksel sınırı: monitörün tam genişliği, en az 1 px. <paramref name="topOffsetDip"/> çentiğin üst boşluğudur
    /// (<see cref="NotchStyle.TopOffsetDip"/>): Yüzen kipte çentik üst kenardan 9 DIP aşağıda durur, çizgi de onunla birlikte
    /// aşağı iner (yoksa çentik ile çizgi arasında boşluk kalır ve çizgi çentikten kopuk görünür). Klasik kipte 0: kenara yapışık.
    /// </summary>
    public static WindowRect Bounds(DisplayMonitor monitor, int thicknessDip, double topOffsetDip = 0)
    {
        var height = Math.Max(1, (int)Math.Round(ClampThickness(thicknessDip) * monitor.DpiScaleY));
        var top = monitor.Bounds.Top + WindowPlacementMath.ToPixels(Math.Max(0.0, topOffsetDip), monitor.DpiScaleY);
        return new WindowRect(monitor.Bounds.Left, top, monitor.Bounds.Right, top + height);
    }

    /// <summary>Çizgi görünsün mü: tam ekranda çentik gizleniyorsa çizgi de gizlenir.</summary>
    public static bool IsVisible(bool isFullscreen, bool hideInFullscreen) => !(isFullscreen && hideInFullscreen);
}
