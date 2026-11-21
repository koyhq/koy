namespace Koy.Core.Media;

/// <summary>
/// Müzik kartı çentik zeminini örtüyor mu. Opak kartın altındaki canlı arka plan görünmez; videoyu çözmek ve
/// her karede çizmek boşa CPU yakar (kural #5, #6). Karar <see cref="Koy.Core.Notch.LiveBackgroundPlaybackPolicy.Decide"/>'a girdi olur.
/// </summary>
public static class MusicCardCoverage
{
    /// <summary>Bu alfa ve üstü zemin gerçek anlamda örter (Katı 255; Buzlu 219 ve Cam 184 altını gösterir).</summary>
    public const byte OpaqueAlpha = 230;

    /// <summary>
    /// Kart zemini opak mı: kapak resmi stilinde (büyütülmüş kapak, opak) her zaman; albüm rengi stilinde kart
    /// fırçası boyalıysa ve alfa (çentik malzemesinin opaklığı) yeterliyse. Düz kart ya da palet yokken kart yarı saydamdır.
    /// </summary>
    public static bool IsOpaque(bool hasTint, bool hasBackdrop, byte alpha) =>
        hasBackdrop || (hasTint && alpha >= OpaqueAlpha);

    /// <summary>Çentik zemini bu anda örtülü mü: müzik yüzü görünüyor ve kart opak.</summary>
    public static bool CoversNotchBackground(bool musicFaceShown, bool hasTint, bool hasBackdrop, byte alpha) =>
        musicFaceShown && IsOpaque(hasTint, hasBackdrop, alpha);
}
