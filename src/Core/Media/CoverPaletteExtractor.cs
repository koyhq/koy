using Koy.Core.Appearance;

namespace Koy.Core.Media;

/// <summary>Albüm kapağından türetilen müzik kartı renkleri. Spectrum App tarafında alfa 0.75 ile kullanılır.</summary>
public sealed record CoverPalette(Rgb Card, Rgb SubText, Rgb Spectrum);

/// <summary>
/// Küçük bir kapak resminden (BGRA, örn. 32 px) vurgu paleti çıkarır. Tek geçiş, yığın üstü kovalar;
/// WPF tipi yok. Gri/monokrom kapakta null döner (varsayılan tema kullanılır).
/// </summary>
public static class CoverPaletteExtractor
{
    private const int HueBuckets = 24;
    private const double MinSaturation = 0.20;
    private const double MinLightness = 0.08;
    private const double MaxLightness = 0.95;
    private const double MinCandidateRatio = 0.10;
    private const double CardLightness = 0.14;
    private const double SubTextSaturation = 0.60;
    private const double SubTextLightness = 0.80;
    private const double MinTitleContrast = 7.0;
    private static readonly Rgb Title = new(0xF4, 0xF4, 0xF5);

    public static CoverPalette? Extract(ReadOnlySpan<byte> bgra, int width, int height, int stride)
    {
        if (width <= 0 || height <= 0 || stride < width * 4)
            return null;
        if (bgra.Length < (long)(height - 1) * stride + width * 4)
            return null;

        Span<double> weight = stackalloc double[HueBuckets];
        Span<double> hueSum = stackalloc double[HueBuckets];
        Span<double> satSum = stackalloc double[HueBuckets];
        var total = 0;
        var candidates = 0;

        for (var y = 0; y < height; y++)
        {
            var row = bgra.Slice(y * stride, width * 4);
            for (var x = 0; x < row.Length; x += 4)
            {
                if (row[x + 3] < 16)
                    continue; // saydam piksel kapağın parçası değil

                total++;
                var hsl = ColorMath.ToHsl(row[x + 2], row[x + 1], row[x]);
                if (hsl.S < MinSaturation || hsl.L < MinLightness || hsl.L > MaxLightness)
                    continue;

                candidates++;
                var bucket = Math.Min((int)(hsl.H / (360.0 / HueBuckets)), HueBuckets - 1);
                weight[bucket] += hsl.S;
                hueSum[bucket] += hsl.H * hsl.S;
                satSum[bucket] += hsl.S * hsl.S;
            }
        }

        if (total == 0 || candidates < total * MinCandidateRatio)
            return null;

        var best = 0;
        for (var i = 1; i < HueBuckets; i++)
        {
            if (weight[i] > weight[best])
                best = i;
        }

        if (weight[best] <= 0)
            return null;

        var hue = hueSum[best] / weight[best];
        var cardSat = Math.Clamp(satSum[best] / weight[best], 0.35, 0.55);

        var cardL = CardLightness;
        var card = ColorMath.FromHsl(hue, cardSat, cardL);
        while (cardL > 0 && ColorMath.Contrast(Title, card) < MinTitleContrast)
        {
            cardL = Math.Max(0, cardL - 0.01);
            card = ColorMath.FromHsl(hue, cardSat, cardL);
        }

        var sub = ColorMath.FromHsl(hue, SubTextSaturation, SubTextLightness);
        return new CoverPalette(card, sub, sub);
    }
}
