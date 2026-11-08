using Koy.Core.Diagnostics;

namespace Koy.Core.Search;

/// <summary>Yazılanın ilk sözcüğü PATH'te bir programsa "Çalıştır" önerir.</summary>
public sealed class ShellCommandSearchSource(ISearchShell shell, Func<string, string> t) : SearchSource("shell", t("SearchCatShell"))
{
    private static readonly string[] DefaultExtensions = [".com", ".exe", ".bat", ".cmd"];

    // Her tuşta PATH × PATHEXT taranmasın: çözülen (ya da bulunamayan) komutlar panel oturumu boyunca saklanır.
    private readonly Dictionary<string, string?> _resolved = new(StringComparer.OrdinalIgnoreCase);

    public override void Release() => _resolved.Clear();

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        // Kabuğa satır sonu hiç gitmez; pano ve çok satırlı metin ayrıca onay ister.
        var line = SearchCommandSafety.ToSingleLine(query.Raw);
        var (command, arguments) = SplitCommand(line);
        if (command.Length == 0 || FindExecutable(command) is not { } executable)
            return [];

        var confirm = query.NeedsCommandConfirmation;
        var shown = SearchCommandSafety.ForDisplay(query.Raw);
        return
        [
            new SearchResult("exec:" + executable + " " + arguments, Name, string.Format(t("SearchRunCommand"), shown), () => shell.Run(executable, arguments))
            {
                Glyph = SearchGlyph.Terminal,
                CategoryRelevance = SearchRelevance.Highest,
                Relevance = 0.7,
                RequiresConfirmation = confirm,
                ConfirmationDetail = confirm ? shown : null,
                Actions = [new SearchAction("terminal", t("SearchActRunInTerminal"), () => shell.RunInTerminal(line)) { RequiresConfirmation = confirm }]
            }
        ];
    }

    /// <summary>İlk sözcük (çift tırnaklı olabilir) ve kalan metin.</summary>
    public static (string Command, string? Arguments) SplitCommand(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return ("", null);

        int end;
        string command;
        if (trimmed[0] == '"')
        {
            var close = trimmed.IndexOf('"', 1);
            if (close < 0)
                return ("", null);

            command = trimmed[1..close];
            end = close + 1;
        }
        else
        {
            end = trimmed.IndexOf(' ');
            if (end < 0)
                end = trimmed.Length;
            command = trimmed[..end];
        }

        var rest = trimmed[end..].Trim();
        return (command, rest.Length == 0 ? null : rest);
    }

    private string? FindExecutable(string command)
    {
        if (_resolved.TryGetValue(command, out var known))
            return known;

        var found = Resolve(command);
        _resolved[command] = found;
        return found;
    }

    /// <summary>Ağ yolları (\\sunucu\paylaşım) yoklanmaz: yanıt vermeyen paylaşım arayüz iş parçacığını bekletir.</summary>
    public static bool IsNetworkPath(string path)
    {
        var trimmed = path.TrimStart();
        return trimmed.StartsWith(@"\\", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal);
    }

    private string? Resolve(string command)
    {
        // Yolu verilmiş komutu Konumlar değil, burası çalıştırır; ama yalnız program uzantılarında.
        if (command.Contains('\\') || command.Contains('/'))
            return !IsNetworkPath(command) && HasExecutableExtension(command) && shell.FileExists(command) ? command : null;

        var extensions = Extensions();
        var hasExtension = HasExecutableExtension(command);
        foreach (var directory in (shell.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (IsNetworkPath(directory))
                continue;

            if (hasExtension)
            {
                var direct = Path.Combine(directory.Trim(), command);
                if (shell.FileExists(direct))
                    return direct;
                continue;
            }

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory.Trim(), command + extension);
                if (shell.FileExists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private bool HasExecutableExtension(string command) =>
        Extensions().Any(e => command.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<string> Extensions()
    {
        var value = shell.GetEnvironmentVariable("PATHEXT");
        return string.IsNullOrWhiteSpace(value) ? DefaultExtensions : value.Split(';', StringSplitOptions.RemoveEmptyEntries);
    }
}

/// <summary>Yazılan yol varsa açar, adresse gider. <c>%USERPROFILE%</c> gibi değişkenler çözülür.</summary>
public sealed class LocationSearchSource(ISearchShell shell, Func<string, string> t) : SearchSource("locations", t("SearchCatLocations"))
{
    private static readonly string[] WebSchemes = ["http", "https", "ftp"];

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        var text = query.Raw;
        if (text.Length == 0)
            return [];

        if (ExpandEnvironment(text) is { } expanded && IsLocalPathLike(expanded))
        {
            if (shell.DirectoryExists(expanded))
                return [Local(text, expanded, SearchGlyph.Folder)];

            if (shell.FileExists(expanded) && !IsProgram(expanded))
                return [Local(text, expanded, SearchGlyph.File)];

            return [];
        }

        var url = NormalizeUrl(text);
        if (url is null)
            return [];

        var title = url.Scheme == Uri.UriSchemeMailto
            ? string.Format(t("SearchSendMail"), text[(text.IndexOf(':') + 1)..])
            : string.Format(t("SearchGoTo"), text);
        var target = url.AbsoluteUri;
        return
        [
            new SearchResult("url:" + target, Name, title, () => shell.Open(target))
            {
                Glyph = SearchGlyph.Globe,
                CategoryRelevance = SearchRelevance.Low,
                Relevance = 1
            }
        ];
    }

    private SearchResult Local(string text, string path, SearchGlyph glyph) =>
        new("path:" + path, Name, string.Format(t("SearchOpen"), text), () => shell.Open(path))
        {
            Glyph = glyph,
            CategoryRelevance = SearchRelevance.Highest,
            Relevance = 1
        };

    private static bool IsProgram(string path) =>
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);

    /// <summary>"C:\..." ya da değişkenle başlayan yol; ağ yolu denetimi UI'yi bekletebilir, bu yüzden yerel diskle sınırlı.</summary>
    private static bool IsLocalPathLike(string text) =>
        text.Length >= 3 && char.IsAsciiLetter(text[0]) && text[1] == ':' && (text[2] == '\\' || text[2] == '/');

    private string? ExpandEnvironment(string text)
    {
        if (!text.Contains('%'))
            return text;

        var result = text;
        var start = result.IndexOf('%');
        while (start >= 0)
        {
            var end = result.IndexOf('%', start + 1);
            if (end < 0)
                break;

            var value = shell.GetEnvironmentVariable(result[(start + 1)..end]);
            if (value is null)
                return null;

            result = result[..start] + value + result[(end + 1)..];
            start = result.IndexOf('%', start + value.Length);
        }

        return result;
    }

    private static Uri? NormalizeUrl(string text)
    {
        if (text.Contains(' '))
            return null;

        if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return null;

        return WebSchemes.Contains(uri.Scheme) || uri.Scheme == Uri.UriSchemeMailto ? uri : null;
    }
}

/// <summary>Masaüstü, Belgeler gibi bilinen klasörler ve sürücüler. Anahtar sözcük hepsini listeler.</summary>
public sealed class PlacesSearchSource(Func<IReadOnlyList<SearchPlace>> places, ISearchShell shell, Func<string, string> t)
    : SearchSource("places", t("SearchCatPlaces"))
{
    private IReadOnlyList<SearchPlace> _places = [];

    public override void Prepare() => _places = places();

    public override void Release() => _places = [];

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        var all = query.Folded == SearchText.Fold(t("SearchKwPlaces"));
        foreach (var place in _places)
        {
            var folded = SearchText.Fold(place.Name);
            int category;
            double relevance;
            if (all || folded == query.Folded)
            {
                category = SearchRelevance.Highest;
                relevance = all ? 0.9 : 1.0;
            }
            else if (folded.Contains(query.Folded, StringComparison.Ordinal))
            {
                category = SearchRelevance.High;
                relevance = 0.7;
            }
            else
            {
                continue;
            }

            var path = place.Path;
            yield return new SearchResult("path:" + path, Name, place.Name, () => shell.Open(path))
            {
                Subtitle = place.Group,
                Glyph = SearchGlyph.Folder,
                CategoryRelevance = category,
                Relevance = relevance
            };
        }
    }
}

/// <summary>Son kullanılan belgeler; panel açılırken bir kez okunur.</summary>
public sealed class RecentDocumentsSearchSource(Func<IReadOnlyList<string>> paths, ISearchShell shell, Func<string, string> t)
    : SearchSource("recent", t("SearchCatRecent"))
{
    private const int MinExactLength = 5;

    private IReadOnlyList<string> _paths = [];

    public override void Prepare() => _paths = paths();

    public override void Release() => _paths = [];

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        var relevance = 0.75;
        foreach (var path in _paths)
        {
            var name = Path.GetFileName(path);
            var folded = SearchText.Fold(name);
            var index = folded.IndexOf(query.Folded, StringComparison.Ordinal);
            if (index < 0)
                continue;

            var exact = query.Folded.Length >= MinExactLength && index == 0
                && (folded.Length == query.Folded.Length || SearchText.Fold(Path.GetFileNameWithoutExtension(name)).Length == query.Folded.Length);

            var captured = path;
            yield return new SearchResult("path:" + path, Name, name, () => shell.Open(captured))
            {
                Subtitle = Path.GetDirectoryName(path),
                Glyph = SearchGlyph.File,
                Placement = SearchCategoryPlacement.Bottom,
                CategoryRelevance = exact ? SearchRelevance.Highest : index == 0 ? SearchRelevance.High : SearchRelevance.Low,
                Relevance = index == 0 ? relevance + 0.1 : relevance,
                Actions = [new SearchAction("folder", t("SearchActOpenFolder"), () => shell.OpenContainingFolder(captured))]
            };

            relevance -= 0.05;
        }
    }
}

/// <summary>Windows Arama dizininde dosya adı araması; türe göre gruplanır, yazma durunca çalışır.</summary>
public sealed class FileSearchSource(IFileSearchService service, ISearchShell shell, Func<string, string> t, Func<byte[]?>? noIcon = null)
    : SearchSource("files", t("SearchCatFiles"))
{
    private const int MinQueryLength = 2;
    private const int FetchLimit = 60;
    private const int PerCategory = 10;
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);

    // Arka plan görevi yazar, arayüz iş parçacığı okur: bütün durum bu kilitle korunur.
    private readonly object _gate = new();
    private CancellationTokenSource? _running;
    private string _pending = "";
    private string _ready = "";
    private IReadOnlyList<IndexedItem> _items = [];

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
            _items = [];
        }
    }

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        // "=" hesap makinesinindir; dizin sorgusunda işleç sayılır.
        if (query.Raw.Length < MinQueryLength || query.Raw.StartsWith('='))
        {
            lock (_gate)
                CancelLocked();
            return [];
        }

        IReadOnlyList<IndexedItem> items;
        bool ready;
        lock (_gate)
        {
            if (_ready != query.Raw && _pending != query.Raw)
                StartLocked(query.Raw);

            ready = _ready == query.Raw;
            items = _items;
        }

        return ready ? Build(query, items) : [];
    }

    /// <summary>Uzantıya göre sonuç grubu; tanınmayan "Dosyalar".</summary>
    public static string CategoryKey(string path, bool isFolder)
    {
        if (isFolder)
            return "SearchCatFolders";

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" or ".flac" or ".wav" or ".aac" or ".ogg" or ".m4a" or ".wma" => "SearchCatAudio",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".heic" or ".tiff" => "SearchCatImages",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".wmv" or ".m4v" => "SearchCatVideos",
            ".xls" or ".xlsx" or ".csv" or ".ods" => "SearchCatSpreadsheets",
            ".ppt" or ".pptx" or ".odp" => "SearchCatPresentations",
            ".doc" or ".docx" or ".pdf" or ".odt" or ".rtf" => "SearchCatDocuments",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "SearchCatArchives",
            ".txt" or ".md" or ".log" or ".json" or ".xml" or ".yml" or ".yaml" or ".ini" => "SearchCatTexts",
            _ => "SearchCatFiles"
        };
    }

    private IEnumerable<SearchResult> Build(SearchQuery query, IReadOnlyList<IndexedItem> items) => items
        .GroupBy(item => CategoryKey(item.Path, item.IsFolder))
        .SelectMany(group => group.Take(PerCategory).Select(item => ToResult(query, item, t(group.Key))))
        .ToList();

    private SearchResult ToResult(SearchQuery query, IndexedItem item, string category)
    {
        var name = Path.GetFileName(item.Path.TrimEnd('\\'));
        var folded = SearchText.Fold(name);
        var exact = folded == query.Folded || SearchText.Fold(Path.GetFileNameWithoutExtension(name)) == query.Folded;
        var path = item.Path;
        return new SearchResult("path:" + path, category, name, () => shell.Open(path))
        {
            Subtitle = Path.GetDirectoryName(path),
            Glyph = item.IsFolder ? SearchGlyph.Folder : SearchGlyph.File,
            Placement = SearchCategoryPlacement.Bottom,
            CategoryRelevance = exact ? SearchRelevance.Highest : folded.Contains(query.Folded, StringComparison.Ordinal) ? SearchRelevance.High : SearchRelevance.Low,
            Relevance = 0.75,
            Actions = [new SearchAction("folder", t("SearchActOpenFolder"), () => shell.OpenContainingFolder(path))],
            LoadIcon = noIcon
        };
    }

    private void CancelLocked()
    {
        // Kaynağı görev kendi finally'sinde bırakır; burada yalnız iptal edilir.
        _running?.Cancel();
        _running = null;
        _pending = "";
    }

    private void StartLocked(string term)
    {
        _running?.Cancel();
        var running = new CancellationTokenSource();
        _running = running;
        _pending = term;
        _ = RunAsync(term, running);
    }

    private async Task RunAsync(string term, CancellationTokenSource running)
    {
        // Kaynak başka iş parçacığında bırakılabilir; belirteç baştan alınır.
        var token = running.Token;
        try
        {
            await Task.Delay(Debounce, token).ConfigureAwait(false);
            var items = await service.SearchAsync(term, FetchLimit, token).ConfigureAwait(false);

            lock (_gate)
            {
                if (token.IsCancellationRequested || !ReferenceEquals(_running, running))
                    return;

                _items = items;
                _ready = term;
                _pending = "";
                _running = null;
            }

            RaiseChanged();
        }
        catch (OperationCanceledException)
        {
            // Yeni arama geldi ya da panel kapandı.
        }
        catch (Exception ex)
        {
            CrashLog.Write("Dosya arama", ex);
            bool cleared;
            lock (_gate)
            {
                cleared = ReferenceEquals(_running, running);
                if (cleared)
                {
                    _running = null;
                    _pending = "";
                }
            }

            if (cleared)
                RaiseChanged();
        }
        finally
        {
            running.Dispose();
        }
    }
}

/// <summary>
/// "kill ad": eşleşen süreçleri listeler. Tetik sözcüğü olmadan hiç çalışmaz.
/// Ana eylem nazik kapatma; zorla sonlandırma ayrı eylemdir ve onay ister.
/// </summary>
public sealed class KillSearchSource(
    Func<IReadOnlyList<RunningProcess>> processes,
    Action<RunningProcess> terminate,
    Action<RunningProcess> requestClose,
    Func<string, string> t,
    Func<long>? clock = null) : SearchSource("kill", t("SearchCatProcesses"))
{
    private const int MinNameLength = 2;

    /// <summary>Liste bundan eskiyse yeniden alınır (panel iğnelenip açık kalırsa kimlikler eskir).</summary>
    public const long MaxSnapshotAgeMs = 10_000;

    private readonly Func<long> _clock = clock ?? (() => Environment.TickCount64);
    private IReadOnlyList<RunningProcess>? _snapshot;
    private long _snapshotAt;

    public override void Release() => _snapshot = null;

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        var word = SearchText.Fold(t("SearchKwKill")) + " ";
        if (!query.Folded.StartsWith(word, StringComparison.Ordinal))
            return [];

        var term = query.Folded[word.Length..].Trim();
        if (term.Length < MinNameLength)
            return [];

        // Süreç listesi pahalı: yalnız bu kaynak gerçekten kullanılınca, kısa süreliğine saklanır.
        var now = _clock();
        if (_snapshot is null || now - _snapshotAt > MaxSnapshotAgeMs)
        {
            _snapshot = processes();
            _snapshotAt = now;
        }

        return _snapshot
            .Where(p => SearchText.Fold(p.Name).Contains(term, StringComparison.Ordinal))
            .Select(p => new SearchResult("kill:" + p.Id, Name, string.Format(t("SearchCloseProcess"), p.Name), () => requestClose(p))
            {
                Subtitle = string.Format(t("SearchProcessId"), p.Id),
                Glyph = SearchGlyph.Process,
                CategoryRelevance = SearchRelevance.Moderate,
                Relevance = SearchText.Fold(p.Name) == term ? 1 : 0.9,
                Actions = [new SearchAction("terminate", t("SearchActTerminate"), () => terminate(p)) { RequiresConfirmation = true }]
            })
            .ToList();
    }
}
