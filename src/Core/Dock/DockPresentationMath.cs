namespace Koy.Core.Dock;

/// <summary>
/// Dock göstergesi, rozet, gizlenme/çıkma zamanlaması ve tetikleyici kararları (saf mantık, çizim yok).
/// App katmanı bu sonuçları XAML'e bağlar; kural burada test edilir.
/// </summary>
public static class DockPresentationMath
{
    /// <summary>Gecikmeler için alt ve üst sınır (ms). Bunun dışındaki dosya değeri kıstırılır.</summary>
    public const int MinDelayMs = 0;
    public const int MaxDelayMs = 2000;

    /// <summary>Tek simgede en çok kaç gösterge noktası çizilir (pencere başına kipte).</summary>
    public const int MaxIndicatorDots = 3;

    /// <summary>Rozetteki en büyük sayı; bunun üstü "9+" olarak gösterilir.</summary>
    public const int MaxBadgeCount = 9;

    public static int ClampDelay(int ms) => Math.Clamp(ms, MinDelayMs, MaxDelayMs);

    /// <summary>
    /// Bir simgede kaç gösterge çizilir. Yalnız nokta kipinde ve "pencere başına" açıkken her pencere için bir nokta
    /// (en çok <see cref="MaxIndicatorDots"/>); diğer durumlarda pencere varsa tek gösterge.
    /// </summary>
    public static int IndicatorCount(int windowCount, DockIndicatorStyle style, bool onePerWindow)
    {
        if (windowCount <= 0)
            return 0;

        if (style != DockIndicatorStyle.Dot || !onePerWindow)
            return 1;

        return Math.Min(windowCount, MaxIndicatorDots);
    }

    /// <summary>Rozet metni. Nokta kipinde ya da sayı sıfırsa boş döner.</summary>
    public static string BadgeText(int count, DockBadgeStyle style)
    {
        if (count <= 0 || style == DockBadgeStyle.DotOnly)
            return "";

        return count > MaxBadgeCount
            ? $"{MaxBadgeCount}+"
            : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Rozet görünür mü: sayı kipinde sayı varsa, nokta kipinde en az bir bildirim varsa.</summary>
    public static bool ShowsBadge(int count, DockBadgeStyle style) =>
        style == DockBadgeStyle.DotOnly ? count > 0 : BadgeText(count, style).Length > 0;

    /// <summary>
    /// Dock'un çıkıp çıkmayacağı. <paramref name="edgeHeldFor"/> fare alt kenarda ne kadar süredir kesintisiz durduğu.
    /// Fare tuşu basılıyken (sürükleme, seçim) hiçbir tetikleyici dock'u açmaz.
    /// </summary>
    public static bool ShouldReveal(
        DockRevealTrigger trigger,
        bool atEdge,
        bool buttonPressed,
        bool shortcutActive,
        TimeSpan edgeHeldFor,
        int revealDelayMs)
    {
        if (buttonPressed)
            return false;

        return trigger switch
        {
            DockRevealTrigger.EdgeTouch => atEdge,
            DockRevealTrigger.EdgeHold => atEdge && edgeHeldFor >= TimeSpan.FromMilliseconds(ClampDelay(revealDelayMs)),
            DockRevealTrigger.ShortcutOnly => shortcutActive,
            _ => false
        };
    }

    /// <summary>
    /// Gizli dock'un altındaki ince çizgi. Ayar açıksa, dock gizliyse ve tam ekran bir uygulama yoksa görünür;
    /// tam ekranda çizgi her zaman gizlenir (oyun ve video kenarda bir şerit görmesin).
    /// </summary>
    public static bool ShowsHiddenEdgeLine(bool lineEnabled, bool dockHidden, bool fullscreenActive) =>
        lineEnabled && dockHidden && !fullscreenActive;

    /// <summary>Özel renk metni geçerli mi: "#RRGGBB" biçimi (büyük/küçük harf fark etmez).</summary>
    public static bool IsValidHexColor(string? text)
    {
        if (text is null || text.Length != 7 || text[0] != '#')
            return false;

        for (var i = 1; i < text.Length; i++)
        {
            if (!Uri.IsHexDigit(text[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Uygulanacak renk kaynağı. Özel renk seçili ama metin geçersizse vurgu rengine düşer (bozuk dosya dock'u boyamaz).
    /// </summary>
    public static DockColorSource EffectiveColorSource(DockColorSource source, string? customHex) =>
        source == DockColorSource.Custom && !IsValidHexColor(customHex) ? DockColorSource.Accent : source;
}
