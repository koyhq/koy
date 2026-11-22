namespace Koy.Core.Notch;

/// <summary>Ana sekmesinin iki yüzü: tam sayfa müzik ya da Ana (gün, hava, etkinlikler, sistem, hızlı başlat).</summary>
public enum NotchHomePage
{
    Music,
    Home
}

/// <summary>
/// Ana sekmesinde hangi yüzün gösterileceği. Kullanıcının son bıraktığı yüz (Ana veya Müzik) çentik kapansa da açılışta korunur.
/// Müzik yüzü parça çalmıyorken de seçilebilir (boş durumunu gösterir); parça yokluğu ya da "Ana sayfa kartları" listesi karar vermez.
/// Yalnız Müzik yüzüne gerçekten ulaşılamıyorsa (<paramref name="musicFaceReachable"/> yanlış) Ana'ya düşülür.
/// </summary>
public static class NotchHomePages
{
    public static NotchHomePage OnOpen(NotchHomePage current, bool musicFaceReachable = true) =>
        musicFaceReachable ? current : NotchHomePage.Home;
}
