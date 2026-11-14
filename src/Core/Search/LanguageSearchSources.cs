using System.Globalization;
using Koy.Core.Diagnostics;

namespace Koy.Core.Search;

/// <summary>Yazım denetimi sonucu.</summary>
/// <param name="IsSupported">Sistemde uygun sözlük bulunabildi mi.</param>
/// <param name="IsCorrect">Metin yazım hatası içermiyor mu.</param>
/// <param name="Suggestions">Önerilen düzeltmeler.</param>
public sealed record SpellCheckResult(bool IsSupported, bool IsCorrect, IReadOnlyList<string> Suggestions);

public interface ISpellCheckerService
{
    SpellCheckResult Check(string text);

    void Release();
}

/// <summary>Sözlük anlamı.</summary>
public sealed record DictionaryMeaning(string PartOfSpeech, string Definition, string? Example = null);

/// <summary>Sözlük kaydı.</summary>
public sealed record DictionaryEntry(string Word, string? Phonetic, IReadOnlyList<DictionaryMeaning> Meanings, string? SourceUrl = null);

public interface IDictionaryService
{
    Task<DictionaryEntry?> LookupAsync(string word, CancellationToken cancellationToken = default);
}

internal static class SearchPrefix
{
    public static bool TryExtract(string raw, string semicolonSeparatedKeywords, out string term)
    {
        var trimmed = raw.Trim();
        foreach (var kw in semicolonSeparatedKeywords.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (trimmed.StartsWith(kw + " ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(kw + ":", StringComparison.OrdinalIgnoreCase))
            {
                term = trimmed[(kw.Length + 1)..].Trim();
                return term.Length > 0;
            }
        }

        term = "";
        return false;
    }
}

/// <summary>Yazım denetimi: "spell &lt;kelime&gt;" veya "yazım &lt;kelime&gt;". Enter öneriyi panoya kopyalar.</summary>
public sealed class SpellCheckSource(ISpellCheckerService service, ISearchShell shell, Func<string, string> t)
    : SearchSource("spell", t("SearchCatSpellCheck"))
{
    private const int MaxSuggestions = 8;

    public override void Release() => service.Release();

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (!SearchPrefix.TryExtract(query.Raw, t("SearchKwSpellCheck"), out var term))
            return [];

        var check = service.Check(term);
        if (!check.IsSupported)
            return [];

        if (check.IsCorrect)
        {
            return
            [
                new SearchResult("spell:ok:" + term, Name, $"✓ {term}", () => shell.CopyText(term))
                {
                    Subtitle = t("SearchSpellCorrect"),
                    Glyph = SearchGlyph.SpellCheck,
                    CategoryRelevance = SearchRelevance.Highest,
                    Relevance = 1,
                    RestoresFocus = true,
                    Drag = new SearchDragData(SearchDragKind.Text, term)
                }
            ];
        }

        if (check.Suggestions.Count == 0)
        {
            return
            [
                new SearchResult("spell:none:" + term, Name, $"✗ {term}", () => { })
                {
                    Subtitle = t("SearchSpellNoSuggestions"),
                    Glyph = SearchGlyph.SpellCheck,
                    CategoryRelevance = SearchRelevance.Highest,
                    Relevance = 0.8
                }
            ];
        }

        return check.Suggestions.Take(MaxSuggestions).Select((suggestion, i) =>
            new SearchResult("spell:" + suggestion, Name, suggestion, () => shell.CopyText(suggestion))
            {
                Subtitle = $"{string.Format(CultureInfo.CurrentCulture, t("SearchSpellSuggestionFor"), term)} · {t("SearchHintCopy")}",
                Glyph = SearchGlyph.SpellCheck,
                CategoryRelevance = SearchRelevance.Highest,
                Relevance = 1.0 - (i * 0.05),
                RestoresFocus = true,
                Drag = new SearchDragData(SearchDragKind.Text, suggestion)
            });
    }
}

/// <summary>Sözlük araması: "dict &lt;kelime&gt;" veya "sözlük &lt;kelime&gt;". Boşta ağ isteği yapmaz, yazma durunca sorgular.</summary>
public sealed class DictionarySource(
    IDictionaryService service,
    ISearchShell shell,
    Func<string, string> t,
    CultureInfo? culture = null)
    : SearchSource("dictionary", t("SearchCatDictionary"))
{
    private const int MaxMeanings = 6;
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(220);

    private readonly bool _turkish = string.Equals((culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName, "tr", StringComparison.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private CancellationTokenSource? _running;
    private string _pending = "";
    private string _ready = "";
    private DictionaryEntry? _entry;

    public override bool IsBusy
    {
        get
        {
            lock (_gate)
                return _running is not null;
        }
    }

    public override void Release()
    {
        lock (_gate)
        {
            CancelLocked();
            _ready = "";
            _entry = null;
        }
    }

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (!SearchPrefix.TryExtract(query.Raw, t("SearchKwDictionary"), out var word))
        {
            lock (_gate)
                CancelLocked();
            return [];
        }

        DictionaryEntry? entry;
        bool ready;
        lock (_gate)
        {
            if (!string.Equals(_ready, word, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(_pending, word, StringComparison.OrdinalIgnoreCase))
            {
                StartLocked(word);
            }

            ready = string.Equals(_ready, word, StringComparison.OrdinalIgnoreCase);
            entry = _entry;
        }

        if (ready && entry is { Meanings.Count: > 0 })
            return BuildResults(entry);

        if (ready)
            return [OnlineFallback(word)];

        return [];
    }

    private IEnumerable<SearchResult> BuildResults(DictionaryEntry entry)
    {
        var openUrl = entry.SourceUrl ?? OnlineUrl(entry.Word);
        var count = Math.Min(entry.Meanings.Count, MaxMeanings);
        for (var i = 0; i < count; i++)
        {
            var meaning = entry.Meanings[i];
            var header = string.IsNullOrWhiteSpace(entry.Phonetic)
                ? $"{entry.Word} ({meaning.PartOfSpeech})"
                : $"{entry.Word} {entry.Phonetic} ({meaning.PartOfSpeech})";
            var copyText = $"{entry.Word}: {meaning.Definition}";

            yield return new SearchResult($"dict:{entry.Word}:{i}", Name, meaning.Definition, () => shell.CopyText(copyText))
            {
                Subtitle = string.IsNullOrWhiteSpace(meaning.Example) ? header : $"{header} · \"{meaning.Example}\"",
                Glyph = SearchGlyph.Dictionary,
                CategoryRelevance = SearchRelevance.Highest,
                Relevance = 1.0 - (i * 0.05),
                RestoresFocus = true,
                Drag = new SearchDragData(SearchDragKind.Text, copyText),
                Actions = [new SearchAction("open", t("SearchActOpenWeb"), () => shell.Open(openUrl))]
            };
        }
    }

    private SearchResult OnlineFallback(string word)
    {
        var url = OnlineUrl(word);
        return new SearchResult("dict:web:" + word, Name, string.Format(CultureInfo.CurrentCulture, t("SearchDictWebFor"), word), () => shell.Open(url))
        {
            Subtitle = url,
            Glyph = SearchGlyph.Dictionary,
            CategoryRelevance = SearchRelevance.High,
            Relevance = 0.7,
            Drag = new SearchDragData(SearchDragKind.Url, url)
        };
    }

    private string OnlineUrl(string word)
    {
        var escaped = Uri.EscapeDataString(word);
        return _turkish
            ? $"https://sozluk.gov.tr/?ara={escaped}"
            : $"https://www.merriam-webster.com/dictionary/{escaped}";
    }

    private void CancelLocked()
    {
        _running?.Cancel();
        _running = null;
        _pending = "";
    }

    private void StartLocked(string word)
    {
        _running?.Cancel();
        var cts = new CancellationTokenSource();
        _running = cts;
        _pending = word;
        _ = RunAsync(word, cts);
    }

    private async Task RunAsync(string word, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(Debounce, cts.Token).ConfigureAwait(false);
            var entry = await service.LookupAsync(word, cts.Token).ConfigureAwait(false);
            if (cts.IsCancellationRequested)
                return;

            lock (_gate)
            {
                if (_running != cts)
                    return;
                _ready = word;
                _entry = entry;
                _pending = "";
                _running = null;
            }

            RaiseChanged();
        }
        catch (OperationCanceledException)
        {
            // Yeni sorgu geldi veya panel kapandı.
        }
        catch (Exception ex)
        {
            CrashLog.Write("Sözlük araması", ex);
            lock (_gate)
            {
                if (_running == cts)
                {
                    _ready = word;
                    _entry = null;
                    _pending = "";
                    _running = null;
                }
            }

            RaiseChanged();
        }
        finally
        {
            cts.Dispose();
        }
    }
}
