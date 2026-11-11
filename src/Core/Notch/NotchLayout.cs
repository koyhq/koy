namespace Koy.Core.Notch;

/// <summary>
/// Kapalı ve ipucu (Peek) durumlarında hangi yerleşimin çizileceği. Peek yalnız şarkı bilgisi için tasarlandı;
/// parça yokken fareyle üstüne gelmek boş bir ipucu çizmek yerine boşta yerleşimini (saat, pil, Wi-Fi / Bluetooth)
/// açık tutar. Böylece simgelere tıklanabilir ve ipucu metinleri görünür (simgeler fare çentiğe girer girmez
/// kaybolmaz).
/// </summary>
public static class NotchLayout
{
    /// <summary>Sabit pencere tuvalinin genişliği (kural #13). Açık çentik (620) ve kenar payı sığar.</summary>
    public const double CanvasWidth = 640;

    /// <summary>
    /// Sabit pencere tuvalinin yüksekliği (kural #13). Açık çentik 352 DIP; üstüne titreme (shake) payı kalır.
    /// </summary>
    public const double CanvasHeight = 364;

    /// <summary>Açık çentiğin genişliği (H tasarımı; kenar boşlukları dahil).</summary>
    public const double ExpandedWidth = 620;

    /// <summary>
    /// Açık çentiğin yüksekliği, her sekmede aynı (sabit gövde): üst boşluk 10 + durum satırı 26 + 12 + gövde 236 + 12 +
    /// sekme çubuğu 44 + alt boşluk 12. Gövde sabit olduğu için sekme değişince çentik boyut değiştirmez.
    /// </summary>
    public const double ExpandedHeight = 352;

    /// <summary>Çentik kartının genişliği (güncelleme, bildirim). HUD'dan (260×40) ayrıdır; tuvalin içinde kalır.</summary>
    public const double CardWidth = 380;

    /// <summary>Çentik kartının yüksekliği.</summary>
    public const double CardHeight = 72;

    /// <summary>Boşta yerleşimi (saat, pil, durum simgeleri ya da müzik çalıyorsa kapak) çizilsin mi.</summary>
    public static bool ShowCollapsedLayout(NotchState state, bool hasTrack) =>
        state == NotchState.Collapsed || (state == NotchState.Peek && !hasTrack);

    /// <summary>
    /// Üzerine gelince (Peek) boşta yerleşimine tarih ve bağlantı simgeleri (ağ, Bluetooth) eklensin mi.
    /// Kapalı çentik kalabalıklaşmasın diye bu ayrıntılar yalnız Peek'te görünür; kapalıyken yalnız saat ve pil kalır.
    /// </summary>
    public static bool ShowPeekDetails(NotchState state, bool hasTrack) =>
        state == NotchState.Peek && !hasTrack;

    /// <summary>Şarkı adı ve sanatçıyı gösteren ipucu yerleşimi çizilsin mi.</summary>
    public static bool ShowPeekLayout(NotchState state, bool hasTrack) =>
        state == NotchState.Peek && hasTrack;
}
