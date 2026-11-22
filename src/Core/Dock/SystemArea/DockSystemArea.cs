using System.Globalization;
using Koy.Core.Dock.Items;

namespace Koy.Core.Dock.SystemArea;

/// <summary>
/// Tam genişlik dock'un sağ ucundaki sistem alanının (Windows 11 görev çubuğundaki gibi) hangi parçalarının görüneceği.
/// Soldan sağa: taşma oku, görünür tepsi simgeleri, Wi-Fi + ses + pil grubu, saat ve tarih, bildirim zili, masaüstünü göster şeridi.
/// </summary>
public readonly record struct DockSystemAreaLayout(
    bool ShowOverflowArrow,
    bool ShowVisibleTray,
    bool ShowStatusGroup,
    bool ShowBattery,
    bool ShowClock,
    bool ShowBell,
    bool ShowDesktopStrip)
{
    /// <summary>Alan hiç gösterilmiyor (kapsül, dikey kenar): eski sağ bölüm çizilir.</summary>
    public bool IsActive => ShowStatusGroup || ShowClock || ShowBell || ShowDesktopStrip;
}

/// <summary>
/// Sistem alanı kuralları (saf mantık). Windows'ta bu alan hep açıktır; ayarı yoktur. Yalnız tam genişlik ve yatay kenarda
/// vardır (Windows 11'in görev çubuğu da yataydır); kapsül Koy'un kendi düzenini korur.
/// </summary>
public static class DockSystemArea
{
    /// <summary>"Masaüstünü göster" şeridinin genişliği (DIP): Windows'taki gibi ince, tıklanabilir.</summary>
    public const double DesktopStripWidthDip = 6.0;

    /// <param name="isFullWidth">Dock tam genişlik stilinde.</param>
    /// <param name="isVertical">Dock sol ya da sağ kenarda.</param>
    /// <param name="hasTray">Koy'un tepsi penceresi çalışıyor (Windows görev çubuğu gizli): ok ve görünür simgeler ona bağlı.</param>
    /// <param name="hasBattery">Makinede pil var (masaüstünde pil simgesi hiç çıkmaz).</param>
    public static DockSystemAreaLayout Resolve(bool isFullWidth, bool isVertical, bool hasTray, bool hasBattery, bool showDesktopCorner = true)
    {
        if (!isFullWidth || isVertical)
            return default;

        return new DockSystemAreaLayout(
            ShowOverflowArrow: hasTray,
            ShowVisibleTray: hasTray,
            ShowStatusGroup: true,
            ShowBattery: hasBattery,
            ShowClock: true,
            ShowBell: true,
            ShowDesktopStrip: showDesktopCorner);
    }

    /// <summary>
    /// Sistem alanı pili ve saati zaten gösteriyorsa şeritteki ek öğe (Pil, Saat) ikinci kez çizilmez. Kayıtlı düzenden
    /// silinmez: dock kapsüle dönerse yeniden görünür.
    /// </summary>
    public static bool HidesExtra(DockItemKind kind, DockSystemAreaLayout area) => kind switch
    {
        DockItemKind.Battery => area.ShowStatusGroup,
        DockItemKind.Clock => area.ShowClock,
        _ => false
    };
}

/// <summary>Sistem alanındaki saat ve tarih metinleri (Windows kültür biçimi: kısa saat, kısa tarih).</summary>
public static class SystemClockText
{
    /// <summary>Kısa saat; saniyeler açıksa uzun saat ("T"). Windows "Saatte saniyeleri göster".</summary>
    public static string Time(DateTime now, CultureInfo culture, bool showSeconds = false) => now.ToString(showSeconds ? "T" : "t", culture);

    /// <summary>Bir sonraki güncellemeye kalan süre: saniyeler açıksa saniye başına, değilse dakika başına (tek zamanlayıcı).</summary>
    public static TimeSpan DelayToNextTick(DateTime now, bool showSeconds) =>
        showSeconds
            ? TimeSpan.FromMilliseconds(1000 - now.Millisecond) + TimeSpan.FromMilliseconds(20)
            : DockClockTick.DelayToNextMinute(now);

    public static string Date(DateTime now, CultureInfo culture) => now.ToString("d", culture);

    /// <summary>İpucu: uzun tarih ve gün ("Pazar, 22 Kasım 2026").</summary>
    public static string Tooltip(DateTime now, CultureInfo culture) => now.ToString("D", culture);
}
