namespace Koy.Core.Notch;

/// <summary>Ana sekmesinin iki yüzü: tam sayfa müzik ya da Ana (gün, hava, etkinlikler, sistem, hızlı başlat).</summary>
public enum NotchHomePage
{
    Music,
    Home
}

/// <summary>
/// Ana sekmesinde hangi yüzün gösterileceği. Kullanıcının seçtiği yüz (Ana veya Müzik) çentik kapansa da korunur;
/// ancak parça yoksa ya da müzik kartı ayarlarda kapalıysa Ana yüzüne düşer.
/// </summary>
public static class NotchHomePages
{
    public static NotchHomePage OnOpen(NotchHomePage current, bool hasTrack, bool musicEnabled) =>
        hasTrack && musicEnabled ? current : NotchHomePage.Home;
}
