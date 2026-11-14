using System.Globalization;
using Koy.Core.JumpList;
using Koy.Core.Search;

namespace Koy.Core.Tests.Search;

public class NewSearchSourcesTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static string T(string key) => key switch
    {
        "SearchKwSpecialChars" => "char;sym;sembol",
        "SearchKwSpellCheck" => "spell;yazım",
        "SearchKwDictionary" => "dict;def;sözlük",
        "SearchKwStore" => "store;mağaza",
        "SearchSpellSuggestionFor" => "Suggestion for \"{0}\"",
        "SearchDictWebFor" => "Look up \"{0}\" online",
        "SearchStoreFor" => "Search Microsoft Store for \"{0}\"",
        _ => key
    };

    private sealed class FakeShell : ISearchShell
    {
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Log { get; } = [];

        public bool FileExists(string path) => Files.Contains(path);
        public bool DirectoryExists(string path) => Directories.Contains(path);
        public string? GetEnvironmentVariable(string name) => null;
        public void Open(string target) => Log.Add("open " + target);
        public void Run(string executable, string? arguments) { }
        public void RunWithArguments(string executable, IReadOnlyList<string> arguments) { }
        public void RunInTerminal(string commandLine) { }
        public void OpenContainingFolder(string path) => Log.Add("folder " + path);
        public void CopyText(string text) => Log.Add("copy " + text);
    }

    private sealed class FakeSpellChecker(Func<string, SpellCheckResult> check) : ISpellCheckerService
    {
        public bool Released { get; private set; }
        public SpellCheckResult Check(string text) => check(text);
        public void Release() => Released = true;
    }

    private sealed class FakeDictionary(DictionaryEntry? entry) : IDictionaryService
    {
        public List<string> Queries { get; } = [];

        public Task<DictionaryEntry?> LookupAsync(string word, CancellationToken cancellationToken = default)
        {
            Queries.Add(word);
            return Task.FromResult(entry);
        }
    }

    private sealed class FakeApps(params InstalledApp[] apps) : IInstalledAppsService
    {
        public Task<IReadOnlyList<InstalledApp>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InstalledApp>>(apps);
    }

    private sealed class FakeJumpList(Dictionary<string, IReadOnlyList<JumpListSection>> data) : IJumpListService
    {
        public List<string> Launched { get; } = [];

        public Task<IReadOnlyList<JumpListSection>> GetAsync(string appUserModelId, CancellationToken cancellationToken = default) =>
            Task.FromResult(data.GetValueOrDefault(appUserModelId) ?? []);

        public Task<string?> ResolveAppIdAsync(string exePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public void Launch(JumpListEntry entry, string? appExePath) => Launched.Add(entry.Title);

        public void Clear() { }
    }

    // --- Special characters ---

    [Fact]
    public void SpecialCharacters_keyword_alone_lists_catalog_and_copies_on_execute()
    {
        var shell = new FakeShell();
        var source = new SpecialCharactersSource(shell, T, En);

        var results = source.Search(new SearchQuery("char")).ToList();

        Assert.NotEmpty(results);
        var first = results[0];
        Assert.Equal(SearchDragKind.Text, first.Drag?.Kind);
        Assert.True(first.RestoresFocus);
        first.Execute();
        Assert.Single(shell.Log, "copy " + first.Drag!.Value);
    }

    [Fact]
    public void SpecialCharacters_keyword_filter_and_direct_lookup_find_expected_symbols()
    {
        var shell = new FakeShell();
        var source = new SpecialCharactersSource(shell, T, Tr);

        var arrowResults = source.Search(new SearchQuery("sembol:ok")).ToList();
        Assert.Contains(arrowResults, r => r.Drag?.Value == "→" && r.CategoryRelevance == SearchRelevance.Highest);

        var euroResults = source.Search(new SearchQuery("euro")).ToList();
        Assert.Contains(euroResults, r => r.Drag?.Value == "€");

        var codePointResults = source.Search(new SearchQuery("U+2192")).ToList();
        Assert.Contains(codePointResults, r => r.Drag?.Value == "→");
    }

    // --- Spell check ---

    [Fact]
    public void SpellCheck_ignores_queries_without_prefix()
    {
        var spell = new FakeSpellChecker(_ => new SpellCheckResult(true, false, ["receive"]));
        var source = new SpellCheckSource(spell, new FakeShell(), T);

        Assert.Empty(source.Search(new SearchQuery("recieve")));
    }

    [Fact]
    public void SpellCheck_returns_suggestions_and_copies_on_execute()
    {
        var shell = new FakeShell();
        var spell = new FakeSpellChecker(_ => new SpellCheckResult(true, false, ["receive", "relieve"]));
        var source = new SpellCheckSource(spell, shell, T);

        var results = source.Search(new SearchQuery("spell recieve")).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("receive", results[0].Title);
        Assert.Equal(new SearchDragData(SearchDragKind.Text, "receive"), results[0].Drag);
        results[0].Execute();
        Assert.Single(shell.Log, "copy receive");

        source.Release();
        Assert.True(spell.Released);
    }

    [Fact]
    public void SpellCheck_correct_word_shows_checkmark_and_copies()
    {
        var shell = new FakeShell();
        var spell = new FakeSpellChecker(_ => new SpellCheckResult(true, true, []));
        var source = new SpellCheckSource(spell, shell, T);

        var result = source.Search(new SearchQuery("yazım:merhaba")).Single();

        Assert.Equal("✓ merhaba", result.Title);
        result.Execute();
        Assert.Single(shell.Log, "copy merhaba");
    }

    // --- Dictionary ---

    [Fact]
    public async Task Dictionary_debounced_lookup_returns_meanings_and_web_action()
    {
        var shell = new FakeShell();
        var entry = new DictionaryEntry(
            "koy",
            "/koj/",
            [new DictionaryMeaning("isim", "Küçük körfez", "Sakin bir koy")],
            "https://sozluk.gov.tr/?ara=koy");
        var service = new FakeDictionary(entry);
        var source = new DictionarySource(service, shell, T, Tr);
        var changed = new TaskCompletionSource();
        source.Changed += (_, _) => changed.TrySetResult();

        Assert.Empty(source.Search(new SearchQuery("sözlük koy")));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var results = source.Search(new SearchQuery("sözlük koy")).ToList();
        var first = Assert.Single(results);
        Assert.Equal("Küçük körfez", first.Title);
        Assert.Equal(new SearchDragData(SearchDragKind.Text, "koy: Küçük körfez"), first.Drag);

        first.Execute();
        Assert.Contains("copy koy: Küçük körfez", shell.Log);
        first.Actions[0].Execute();
        Assert.Contains("open https://sozluk.gov.tr/?ara=koy", shell.Log);
    }

    [Fact]
    public async Task Dictionary_falls_back_to_online_link_when_not_found()
    {
        var shell = new FakeShell();
        var source = new DictionarySource(new FakeDictionary(null), shell, T, En);
        var changed = new TaskCompletionSource();
        source.Changed += (_, _) => changed.TrySetResult();

        Assert.Empty(source.Search(new SearchQuery("dict xyzzy")));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var result = source.Search(new SearchQuery("dict xyzzy")).Single();
        Assert.Equal(SearchDragKind.Url, result.Drag?.Kind);
        result.Execute();
        Assert.Single(shell.Log, "open https://www.merriam-webster.com/dictionary/xyzzy");
    }

    // --- Jump List ---

    [Fact]
    public async Task JumpList_lazily_fetches_matching_app_and_launches_entry()
    {
        var apps = new FakeApps(new InstalledApp("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "Windows Terminal"));
        var jump = new FakeJumpList(new Dictionary<string, IReadOnlyList<JumpListSection>>
        {
            ["Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"] =
            [
                new JumpListSection(JumpListSectionKind.Tasks, null,
                [
                    JumpListEntry.Separator,
                    new JumpListEntry("PowerShell", new JumpListLaunch("pwsh.exe")),
                    new JumpListEntry("Docs", new JumpListLaunch("https://learn.microsoft.com"))
                ])
            ]
        });

        var source = new JumpListSearchSource(apps, jump, T);
        var changed = new TaskCompletionSource();
        source.Changed += (_, _) => changed.TrySetResult();
        source.Prepare();
        await Task.Delay(50);

        // İlk aramada tembel yükleme tetiklenir.
        _ = source.Search(new SearchQuery("terminal")).ToList();
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var results = source.Search(new SearchQuery("terminal powershell")).ToList();
        var ps = Assert.Single(results, r => r.Title == "PowerShell");
        ps.Execute();
        Assert.Single(jump.Launched, "PowerShell");

        var allForTerminal = source.Search(new SearchQuery("terminal")).ToList();
        var docs = Assert.Single(allForTerminal, r => r.Title == "Docs");
        Assert.Equal(new SearchDragData(SearchDragKind.Url, "https://learn.microsoft.com"), docs.Drag);
    }

    // --- Microsoft Store ---

    [Fact]
    public void MicrosoftStore_builds_protocol_uri_and_web_drag_url()
    {
        var shell = new FakeShell();
        var source = new MicrosoftStoreSearchSource(shell, T);

        Assert.Empty(source.Search(new SearchQuery("terminal")));

        var result = source.Search(new SearchQuery("store windows terminal")).Single();
        Assert.Equal(new SearchDragData(SearchDragKind.Url, "https://apps.microsoft.com/search?q=windows%20terminal"), result.Drag);

        result.Execute();
        Assert.Contains("open ms-windows-store://search/?query=windows%20terminal", shell.Log);
        result.Actions[0].Execute();
        Assert.Contains("open https://apps.microsoft.com/search?q=windows%20terminal", shell.Log);
    }

    // --- Drag payloads on existing sources ---

    [Fact]
    public void Existing_sources_expose_drag_payloads()
    {
        var shell = new FakeShell();
        shell.Files.Add(@"C:\test\notes.txt");

        var calc = new CalculatorSearchSource(shell, T, En).Search(new SearchQuery("2+2")).Single();
        Assert.Equal(new SearchDragData(SearchDragKind.Text, "4"), calc.Drag);

        var file = new LocationSearchSource(shell, T).Search(new SearchQuery(@"C:\test\notes.txt")).Single();
        Assert.Equal(new SearchDragData(SearchDragKind.File, @"C:\test\notes.txt"), file.Drag);

        var url = new LocationSearchSource(shell, T).Search(new SearchQuery("https://example.com")).Single();
        Assert.Equal(new SearchDragData(SearchDragKind.Url, "https://example.com/"), url.Drag);
    }
}
