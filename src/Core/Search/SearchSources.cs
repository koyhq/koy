using System.Globalization;
using Koy.Core.Clipboard;
using Koy.Core.Diagnostics;
using Koy.Core.Shell;

namespace Koy.Core.Search;

/// <summary>Kurulu uygulamalar. Liste ~1 sn sürdüğü için panel açılınca arka planda tazelenir, eski kopya hemen kullanılır.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "_loading'i LoadAsync bitince kendisi atar.")]
public sealed class AppSearchSource(
    IInstalledAppsService service,
    Action<InstalledApp> launch,
    Func<string, string> t,
    Func<InstalledApp, byte[]?>? loadIcon = null) : SearchSource("apps", t("SearchCatApps"))
{
    private const double SecondaryKeyWeight = 0.6;

    // Başlat menüsündeki belge kısayolları (benioku, yardım) uygulama sayılmaz.
    private static readonly string[] DocumentExtensions = [".txt", ".pdf", ".htm", ".html", ".chm", ".rtf"];

    private sealed record Entry(InstalledApp App, string Name, string Key);

    private IReadOnlyList<Entry> _entries = [];
    private CancellationTokenSource? _loading;

    public override void Prepare()
    {
        if (_loading is not null)
            return;

        _loading = new CancellationTokenSource();
        _ = LoadAsync(_loading);
    }

    public override void Release()
    {
        _loading?.Cancel();
        _loading = null;
    }

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        foreach (var entry in _entries)
        {
            var score = Math.Max(SearchMatcher.Score(query, entry.Name), SearchMatcher.Score(query, entry.Key) * SecondaryKeyWeight);
            if (score <= 0)
                continue;

            var app = entry.App;
            yield return new SearchResult("app:" + app.Id, Name, app.Name, () => launch(app))
            {
                Glyph = SearchGlyph.App,
                Relevance = score,
                Placement = SearchCategoryPlacement.Top,
                CategoryRelevance = entry.Name == query.Folded ? SearchRelevance.Highest : SearchRelevance.Moderate,
                LoadIcon = loadIcon is null ? null : () => loadIcon(app)
            };
        }
    }

    /// <summary>Listeye girecek mi: belge kısayolları ve kaldırıcılar uygulama değildir.</summary>
    public static bool ShouldList(InstalledApp app)
    {
        if (string.IsNullOrWhiteSpace(app.Name))
            return false;

        var name = SearchText.Fold(app.Name);
        if (name.Contains("uninstall", StringComparison.Ordinal) || name.Contains("kaldir", StringComparison.Ordinal))
            return false;

        return !DocumentExtensions.Any(e => app.Id.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// İkinci arama anahtarı: Türkçe Windows'ta ad "Hesap Makinesi" olsa da "calc" yazan bulsun. Paketli uygulamada
    /// paket adı, klasik uygulamada exe adı.
    /// </summary>
    public static string SecondaryKey(string id)
    {
        var bang = id.IndexOf('!');
        if (bang > 0)
        {
            var package = id[..bang];
            var underscore = package.IndexOf('_');
            return SearchText.Fold(underscore > 0 ? package[..underscore] : package);
        }

        var name = id[(id.LastIndexOfAny(['\\', '/']) + 1)..];
        var dot = name.LastIndexOf('.');
        return SearchText.Fold(dot > 0 ? name[..dot] : name);
    }

    private async Task LoadAsync(CancellationTokenSource loading)
    {
        try
        {
            var apps = await service.LoadAsync(loading.Token).ConfigureAwait(false);
            if (loading.IsCancellationRequested)
                return;

            _entries = apps
                .Where(ShouldList)
                .DistinctBy(a => SearchText.Fold(a.Name))
                .Select(a => new Entry(a, SearchText.Fold(a.Name), SecondaryKey(a.Id)))
                .ToList();
            RaiseChanged();
        }
        catch (OperationCanceledException)
        {
            // Panel kapandı; önceki liste kalır.
        }
        catch (Exception ex)
        {
            CrashLog.Write("Uygulama listesi", ex);
        }
        finally
        {
            // Kaynağın sahibi yükleme görevidir: Release yalnız iptal eder, yükleme hâlâ token'ı kullanıyor olabilir.
            loading.Dispose();
        }
    }
}

/// <summary>Açık pencereye geçiş. Pencere listesi panel açılırken bir kez alınır.</summary>
public sealed class WindowSearchSource(Func<IReadOnlyList<OpenWindowInfo>> windows, Action<nint> activate, Func<string, string> t)
    : SearchSource("windows", t("SearchCatWindows"))
{
    private const int MinQueryLength = 2;
    private const double ProcessNameWeight = 0.6;

    private sealed record Entry(OpenWindowInfo Window, string Title, string Process);

    private IReadOnlyList<Entry> _entries = [];

    public override void Prepare() =>
        _entries = windows().Where(w => !string.IsNullOrWhiteSpace(w.Title)).Select(Describe).ToList();

    public override void Release() => _entries = [];

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (query.Folded.Length < MinQueryLength)
            yield break;

        foreach (var entry in _entries)
        {
            var score = Math.Max(SearchMatcher.Score(query, entry.Title), SearchMatcher.Score(query, entry.Process) * ProcessNameWeight);
            if (score <= 0)
                continue;

            var window = entry.Window;
            var icon = window.IconData;
            yield return new SearchResult("window:" + window.Handle, Name, window.Title, () => activate(window.Handle))
            {
                Subtitle = ProcessDisplayName(window.ProcessPath),
                Glyph = SearchGlyph.Window,
                Relevance = score,
                CategoryRelevance = SearchRelevance.Moderate,
                LoadIcon = icon is null ? null : () => icon
            };
        }
    }

    private static Entry Describe(OpenWindowInfo window) =>
        new(window, SearchText.Fold(window.Title), SearchText.Fold(ProcessDisplayName(window.ProcessPath)));

    private static string ProcessDisplayName(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        var name = path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }
}

/// <summary>Hesap. Enter sonucu yazı kutusuna alır (devam edilebilsin), ilk eylem panoya kopyalar.</summary>
public sealed class CalculatorSearchSource(ISearchShell shell, Func<string, string> t, CultureInfo? culture = null)
    : SearchSource("calculator", t("SearchCatCalculator"))
{
    private readonly CultureInfo _culture = culture ?? CultureInfo.CurrentCulture;

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (!ExpressionCalculator.TryCalculate(query.Raw, _culture, out var display, out var copy))
            return [];

        return
        [
            new SearchResult("calc:" + query.Raw, Name, display, () => { })
            {
                Glyph = SearchGlyph.Calculator,
                CategoryRelevance = SearchRelevance.High,
                Relevance = 1,
                ReplacesQuery = copy,
                Actions = [new SearchAction("copy", t("SearchActCopy"), () => shell.CopyText(copy))],
                RestoresFocus = true
            }
        ];
    }
}

public sealed record SystemCommandEntry(SystemCommand Command, string Title, IReadOnlyList<string> Keywords);

/// <summary>Kilitle, uyku, oturumu kapat, kullanıcı değiştir, yeniden başlat, kapat.</summary>
public sealed class SessionSearchSource(ISystemCommandService service, IReadOnlyList<SystemCommandEntry> entries, Func<string, string> t)
    : SearchSource("session", t("SearchCatSession"))
{
    public static bool NeedsConfirmation(SystemCommand command) =>
        command is SystemCommand.Restart or SystemCommand.Shutdown or SystemCommand.SignOut;

    /// <summary>Tam eşleşme 1; anahtar sözcüğün başıysa yazılan oran; başka durumda 0.</summary>
    public static double KeywordScore(IEnumerable<string> keywords, string foldedTerm)
    {
        var best = 0.0;
        foreach (var keyword in keywords.Select(SearchText.Fold))
        {
            if (keyword == foldedTerm)
                return 1;

            if (foldedTerm.Length > 0 && keyword.StartsWith(foldedTerm, StringComparison.Ordinal))
                best = Math.Max(best, (double)foldedTerm.Length / keyword.Length);
        }

        return best;
    }

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        foreach (var entry in entries)
        {
            var score = KeywordScore(entry.Keywords, query.Folded);
            if (score <= 0)
                continue;

            yield return new SearchResult("session:" + entry.Command, Name, entry.Title, () => service.Execute(entry.Command))
            {
                Glyph = SearchGlyph.Power,
                CategoryRelevance = score >= 1 ? SearchRelevance.Highest : SearchRelevance.Low,
                Relevance = score >= 1 ? 0.9 : 0.9 * score,
                RequiresConfirmation = NeedsConfirmation(entry.Command)
            };
        }
    }
}

/// <summary>Pano geçmişi. İçerik ekrana gelir, bu yüzden tek harfte listelenmez.</summary>
public sealed class ClipboardSearchSource(IClipboardHistoryService service, Func<string, string> t) : SearchSource("clipboard", t("SearchCatClipboard"))
{
    private const int MinQueryLength = 3;
    private const int MaxResults = 3;
    private const int MaxTitleLength = 80;

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (query.Raw.Length < MinQueryLength)
            return [];

        return service.Query(ClipboardFilter.All, query.Raw)
            .Take(MaxResults)
            .Select(item => new SearchResult("clip:" + item.Id, Name, TitleOf(item.Text), () => service.CopyBack(item.Id))
            {
                Subtitle = t("SearchHintCopy"),
                Glyph = SearchGlyph.Clipboard,
                CategoryRelevance = SearchRelevance.Low,
                Relevance = 0.4,
                RestoresFocus = true
            })
            .ToList();
    }

    private static string TitleOf(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= MaxTitleLength ? line : line[..MaxTitleLength] + "…";
    }
}

/// <summary>"tarih" ve "saat": şimdiki değeri gösterir, Enter panoya kopyalar.</summary>
public sealed class DateTimeSearchSource(ISearchShell shell, Func<string, string> t, Func<DateTime>? now = null, CultureInfo? culture = null)
    : SearchSource("datetime", t("SearchCatDateTime"))
{
    private readonly CultureInfo _culture = culture ?? CultureInfo.CurrentCulture;
    private readonly Func<DateTime> _now = now ?? (() => DateTime.Now);

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        var dateWords = Keywords("SearchKwDate");
        var timeWords = Keywords("SearchKwTime");
        if (dateWords.Contains(query.Folded))
            yield return Row("date", _now().ToString("D", _culture));
        else if (timeWords.Contains(query.Folded))
            yield return Row("time", _now().ToString("T", _culture));
    }

    private List<string> Keywords(string key) => t(key).Split(';', StringSplitOptions.RemoveEmptyEntries).Select(SearchText.Fold).ToList();

    private SearchResult Row(string id, string text) => new("datetime:" + id, Name, text, () => shell.CopyText(text))
    {
        Subtitle = t("SearchHintCopy"),
        Glyph = SearchGlyph.Clock,
        CategoryRelevance = SearchRelevance.Highest,
        Relevance = 1,
        RestoresFocus = true
    };
}

/// <summary>"?" ya da "yardım": kaynakların listesi; "?ad" o kaynağın kullanım örnekleri.</summary>
public sealed class HelpSearchSource(Func<IReadOnlyList<ISearchSource>> sources, Func<string, string> t) : SearchSource("help", t("SearchCatHelp"))
{
    private const string Trigger = "?";
    private const int MinExtendedLength = 3;

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        var filter = StripTrigger(query);
        if (filter is null)
            return [];

        var documented = sources().Where(s => !ReferenceEquals(s, this) && s.Syntaxes.Count > 0).ToList();
        var matching = filter.Length == 0
            ? documented
            : documented.Where(s => SearchText.Fold(s.Name).Contains(filter, StringComparison.Ordinal) || s.Id.Contains(filter, StringComparison.Ordinal)).ToList();

        if (matching.Count == 1 && filter.Length >= MinExtendedLength)
            return Extended(matching[0]);

        return matching.Select(source => new SearchResult("help:" + source.Id, Name, source.Syntaxes[0].Example, () => { })
        {
            Subtitle = source.Name,
            Glyph = SearchGlyph.Help,
            CategoryRelevance = SearchRelevance.Low,
            ReplacesQuery = Trigger + source.Name,
            SkipHistory = true
        }).ToList();
    }

    private static IEnumerable<SearchResult> Extended(ISearchSource source) => source.Syntaxes.Select((syntax, index) =>
        new SearchResult("help:" + source.Id + ":" + index, source.Name, syntax.Example, () => { })
        {
            Subtitle = syntax.Description,
            Glyph = SearchGlyph.Help,
            CategoryRelevance = SearchRelevance.Moderate,
            Relevance = 1.0 / (index + 1),
            ReplacesQuery = syntax.Example,
            SkipHistory = true
        });

    /// <summary>Tetikleyici varsa ardındaki süzgeç metni, yoksa null.</summary>
    private string? StripTrigger(SearchQuery query)
    {
        if (query.Folded.StartsWith(Trigger, StringComparison.Ordinal))
            return query.Folded[Trigger.Length..].Trim();

        var word = SearchText.Fold(t("SearchKwHelp"));
        return query.Folded == word ? "" : query.Folded.StartsWith(word + " ", StringComparison.Ordinal) ? query.Folded[(word.Length + 1)..].Trim() : null;
    }
}
