namespace Koy.Core.Appearance;

// Ad settings.json'a yazılır: yayımlandıktan sonra adları değiştirilmez (yalnız yeni değer eklenir).

/// <summary>
/// Arayüz simge kaynağı. Builtin: uygulamayla gelen simgeler. Folder: kullanıcının seçtiği klasördeki
/// simgeler; klasörde olmayan anahtar yerleşik simgeye düşer. Dosyanın yüklenmesi Core dışında yapılır.
/// </summary>
public enum IconPack
{
    Builtin,
    Folder
}
