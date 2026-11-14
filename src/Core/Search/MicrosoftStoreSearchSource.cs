using System.Globalization;

namespace Koy.Core.Search;

/// <summary>Microsoft Store çevrimiçi araması: "store &lt;terim&gt;" veya "mağaza &lt;terim&gt;".</summary>
public sealed class MicrosoftStoreSearchSource(ISearchShell shell, Func<string, string> t)
    : SearchSource("store", t("SearchCatStore"))
{
    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (!SearchPrefix.TryExtract(query.Raw, t("SearchKwStore"), out var term))
            return [];

        var escaped = Uri.EscapeDataString(term);
        var storeUri = "ms-windows-store://search/?query=" + escaped;
        var webUrl = "https://apps.microsoft.com/search?q=" + escaped;

        return
        [
            new SearchResult("store:" + term, Name, string.Format(CultureInfo.CurrentCulture, t("SearchStoreFor"), term), () => shell.Open(storeUri))
            {
                Subtitle = "Microsoft Store",
                Glyph = SearchGlyph.Store,
                CategoryRelevance = SearchRelevance.High,
                Relevance = 0.9,
                Drag = new SearchDragData(SearchDragKind.Url, webUrl),
                Actions = [new SearchAction("web", t("SearchActOpenWeb"), () => shell.Open(webUrl))]
            }
        ];
    }
}
