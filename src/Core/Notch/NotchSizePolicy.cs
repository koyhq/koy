namespace Koy.Core.Notch;

/// <summary>Çentiğin hedef boyutu: genişlik, yükseklik, çizim yüzeyi ve yapışık biçimdeki köşe yarıçapı (DIP).</summary>
public readonly record struct NotchTargetSize(double Width, double Height, NotchSurface Surface, double PinnedRadius);

/// <summary>
/// Çentiğin hangi durumda ne boyutta olduğu (kapalı, Peek, ses/parlaklık göstergesi, kart, açık). NotchViewModel'in
/// sihirli sayıları burada toplanır; açık boyut <see cref="NotchLayout"/> sabitlerinden gelir (AGENTS #13).
/// Biçime bağlı son köşe yarıçapı <see cref="NotchStyle.CornerRadius"/> ile hesaplanır (<see cref="CornerRadius"/>).
/// </summary>
public static class NotchSizePolicy
{
    public const double OsdWidth = 260;
    public const double OsdHeight = 40;
    public const double OsdPinnedRadius = 20;
    public const double CardPinnedRadius = 28;

    public const double CollapsedWidth = 236;
    public const double CollapsedPlayingWidth = 280;
    public const double CollapsedHeight = 34;
    public const double CollapsedPinnedRadius = 18;

    public const double PeekWidth = 300;
    public const double PeekHeight = 38;
    public const double PeekPinnedRadius = 20;

    public const double ExpandedPinnedRadius = 34;

    /// <param name="state">Çentiğin durumu (gösterge yokken boyutu belirler).</param>
    /// <param name="osdActive">Ses/parlaklık göstergesi ya da kart gösteriliyor (durumu ezer).</param>
    /// <param name="osdIsCard">Gösterge kart biçiminde (güncelleme/bildirim kartı).</param>
    /// <param name="isMusicPlaying">Kapalı çentikte çalan müzik için daha geniş gövde.</param>
    public static NotchTargetSize Resolve(NotchState state, bool osdActive, bool osdIsCard, bool isMusicPlaying)
    {
        if (osdActive)
        {
            return osdIsCard
                ? new NotchTargetSize(NotchLayout.CardWidth, NotchLayout.CardHeight, NotchSurface.Card, CardPinnedRadius)
                : new NotchTargetSize(OsdWidth, OsdHeight, NotchSurface.Closed, OsdPinnedRadius);
        }

        return state switch
        {
            NotchState.Peek => new NotchTargetSize(PeekWidth, PeekHeight, NotchSurface.Closed, PeekPinnedRadius),
            // Her sekmede aynı sabit gövde: sekme değişince çentik boyut değiştirmez (bkz. NotchLayout).
            NotchState.Expanded => new NotchTargetSize(NotchLayout.ExpandedWidth, NotchLayout.ExpandedHeight, NotchSurface.Expanded, ExpandedPinnedRadius),
            _ => new NotchTargetSize(isMusicPlaying ? CollapsedPlayingWidth : CollapsedWidth, CollapsedHeight, NotchSurface.Closed, CollapsedPinnedRadius),
        };
    }

    /// <summary>Biçime göre son köşe yarıçapı: yapışıkta tablodaki değer, yüzen adada yüzeye göre (bkz. <see cref="NotchStyle.CornerRadius"/>).</summary>
    public static double CornerRadius(NotchStyleMode style, NotchTargetSize size) =>
        NotchStyle.CornerRadius(style, size.Surface, size.Height, size.PinnedRadius);
}
