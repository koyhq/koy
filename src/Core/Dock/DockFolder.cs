namespace Koy.Core.Dock;

/// <summary>
/// Dock üzerinde sabitlenmiş uygulamaları gruplayan klasör veri modeli (saf C#, bağımsız).
/// </summary>
public sealed record DockFolder(
    string Id,
    string Name,
    List<string> Items)
{
    /// <summary>
    /// Varsayılan yapıcı: boş öğe listesiyle yeni bir klasör oluşturur.
    /// </summary>
    public DockFolder() : this(Guid.NewGuid().ToString("N")[..8], "Folder", []) { }

    /// <summary>
    /// Kopya yapıcı: 'with' ifadesinde Items koleksiyonunu klonlayarak paylaşılan referans sızıntısını önler.
    /// </summary>
    public DockFolder(DockFolder original)
    {
        Id = original.Id;
        Name = original.Name;
        Items = original.Items is not null ? new List<string>(original.Items) : [];
    }

    /// <summary>
    /// Değer tabanlı eşitlik: Id (büyük/küçük harf duyarsız), Name ve Items sırasını doğrular.
    /// </summary>
    public bool Equals(DockFolder? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && (Items == other.Items || (Items is not null && other.Items is not null && Items.SequenceEqual(other.Items, StringComparer.OrdinalIgnoreCase)));
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id, StringComparer.OrdinalIgnoreCase);
        hash.Add(Name);
        if (Items is not null)
        {
            foreach (var item in Items)
                hash.Add(item, StringComparer.OrdinalIgnoreCase);
        }
        return hash.ToHashCode();
    }
}
