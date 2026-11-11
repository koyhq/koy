namespace Koy.Core.Appearance;

/// <summary>
/// Animasyon hızı ve azaltılmış hareket kararları. Tek bir çarpan üretir: süreler bununla çarpılır,
/// 0 ise geçişler anında yapılır (Corvus'taki "motion scale" mantığı).
/// </summary>
public static class MotionPolicy
{
    /// <summary>Hız ayarının kendi çarpanı (azaltılmış hareket hesaba katılmadan).</summary>
    public static double SpeedFactor(AnimationSpeed value) =>
        value switch
        {
            AnimationSpeed.Calm => 1.5,
            AnimationSpeed.Fast => 0.65,
            AnimationSpeed.Off => 0.0,
            _ => 1.0
        };

    /// <summary>Son çarpan: 0 ise animasyon yok.</summary>
    /// <param name="speed">Animasyon ayarı (Sakin / Normal / Hızlı / Kapalı).</param>
    /// <param name="respectReduceMotion">"Windows'un animasyonları azalt ayarına uy" açık mı.</param>
    /// <param name="systemAnimationsEnabled">Windows istemci alanı animasyonları açık mı (SystemParameters.ClientAreaAnimation).</param>
    /// <param name="batterySaverReduceMotion">Pil tasarrufu politikası şu an hareketi azaltmayı istiyor mu (BatterySaverPolicy).</param>
    public static double Scale(AnimationSpeed speed, bool respectReduceMotion, bool systemAnimationsEnabled, bool batterySaverReduceMotion)
    {
        if (batterySaverReduceMotion)
            return 0.0;
        if (respectReduceMotion && !systemAnimationsEnabled)
            return 0.0;
        return SpeedFactor(speed);
    }

    /// <summary>Taban süreyi (ms) çarpanla ölçekler; 0 çarpan 0 ms verir.</summary>
    public static int ScaledMilliseconds(double baseMilliseconds, double scale) =>
        scale <= 0 ? 0 : (int)Math.Round(baseMilliseconds * scale);

    /// <summary>Animasyon başına açma/kapama ile ayrılan animasyon türleri (issue #79).</summary>
    public enum MotionKind
    {
        NotchOpenClose,
        DockMagnify,
        FolderPanel,
        Osd,
        BadgeBounce,
        TabTransitions,
    }

    /// <summary>
    /// Bir animasyon türünün son çarpanı. Kendi anahtarı kapalıysa 0 (son durum anında); açıksa genel çarpan.
    /// Genel çarpan (azaltılmış hareket, pil tasarrufu, hız) her zaman kendi anahtarının üstündedir: anahtar açık
    /// olsa da genel çarpan 0 ise animasyon yoktur.
    /// </summary>
    public static double ScaleFor(MotionKind kind, Koy.Core.Settings.MotionSettings toggles, double globalScale)
    {
        if (globalScale <= 0 || !IsEnabled(kind, toggles))
            return 0.0;
        return globalScale;
    }

    private static bool IsEnabled(MotionKind kind, Koy.Core.Settings.MotionSettings t) => kind switch
    {
        MotionKind.NotchOpenClose => t.NotchOpenClose,
        MotionKind.DockMagnify => t.DockMagnify,
        MotionKind.FolderPanel => t.FolderPanel,
        MotionKind.Osd => t.Osd,
        MotionKind.BadgeBounce => t.BadgeBounce,
        MotionKind.TabTransitions => t.TabTransitions,
        _ => true,
    };
}
