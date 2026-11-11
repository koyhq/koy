namespace Koy.Core.Settings;

/// <summary>
/// Harici kaynaklardan (sağ alt menü, kısayollar) gelen çentik/dock durumlarını
/// mevcut ayar durumuyla birleştirir; bayat değerlerin diske yazılmasını önler.
/// </summary>
public static class FeatureStateSyncPolicy
{
    /// <summary>
    /// Verilen hedef ayar nesnesini, harici olarak sağlanan özellik durumlarıyla uzlaştırır.
    /// Belirtilmeyen (null) durumlar için mevcut hedef değerler korunur.
    /// </summary>
    public static AppSettings Reconcile(AppSettings current, bool? notchEnabled = null, bool? dockEnabled = null) =>
        current with
        {
            Notch = current.Notch with
            {
                Enabled = notchEnabled ?? current.Notch.Enabled
            },
            Dock = current.Dock with
            {
                Enabled = dockEnabled ?? current.Dock.Enabled
            }
        };
}
