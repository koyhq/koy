namespace Koy.Core.Notch;

/// <summary>
/// Müzik sayfasındaki ayna dalga (issue #182): çubuklar satırın bütün genişliğini doldurur, ortadan iki yana
/// simetriktir. Ortada bas, kenarlarda tiz bantlar durur; kenarlara doğru hafifçe kısalır ki dalga bir zarf
/// gibi dursun. Çubuk dikeyde de ortadan iki yöne büyür (çizim App'te).
/// </summary>
public static class MirrorWave
{
    public const int DefaultBarCount = 28;

    // Kenardaki çubuk ortadakinin bu oranına iner (tasarımda kenar çubukları daha kısa).
    private const double EdgeTaper = 0.35;

    /// <summary>Çubuğun ortadan uzaklığı: ortada 0, kenarda 1.</summary>
    public static double DistanceFromCenter(int bar, int barCount)
    {
        if (barCount <= 1)
            return 0;

        var half = (barCount - 1) / 2.0;
        return Math.Abs(bar - half) / half;
    }

    /// <summary>
    /// <paramref name="bar"/>. çubuğun düzeyi (0–1). Uzaklık bantlara doğrusal eşlenir: ortadaki çubuk ilk (bas)
    /// banttan, kenar çubukları son (tiz) banttan okur; aradakiler iki komşu bant arasında karışır.
    /// </summary>
    public static double LevelFor(IReadOnlyList<float> bands, int bar, int barCount)
    {
        if (bands.Count == 0 || barCount <= 0 || bar < 0 || bar >= barCount)
            return 0;

        var d = DistanceFromCenter(bar, barCount);
        var position = d * (bands.Count - 1);
        var low = (int)position;
        var high = Math.Min(low + 1, bands.Count - 1);
        var t = position - low;
        var level = bands[low] + (bands[high] - bands[low]) * t;
        level = Math.Clamp(level, 0, 1);
        return level * (1 - EdgeTaper * d);
    }
}
