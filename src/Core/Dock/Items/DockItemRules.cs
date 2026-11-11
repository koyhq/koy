namespace Koy.Core.Dock.Items;

/// <summary>
/// Dock öğe dizisinin saf kuralları: doğrulama, tekilleştirme, sıra ve PinnedOrder ile çift yönlü dönüşüm.
/// Çalıştırma ve çizim burada yok. Özel düğmenin komutu yalnız saklanır.
/// </summary>
public static class DockItemRules
{
    /// <summary>Dock'taki en çok öğe. Bellek ve çizim bütçesi için (AGENTS #1).</summary>
    public const int MaxItemCount = 32;

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>Özel komut uzunluk sınırı; bunu aşan komutlu öğe atılır.</summary>
    public const int MaxCommandLength = 2048;

    /// <summary>Tür metnini ayrıştırır (büyük/küçük harf duyarsız). Bilinmeyen metinde false döner.</summary>
    public static bool TryParseKind(string? text, out DockItemKind kind)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "app": kind = DockItemKind.App; return true;
            case "folder": kind = DockItemKind.Folder; return true;
            case "separator": kind = DockItemKind.Separator; return true;
            case "spacer": kind = DockItemKind.Spacer; return true;
            case "clock": kind = DockItemKind.Clock; return true;
            case "trash": kind = DockItemKind.Trash; return true;
            case "custom": kind = DockItemKind.Custom; return true;
            default: kind = default; return false;
        }
    }

    /// <summary>
    /// Ham kayıtları doğrular ve sırasını korur. Bilinmeyen tür, boş kimlik, dosya adı olamayan uygulama kimliği,
    /// boş ya da çok uzun özel komut atılır.
    /// Tekilleştirme: App, Folder ve Custom kendi türünde kimliğe göre (büyük/küçük harf duyarsız) tekildir;
    /// Clock ve Trash en fazla bir kez bulunur; Separator ve Spacer tekrarlanabilir.
    /// Sınır aşılınca kalan kayıtlar atılır (ilk gelenler kalır).
    /// </summary>
    public static IReadOnlyList<DockItemSpec> Normalize(IEnumerable<DockItemEntry> entries, int maxCount = MaxItemCount)
    {
        var result = new List<DockItemSpec>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (result.Count >= maxCount)
                break;
            if (entry is null || !TryParseKind(entry.Kind, out var kind))
                continue;

            var id = (entry.Id ?? "").Trim();
            var command = (entry.Command ?? "").Trim();
            string key;

            switch (kind)
            {
                case DockItemKind.App:
                case DockItemKind.Folder:
                    if (id.Length == 0)
                        continue;
                    // Uygulama kimliği .lnk dosya adıdır; "folder:x" ya da yol/geçersiz karakter PinnedOrder'a yazılınca
                    // başka bir şeye (klasöre) dönüşür ya da hiçbir kısayolla eşleşmez.
                    if (kind == DockItemKind.App && (PinnedOrder.IsFolderId(id) || id.IndexOfAny(InvalidFileNameChars) >= 0))
                        continue;
                    command = "";
                    key = $"{kind}:{id}";
                    break;

                case DockItemKind.Custom:
                    // Komut Core'da yalnız saklanır; boş ya da aşırı uzun komutlu düğme anlamsızdır.
                    if (id.Length == 0 || command.Length == 0 || command.Length > MaxCommandLength)
                        continue;
                    key = $"{kind}:{id}";
                    break;

                case DockItemKind.Clock:
                case DockItemKind.Trash:
                    // Tekil öğeler: kimlik türün adına sabitlenir, böylece iki kez eklenemez.
                    id = kind.ToString();
                    command = "";
                    key = $"{kind}:{id}";
                    break;

                default:
                    // Ayırıcı ve boşluk kimliksiz ve tekrarlanabilir; anahtar her seferinde benzersiz.
                    id = "";
                    command = "";
                    key = $"{kind}:{result.Count}";
                    break;
            }

            if (!seenKeys.Add(key))
                continue;

            result.Add(new DockItemSpec(kind, id, command));
        }

        return result;
    }

    /// <summary>
    /// Öğeyi <paramref name="fromIndex"/> konumundan <paramref name="toIndex"/> konumuna taşır ve yeni bir liste döndürür.
    /// Geçersiz kaynak indeksinde liste değişmeden kopyalanır; hedef sınırların dışındaysa uca kenetlenir.
    /// </summary>
    public static IReadOnlyList<DockItemSpec> Move(IReadOnlyList<DockItemSpec> items, int fromIndex, int toIndex)
    {
        var copy = items.ToList();
        if (fromIndex < 0 || fromIndex >= copy.Count)
            return copy;

        var target = Math.Clamp(toIndex, 0, copy.Count - 1);
        if (target == fromIndex)
            return copy;

        var item = copy[fromIndex];
        copy.RemoveAt(fromIndex);
        copy.Insert(target, item);
        return copy;
    }

    /// <summary>
    /// Eski PinnedOrder listesini öğe kayıtlarına çevirir (sıra aynen korunur). "folder:" belirteçleri klasör olur.
    /// PinnedOrder ayarı değişmez; bu yalnız yeni listeye geçişte ya da karşılaştırmada kullanılır.
    /// </summary>
    public static IReadOnlyList<DockItemEntry> FromPinnedOrder(IEnumerable<string> pinnedOrder)
    {
        var entries = new List<DockItemEntry>();
        foreach (var token in pinnedOrder)
        {
            if (string.IsNullOrWhiteSpace(token))
                continue;

            entries.Add(PinnedOrder.IsFolderId(token)
                ? new DockItemEntry { Kind = "folder", Id = PinnedOrder.ExtractFolderId(token) }
                : new DockItemEntry { Kind = "app", Id = token });
        }
        return entries;
    }

    /// <summary>
    /// Dock'un gösterecek öğe listesi. Kayıtlı <paramref name="items"/> varsa o doğrulanıp kullanılır; hiç kayıt yoksa
    /// (ilk geçiş) bugünkü dock korunsun diye <paramref name="pinnedOrder"/> öğelere çevrilir. Kayıtlar var ama hepsi
    /// geçersizse kullanıcının sırası sessizce yeniden türetilmez: sonuç boş döner.
    /// Üst sınır geçişte de uygulanır: 32'den fazla sabitli uygulaması olan kullanıcı fazlasını bu listede görmez
    /// (PinnedOrder ayarı yine dokunulmaz kalır).
    /// </summary>
    public static IReadOnlyList<DockItemSpec> Resolve(IReadOnlyList<DockItemEntry> items, IEnumerable<string> pinnedOrder, int maxCount = MaxItemCount) =>
        Normalize(items.Count > 0 ? items : FromPinnedOrder(pinnedOrder), maxCount);

    /// <summary>
    /// Öğe listesinden PinnedOrder biçimindeki sırayı üretir: uygulamalar kısayol adı, klasörler "folder:" belirteciyle.
    /// Özel düğme, ayırıcı, boşluk, saat ve çöp kutusu PinnedOrder'a yazılmaz.
    /// </summary>
    public static List<string> ToPinnedOrder(IEnumerable<DockItemSpec> items)
    {
        var order = new List<string>();
        foreach (var item in items)
        {
            if (item.Kind == DockItemKind.App)
                order.Add(item.Id);
            else if (item.Kind == DockItemKind.Folder)
                order.Add(PinnedOrder.ToFolderToken(item.Id));
        }
        return order;
    }
}
