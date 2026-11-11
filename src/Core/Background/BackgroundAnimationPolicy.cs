namespace Koy.Core.Background;

/// <summary>
/// Pencere ve güç durumu. Arka planın hareket edip etmeyeceğine bu üçü karar verir.
/// </summary>
/// <param name="IsNotchIdle">Çentik kapalı ya da boşta (genişletilmemiş).</param>
/// <param name="BatterySaver">Pil tasarrufu açık ya da cihaz pilde.</param>
/// <param name="Fullscreen">Tam ekran bir uygulama önde.</param>
public readonly record struct BackgroundPlaybackState(bool IsNotchIdle, bool BatterySaver, bool Fullscreen);

/// <summary>
/// Kullanıcı tercihi: boşta ya da pilde de hareket izni. Varsayılan ikisi de kapalı.
/// </summary>
/// <param name="AnimateWhenIdle">Çentik boştayken de hareket etsin mi.</param>
/// <param name="AnimateOnBattery">Pilde (pil tasarrufu) de hareket etsin mi.</param>
public readonly record struct BackgroundAnimationOptions(bool AnimateWhenIdle = false, bool AnimateOnBattery = false);

/// <summary>
/// Arka planın hareket edip etmesini belirleyen saf karar. Durağan görsel hiçbir zaman hareket etmez.
/// Tam ekran her zaman durdurur: arkada yer alan ve kaynak tüketen bir çizim gereksiz.
/// Dekoder, oynatıcı ve WPF çizimi bu kararı uygulayan ayrı katmanlardır (Core dışı).
/// </summary>
public static class BackgroundAnimationPolicy
{
    /// <summary>Verilen tür ve durumda arka plan hareket etmeli mi.</summary>
    public static bool ShouldAnimate(BackgroundKind kind, BackgroundPlaybackState state, BackgroundAnimationOptions options = default)
    {
        if (kind is BackgroundKind.None or BackgroundKind.StillImage)
            return false;

        if (state.Fullscreen)
            return false;

        if (state.IsNotchIdle && !options.AnimateWhenIdle)
            return false;

        if (state.BatterySaver && !options.AnimateOnBattery)
            return false;

        return true;
    }
}
