namespace Koy.Core.Dock;

/// <summary>
/// Çok uygulama açıkken dock'un ekrana sığması: simgeler tercih edilen boyuttan en küçük boyuta kadar küçülür,
/// o zaman da sığmıyorsa küçülme durur (taşan öğeler kırpılır). Saf hesap; dock ölçüleri DockViewModel'de uygulanır.
/// </summary>
public static class DockFitMath
{
    /// <summary>Simgenin inebileceği en küçük boyut (DIP).</summary>
    public const double MinIconSizeDip = 24.0;

    /// <summary>Öğe kutusu (ItemSize) simgeden bu kadar büyüktür.</summary>
    public const double ItemChromeDip = 10.0;

    /// <summary>Her öğenin sağında ve solunda 1 DIP boşluk (Margin="1,0").</summary>
    public const double SlotMarginDip = 2.0;

    public static double ItemSizeFor(double iconSize) => iconSize + ItemChromeDip;

    /// <summary>Bir yuvanın kapladığı yatay genişlik.</summary>
    public static double SlotWidth(double iconSize) => iconSize + ItemChromeDip + SlotMarginDip;

    /// <summary>
    /// Verilen genişliğe sığan simge boyutu (tam sayı DIP). Tercih edilenden büyümez, en küçük boyutun altına inmez;
    /// tercih edilen zaten en küçükten küçükse olduğu gibi kalır.
    /// </summary>
    /// <param name="preferredIconSize">Ayardaki simge boyutu.</param>
    /// <param name="slotCount">Simge boyutuyla ölçeklenen yuva sayısı.</param>
    /// <param name="fixedWidth">Boyuta bağlı olmayan genişlik (dolgu, ayırıcı, sabit düğmeler).</param>
    /// <param name="availableWidth">Kullanılabilir toplam genişlik.</param>
    public static double FitIconSize(double preferredIconSize, int slotCount, double fixedWidth, double availableWidth)
    {
        if (preferredIconSize <= MinIconSizeDip || slotCount <= 0 || !double.IsFinite(availableWidth) || availableWidth <= 0)
            return preferredIconSize;

        var perSlot = (availableWidth - fixedWidth) / slotCount;
        var fit = Math.Floor(perSlot - ItemChromeDip - SlotMarginDip);
        return Math.Clamp(fit, MinIconSizeDip, preferredIconSize);
    }

    /// <summary>
    /// Fare üstünde öğenin en çok büyüyeceği ölçek: çubuk yüksekliğine (1 px üst çizgi + 1 px pay) sığacak kadar,
    /// en fazla <paramref name="maxScale"/>. 48 px çubukta 38 px'lik kutu ~1,21 kata çıkar, 60 px kapsülde 1,30.
    /// </summary>
    public static double HoverPeakScale(double barHeight, double itemSize, double maxScale = 1.30)
    {
        if (itemSize <= 0 || barHeight <= 0)
            return 1.0;

        return Math.Clamp((barHeight - 2.0) / itemSize, 1.0, maxScale);
    }

    /// <summary>Uygulama şeridine kalan en çok genişlik; 0'ın altına inmez.</summary>
    public static double StripMaxWidth(double availableWidth, double otherWidth)
        => Math.Max(0.0, availableWidth - otherWidth);
}
