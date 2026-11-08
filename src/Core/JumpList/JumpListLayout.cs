namespace Koy.Core.JumpList;

/// <summary>
/// Atlama listesi bölümlerinin sırası ve sınırları (saf mantık). Menü sırası: uygulamanın kendi kategorileri, sık
/// kullanılanlar, son kullanılanlar, görevler. (Koy'un kendi öğeleri bunların altına App'te eklenir.)
/// </summary>
public static class JumpListLayout
{
    /// <summary>
    /// Son kullanılanlar bölümü gösterilsin mi. Uygulamanın kendi listesi yoksa (dosya yok ya da okunamadı) Windows varsayılanı (son kullanılanlar)
    /// geçerlidir; kendi listesi varsa yalnız <c>AppendKnownCategory(RECENT)</c> demişse.
    /// </summary>
    public static bool ShowsRecent(CustomDestinationsFile? file) =>
        file is null || !file.IsValid || file.Declares(JumpListKnownCategory.Recent);

    /// <summary>Sık kullanılanlar yalnız uygulamanın listesi isterse gösterilir (Explorer gibi).</summary>
    public static bool ShowsFrequent(CustomDestinationsFile? file) =>
        file is not null && file.Declares(JumpListKnownCategory.Frequent);

    /// <summary>Bir bölüm türünün girdi sınırı (ayırıcılar sayılmaz).</summary>
    public static int CapFor(JumpListSectionKind kind) => kind switch
    {
        JumpListSectionKind.Custom => JumpListLimits.MaxPerCustomSection,
        JumpListSectionKind.Frequent => JumpListLimits.MaxFrequent,
        JumpListSectionKind.Recent => JumpListLimits.MaxRecent,
        _ => JumpListLimits.MaxTasks,
    };

    /// <summary>
    /// Ham bölümleri menü sırasına koyar ve sınırlar. Boş ve adsız özel kategoriler atılır; baştaki, sondaki ve art arda
    /// ayırıcılar temizlenir; bölüm başına ve toplam girdi sınırı uygulanır. Aynı türden (özel dışı) birden çok bölüm gelirse
    /// ilki alınır.
    /// </summary>
    public static IReadOnlyList<JumpListSection> Arrange(IEnumerable<JumpListSection> sections)
    {
        var custom = new List<Working>();
        Working? frequent = null, recent = null, tasks = null;

        foreach (var section in sections)
        {
            if (section is null)
                continue;

            var name = section.Kind == JumpListSectionKind.Custom
                ? JumpListText.Sanitize(section.Name, JumpListLimits.MaxCategoryNameChars)
                : null;
            if (section.Kind == JumpListSectionKind.Custom && string.IsNullOrEmpty(name))
                continue; // adsız kategori etiketlenemez

            var entries = Cap(Normalize(section.Entries), CapFor(section.Kind));
            if (CountEntries(entries) == 0)
                continue;

            var working = new Working(section.Kind, name, entries);
            switch (section.Kind)
            {
                case JumpListSectionKind.Custom:
                    if (custom.Count < JumpListLimits.MaxCustomSections)
                        custom.Add(working);
                    break;
                case JumpListSectionKind.Frequent:
                    frequent ??= working;
                    break;
                case JumpListSectionKind.Recent:
                    recent ??= working;
                    break;
                default:
                    tasks ??= working;
                    break;
            }
        }

        var ordered = new List<Working>(custom);
        if (frequent is not null) ordered.Add(frequent);
        if (recent is not null) ordered.Add(recent);
        if (tasks is not null) ordered.Add(tasks);

        TrimToTotal(ordered);

        return ordered
            .Select(w => new JumpListSection(w.Kind, w.Name, Normalize(w.Entries)))
            .Where(s => CountEntries(s.Entries) > 0)
            .ToList();
    }

    /// <summary>Ayırıcı olmayan girdi sayısı.</summary>
    public static int CountEntries(IEnumerable<JumpListEntry> entries) => entries.Count(e => !e.IsSeparator);

    /// <summary>Toplam sınırı aşılırsa önce sık, sonra son kullanılanlar, sonra özel kategoriler (sondan), en son görevler kırpılır.</summary>
    private static void TrimToTotal(List<Working> ordered)
    {
        var trimOrder = ordered
            .OrderBy(w => w.Kind switch
            {
                JumpListSectionKind.Frequent => 0,
                JumpListSectionKind.Recent => 1,
                JumpListSectionKind.Custom => 2,
                _ => 3,
            })
            .ThenByDescending(w => ordered.IndexOf(w))
            .ToList();

        while (ordered.Sum(w => CountEntries(w.Entries)) > JumpListLimits.MaxTotalEntries)
        {
            var victim = trimOrder.FirstOrDefault(w => CountEntries(w.Entries) > JumpListLimits.MinPerSectionWhenTrimming);
            if (victim is null)
                break;

            victim.Entries = Cap(victim.Entries, CountEntries(victim.Entries) - 1);
        }
    }

    /// <summary>İlk <paramref name="max"/> girdiyi (ayırıcılar dahil sırayla) tutar ve ayırıcıları temizler.</summary>
    private static List<JumpListEntry> Cap(IReadOnlyList<JumpListEntry> entries, int max)
    {
        var result = new List<JumpListEntry>();
        var count = 0;
        foreach (var entry in entries)
        {
            if (!entry.IsSeparator)
            {
                if (count >= max)
                    break;
                count++;
            }

            result.Add(entry);
        }

        return Normalize(result);
    }

    /// <summary>Baştaki, sondaki ve art arda ayırıcıları atar.</summary>
    private static List<JumpListEntry> Normalize(IReadOnlyList<JumpListEntry> entries)
    {
        var result = new List<JumpListEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.IsSeparator && (result.Count == 0 || result[^1].IsSeparator))
                continue;
            result.Add(entry);
        }

        while (result.Count > 0 && result[^1].IsSeparator)
            result.RemoveAt(result.Count - 1);

        return result;
    }

    private sealed class Working(JumpListSectionKind kind, string? name, List<JumpListEntry> entries)
    {
        public JumpListSectionKind Kind { get; } = kind;
        public string? Name { get; } = name;
        public List<JumpListEntry> Entries { get; set; } = entries;
    }
}
