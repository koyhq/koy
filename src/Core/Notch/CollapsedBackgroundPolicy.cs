using Koy.Core.Appearance;

namespace Koy.Core.Notch;

/// <summary>
/// Kapalı çentiğin statik arka plan renklerini çözer (Core saf hesap, WPF yok).
/// Ambiyans kipinde video ya da zamanlayıcı çalıştırılmaz; seçili önayarın renkleri okunabilir
/// koyuluğa çekilerek statik bir renk listesi üretilir.
/// </summary>
public static class CollapsedBackgroundPolicy
{
    private static readonly Rgb DarkFallback = new(0x0A, 0x0A, 0x0B);
    private static readonly Rgb TitleWhite = new(0xF4, 0xF4, 0xF5);
    public const double MinTextContrast = 7.0;
    private const double BaseDarkMix = 0.25;

    /// <summary>
    /// Verilen önayar kimliğinin renk duraklarını, üstündeki beyaz saat/metin en az 7:1 kontrast
    /// alacak biçimde koyulaştırılmış sRGB listesi olarak döner. En az 1 renk garantilenir.
    /// </summary>
    public static IReadOnlyList<Rgb> ResolveAmbientStops(string? presetId)
    {
        var preset = LiveBackgroundPresets.Find(presetId);
        var stops = new List<Rgb>(preset.Colors.Count);
        foreach (var hex in preset.Colors)
        {
            if (Rgb.TryParse(hex, out var parsed))
                stops.Add(EnsureReadable(parsed));
        }

        if (stops.Count == 0)
            stops.Add(DarkFallback);

        return stops;
    }

    /// <summary>
    /// Rengi kapalı çentik zeminine uygun koyuluğa çeker; parlak önayar duraklarında (ör. snow, nightsky, lava)
    /// beyaz saat ve simgeler okunaksız kalmasın diye kontrast en az <see cref="MinTextContrast"/> olana
    /// dek koyu tabana karıştırır.
    /// </summary>
    public static Rgb EnsureReadable(Rgb color)
    {
        var candidate = ColorMath.Mix(color, DarkFallback, BaseDarkMix);
        if (ColorMath.Contrast(TitleWhite, candidate) >= MinTextContrast)
            return candidate;

        // İkili arama ile gereken en küçük koyulaştırma oranını bulur; renk tonu olabildiğince korunur.
        var lo = BaseDarkMix;
        var hi = 1.0;
        for (var i = 0; i < 10; i++)
        {
            var mid = (lo + hi) * 0.5;
            var mixed = ColorMath.Mix(color, DarkFallback, mid);
            if (ColorMath.Contrast(TitleWhite, mixed) >= MinTextContrast)
                hi = mid;
            else
                lo = mid;
        }

        return ColorMath.Mix(color, DarkFallback, hi);
    }
}
