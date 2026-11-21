namespace Koy.Core.Media;

/// <summary>
/// Müzik sayfasındaki animasyonların ortak kare ızgarası. Her görünüm kendi saatini koşturursa pencere her
/// birinin ritminde ayrı sunulur (yazılımla çizimde her sunum bütün katmanlı pencereyi gönderir); hepsi aynı
/// 30 Hz ızgaranın bölenlerine oturursa kareler aynı vuruşta toplanır. Saf hesap: saat App'tedir.
/// </summary>
public static class FrameGrid
{
    /// <summary>Izgaranın temel hızı (vuruş/sn); görünümler bunun böleniyle çalışır (30, 15, 10…).</summary>
    public const int BaseHz = 30;

    /// <summary>Verilen anın (sn) ızgaradaki vuruş numarası.</summary>
    public static long TickAt(double seconds) => (long)Math.Floor(Math.Max(0, seconds) * BaseHz);

    /// <summary>İstenen kare hızına en yakın bölen (en az 1; 30 fps → 1, 15 → 2, 10 → 3).</summary>
    public static int DivisorFor(double fps) =>
        fps <= 0 ? BaseHz : Math.Clamp((int)Math.Round(BaseHz / fps), 1, BaseHz);

    /// <summary>
    /// <paramref name="divisor"/> ile çalışan görünümün çizim zamanı geldi mi: önceki vuruştan bu vuruşa
    /// (sonuncusu dahil) bölenin bir katı geçildiyse. Vuruşlar atlanırsa (yavaş kare) da yalnız bir kez çizer.
    /// </summary>
    public static bool IsDue(long previousTick, long tick, int divisor)
    {
        if (tick <= previousTick)
            return false;

        var d = Math.Max(1, divisor);
        return tick / d > previousTick / d;
    }

    /// <summary>Bölenin ızgara süresi (sn): iki çizim arasındaki en az süre.</summary>
    public static double PeriodSeconds(int divisor) => Math.Max(1, divisor) / (double)BaseHz;
}
