namespace Koy.Core.Shelf;

public enum ShelfItemKind
{
    File,
    Directory,
    Text
}

public sealed record ShelfItem(
    string Id,
    string DisplayName,
    string PathOrContent,
    ShelfItemKind Kind,
    DateTimeOffset AddedAt);

/// <summary>
/// Notch raf (shelf) sözleşmesi.
/// Dosya ve metin yollarını saklar, dosyaları kopyalamaz.
/// </summary>
public interface IShelfService : IDisposable
{
    IReadOnlyList<ShelfItem> Items { get; }
    event EventHandler? ItemsChanged;

    void AddFile(string path);
    void AddText(string text);
    void Remove(string id);
    void Clear();

    /// <summary>Kaldırılan öğeleri aynı kimlikle geri koyar ("Geri al"); en yeni başta olacak biçimde yerleşir. Yalnız yol tutulur, dosyaya dokunulmaz.</summary>
    void Restore(IReadOnlyList<ShelfItem> items);
}
