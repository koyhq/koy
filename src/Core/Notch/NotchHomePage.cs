namespace Koy.Core.Notch;

/// <summary>Ana sekmesinin iki yüzü: tam sayfa müzik ya da Ana (gün, hava, etkinlikler, sistem, hızlı başlat).</summary>
public enum NotchHomePage
{
    Music,
    Home
}

/// <summary>
/// Ana sekmesinde hangi yüzün gösterileceği. Çentik her açılışta yeniden karar verir: parça varsa ve müzik kartı
/// ayarlarda açıksa Müzik, yoksa Ana. Kullanıcı açıkken üstteki anahtarla değiştirir; seçim kapanınca unutulur.
/// </summary>
public static class NotchHomePages
{
    public static NotchHomePage OnOpen(bool hasTrack, bool musicEnabled) =>
        hasTrack && musicEnabled ? NotchHomePage.Music : NotchHomePage.Home;
}
