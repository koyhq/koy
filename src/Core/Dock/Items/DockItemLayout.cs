namespace Koy.Core.Dock.Items;

/// <summary>
/// Dock şeridinin düzeni: uygulama/klasör sırası (PinnedOrder) ile ayırıcı, boşluk, saat, çöp kutusu ve özel düğmelerin
/// birleşimi. Saf kurallar; çizim ve çalıştırma App'te. Hiç ek öğe yoksa düzen bugünkü dock'la aynıdır.
/// </summary>
public static class DockItemLayout
{
    /// <summary>Uygulama ve klasör dışındaki (ek) öğe mi?</summary>
    public static bool IsExtra(DockItemKind kind) => kind is not (DockItemKind.App or DockItemKind.Folder);

    /// <summary>Düzende en az bir ek öğe var mı? Yoksa dock hiç değişmez.</summary>
    public static bool HasExtras(IEnumerable<DockItemSpec> items) => items.Any(i => IsExtra(i.Kind));

    /// <summary>Uygulama ya da klasörün PinnedOrder belirteci. Ek öğeler için null.</summary>
    public static string? TokenOf(DockItemSpec item) => item.Kind switch
    {
        DockItemKind.App => item.Id,
        DockItemKind.Folder => PinnedOrder.ToFolderToken(item.Id),
        _ => null,
    };

    /// <summary>
    /// Kayıtlı öğe listesini şu an dock'ta bulunan uygulama/klasör sırasıyla uzlaştırır (<paramref name="effectiveOrder"/>
    /// PinnedOrder uzlaştırmasının sonucudur). Listede olup dock'ta artık bulunmayan uygulama ya da klasör düşer; ek
    /// öğeler yerinde kalır. Listede olmayan yeni uygulama/klasör, son uygulama/klasörün hemen arkasına (hiç yoksa
    /// başa) kendi sırasıyla eklenir.
    /// </summary>
    public static IReadOnlyList<DockItemSpec> Merge(IReadOnlyList<DockItemSpec> items, IReadOnlyList<string> effectiveOrder)
    {
        var present = new HashSet<string>(effectiveOrder, StringComparer.OrdinalIgnoreCase);
        var result = new List<DockItemSpec>();
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var token = TokenOf(item);
            if (token is null)
            {
                result.Add(item);
                continue;
            }

            if (present.Contains(token) && placed.Add(token))
                result.Add(item);
        }

        var missing = new List<DockItemSpec>();
        foreach (var token in effectiveOrder)
        {
            if (string.IsNullOrWhiteSpace(token) || placed.Contains(token))
                continue;

            placed.Add(token);
            missing.Add(PinnedOrder.IsFolderId(token)
                ? new DockItemSpec(DockItemKind.Folder, PinnedOrder.ExtractFolderId(token), "")
                : new DockItemSpec(DockItemKind.App, token, ""));
        }

        if (missing.Count > 0)
        {
            var lastIndex = result.FindLastIndex(i => !IsExtra(i.Kind));
            result.InsertRange(lastIndex + 1, missing);
        }

        return result;
    }

    /// <summary>
    /// Öğeyi sona ekler. Sınır aşılırsa ya da tekil bir öğe (saat, çöp kutusu) zaten varsa liste değişmeden döner
    /// (doğrulama <see cref="DockItemRules.Normalize"/> ile aynıdır).
    /// </summary>
    public static IReadOnlyList<DockItemSpec> Add(IReadOnlyList<DockItemSpec> items, DockItemSpec item)
    {
        var next = DockItemRules.Normalize(ToEntries(items.Append(item)));
        return next.Count == items.Count + 1 ? next : items;
    }

    /// <summary>İndeksteki öğeyi çıkarır. Geçersiz indekste liste değişmeden kopyalanır.</summary>
    public static IReadOnlyList<DockItemSpec> RemoveAt(IReadOnlyList<DockItemSpec> items, int index)
    {
        var copy = items.ToList();
        if (index >= 0 && index < copy.Count)
            copy.RemoveAt(index);
        return copy;
    }

    /// <summary>Doğrulanmış öğeleri ayar kaydına çevirir. Hiç ek öğe kalmadıysa boş döner: dock PinnedOrder'dan türetilir.</summary>
    public static List<DockItemEntry> ToStoredEntries(IReadOnlyList<DockItemSpec> items) =>
        HasExtras(items) ? ToEntries(items) : [];

    /// <summary>Özel düğme için yeni kimlik (kısa, dosya adı olmayan, tahmin edilemez).</summary>
    public static string NewCustomId() => Guid.NewGuid().ToString("N")[..8];

    private static List<DockItemEntry> ToEntries(IEnumerable<DockItemSpec> items) =>
        items.Select(i => new DockItemEntry { Kind = i.Kind.ToString(), Id = i.Id, Command = i.Command }).ToList();
}
