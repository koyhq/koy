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
    /// Sürükleme sonrası düzen. <paramref name="visibleInNewOrder"/> şeritte görünen öğelerin (kayıtlı düzendeki dizinleriyle)
    /// yeni şerit sırasıdır; görünen öğeler, kendi yuvalarını (dizinlerinin artan sırası) bu sırayla doldurur.
    /// Şeritte görünmeyen öğeler (pili olmayan makinedeki pil) yerinde kalır. Geçersiz ya da yinelenen dizinde düzen değişmez.
    /// </summary>
    public static IReadOnlyList<DockItemSpec> Reorder(IReadOnlyList<DockItemSpec> layout, IReadOnlyList<int> visibleInNewOrder)
    {
        var slots = visibleInNewOrder.OrderBy(i => i).ToList();
        if (slots.Count != slots.Distinct().Count() || slots.Any(i => i < 0 || i >= layout.Count))
            return layout;

        var result = layout.ToList();
        for (var k = 0; k < slots.Count; k++)
            result[slots[k]] = layout[visibleInNewOrder[k]];
        return result;
    }

    /// <summary>
    /// Uygulama/klasör kümesi ya da sırası değişti (klasör oluşturma, ekleme, çıkarma, dağıtma): düzeni yeni sıraya uyarlar.
    /// Ek öğeler, kendinden önce kaç uygulama/klasör olduğunu korur; uygulamalar klasörde birleşince klasör, uygulamaların
    /// en sağdakinin yerini alır (ek öğe klasörün hemen önünde ya da arkasında kalır).
    /// </summary>
    public static IReadOnlyList<DockItemSpec> Rebase(
        IReadOnlyList<DockItemSpec> layout, IReadOnlyList<string> newOrder,
        IReadOnlyList<DockFolder> oldFolders, IReadOnlyList<DockFolder> newFolders)
    {
        // Eski sıradaki konum: uygulama kendi konumu, klasördeki uygulama klasörünün konumu.
        var oldTokens = ToTokens(layout);
        var position = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < oldTokens.Count; i++)
        {
            position[oldTokens[i]] = i;
            if (PinnedOrder.IsFolderId(oldTokens[i]))
            {
                var folder = oldFolders.FirstOrDefault(f => string.Equals(f.Id, PinnedOrder.ExtractFolderId(oldTokens[i]), StringComparison.OrdinalIgnoreCase));
                foreach (var member in folder?.Items ?? [])
                    position.TryAdd(member, i);
            }
        }

        int PositionOf(string token)
        {
            if (position.TryGetValue(token, out var known))
                return known;
            if (PinnedOrder.IsFolderId(token))
            {
                var folder = newFolders.FirstOrDefault(f => string.Equals(f.Id, PinnedOrder.ExtractFolderId(token), StringComparison.OrdinalIgnoreCase));
                var members = (folder?.Items ?? []).Where(position.ContainsKey).Select(m => position[m]).ToList();
                if (members.Count > 0)
                    return members.Max();
            }
            return int.MaxValue;
        }

        // Her ek öğe, kendinden önceki uygulama/klasör sayısıyla tutunur.
        var extrasAt = new SortedDictionary<int, List<DockItemSpec>>();
        var before = 0;
        foreach (var spec in layout)
        {
            if (IsExtra(spec.Kind))
            {
                if (!extrasAt.TryGetValue(before, out var list))
                    extrasAt[before] = list = [];
                list.Add(spec);
            }
            else
            {
                before++;
            }
        }

        var newPositions = newOrder.Select(PositionOf).ToList();
        var result = new List<DockItemSpec>();
        for (var k = 0; k <= newOrder.Count; k++)
        {
            // Eski konumu "kendinden önceki sayı"dan küçük olan yeni öğe sayısı k ise ek öğe k'inci yuvadan önce gelir.
            foreach (var (oldCount, extras) in extrasAt)
            {
                if (newPositions.Count(p => p < oldCount) == k)
                    result.AddRange(extras);
            }

            if (k < newOrder.Count)
            {
                var token = newOrder[k];
                result.Add(PinnedOrder.IsFolderId(token)
                    ? new DockItemSpec(DockItemKind.Folder, PinnedOrder.ExtractFolderId(token), "")
                    : new DockItemSpec(DockItemKind.App, token, ""));
            }
        }

        return result;
    }

    /// <summary>Düzendeki uygulama ve klasörlerin PinnedOrder belirteçleri, düzen sırasıyla.</summary>
    public static List<string> ToTokens(IEnumerable<DockItemSpec> layout) => DockItemRules.ToPinnedOrder(layout);

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
