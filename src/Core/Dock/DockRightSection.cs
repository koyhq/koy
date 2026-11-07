namespace Koy.Core.Dock;

/// <summary>
/// Dock'un sağ bölümünün hangi parçalarının görüneceği (yüzen kapsül ve tam genişlik aynı kuralı kullanır).
/// Kapalı düğmelerin ayırıcısı da gizlenir; küçük tepsi ok olarak Koy menüsü düğmesinin sağına geçer.
/// </summary>
public readonly record struct DockRightSection(
    bool ShowFirstSeparator,
    bool ShowFullTray,
    bool ShowSmallTray)
{
    /// <summary>Ok düğmesinin genişliği (DIP); tam boy düğmenin yerine yalnız bu kadar yer tutar.</summary>
    public const double SmallTrayWidthDip = 18.0;

    /// <param name="showFolders">İndirilenler düğmesi açık mı.</param>
    /// <param name="showRecents">Son kullanılanlar düğmesi açık mı (ayar ve servis).</param>
    /// <param name="hasTray">Tepsi penceresi gerçekten çalışıyor mu.</param>
    /// <param name="smallTray">Küçük tepsi ayarı.</param>
    public static DockRightSection Resolve(bool showFolders, bool showRecents, bool hasTray, bool smallTray)
    {
        var fullTray = hasTray && !smallTray;
        return new DockRightSection(
            ShowFirstSeparator: showFolders || showRecents || fullTray,
            ShowFullTray: fullTray,
            ShowSmallTray: hasTray && smallTray);
    }
}
