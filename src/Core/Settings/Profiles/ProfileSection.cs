namespace Koy.Core.Settings.Profiles;

/// <summary>
/// Profil ve dışa aktarımda taşınabilen ayar bölümleri. Adları settings.json'daki bölüm anahtarlarıyla aynıdır.
/// Bilerek dışarıda kalanlar makineye özgüdür ya da kişisel veri taşır ve içe aktarılmaz:
/// General (dil, sihirbaz, güncelleme kanalı), Displays (monitör kimlikleri), Weather (konum), DockItems (sabitli
/// uygulama yolları, klasörler), QuickLaunch, Clipboard, Search (dosya yolları, geçmiş), Voice (çalışma klasörü, komutlar).
/// Dock içindeki StartIconPath (özel başlat simgesinin yolu) da makineye özgüdür: dışa aktarmada boşaltılır, içe aktarmada yerel değer korunur.
/// </summary>
public enum ProfileSection
{
    Appearance,
    Dock,
    Notch,
    Music,
    Shortcuts,
    Focus,
}
