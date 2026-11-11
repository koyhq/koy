namespace Koy.Core.Background;

/// <summary>
/// Pencere, güç ve hareket durumu. Arka planın hareket edip etmeyeceğine bunlar karar verir.
/// Varsayılan (default) değer "görünmez ve hareket yok" demektir: unutulan alan animasyonu açmaz.
/// </summary>
/// <param name="IsNotchIdle">Çentik kapalı ya da boşta (genişletilmemiş) ama ekranda görünüyor.</param>
/// <param name="BatterySaver">Pil tasarrufu açık ya da cihaz pilde.</param>
/// <param name="Fullscreen">Tam ekran bir uygulama önde.</param>
/// <param name="IsNotchVisible">Çentik ekranda görünüyor (VisibilityController.IsVisible). Gizliyken kimse görmez; "boşta da oynat" tercihi bunu aşamaz.</param>
/// <param name="MotionScale">
/// <see cref="Koy.Core.Appearance.MotionPolicy.Scale"/> sonucu; 0 (ya da sayı değil) ise Windows'un animasyonları azalt ayarı
/// ya da Koy'un Animasyon: Kapalı seçimi var, arka plan hareket etmez. Pil teriminin çift sayılmaması için
/// <c>batterySaverReduceMotion</c> burada false verilir; pil kararı <paramref name="BatterySaver"/> ile tercihe bağlıdır.
/// </param>
public readonly record struct BackgroundPlaybackState(
    bool IsNotchIdle, bool BatterySaver, bool Fullscreen, bool IsNotchVisible, double MotionScale);

/// <summary>
/// Kullanıcı tercihi: boşta ya da pilde de hareket izni. Varsayılan ikisi de kapalı.
/// </summary>
/// <param name="AnimateWhenIdle">Çentik boştayken de hareket etsin mi.</param>
/// <param name="AnimateOnBattery">Pilde (pil tasarrufu) de hareket etsin mi.</param>
public readonly record struct BackgroundAnimationOptions(bool AnimateWhenIdle = false, bool AnimateOnBattery = false);

/// <summary>
/// Arka planın hareket edip etmesini belirleyen saf karar. Durağan görsel hiçbir zaman hareket etmez.
/// Gizli çentik, tam ekran ve azaltılmış hareket her zaman durdurur (AGENTS.md #5, #6): tercihler bunları aşamaz.
/// Dekoder, oynatıcı ve WPF çizimi bu kararı uygulayan ayrı katmanlardır (Core dışı).
/// </summary>
public static class BackgroundAnimationPolicy
{
    /// <summary>Verilen tür ve durumda arka plan hareket etmeli mi.</summary>
    public static bool ShouldAnimate(BackgroundKind kind, BackgroundPlaybackState state, BackgroundAnimationOptions options = default)
    {
        // Beyaz liste: ayar dosyasından gelen tanımsız sayı (örn. 99) hareket sayılmaz.
        if (kind is not (BackgroundKind.AnimatedImage or BackgroundKind.Video))
            return false;

        if (!state.IsNotchVisible || state.Fullscreen)
            return false;

        // NaN'da "> 0" yanlış çıkar; böylece bozuk çarpan da hareketsiz sayılır.
        if (!(state.MotionScale > 0))
            return false;

        if (state.IsNotchIdle && !options.AnimateWhenIdle)
            return false;

        if (state.BatterySaver && !options.AnimateOnBattery)
            return false;

        return true;
    }
}
