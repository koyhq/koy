using Koy.Core.Search;

namespace Koy.Core.Extensions.Store;

public enum StoreItemStatus
{
    NotInstalled,
    Installed,
    UpdateAvailable,

    /// <summary>Kurulum ya da güncelleme sürüyor (kimlik <see cref="StoreListState.BusyIds"/> içinde).</summary>
    Installing,
}

/// <summary>Süzgeç: Tümü / Kurulu / Güncellemeler.</summary>
public enum StoreFilter
{
    All,
    Installed,
    Updates,
}

public enum StoreSort
{
    Newest,
    Alphabetical,
    MostDownloads,
}

/// <param name="Category">null = bütün türler.</param>
/// <param name="Search">Ad, özet ve yazarda; büyük/küçük harf ve Türkçe harf duyarsız; her sözcük bulunmalı.</param>
public sealed record StoreQuery(
    StoreFilter Filter = StoreFilter.All,
    ExtensionKind? Category = null,
    string? Search = null,
    StoreSort Sort = StoreSort.Newest);

/// <summary>
/// Listede bir satır: indeks öğesi, kurulu kayıt ya da ikisinin birleşimi. <see cref="Item"/> yoksa eklenti dosyadan
/// kurulmuştur (ya da indeksten kalkmıştır) ve yalnız kurulu süzgecinde görünür.
/// </summary>
public sealed record StoreEntryView(
    string Id,
    string Name,
    ExtensionKind Kind,
    string Author,
    string Summary,
    string License,
    string? AvailableVersion,
    string? InstalledVersion,
    StoreItemStatus Status,
    bool IsInstalled,
    bool HasUpdate,
    bool Applied,
    DateOnly Updated,
    long Downloads,
    StoreItem? Item,
    InstalledEntry? Installed);

/// <summary>
/// Mağaza sayfasının girdileri: indeks (henüz çekilmediyse null), kurulu kayıt, işlemi süren kimlikler ve sorgu.
/// Değişmez; <see cref="Visible"/> her çağrıda hesaplar (en çok 500 + 64 satır).
/// </summary>
public sealed record StoreListState(
    StoreIndex? Index,
    InstalledRegistry Registry,
    IReadOnlySet<string> BusyIds,
    StoreQuery Query)
{
    public static StoreListState Initial { get; } = new(
        null, InstalledRegistry.Empty, new HashSet<string>(), new StoreQuery());

    /// <summary>Sorguya göre süzülmüş, sıralanmış satırlar.</summary>
    public IReadOnlyList<StoreEntryView> Visible() => StoreCatalog.Query(this);

    /// <summary>Süzgeçsiz bütün satırlar (sayaçlar için).</summary>
    public IReadOnlyList<StoreEntryView> All() => StoreCatalog.Merge(this);

    /// <summary>Güncellemesi olan kurulu eklenti sayısı ("Güncellemeler" sekmesindeki rozet).</summary>
    public int UpdateCount => All().Count(e => e.HasUpdate);
}

/// <summary>İndeks ile kurulu kaydı birleştirir; süzgeç, kategori, arama ve sıralama uygular (saf).</summary>
public static class StoreCatalog
{
    public static IReadOnlyList<StoreEntryView> Merge(StoreListState state)
    {
        var rows = new List<StoreEntryView>();
        var indexed = new HashSet<string>(StringComparer.Ordinal);

        if (state.Index is not null)
        {
            foreach (var item in state.Index.Items)
            {
                indexed.Add(item.Id);
                var installed = state.Registry.Find(item.Id);
                rows.Add(FromItem(item, installed, state.BusyIds.Contains(item.Id)));
            }
        }

        // Dosyadan kurulan (ya da indeksten kalkmış) kurulu eklentiler.
        foreach (var entry in state.Registry.Entries)
        {
            if (!indexed.Contains(entry.Id))
            {
                rows.Add(FromInstalledOnly(entry, state.BusyIds.Contains(entry.Id)));
            }
        }

        return rows;
    }

    public static IReadOnlyList<StoreEntryView> Query(StoreListState state)
    {
        var query = state.Query;
        IEnumerable<StoreEntryView> rows = Merge(state);

        rows = query.Filter switch
        {
            StoreFilter.Installed => rows.Where(r => r.IsInstalled),
            StoreFilter.Updates => rows.Where(r => r.HasUpdate),
            _ => rows,
        };

        if (query.Category is { } category)
        {
            rows = rows.Where(r => r.Kind == category);
        }

        var words = new SearchQuery(query.Search).Words;
        if (words.Count > 0)
        {
            rows = rows.Where(r => MatchesAll(r, words));
        }

        // Eşit anahtarda ada, sonra kimliğe göre: sıra her çağrıda aynı.
        var ordered = query.Sort switch
        {
            StoreSort.Alphabetical => rows.OrderBy(r => SearchText.Fold(r.Name), StringComparer.Ordinal),
            StoreSort.MostDownloads => rows.OrderByDescending(r => r.Downloads).ThenBy(r => SearchText.Fold(r.Name), StringComparer.Ordinal),
            _ => rows.OrderByDescending(r => r.Updated).ThenBy(r => SearchText.Fold(r.Name), StringComparer.Ordinal),
        };

        return ordered.ThenBy(r => r.Id, StringComparer.Ordinal).ToList().AsReadOnly();
    }

    private static StoreEntryView FromItem(StoreItem item, InstalledEntry? installed, bool busy)
    {
        // Aynı kimlik farklı türde ise güncelleme sayılmaz (kurulu olan başka bir şeydir).
        var hasUpdate = installed is not null
            && installed.Kind == item.Kind
            && ExtensionVersion.IsNewer(item.Version, installed.Version);

        return new StoreEntryView(
            item.Id, item.Name, item.Kind, item.Author, item.Summary, item.License,
            item.Version, installed?.Version,
            StatusOf(installed is not null, hasUpdate, busy),
            installed is not null, hasUpdate, installed?.Applied ?? false,
            item.Updated, item.Downloads, item, installed);
    }

    private static StoreEntryView FromInstalledOnly(InstalledEntry entry, bool busy) => new(
        entry.Id, entry.Name, entry.Kind, entry.Author, "", entry.License,
        null, entry.Version,
        StatusOf(true, false, busy),
        true, false, entry.Applied,
        DateOnly.FromDateTime(entry.InstalledAt.UtcDateTime), 0, null, entry);

    private static StoreItemStatus StatusOf(bool installed, bool hasUpdate, bool busy) =>
        busy ? StoreItemStatus.Installing
        : hasUpdate ? StoreItemStatus.UpdateAvailable
        : installed ? StoreItemStatus.Installed
        : StoreItemStatus.NotInstalled;

    private static bool MatchesAll(StoreEntryView row, IReadOnlyList<string> words)
    {
        var haystack = SearchText.Fold(row.Name) + "\n" + SearchText.Fold(row.Summary) + "\n" + SearchText.Fold(row.Author);
        foreach (var word in words)
        {
            if (!haystack.Contains(word, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
