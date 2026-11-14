using System.Collections.Concurrent;
using Koy.Core.Diagnostics;
using Koy.Core.JumpList;

namespace Koy.Core.Search;

/// <summary>
/// Kurulu uygulamaların Windows Atlama Listesi (Jump List) görev ve son öğelerini arar.
/// Yalnız sorguyla eşleşen en üstteki birkaç uygulama için tembel yükleme yapar; boşta çalışmaz.
/// </summary>
public sealed class JumpListSearchSource(
    IInstalledAppsService appsService,
    IJumpListService jumpListService,
    Func<string, string> t,
    Func<InstalledApp, byte[]?>? loadIcon = null)
    : SearchSource("jumplist", t("SearchCatJumpList")), IDisposable
{
    private const int MinQueryLength = 2;
    private const int MaxAppsToFetch = 3;
    private const int MaxResults = 8;

    private sealed record AppEntry(InstalledApp App, string FoldedName, string SecondaryKey);
    private sealed record CachedAction(InstalledApp App, string SectionLabel, JumpListEntry Entry, string FoldedTitle);

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<CachedAction>> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _fetching = new(StringComparer.OrdinalIgnoreCase);
    private List<AppEntry> _apps = [];
    private CancellationTokenSource? _cts;

    public override void Prepare()
    {
        lock (_gate)
        {
            if (_cts is not null)
                return;
            _cts = new CancellationTokenSource();
        }

        _ = LoadAppsAsync(_cts);
    }

    public override void Release()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _cts;
            _cts = null;
            _fetching.Clear();
        }

        cts?.Cancel();
        cts?.Dispose();
        _loaded.Clear();
    }

    public void Dispose() => Release();

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (query.Folded.Length < MinQueryLength || _apps.Count == 0)
            return [];

        var firstWord = FirstWord(query.Folded);
        var appQuery = firstWord.Length >= MinQueryLength ? new SearchQuery(firstWord) : query;

        var matchingApps = _apps
            .Select(a => (App: a, Score: Math.Max(
                Math.Max(SearchMatcher.Score(query, a.FoldedName), SearchMatcher.Score(query, a.SecondaryKey) * 0.6),
                Math.Max(SearchMatcher.Score(appQuery, a.FoldedName), SearchMatcher.Score(appQuery, a.SecondaryKey) * 0.6) * 0.85)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(MaxAppsToFetch)
            .ToList();

        foreach (var (appEntry, _) in matchingApps)
            EnsureFetched(appEntry.App);

        var remainder = firstWord.Length < query.Folded.Length
            ? new SearchQuery(query.Folded[(firstWord.Length + 1)..].Trim())
            : null;

        var results = new List<(SearchResult Result, double Score)>();
        foreach (var kvp in _loaded)
        {
            foreach (var item in kvp.Value)
            {
                var appScore = Math.Max(
                    SearchMatcher.Score(appQuery, SearchText.Fold(item.App.Name)),
                    SearchMatcher.Score(query, SearchText.Fold(item.App.Name)));
                var titleScore = remainder is not null
                    ? Math.Max(SearchMatcher.Score(remainder, item.FoldedTitle), SearchMatcher.Score(query, item.FoldedTitle))
                    : SearchMatcher.Score(query, item.FoldedTitle);

                var combined = remainder is not null && appScore > 0
                    ? (titleScore > 0 ? Math.Max(titleScore, appScore * 0.7) : appScore * 0.45)
                    : Math.Max(titleScore, appScore * 0.45);

                if (combined <= 0)
                    continue;

                var entry = item.Entry;
                var app = item.App;
                results.Add((new SearchResult(
                    "jumplist:" + app.Id + ":" + entry.Title,
                    Name,
                    entry.Title,
                    () => jumpListService.Launch(entry, appExePath: null))
                {
                    Subtitle = string.IsNullOrEmpty(item.SectionLabel) ? app.Name : $"{app.Name} · {item.SectionLabel}",
                    Glyph = SearchGlyph.App,
                    CategoryRelevance = titleScore >= 0.8 ? SearchRelevance.High : SearchRelevance.Low,
                    Relevance = combined,
                    LoadIcon = loadIcon is null ? null : () => loadIcon(app),
                    Drag = BuildDrag(entry)
                }, combined));
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(MaxResults)
            .Select(r => r.Result);
    }

    private void EnsureFetched(InstalledApp app)
    {
        if (_loaded.ContainsKey(app.Id))
            return;

        CancellationToken token;
        lock (_gate)
        {
            if (_cts is null || !_fetching.Add(app.Id))
                return;
            token = _cts.Token;
        }

        _ = FetchAppJumpListAsync(app, token);
    }

    private async Task LoadAppsAsync(CancellationTokenSource cts)
    {
        try
        {
            var apps = await appsService.LoadAsync(cts.Token).ConfigureAwait(false);
            if (cts.IsCancellationRequested)
                return;

            _apps = apps
                .Where(AppSearchSource.ShouldList)
                .DistinctBy(a => SearchText.Fold(a.Name))
                .Select(a => new AppEntry(a, SearchText.Fold(a.Name), AppSearchSource.SecondaryKey(a.Id)))
                .ToList();
            RaiseChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CrashLog.Write("Atlama listesi uygulamaları", ex);
        }
    }

    private async Task FetchAppJumpListAsync(InstalledApp app, CancellationToken token)
    {
        try
        {
            var sections = await jumpListService.GetAsync(app.Id, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return;

            var list = new List<CachedAction>();
            foreach (var section in sections)
            {
                var label = section.Kind == JumpListSectionKind.Custom && !string.IsNullOrWhiteSpace(section.Name)
                    ? section.Name!
                    : "";
                foreach (var entry in section.Entries)
                {
                    if (entry.IsSeparator || string.IsNullOrWhiteSpace(entry.Title))
                        continue;
                    list.Add(new CachedAction(app, label, entry, SearchText.Fold(entry.Title)));
                }
            }

            _loaded[app.Id] = list;
            if (list.Count > 0)
                RaiseChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CrashLog.Write("Atlama listesi okuma", ex);
        }
        finally
        {
            lock (_gate)
                _fetching.Remove(app.Id);
        }
    }

    private static string FirstWord(string text)
    {
        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }

    private static SearchDragData? BuildDrag(JumpListEntry entry)
    {
        var target = entry.Launch?.Target;
        if (string.IsNullOrWhiteSpace(target))
            return null;

        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new SearchDragData(SearchDragKind.Url, target);

        if (entry.Launch!.IsDocument && Path.IsPathRooted(target))
            return new SearchDragData(SearchDragKind.File, target);

        return null;
    }
}
