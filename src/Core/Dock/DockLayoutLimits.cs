using Koy.Core.Settings;

namespace Koy.Core.Dock;

/// <summary>
/// Dock geometri ayarlarının geçerli aralıkları. Ayar dosyası elle düzenlenmiş olabileceği için değerler
/// kullanılmadan önce burada kenetlenir (<see cref="Sanitize"/>, AppSettings normalizasyonundan çağrılır).
/// </summary>
public static class DockLayoutLimits
{
    /// <summary>Uzunluk yüzdesi: çok kısa bir dock okunmaz, en az %10.</summary>
    public const int MinLengthPercent = 10;
    public const int MaxLengthPercent = 100;
    public const int DefaultLengthPercent = 100;

    /// <summary>Ekran kenarından uzaklık (DIP).</summary>
    public const double MinEdgeOffsetDip = 0.0;
    public const double MaxEdgeOffsetDip = 96.0;
    public const double DefaultEdgeOffsetDip = 0.0;

    /// <summary>Dock içi dolgu (DIP), her kenardan.</summary>
    public const double MinInnerPaddingDip = 0.0;
    public const double MaxInnerPaddingDip = 24.0;
    public const double DefaultInnerPaddingDip = 0.0;

    /// <summary>Simgeler arası boşluk (DIP), yuva başına toplam. Varsayılan DockFitMath.SlotMarginDip ile aynıdır.</summary>
    public const double MinIconSpacingDip = 0.0;
    public const double MaxIconSpacingDip = 16.0;
    public const double DefaultIconSpacingDip = 2.0;

    public static int ClampLengthPercent(int value) => Math.Clamp(value, MinLengthPercent, MaxLengthPercent);

    public static double ClampEdgeOffsetDip(double value) => ClampFinite(value, MinEdgeOffsetDip, MaxEdgeOffsetDip, DefaultEdgeOffsetDip);

    public static double ClampInnerPaddingDip(double value) => ClampFinite(value, MinInnerPaddingDip, MaxInnerPaddingDip, DefaultInnerPaddingDip);

    public static double ClampIconSpacingDip(double value) => ClampFinite(value, MinIconSpacingDip, MaxIconSpacingDip, DefaultIconSpacingDip);

    /// <summary>
    /// Dock ayarlarındaki geometri alanlarını aralığa kenetler. NaN ve sonsuz değerler varsayılana döner
    /// (Math.Clamp NaN'ı korur, bu yüzden ayrı ele alınır).
    /// </summary>
    public static void Sanitize(DockSettings dock)
    {
        // Tanımsız enum sayısı (hand-edited "Edge": 7) alt kenara döner.
        if (!Enum.IsDefined(dock.Edge)) dock.Edge = DockEdge.Bottom;
        dock.LengthPercent = ClampLengthPercent(dock.LengthPercent);
        dock.EdgeOffsetDip = ClampEdgeOffsetDip(dock.EdgeOffsetDip);
        dock.InnerPaddingDip = ClampInnerPaddingDip(dock.InnerPaddingDip);
        dock.IconSpacingDip = ClampIconSpacingDip(dock.IconSpacingDip);
    }

    private static double ClampFinite(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
