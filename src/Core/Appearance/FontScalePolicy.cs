namespace Koy.Core.Appearance;

/// <summary>
/// Yazı tipi ve Koy ölçeği kararları (saf mantık, WPF yok). Değerler ayar dosyasından gelir; burada sınırlanır ki
/// bozuk ya da elle düzenlenmiş bir ayar notch tuvalini taşıran bir değer üretmesin (AGENTS #13). Yüklü yazı tipi
/// listesi çağıran tarafından verilir; yazı tipini gerçekten yükleme ve WPF'e bağlama Core dışındadır.
/// </summary>
public static class FontScalePolicy
{
    public const int MinUiScale = 80;
    public const int MaxUiScale = 150;
    public const int DefaultUiScale = 100;

    public const double MinBaseFontSize = 10;
    public const double MaxBaseFontSize = 18;
    public const double DefaultBaseFontSize = 13;

    public const int MinFontWeight = 100;
    public const int MaxFontWeight = 900;
    public const int DefaultFontWeight = 400;

    /// <summary>Ayarda boş ya da yüklü olmayan yazı tipi adı için kullanılan Koy varsayılanı.</summary>
    public const string DefaultFontFamily = "Segoe UI";

    /// <summary>Koy ölçeği yüzde olarak 80-150 aralığına sıkıştırılır. Sayı değilse varsayılana döner.</summary>
    public static int ClampUiScale(int percent) => Math.Clamp(percent, MinUiScale, MaxUiScale);

    /// <summary>Taban yazı boyu (DIP) sınırlanır. NaN ya da sonsuz değer varsayılana döner.</summary>
    public static double ClampBaseFontSize(double size) =>
        double.IsFinite(size) ? Math.Clamp(size, MinBaseFontSize, MaxBaseFontSize) : DefaultBaseFontSize;

    /// <summary>Kalınlık 100-900 aralığında ve yüzün 100'lük adımlarına yuvarlanır (WPF yalnız bu değerlerle anlamlı).</summary>
    public static int ClampFontWeight(int weight)
    {
        var clamped = Math.Clamp(weight, MinFontWeight, MaxFontWeight);
        return (int)(Math.Round(clamped / 100.0, MidpointRounding.AwayFromZero) * 100);
    }

    /// <summary>
    /// İstenen yazı tipini yüklü listede arar (büyük küçük harf duyarsız) ve listedeki yazımını döndürür.
    /// Boş ya da yüklü olmayan ad (yazı tipi kaldırılmış) <paramref name="fallback"/> değerine düşer.
    /// </summary>
    public static string ResolveFontFamily(string? requested, IEnumerable<string> installed, string fallback = DefaultFontFamily)
    {
        var name = requested?.Trim();
        if (string.IsNullOrEmpty(name))
            return fallback;

        foreach (var font in installed)
        {
            if (string.Equals(font, name, StringComparison.OrdinalIgnoreCase))
                return font;
        }

        return fallback;
    }

    /// <summary>
    /// Saat ve sayı yazı tipi. Boşsa ya da yüklü değilse arayüz yazı tipine düşer (Koy varsayılanına değil), böylece
    /// kaldırılmış bir saat yazı tipi yüzünden iki yazı tipi birbirinden kopmaz.
    /// </summary>
    public static string ResolveClockFontFamily(string? requested, string resolvedUiFamily, IEnumerable<string> installed) =>
        string.IsNullOrWhiteSpace(requested)
            ? resolvedUiFamily
            : ResolveFontFamily(requested, installed, resolvedUiFamily);

    /// <summary>
    /// Koy ölçeğini bir DIP değerine uygular (yazı boyu, ikon, boşluk). Ölçek sınırlandıktan sonra çarpılır;
    /// monitör DPI'si WPF tarafından ayrıca uygulandığı için burada yalnız Koy'un kendi yüzdesi vardır.
    /// </summary>
    public static double ScaleDip(double dip, int uiScalePercent) =>
        dip * ClampUiScale(uiScalePercent) / 100.0;
}
