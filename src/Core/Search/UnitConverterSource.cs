using System.Globalization;

namespace Koy.Core.Search;

/// <summary>
/// Birim çevirici: "10 km to mi", "100 c in f", "1 gb in mb". Ağ yok; Enter yalnız sayıyı (kültüre göre, binliksiz)
/// panoya kopyalar, satırda birimli hâli görünür.
/// </summary>
public sealed class UnitConverterSource(ISearchShell shell, Func<string, string> t, CultureInfo? culture = null)
    : SearchSource("units", t("SearchCatUnits"))
{
    private readonly CultureInfo _culture = culture ?? CultureInfo.CurrentCulture;

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (!UnitConverter.TryConvert(query.Raw, _culture, out var conversion))
            return [];

        var number = UnitConverter.FormatNumber(conversion.Value, _culture, grouping: false);
        if (number.Length == 0)
            return [];

        var input = UnitConverter.FormatNumber(conversion.Input, _culture, grouping: true);
        var title = $"{UnitConverter.FormatNumber(conversion.Value, _culture, grouping: true)} {conversion.To.Symbol}";
        return
        [
            new SearchResult("unit:" + query.Raw, Name, title, () => shell.CopyText(number))
            {
                Subtitle = $"{input} {conversion.From.Symbol} · {t("SearchHintCopy")}",
                Glyph = SearchGlyph.Calculator,
                CategoryRelevance = SearchRelevance.Highest,
                Relevance = 1,
                RestoresFocus = true,
                Drag = new SearchDragData(SearchDragKind.Text, number)
            }
        ];
    }
}
