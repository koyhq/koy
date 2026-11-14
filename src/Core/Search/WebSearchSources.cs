using System.Text.Json;
using Koy.Core.Diagnostics;

namespace Koy.Core.Search;

public sealed record WebShortcut(string Key, string Provider, string UrlTemplate);

/// <summary>
/// "gg:kedi" ya da "!gg kedi": seçilen sitede arar. Hiç sonuç yoksa varsayılan tarayıcının kendi arama motoruyla aranır
/// (<see cref="DefaultBrowserSearch"/>); tarayıcı tanınmazsa DuckDuckGo.
/// </summary>
public sealed class WebShortcutSearchSource(
    IReadOnlyList<WebShortcut> shortcuts,
    ISearchShell shell,
    Func<string, string> t,
    Func<DefaultBrowser?>? defaultBrowser = null) : SearchSource("web", t("SearchCatWeb"))
{
    public static IReadOnlyList<WebShortcut> Defaults { get; } =
    [
        new("gg", "Google", "https://www.google.com/search?q={0}"),
        new("ddg", "DuckDuckGo", "https://duckduckgo.com/?q={0}"),
        new("bi", "Bing", "https://www.bing.com/search?q={0}"),
        new("yt", "YouTube", "https://www.youtube.com/results?search_query={0}"),
        new("wp", "Wikipedia", "https://www.wikipedia.org/search-redirect.php?search={0}"),
        new("gh", "GitHub", "https://github.com/search?q={0}"),
        new("so", "Stack Overflow", "https://stackoverflow.com/search?q={0}")
    ];

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (!TryParse(query.Raw, out var key, out var term))
            return [];

        var shortcut = shortcuts.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
        return shortcut is null ? [] : [Row(shortcut, term)];
    }

    /// <summary>Hiç sonuç yokken arama satırı: varsayılan tarayıcının motoru, tanınmazsa DuckDuckGo.</summary>
    public SearchResult? Fallback(SearchQuery query)
    {
        if (query.IsEmpty)
            return null;

        // Kayıt defteri okuması ucuz ve yalnız sonuçsuz arama bitince yapılır; kullanıcı tarayıcıyı değiştirmiş olabilir.
        if (defaultBrowser?.Invoke() is { } browser && DefaultBrowserSearch.SearchCommand(browser, query.Raw) is { } command)
            return Row("browser", browser.Name, query.Raw, () => shell.RunWithArguments(command.Executable, command.Arguments));

        var shortcut = shortcuts.FirstOrDefault(s => s.Key == "ddg") ?? (shortcuts.Count > 0 ? shortcuts[0] : null);
        return shortcut is null ? null : Row(shortcut, query.Raw);
    }

    public static bool TryParse(string text, out string key, out string term)
    {
        key = term = "";
        if (text.StartsWith('!'))
        {
            var space = text.IndexOf(' ');
            if (space <= 1)
                return false;

            key = text[1..space];
            term = text[(space + 1)..].Trim();
            return term.Length > 0;
        }

        var colon = text.IndexOf(':');
        if (colon <= 0 || text[..colon].Contains(' '))
            return false;

        key = text[..colon];
        term = text[(colon + 1)..].Trim();

        // "C:\klasör" ya da "http://..." kısayol değildir.
        return term.Length > 0 && !term.StartsWith('\\') && !term.StartsWith('/');
    }

    private SearchResult Row(WebShortcut shortcut, string term)
    {
        var url = string.Format(System.Globalization.CultureInfo.InvariantCulture, shortcut.UrlTemplate, Uri.EscapeDataString(term));
        return Row(shortcut.Key, shortcut.Provider, term, () => shell.Open(url));
    }

    private SearchResult Row(string key, string provider, string term, Action execute)
    {
        return new SearchResult("web:" + key + ":" + term, Name, string.Format(System.Globalization.CultureInfo.CurrentCulture, t("SearchWebFor"), provider, term), execute)
        {
            Glyph = SearchGlyph.Globe,
            CategoryRelevance = SearchRelevance.Highest,
            Relevance = 0.9
        };
    }
}

/// <summary>Tarayıcı yer imleri (Chrome ailesi). Panel açılınca arka planda okunur.</summary>
public sealed class BookmarksSearchSource(Func<IReadOnlyList<BrowserBookmark>> load, ISearchShell shell, Func<string, string> t)
    : SearchSource("bookmarks", t("SearchCatBookmarks"))
{
    private const int MinQueryLength = 3;

    private IReadOnlyList<BrowserBookmark> _bookmarks = [];

    public override void Prepare() => _ = LoadAsync();

    public override void Release() => _bookmarks = [];

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        var all = query.Folded == SearchText.Fold(t("SearchKwBookmarks"));
        if (!all && query.Folded.Length < MinQueryLength)
            yield break;

        foreach (var bookmark in _bookmarks)
        {
            var title = SearchText.Fold(bookmark.Title);
            var score = all ? 0.5 : Math.Max(SearchMatcher.Score(query, title), SearchMatcher.Score(query, SearchText.Fold(bookmark.Url)) * 0.6);
            if (score <= 0)
                continue;

            var url = bookmark.Url;
            yield return new SearchResult("url:" + url, Name, bookmark.Title, () => shell.Open(url))
            {
                Subtitle = url,
                Glyph = SearchGlyph.Bookmark,
                CategoryRelevance = title == query.Folded ? SearchRelevance.High : SearchRelevance.Moderate,
                Relevance = score,
                Drag = new SearchDragData(SearchDragKind.Url, url)
            };
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            _bookmarks = await Task.Run(load).ConfigureAwait(false);
            RaiseChanged();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Yer imleri", ex);
        }
    }
}

/// <summary>Chrome, Edge ve Brave'in <c>Bookmarks</c> dosyasını okur. Bozuk dosyada boş döner.</summary>
public static class ChromiumBookmarks
{
    private const int MaxDepth = 64;
    private const int MaxBookmarks = 5000;

    public static IReadOnlyList<BrowserBookmark> Parse(string json)
    {
        var found = new List<BrowserBookmark>();
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxDepth * 2 });
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("roots", out var roots) && roots.ValueKind == JsonValueKind.Object)
            {
                foreach (var root in roots.EnumerateObject())
                    Collect(root.Value, found, 0);
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return found;
    }

    private static void Collect(JsonElement node, List<BrowserBookmark> found, int depth)
    {
        if (depth > MaxDepth || found.Count >= MaxBookmarks || node.ValueKind != JsonValueKind.Object)
            return;

        if (Text(node, "type") == "url" && Text(node, "url") is { Length: > 0 } url
            && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            found.Add(new BrowserBookmark(Text(node, "name") ?? url, url));
            return;
        }

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
                Collect(child, found, depth + 1);
        }
    }

    private static string? Text(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
