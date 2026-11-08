namespace Koy.Core.Search;

/// <summary>
/// Sonuçları listeye dizer: aynı kategori bir arada, önce kategorinin yeri (uygulamalar üstte, dosyalar altta),
/// sonra önemi. Aşağıdaki kategori daha az yer alır:
/// ilk kategori sınırın en çok yarısını, ikincisi üçte birini kullanır; her kategoriye en az bir satır kalır.
/// </summary>
public static class SearchLayout
{
    /// <summary>Listedeki en fazla satır; çentikte görünmeyenlere kaydırılarak ulaşılır.</summary>
    public const int MaxResults = 15;

    public static IReadOnlyList<SearchResult> Arrange(IEnumerable<SearchResult> results, int limit = MaxResults)
    {
        var groups = results
            .GroupBy(r => r.Category)
            .Select(g => g.OrderByDescending(r => r.Relevance).ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase).ToList())
            .OrderBy(g => g.Min(r => r.Placement))
            .ThenByDescending(g => g.Max(r => r.CategoryRelevance))
            .ToList();

        if (groups.Count == 1)
            return groups[0].Take(limit).ToList();

        var arranged = new List<SearchResult>();
        var reserve = (int)Math.Ceiling(limit / (double)Math.Max(1, groups.Count));
        for (var i = 0; i < groups.Count; i++)
        {
            var share = (int)Math.Ceiling(limit / (double)(i + 2));
            var cap = Math.Max(1, Math.Min(limit - arranged.Count - reserve, share));
            arranged.AddRange(groups[i].Take(cap));
        }

        return arranged;
    }
}
