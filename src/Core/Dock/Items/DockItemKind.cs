namespace Koy.Core.Dock.Items;

/// <summary>
/// Dock öğe türleri. Yeni tür eklenirse <see cref="DockItemRules"/> ayrıştırıcısı ve testleri de güncellenir.
/// Core yalnız türü ve kimliği tutar; öğenin nasıl çizildiği ya da çalıştığı App ve Platform katmanının işidir.
/// </summary>
public enum DockItemKind
{
    App,
    Folder,
    Separator,
    Spacer,
    Clock,
    Trash,
    Custom,
}
