namespace Koy.Core.Voice;

/// <summary>Bir kaydın blok düzeylerinden çıkan sayılar; desibel olarak (tam ölçek = 0 dB).</summary>
/// <param name="NoiseDb">Kaydın kendi gürültü tabanı (en sessiz %10'luk dilim).</param>
/// <param name="SpeechDb">Kaydın yüksek ucu (%90'lık dilim).</param>
/// <param name="DynamicDb">Yüksek uç ile taban arasındaki fark.</param>
/// <param name="VoicedSeconds">Tabanın eşik payı üstüne çıkan blokların toplam süresi.</param>
public sealed record SpeechStats(double NoiseDb, double SpeechDb, double DynamicDb, double VoicedSeconds);

/// <summary>
/// Kayıtta konuşma var mı? Konuşma tanıma modelleri neredeyse sessiz sese boş yanıt vermez, bir cümle uydurur;
/// bu yüzden boş kayıt modele hiç gönderilmez. Mutlak dB değerleri makineden makineye taşınmaz (bir mikrofonun sessiz
/// odası -70 dB, ötekininki -35 dB olabilir), bu yüzden asıl ölçüt göreli: konuşma, <b>kaydın kendi</b> gürültü tabanının
/// belirgin üstüne çıkmalı ve bir sözcük olacak kadar sürmeli. Sabit fan sesi ne kadar yüksek olursa olsun böylece elenir.
/// </summary>
public static class SpeechGate
{
    /// <summary>Konuşmanın tabanın en az bu kadar üstüne çıkması gerekir.</summary>
    public const double DefaultMarginDb = 10;

    /// <summary>Yüksek ucu bunun altında kalan kayıt, taban ne olursa olsun konuşma sayılmaz.</summary>
    public const double DefaultFloorDb = -55;

    /// <summary>Sesli blokların toplamı bundan kısaysa kayıt bir sözcük bile değildir.</summary>
    public const double DefaultMinVoicedSeconds = 0.3;

    private const double SilenceDb = -120;
    private const double Epsilon = 1e-6;

    /// <summary>0..1 aralığındaki RMS değerini desibele çevirir; sıfıra yakın değer -120 dB'de kalır.</summary>
    public static double ToDb(double rms) => rms <= Epsilon ? SilenceDb : 20 * Math.Log10(rms);

    /// <summary>Blok başına RMS düzeylerinden (0..1) karar için gereken sayıları çıkarır.</summary>
    public static SpeechStats Analyse(IReadOnlyList<double> rmsValues, double chunkSeconds, double marginDb = DefaultMarginDb)
    {
        ArgumentNullException.ThrowIfNull(rmsValues);
        if (rmsValues.Count == 0)
            return new SpeechStats(SilenceDb, SilenceDb, 0, 0);

        var ordered = rmsValues.Order().ToArray();
        var noiseDb = ToDb(Percentile(ordered, 0.10));
        var speechDb = ToDb(Percentile(ordered, 0.90));

        var gate = noiseDb + marginDb;
        var voiced = rmsValues.Count(value => ToDb(value) >= gate);

        return new SpeechStats(noiseDb, speechDb, speechDb - noiseDb, voiced * chunkSeconds);
    }

    /// <summary>Kayıt modele gönderilmeye değmeyecek kadar sessizse true.</summary>
    public static bool IsSilent(SpeechStats stats, double floorDb = DefaultFloorDb, double minVoicedSeconds = DefaultMinVoicedSeconds)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return stats.SpeechDb < floorDb || stats.VoicedSeconds < minVoicedSeconds;
    }

    /// <summary>En yakın sıra yöntemi; sıralı dizide p. dilimdeki değer.</summary>
    private static double Percentile(double[] ordered, double p)
    {
        var index = (int)Math.Ceiling(p * ordered.Length - 1e-9) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }
}
