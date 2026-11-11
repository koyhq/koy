namespace Koy.Core.Dock.Items;

/// <summary>
/// Ayar dosyasındaki ham dock öğesi. Tür bilerek metin: bilinmeyen bir tür (yeni sürümden ya da elle düzenlenmiş dosya)
/// JSON okumayı bozmasın diye enum'a çevrilmez; <see cref="DockItemRules.Normalize"/> bunları atar.
/// </summary>
public sealed record DockItemEntry
{
    public string Kind { get; set; } = "";

    /// <summary>App: kısayol dosya adı (.lnk). Folder: klasör kimliği. Custom: düğmenin benzersiz kimliği. Clock, Trash, Separator, Spacer için yok sayılır.</summary>
    public string Id { get; set; } = "";

    /// <summary>Yalnız Custom türünde: çalıştırılacak komut, URL ya da dosya. Core bunu asla çalıştırmaz.</summary>
    public string Command { get; set; } = "";
}
