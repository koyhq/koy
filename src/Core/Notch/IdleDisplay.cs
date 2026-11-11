namespace Koy.Core.Notch;

/// <summary>
/// Çentik boştayken (kapalı, müzik çalmıyor) ne gösterileceğine dair kararlar (<see cref="IdleDisplayMode"/>).
/// </summary>
public static class IdleDisplay
{
    public static bool ShowClock(IdleDisplayMode mode) => mode is not IdleDisplayMode.Nothing;

    /// <summary>Pil (kutu ya da halka) gösterilsin mi.</summary>
    public static bool ShowBattery(IdleDisplayMode mode) => mode is not (IdleDisplayMode.ClockOnly or IdleDisplayMode.Nothing);

    /// <summary>Pil kutu yerine halka olarak, saat de gün adıyla birlikte çizilsin mi.</summary>
    public static bool IsRing(IdleDisplayMode mode) => mode == IdleDisplayMode.RingClockDay;

    /// <summary>
    /// Wi-Fi / Bluetooth durum simgeleri pil gibi "ek bilgi"dir: "saat, pil ve bağlantılar" ve halka modunda görünür
    /// (halka modunda yalnız Peek'te, bkz. <see cref="ShowRadioWhenCollapsed"/>). "Yalnız saat" ve "Hiçbiri" seçimi
    /// kullanıcının çentikte başka bir şey istemediği anlamına gelir. Bu değer radyo izleyicisinin açılışta kurulup
    /// kurulmayacağını da belirler.
    /// </summary>
    public static bool ShowRadio(IdleDisplayMode mode) => mode is not (IdleDisplayMode.ClockOnly or IdleDisplayMode.Nothing);

    /// <summary>
    /// Radyo simgeleri kapalı çentikte de görünsün mü. Halka modunda kapalı çentik yalnız halka ve gün/saatle sade kalır;
    /// simgeler fareyle üstüne gelince (Peek) çıkar.
    /// </summary>
    public static bool ShowRadioWhenCollapsed(IdleDisplayMode mode) => ShowRadio(mode) && !IsRing(mode);

    /// <summary>
    /// Üzerine gelince görünen tarih ve ağ simgesi de "ek bilgi"dir: "saat, pil ve bağlantılar" ve halka modunda çıkar.
    /// </summary>
    public static bool ShowPeekDetails(IdleDisplayMode mode) => mode is not (IdleDisplayMode.ClockOnly or IdleDisplayMode.Nothing);
}
