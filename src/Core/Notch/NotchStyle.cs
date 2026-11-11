using Koy.Core.Display;

namespace Koy.Core.Notch;

/// <summary>Çentiğin hangi yüzey olarak çizildiği (yüzen adada köşe yarıçapı bunlara göre seçilir).</summary>
public enum NotchSurface
{
    /// <summary>Kapalı, Peek ve ses/parlaklık göstergesi: küçük hap.</summary>
    Closed,
    /// <summary>Açık çentik.</summary>
    Expanded,
    /// <summary>Güncelleme ve bildirim kartı.</summary>
    Card
}

/// <summary>Çentik biçimi: ekrana yapışık (kulaklı) ya da yüzen ada (kulaksız kapsül, üstten boşluklu).</summary>
public static class NotchStyle
{
    public const string Pinned = "Yapışık";
    public const string Floating = "Yüzen";

    /// <summary>Yapışık çentiğin üst köşelerindeki içbükey kulak yarıçapı (DIP).</summary>
    public const double PinnedEarRadius = 12;

    /// <summary>Yüzen adanın ekranın üst kenarından uzaklığı (DIP).</summary>
    public const double FloatingTopOffsetDip = 9;

    /// <summary>Açık çentiğin yüzen adadaki köşe yarıçapı.</summary>
    public const double FloatingExpandedRadius = 30;

    /// <summary>Kartın yüzen adadaki köşe yarıçapı.</summary>
    public const double FloatingCardRadius = 26;

    /// <summary>Fare üst kenar bölgesi: ekranın üst kenarından bu kadar piksel (yüzen adada ofsete eklenir).</summary>
    public const int EdgeTriggerPixels = 2;

    public static bool IsFloating(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return v is "yüzen" or "yuzen" or "yüzen ada" or "floating" or "island";
    }

    public static string Normalize(string? value) => IsFloating(value) ? Floating : Pinned;

    /// <summary>Üst köşelerdeki kulak yarıçapı: yapışıkta 12, yüzende 0 (üst köşeler kapsül gibi yuvarlanır).</summary>
    public static double EarRadius(string? style) => IsFloating(style) ? 0 : PinnedEarRadius;

    /// <summary>Pencerenin ekranın üstünden uzaklığı (DIP): yapışıkta 0, yüzende 9.</summary>
    public static double TopOffsetDip(string? style) => IsFloating(style) ? FloatingTopOffsetDip : 0;

    /// <summary>
    /// Köşe yarıçapı. Yapışıkta mevcut değer aynen; yüzen adada kapalı hâllerde yükseklik/2 (kapsül),
    /// açık çentikte 30, kartta 26.
    /// </summary>
    public static double CornerRadius(string? style, NotchSurface surface, double height, double pinnedRadius)
    {
        if (!IsFloating(style))
            return pinnedRadius;

        return surface switch
        {
            NotchSurface.Expanded => FloatingExpandedRadius,
            NotchSurface.Card => FloatingCardRadius,
            _ => height / 2
        };
    }

    /// <summary>
    /// Fare kenar bölgesinin alt sınırı (piksel, ekranın üstünden ölçülür): çentiği açan bölge ekranın üst kenarından
    /// başlar ve yüzen adadaki boşluğu da kapsar.
    /// </summary>
    public static int EdgeTriggerBottomPixels(string? style, double dpiScaleY) =>
        EdgeTriggerPixels + WindowPlacementMath.ToPixels(TopOffsetDip(style), dpiScaleY);
}
