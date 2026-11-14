using Koy.Core.Appearance;

namespace Koy.Core.Media;

/// <summary>
/// Albüm kapağından türetilen müzik kartı renkleri. Spectrum App tarafında alfa 0.75 ile kullanılır.
/// <paramref name="Blend"/> doluysa kapak çok renklidir (ör. kick.com yayınında kapak yerine gelen Chrome logosu):
/// kart tek bir tona boyanmaz, bu renklerin köşelerden yayılan yumuşak karışımı çizilir; <paramref name="Card"/> o
/// zaman karışımın altındaki nötr zemindir.
/// </summary>
public sealed record CoverPalette(Rgb Card, Rgb SubText, Rgb Spectrum, IReadOnlyList<Rgb>? Blend = null);

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

        var blend = BlendColors(weight, hueSum, satSum);
        if (blend is not null)
            return new CoverPalette(BlendBase, BlendSubText, BlendSubText, blend);

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

    // Çok renkli kapak: en az üç ayrı ton (aralarında en az 30°), her biri renkli piksellerin %15'inden fazlası.
    // Tek bir baskın ton seçmek burada yanıltıcı (Chrome logosunda sarı kazanıp hardal kart veriyordu).
    private const double BlendMinShare = 0.15;
    private const int BlendMinSeparation = 2; // kova = 15°, 2 kova = 30° (Chrome kırmızısı ile sarısı ~40° ayrık)
    private const int BlendMinColors = 3;
    private const int BlendMaxColors = 4;
    private static readonly Rgb BlendBase = new(0x14, 0x14, 0x16);
    private static readonly Rgb BlendSubText = new(0xC8, 0xC8, 0xCE);

    private static List<Rgb>? BlendColors(ReadOnlySpan<double> weight, ReadOnlySpan<double> hueSum, ReadOnlySpan<double> satSum)
    {
        double total = 0;
        foreach (var w in weight)
            total += w;
        if (total <= 0)
            return null;

        var order = new List<int>();
        for (var i = 0; i < weight.Length; i++)
            order.Add(i);
        var weights = weight.ToArray();
        order.Sort((a, b) => weights[b].CompareTo(weights[a]));

        var picked = new List<int>();
        foreach (var i in order)
        {
            if (weight[i] / total < BlendMinShare)
                break;

            var far = picked.TrueForAll(j =>
            {
                var d = Math.Abs(i - j);
                return Math.Min(d, HueBuckets - d) >= BlendMinSeparation;
            });
            if (far)
                picked.Add(i);
            if (picked.Count == BlendMaxColors)
                break;
        }

        if (picked.Count < BlendMinColors)
            return null;

        // Karışımda renkler canlı kalsın ama yazıyı boğmasın: orta açıklık, yüksek doygunluk (App alfa ~0,35 ile çizer).
        var colors = new List<Rgb>(picked.Count);
        foreach (var i in picked)
            colors.Add(ColorMath.FromHsl(hueSum[i] / weight[i], Math.Clamp(satSum[i] / weight[i], 0.55, 0.85), 0.5));
        return colors;
    }
}
