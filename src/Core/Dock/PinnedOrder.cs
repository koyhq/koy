using System.Text;

namespace Koy.Core.Dock;

/// <summary>
/// Sabitli uygulamaların sırası (saf mantık). Sıra Koy'un ayarlarında (<c>AppSettings.DockPinnedOrder</c>) tutulur;
/// kimlik, görev çubuğu klasöründeki .lnk dosya adıdır (örn. "Obsidian.lnk"). Windows'un kendi sırası yalnız ilk
/// açılışta tohum olarak okunur.
/// </summary>
public static class PinnedOrder
{
    public const string FolderPrefix = "folder:";

    /// <summary>Belirtilen kimliğin bir klasör belirteci olup olmadığını döndürür.</summary>
    public static bool IsFolderId(string id) =>
        id.StartsWith(FolderPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Klasör kimliğini dock sıralama belirtecine dönüştürür ("folder:<Id>").</summary>
    public static string ToFolderToken(string folderId) =>
        FolderPrefix + folderId;

    /// <summary>Klasör belirtecinden klasör kimliğini ayıklar ("folder:abc" -> "abc").</summary>
    public static string ExtractFolderId(string folderToken) =>
        folderToken.StartsWith(FolderPrefix, StringComparison.OrdinalIgnoreCase)
            ? folderToken[FolderPrefix.Length..]
            : folderToken;

    /// <summary>Sabitli uygulamanın sıralama kimliği: kısayol dosyasının adı (yoldan bağımsız, kullanıcıdan bağımsız).</summary>
    public static string IdOf(string shortcutPath) => Path.GetFileName(shortcutPath);

    /// <summary>
    /// Kayıtlı sırayı uygular. Kayıtta olanlar kayıt sırasında, kayıtta olmayan yeni uygulamalar sona (verilen sırayla) gelir.
    /// </summary>
    public static IReadOnlyList<PinnedApp> Apply(IReadOnlyList<PinnedApp> apps, IReadOnlyList<string> order)
    {
        if (apps.Count <= 1 || order.Count == 0)
            return apps;

        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in order)
            rank.TryAdd(id, rank.Count);

        // Kararlı sıralama: aynı sıradakiler (kayıtta olmayanlar) gelen sırayı korur.
        return apps
            .Select((app, index) => (app, index, rank: rank.TryGetValue(IdOf(app.ShortcutPath), out var r) ? r : int.MaxValue))
            .OrderBy(x => x.rank)
            .ThenBy(x => x.index)
            .Select(x => x.app)
            .ToList();
    }

    /// <summary>
    /// Kayıtlı sırayı mevcut kimliklerle eşitler: kaldırılanlar listeden düşer, yeni sabitlenenler sona eklenir.
    /// Klasör belirteçleri ('folder:<Id>') tanınır ve korunur.
    /// Mevcut kimlik listesi boşsa (klasör okunamadı) kayıt bozulmasın diye aynen döner.
    /// </summary>
    public static List<string> Reconcile(IReadOnlyList<string> saved, IReadOnlyList<string> currentIds)
    {
        if (currentIds.Count == 0)
            return [.. saved];

        var present = new HashSet<string>(currentIds, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in saved)
        {
            if (IsFolderId(id))
            {
                if (seen.Add(id))
                    result.Add(id);
            }
            else if (present.Contains(id) && seen.Add(id))
            {
                result.Add(id);
            }
        }

        foreach (var id in currentIds)
        {
            if (seen.Add(id))
                result.Add(id);
        }

        return result;
    }

    /// <summary>
    /// Klasör farkındalıklı sıra eşitlemesi:
    /// - Klasör içindeki uygulamalar ana dock sırasında ayrıca görünmez.
    /// - Diskten silinmiş kısayollar klasörlerden ayıklanır.
    /// - Klasörde 1 öğe kalırsa klasör dağıtılır ve kalan öğe klasörün sırasına yerleştirilir.
    /// - Klasörde 0 öğe kalırsa klasör silinir ve belirteci dock sırasından kaldırılır.
    /// - Kalan aktif klasörler (>= 2 öğe) ve güncellenmiş dock sırası döndürülür.
    /// </summary>
    public static (List<string> Order, List<DockFolder> Folders) Reconcile(
        IReadOnlyList<string> saved,
        IReadOnlyList<string> currentIds,
        IReadOnlyList<DockFolder> folders)
    {
        if (currentIds.Count == 0)
            return ([.. saved], [.. folders]);

        var available = new HashSet<string>(currentIds, StringComparer.OrdinalIgnoreCase);

        // 1. Klasörlerdeki silinmiş kısayolları temizle
        var prunedFolders = new Dictionary<string, (DockFolder Folder, List<string> ValidItems)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in folders)
        {
            var validItems = f.Items.Where(available.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            prunedFolders[f.Id] = (f, validItems);
        }

        // Aktif klasörler: en az 2 öğesi olanlar
        var activeFolders = prunedFolders.Values
            .Where(x => x.ValidItems.Count >= 2)
            .Select(x => x.Folder with { Items = x.ValidItems })
            .ToList();

        // Aktif klasörlerin içindeki uygulamalar dock kök düzeyinde görünmemeli
        var appsInFolders = new HashSet<string>(
            activeFolders.SelectMany(f => f.Items),
            StringComparer.OrdinalIgnoreCase);

        var resultOrder = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 2. Kayıtlı sırayı tara
        foreach (var id in saved)
        {
            if (IsFolderId(id))
            {
                var folderId = ExtractFolderId(id);
                if (prunedFolders.TryGetValue(folderId, out var entry))
                {
                    if (entry.ValidItems.Count >= 2)
                    {
                        var token = ToFolderToken(entry.Folder.Id);
                        if (seen.Add(token))
                            resultOrder.Add(token);
                    }
                    else if (entry.ValidItems.Count == 1)
                    {
                        // 1 öğe kaldı: klasör dağılır, kalan tek öğe klasörün yuvasına geçer
                        var singleItem = entry.ValidItems[0];
                        if (!appsInFolders.Contains(singleItem) && seen.Add(singleItem))
                            resultOrder.Add(singleItem);
                    }
                    // 0 öğe kaldı: klasör silinir (belirteç eklenmez)
                }
            }
            else
            {
                // Kök düzeyindeki kısayol (herhangi bir aktif klasörde yoksa ve diskte mevcutsa)
                if (!appsInFolders.Contains(id) && available.Contains(id) && seen.Add(id))
                {
                    resultOrder.Add(id);
                }
            }
        }

        // 3. Sıraya henüz girmemiş aktif klasörler (yeni oluşturulmuş)
        foreach (var f in activeFolders)
        {
            var token = ToFolderToken(f.Id);
            if (seen.Add(token))
                resultOrder.Add(token);
        }

        // 4. Henüz görülmemiş ve klasörlerde olmayan diskteki yeni kısayolları sona ekle
        foreach (var id in currentIds)
        {
            if (!appsInFolders.Contains(id) && seen.Add(id))
            {
                resultOrder.Add(id);
            }
        }

        return (resultOrder, activeFolders);
    }

    /// <summary>Bir öğeyi yeni dizine taşır (dizin sınırlara kırpılır). Kimlik listede yoksa sıra değişmeden döner.</summary>
    public static List<string> Move(IReadOnlyList<string> order, string id, int newIndex)
    {
        var result = new List<string>(order);
        var from = result.FindIndex(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
        if (from < 0)
            return result;

        var item = result[from];
        result.RemoveAt(from);
        result.Insert(Math.Clamp(newIndex, 0, result.Count), item);
        return result;
    }

    /// <summary>
    /// Windows'un Taskband <c>Favorites</c> ikili değerinden ilk sıra tohumunu çıkarır. Biçim belgesizdir: bayt yapısı
    /// çözülmez, yalnız bilinen .lnk adlarının değerde geçtiği yerlere bakılır (UTF-16, iki hizalama). Bulunanlar
    /// değerdeki sırayla, bulunamayanlar verilen (klasör) sırasıyla arkadan gelir. Değer boş, bozuk ya da hiçbir adı
    /// içermiyorsa sonuç klasör sırasıdır; hiçbir koşulda istisna fırlatmaz.
    /// </summary>
    public static List<string> SeedFromTaskband(byte[]? favorites, IReadOnlyList<string> folderOrderIds)
    {
        var fallback = new List<string>(folderOrderIds);
        if (favorites is not { Length: >= 8 } || folderOrderIds.Count == 0)
            return fallback;

        try
        {
            var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var alignment = 0; alignment < 2; alignment++)
            {
                var length = (favorites.Length - alignment) & ~1;
                if (length < 8)
                    continue;

                var text = Encoding.Unicode.GetString(favorites, alignment, length);

                // Metin, denetim karakterlerinde bölünen koşulara ayrılır. Ad bir koşunun SONUNDA olmalı ve önünde
                // en çok 2 artık karakter bulunabilir (ikili uzunluk baytları yazdırılabilir görünür: "UObsidian.lnk").
                // Böylece "Code.lnk", "Visual Studio Code.lnk" koşusunda ya da "A.lnk", "Baska.lnk" içinde yanlış eşleşmez.
                var runStart = -1;
                for (var i = 0; i <= text.Length; i++)
                {
                    var inRun = i < text.Length && !char.IsControl(text[i]) && !char.IsSurrogate(text[i]);
                    if (inRun)
                    {
                        if (runStart < 0)
                            runStart = i;
                        continue;
                    }

                    if (runStart >= 0)
                    {
                        MatchRun(text.AsSpan(runStart, i - runStart), runStart, alignment, folderOrderIds, positions);
                        runStart = -1;
                    }
                }
            }

            if (positions.Count == 0)
                return fallback;

            var found = folderOrderIds
                .Where(positions.ContainsKey)
                .OrderBy(id => positions[id])
                .ToList();
            var rest = folderOrderIds.Where(id => !positions.ContainsKey(id));
            return [.. found, .. rest];
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Adın önündeki artık ikili veri mi (gerçek bir dosya adının başı değil mi)? En çok bir karakter (uzunluk baytı)
    /// her şey olabilir; ondan öncesi okunabilir harf/rakam içermemeli ("Baska.lnk" içindeki "A.lnk" reddedilir).
    /// </summary>
    private static bool IsBinaryNoise(ReadOnlySpan<char> prefix)
    {
        if (prefix.Length <= 1)
            return true;

        foreach (var c in prefix[..^1])
        {
            if (char.IsAsciiLetterOrDigit(c) || c == ' ')
                return false;
        }

        return true;
    }

    private static void MatchRun(ReadOnlySpan<char> run, int runStart, int alignment,
        IReadOnlyList<string> ids, Dictionary<string, int> positions)
    {
        if (!run.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return;

        string? best = null;
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id) || id.Length > run.Length)
                continue;

            if (run.EndsWith(id, StringComparison.OrdinalIgnoreCase)
                && IsBinaryNoise(run[..(run.Length - id.Length)])
                && (best is null || id.Length > best.Length))
                best = id;
        }

        if (best is null)
            return;

        var bytePosition = (runStart + run.Length - best.Length) * 2 + alignment;
        if (!positions.TryGetValue(best, out var existing) || bytePosition < existing)
            positions[best] = bytePosition;
    }
}
