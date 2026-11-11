namespace Koy.Core.Notch;

/// <summary>
/// Çentik boştayken (kapalı, müzik çalmıyor) ne gösterilir. Ayarda sayı olarak saklanır:
/// 0 = saat ve pil, 1 = yalnız saat, 2 = hiçbiri, 3 = halka pil ve gün/saat. Bilinmeyen değer varsayılana (saat ve pil) düşer.
/// </summary>
public static class IdleDisplay
{
    public const int ClockAndBattery = 0;
    public const int ClockOnly = 1;
    public const int Nothing = 2;

    /// <summary>Solda dairesel pil halkası ve yüzde, sağda kısa gün adı ve saat ("Sal 19:21").</summary>
    public const int RingClockDay = 3;

    public static bool ShowClock(int mode) => mode is not Nothing;

    /// <summary>Pil (kutu ya da halka) gösterilsin mi.</summary>
    public static bool ShowBattery(int mode) => mode is not (ClockOnly or Nothing);

    /// <summary>Pil kutu yerine halka olarak, saat de gün adıyla birlikte çizilsin mi.</summary>
    public static bool IsRing(int mode) => mode == RingClockDay;

    /// <summary>
    /// Wi-Fi / Bluetooth durum simgeleri pil gibi "ek bilgi"dir: "saat, pil ve bağlantılar" ve halka modunda görünür
    /// (halka modunda yalnız Peek'te, bkz. <see cref="ShowRadioWhenCollapsed"/>). "Yalnız saat" ve "Hiçbiri" seçimi
    /// kullanıcının çentikte başka bir şey istemediği anlamına gelir. Bu değer radyo izleyicisinin açılışta kurulup
    /// kurulmayacağını da belirler.
    /// </summary>
    public static bool ShowRadio(int mode) => mode is not (ClockOnly or Nothing);

    /// <summary>
    /// Radyo simgeleri kapalı çentikte de görünsün mü. Halka modunda kapalı çentik yalnız halka ve gün/saatle sade kalır;
    /// simgeler fareyle üstüne gelince (Peek) çıkar.
    /// </summary>
    public static bool ShowRadioWhenCollapsed(int mode) => ShowRadio(mode) && !IsRing(mode);

    /// <summary>
    /// Üzerine gelince görünen tarih ve ağ simgesi de "ek bilgi"dir: "saat, pil ve bağlantılar" ve halka modunda çıkar.
    /// </summary>
    public static bool ShowPeekDetails(int mode) => mode is not (ClockOnly or Nothing);
}
