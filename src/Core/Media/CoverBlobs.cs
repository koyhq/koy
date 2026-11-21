using Koy.Core.Appearance;

namespace Koy.Core.Media;

/// <summary>
/// Müzik sayfasında kapak renginden akan zemin (issue #182): kartın arkasında üç yumuşak leke yavaşça dolaşır.
/// Renkler kapak paletinden gelir; hareket süreye bağlı, kare hızından bağımsız bir döngüdür (çağıran kare hızını
/// sınırlar). Shader efekti yok: lekeler radyal gradyanlı elips, hareket yalnız kayma ve ölçek.
/// </summary>
public static class CoverBlobs
{
    public const int Count = 3;

    /// <summary>Parça değişiminde eski renklerden yenilerine geçiş süresi.</summary>
    public static readonly TimeSpan ColorCrossfade = TimeSpan.FromSeconds(0.6);

    // Her leke için: döngü süresi (sn), en büyük kayma (DIP), ölçek aralığı. Süreler birbirinin katı değil:
    // lekeler aynı anda başa dönmez, desen tekrar ediyormuş gibi görünmez.
    private static readonly (double Period, double Dx, double Dy, double ScaleFrom, double ScaleTo)[] Paths =
    [
        (22, 120, 60, 1.0, 1.25),
        (27, -140, -30, 1.1, 0.9),
        (19, 60, -90, 1.0, 1.2),
    ];

    /// <summary>
    /// Palet renklerinden üç leke rengi. Çok renkli kapakta karışım renkleri sırayla; tek tonlu kapakta açık ton,
    /// kart tonu ve açık tonun renk çarkında kaydırılmış hâli (aynı ailede ama düz olmayan bir zemin).
    /// </summary>
    public static Rgb[] ColorsFrom(CoverPalette palette)
    {
        if (palette.Blend is { Count: > 0 } blend)
            return [blend[0], blend[1 % blend.Count], blend[2 % blend.Count]];

        var light = palette.Spectrum;
        var hsl = ColorMath.ToHsl(light);
        var shifted = ColorMath.FromHsl((hsl.H + 28) % 360, hsl.S, hsl.L);
        return [light, ColorMath.Mix(palette.Card, light, 0.35), shifted];
    }

    /// <summary><paramref name="index"/>. lekenin <paramref name="seconds"/> anındaki kayması ve ölçeği.</summary>
    public static BlobFrame FrameAt(int index, double seconds)
    {
        var path = Paths[index % Paths.Length];
        // 0 → 1 → 0 yumuşak gidip gelme (sinüs): uçlarda yavaşlar, ortada hızlanır.
        var t = (1 - Math.Cos(2 * Math.PI * seconds / path.Period)) / 2;
        return new BlobFrame(path.Dx * t, path.Dy * t, path.ScaleFrom + (path.ScaleTo - path.ScaleFrom) * t);
    }

    /// <summary>İki renk arası geçiş (<paramref name="t"/> 0–1).</summary>
    public static Rgb Blend(Rgb from, Rgb to, double t) => ColorMath.Mix(from, to, Math.Clamp(t, 0, 1));
}

/// <summary>Bir lekenin kayması ve ölçeği.</summary>
public readonly record struct BlobFrame(double OffsetX, double OffsetY, double Scale);
