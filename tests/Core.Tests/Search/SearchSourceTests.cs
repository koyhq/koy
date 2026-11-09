using System.Globalization;
using Koy.Core.Clipboard;
using Koy.Core.Search;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Search;

public class SearchSourceTests
{
    private static string T(string key) => key.StartsWith("SearchKw", StringComparison.Ordinal)
        ? key switch
        {
            "SearchKwPlaces" => "yerler",
            "SearchKwKill" => "kill",
            "SearchKwHelp" => "yardım",
            "SearchKwBookmarks" => "yer imleri",
            "SearchKwDate" => "tarih;date",
            "SearchKwTime" => "saat;time",
            _ => key
        }
        : key == "SearchWebFor" ? key + "|{0}|{1}" : key + "|{0}";

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private sealed class FakeShell : ISearchShell
    {
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Env { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Log { get; } = [];

        public bool FileExists(string path) => Files.Contains(path);
        public bool DirectoryExists(string path) => Directories.Contains(path);
        public string? GetEnvironmentVariable(string name) => Env.GetValueOrDefault(name);
        public void Open(string target) => Log.Add("open " + target);
        public void Run(string executable, string? arguments) => Log.Add($"run {executable} {arguments}".TrimEnd());
        public void RunWithArguments(string executable, IReadOnlyList<string> arguments) => Log.Add($"run {executable} [{string.Join("|", arguments)}]");
        public void RunInTerminal(string commandLine) => Log.Add("term " + commandLine);
        public void OpenContainingFolder(string path) => Log.Add("folder " + path);
        public void CopyText(string text) => Log.Add("copy " + text);
    }

    private sealed class FakeApps(params InstalledApp[] apps) : IInstalledAppsService
    {
        public TaskCompletionSource Gate { get; } = new();

        public async Task<IReadOnlyList<InstalledApp>> LoadAsync(CancellationToken cancellationToken = default)
        {
            await Gate.Task.WaitAsync(cancellationToken);
            return apps;
        }
    }

    private sealed class FakeCommands : ISystemCommandService
    {
        public List<SystemCommand> Executed { get; } = [];
        public void Execute(SystemCommand command) => Executed.Add(command);
    }

    private sealed class FakeFiles(params IndexedItem[] items) : IFileSearchService
    {
        public List<string> Terms { get; } = [];

        public Task<IReadOnlyList<IndexedItem>> SearchAsync(string term, int limit, CancellationToken cancellationToken = default)
        {
            Terms.Add(term);
            return Task.FromResult<IReadOnlyList<IndexedItem>>(items);
        }
    }

    private static SearchQuery Q(string text) => new(text);

    // --- Hesap ---

    [Fact]
    public void Calculator_enter_replaces_query_and_action_copies()
    {
        var shell = new FakeShell();
        var result = new CalculatorSearchSource(shell, T, Tr).Search(Q("1000*2")).Single();

        Assert.Equal("2.000", result.Title);
        Assert.Equal("2000", result.ReplacesQuery);
        Assert.Equal(SearchRelevance.High, result.CategoryRelevance);

        result.Actions[0].Execute();
        Assert.Equal(["copy 2000"], shell.Log);
    }

    [Fact]
    public void Calculator_ignores_plain_words() => Assert.Empty(new CalculatorSearchSource(new FakeShell(), T, Tr).Search(Q("chrome")));

    // --- Oturum ---

    private static SessionSearchSource Session(FakeCommands service) => new(service,
    [
        new(SystemCommand.Lock, "Kilitle", ["kilitle", "lock"]),
        new(SystemCommand.Restart, "Yeniden başlat", ["yeniden başlat", "restart"]),
        new(SystemCommand.Shutdown, "Kapat", ["kapat", "shutdown"]),
        new(SystemCommand.SignOut, "Oturumu kapat", ["oturumu kapat", "logout"]),
    ], T);

    [Fact]
    public void Dangerous_session_commands_need_confirmation()
    {
        var source = Session(new FakeCommands());

        Assert.False(source.Search(Q("kilit")).Single().RequiresConfirmation);
        Assert.True(source.Search(Q("yeniden")).Single().RequiresConfirmation);
        Assert.True(source.Search(Q("kapat")).Single().RequiresConfirmation);
        Assert.True(source.Search(Q("logout")).Single().RequiresConfirmation);
    }

    [Fact]
    public void Exact_keyword_is_highest_and_prefix_is_low()
    {
        var source = Session(new FakeCommands());

        Assert.Equal(SearchRelevance.Highest, source.Search(Q("kilitle")).Single().CategoryRelevance);
        var partial = source.Search(Q("kil")).Single();
        Assert.Equal(SearchRelevance.Low, partial.CategoryRelevance);
        Assert.Equal(0.9 * 3 / 7, partial.Relevance, 3);
    }

    [Fact]
    public void Session_keyword_must_start_with_what_is_typed() => Assert.Empty(Session(new FakeCommands()).Search(Q("apat")));

    [Fact]
    public void Session_runs_through_service()
    {
        var service = new FakeCommands();

        Session(service).Search(Q("lock")).Single().Execute();

        Assert.Equal([SystemCommand.Lock], service.Executed);
    }

    // --- Uygulamalar ---

    [Theory]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "microsoft.windowscalculator")]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Notepad++\notepad++.exe", "notepad++")]
    public void Secondary_key_comes_from_package_or_exe(string id, string expected) =>
        Assert.Equal(expected, AppSearchSource.SecondaryKey(id));

    [Theory]
    [InlineData("App", "app.exe", true)]
    [InlineData("  ", "app.exe", false)]
    [InlineData("Uninstall Foo", "unins.exe", false)]
    [InlineData("Foo'yu Kaldır", "unins.exe", false)]
    [InlineData("Benioku", @"C:\a\readme.txt", false)]
    [InlineData("Oyun", @"C:\a\oyun.url", true)]
    public void App_list_filter(string name, string id, bool expected) =>
        Assert.Equal(expected, AppSearchSource.ShouldList(new InstalledApp(id, name)));

    [Fact]
    public async Task App_source_lists_after_loading_dedupes_and_finds_by_package()
    {
        var service = new FakeApps(
            new InstalledApp("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "Hesap Makinesi"),
            new InstalledApp("dup1", "Çift"),
            new InstalledApp("dup2", "çift"));
        var launched = new List<InstalledApp>();
        var source = new AppSearchSource(service, launched.Add, T);
        var changed = new TaskCompletionSource();
        source.Changed += (_, _) => changed.TrySetResult();

        source.Prepare();
        Assert.Empty(source.Search(Q("hesap")));

        service.Gate.SetResult();
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(source.Search(Q("cift")));
        Assert.Equal(SearchRelevance.Highest, source.Search(Q("hesap makinesi")).Single().CategoryRelevance);
        Assert.Equal(SearchCategoryPlacement.Top, source.Search(Q("hesap makinesi")).Single().Placement);

        source.Search(Q("calc")).Single().Execute();
        Assert.Equal("Hesap Makinesi", Assert.Single(launched).Name);
    }

    // --- Pencereler ---

    [Fact]
    public void Window_source_uses_snapshot_and_releases_it()
    {
        var activated = new List<nint>();
        var calls = 0;
        var source = new WindowSearchSource(() =>
        {
            calls++;
            return [new OpenWindowInfo(42, "Rapor - Word", @"C:\Office\WINWORD.EXE", 1, default), new OpenWindowInfo(43, "  ", null, 2, default)];
        }, activated.Add, T);

        source.Prepare();
        var result = source.Search(Q("rapor")).Single();
        source.Search(Q("rapo"));
        result.Execute();

        Assert.Equal(1, calls);
        Assert.Equal("WINWORD", result.Subtitle);
        Assert.Equal([(nint)42], activated);

        source.Release();
        Assert.Empty(source.Search(Q("rapor")));
    }

    // --- Komut satırı ---

    private static FakeShell PathShell()
    {
        var shell = new FakeShell();
        shell.Env["PATH"] = @"C:\Windows;C:\Tools";
        shell.Env["PATHEXT"] = ".EXE;.CMD";
        shell.Files.Add(@"C:\Windows\ipconfig.EXE");
        shell.Files.Add(@"C:\Tools\build.CMD");
        return shell;
    }

    [Fact]
    public void Shell_finds_program_on_path_and_passes_arguments()
    {
        var shell = PathShell();
        var result = new ShellCommandSearchSource(shell, T).Search(Q("ipconfig /all")).Single();

        Assert.Equal(SearchRelevance.Highest, result.CategoryRelevance);
        result.Execute();

        Assert.Equal([@"run C:\Windows\ipconfig.EXE /all"], shell.Log);
    }

    [Fact]
    public void Shell_run_in_terminal_keeps_original_line()
    {
        var shell = PathShell();

        new ShellCommandSearchSource(shell, T).Search(Q("build -x")).Single().Actions[0].Execute();

        Assert.Equal(["term build -x"], shell.Log);
    }

    [Fact]
    public void Shell_ignores_unknown_commands() => Assert.Empty(new ShellCommandSearchSource(PathShell(), T).Search(Q("merhaba dünya")));

    [Fact]
    public void Shell_accepts_explicit_extension() => Assert.Single(new ShellCommandSearchSource(PathShell(), T).Search(Q("ipconfig.exe")));

    [Theory]
    [InlineData("notepad a.txt", "notepad", "a.txt")]
    [InlineData("\"C:\\Program Files\\x.exe\" -v", @"C:\Program Files\x.exe", "-v")]
    [InlineData("tek", "tek", null)]
    [InlineData("\"kapanmamış", "", null)]
    public void Shell_splits_command(string text, string command, string? arguments) =>
        Assert.Equal((command, arguments), ShellCommandSearchSource.SplitCommand(text));

    // --- Konumlar ---

    [Fact]
    public void Existing_folder_opens_with_highest_importance()
    {
        var shell = new FakeShell();
        shell.Directories.Add(@"C:\Users");

        var result = new LocationSearchSource(shell, T).Search(Q(@"C:\Users")).Single();
        result.Execute();

        Assert.Equal(SearchRelevance.Highest, result.CategoryRelevance);
        Assert.Equal([@"open C:\Users"], shell.Log);
    }

    [Fact]
    public void Environment_variables_expand_in_paths()
    {
        var shell = new FakeShell();
        shell.Env["USERPROFILE"] = @"C:\Users\ali";
        shell.Directories.Add(@"C:\Users\ali\Belgeler");

        var result = new LocationSearchSource(shell, T).Search(Q(@"%USERPROFILE%\Belgeler")).Single();
        result.Execute();

        Assert.Equal([@"open C:\Users\ali\Belgeler"], shell.Log);
    }

    [Fact]
    public void Executable_path_is_left_to_shell_source()
    {
        var shell = new FakeShell();
        shell.Files.Add(@"C:\a\x.exe");

        Assert.Empty(new LocationSearchSource(shell, T).Search(Q(@"C:\a\x.exe")));
    }

    [Theory]
    [InlineData("https://koy.dev/a", "https://koy.dev/a")]
    [InlineData("www.koy.dev", "https://www.koy.dev/")]
    public void Web_addresses_open(string text, string expected)
    {
        var shell = new FakeShell();

        var result = new LocationSearchSource(shell, T).Search(Q(text)).Single();
        result.Execute();

        Assert.Equal(SearchRelevance.Low, result.CategoryRelevance);
        Assert.Equal(["open " + expected], shell.Log);
    }

    [Fact]
    public void Mailto_is_recognised() => Assert.Contains("a@b.c", new LocationSearchSource(new FakeShell(), T).Search(Q("mailto:a@b.c")).Single().Title);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/x")]
    [InlineData("merhaba dünya")]
    [InlineData("ms-settings:display")]
    public void Other_schemes_and_plain_text_are_ignored(string text) => Assert.Empty(new LocationSearchSource(new FakeShell(), T).Search(Q(text)));

    // --- Yerler ---

    private static PlacesSearchSource Places(FakeShell shell)
    {
        var source = new PlacesSearchSource(() => [new SearchPlace("Belgeler", @"C:\B"), new SearchPlace("Resimler", @"C:\R")], shell, T);
        source.Prepare();
        return source;
    }

    [Fact]
    public void Place_keyword_lists_all() => Assert.Equal(2, Places(new FakeShell()).Search(Q("yerler")).Count());

    [Fact]
    public void Place_exact_beats_contains()
    {
        var source = Places(new FakeShell());

        Assert.Equal(SearchRelevance.Highest, source.Search(Q("belgeler")).Single().CategoryRelevance);
        Assert.Equal(SearchRelevance.High, source.Search(Q("belge")).Single().CategoryRelevance);
    }

    // --- Son belgeler ---

    [Fact]
    public void Recent_ranks_by_match_position_and_offers_folder()
    {
        var shell = new FakeShell();
        var source = new RecentDocumentsSearchSource(() => [@"C:\d\rapor.docx", @"C:\d\yillik-rapor.docx", @"C:\d\plan.txt"], shell, T);
        source.Prepare();

        var results = source.Search(Q("rapor")).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(SearchRelevance.Highest, results[0].CategoryRelevance); // uzantısız tam ad
        Assert.Equal(SearchRelevance.Low, results[1].CategoryRelevance);
        Assert.All(results, r => Assert.Equal(SearchCategoryPlacement.Bottom, r.Placement));

        results[1].Actions[0].Execute();
        Assert.Equal([@"folder C:\d\yillik-rapor.docx"], shell.Log);
    }

    // --- Dosyalar ---

    [Theory]
    [InlineData("a.mp3", false, "SearchCatAudio")]
    [InlineData("a.PNG", false, "SearchCatImages")]
    [InlineData("a.mkv", false, "SearchCatVideos")]
    [InlineData("a.xlsx", false, "SearchCatSpreadsheets")]
    [InlineData("a.pptx", false, "SearchCatPresentations")]
    [InlineData("a.pdf", false, "SearchCatDocuments")]
    [InlineData("a.zip", false, "SearchCatArchives")]
    [InlineData("a.txt", false, "SearchCatTexts")]
    [InlineData("a.xyz", false, "SearchCatFiles")]
    [InlineData("klasör", true, "SearchCatFolders")]
    public void File_category_comes_from_extension(string path, bool folder, string expected) =>
        Assert.Equal(expected, FileSearchSource.CategoryKey(path, folder));

    private static async Task<IReadOnlyList<SearchResult>> SearchFiles(FileSearchSource source, string text)
    {
        var changed = new TaskCompletionSource();
        source.Changed += (_, _) => changed.TrySetResult();
        Assert.Empty(source.Search(Q(text)));
        Assert.True(source.IsBusy);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(source.IsBusy);
        return source.Search(Q(text)).ToList();
    }

    [Fact]
    public async Task File_results_arrive_after_search_with_importance_and_folder_action()
    {
        var shell = new FakeShell();
        var source = new FileSearchSource(new FakeFiles(new IndexedItem(@"C:\d\rapor.pdf", false), new IndexedItem(@"C:\d\yillik.pdf", false)), shell, T);

        var results = await SearchFiles(source, "rapor");

        Assert.Equal(SearchRelevance.Highest, results.Single(r => r.Title == "rapor.pdf").CategoryRelevance);
        Assert.Equal(SearchRelevance.Low, results.Single(r => r.Title == "yillik.pdf").CategoryRelevance);
        Assert.All(results, r => Assert.Equal(SearchCategoryPlacement.Bottom, r.Placement));

        results[0].Actions[0].Execute();
        Assert.Contains(shell.Log, l => l.StartsWith("folder ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Each_file_type_shows_at_most_ten()
    {
        var items = Enumerable.Range(0, 25).Select(i => new IndexedItem($@"C:\d\f{i}.pdf", false)).ToArray();
        var source = new FileSearchSource(new FakeFiles(items), new FakeShell(), T);

        Assert.Equal(10, (await SearchFiles(source, "rapor")).Count);
    }

    [Fact]
    public async Task Same_term_is_not_searched_twice()
    {
        var files = new FakeFiles();
        var source = new FileSearchSource(files, new FakeShell(), T);

        await SearchFiles(source, "rapor");
        source.Search(Q("rapor"));

        Assert.Single(files.Terms);
    }

    [Fact]
    public async Task File_search_survives_rapid_queries_and_release_from_other_threads()
    {
        var source = new FileSearchSource(new FakeFiles(new IndexedItem(@"C:\d\f.pdf", false)), new FakeShell(), T);
        var failures = new List<Exception>();

        var worker = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 300; i++)
                {
                    _ = source.IsBusy;
                    source.Search(Q("rapor" + i % 7)).ToList();
                    if (i % 25 == 0)
                        source.Release();
                }
            }
            catch (Exception ex)
            {
                lock (failures)
                    failures.Add(ex);
            }
        });
        for (var i = 0; i < 300; i++)
        {
            _ = source.IsBusy;
            source.Search(Q("belge" + i % 5)).ToList();
        }

        await worker;
        source.Release();

        Assert.Empty(failures);
        Assert.False(source.IsBusy);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("=1+1")]
    public void File_search_skips_short_and_calculator_queries(string text)
    {
        var source = new FileSearchSource(new FakeFiles(), new FakeShell(), T);

        Assert.Empty(source.Search(Q(text)));
        Assert.False(source.IsBusy);
    }

    // --- Süreçler ---

    private static KillSearchSource Kill(List<string> log, Func<int>? onList = null, Func<long>? clock = null) => new(() =>
    {
        onList?.Invoke();
        return [new RunningProcess(7, "notepad", 111), new RunningProcess(8, "chrome", 222)];
    }, p => log.Add("kill " + p.Id), p => log.Add("close " + p.Id), T, clock);

    [Fact]
    public void Kill_needs_trigger_word_and_two_letters()
    {
        var calls = 0;
        var source = Kill([], () => ++calls);

        Assert.Empty(source.Search(Q("notepad")));
        Assert.Empty(source.Search(Q("kill n")));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Kill_lists_matches_and_snapshots_once()
    {
        var calls = 0;
        var source = Kill([], () => ++calls);

        source.Search(Q("kill note")).Single();
        source.Search(Q("kill notep"));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Kill_primary_action_is_graceful_close_and_terminate_needs_confirmation()
    {
        var log = new List<string>();
        var result = Kill(log).Search(Q("kill note")).Single();

        Assert.False(result.RequiresConfirmation);
        Assert.Contains("SearchCloseProcess", result.Title);
        Assert.True(result.Actions.Single().RequiresConfirmation);

        result.Execute();
        result.Actions[0].Execute();

        Assert.Equal(["close 7", "kill 7"], log);
    }

    [Fact]
    public void Kill_terminate_asks_before_running_in_the_panel()
    {
        var log = new List<string>();
        var panel = new SearchPanelController([Kill(log)]);
        panel.Open();
        panel.SetQuery("kill note");

        Assert.Equal(SearchActivation.NeedsConfirmation, panel.Activate(firstAction: true).Outcome);
        Assert.Empty(log);
        Assert.Equal(SearchActivation.Executed, panel.Activate(firstAction: true).Outcome);
        Assert.Equal(["kill 7"], log);
    }

    [Fact]
    public void Kill_snapshot_is_refreshed_when_stale()
    {
        var calls = 0;
        var now = 0L;
        var source = Kill([], () => ++calls, () => now);

        source.Search(Q("kill note"));
        now = KillSearchSource.MaxSnapshotAgeMs;
        source.Search(Q("kill note"));
        Assert.Equal(1, calls);

        now = KillSearchSource.MaxSnapshotAgeMs + 1;
        source.Search(Q("kill note"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Kill_actions_carry_the_listed_process_identity()
    {
        RunningProcess? closed = null;
        var source = new KillSearchSource(() => [new RunningProcess(7, "notepad", 111)], _ => { }, p => closed = p, T);

        source.Search(Q("kill note")).Single().Execute();

        Assert.Equal(new RunningProcess(7, "notepad", 111), closed);
    }

    // --- Web ---

    [Theory]
    [InlineData("gg:kedi", "gg", "kedi")]
    [InlineData("!yt kedi videosu", "yt", "kedi videosu")]
    public void Web_shortcut_parses(string text, string key, string term)
    {
        Assert.True(WebShortcutSearchSource.TryParse(text, out var k, out var t));
        Assert.Equal((key, term), (k, t));
    }

    [Theory]
    [InlineData(@"C:\klasör")]
    [InlineData("https://a.b")]
    [InlineData("düz metin")]
    [InlineData("gg:")]
    [InlineData("!gg")]
    public void Web_shortcut_ignores_other_text(string text) => Assert.False(WebShortcutSearchSource.TryParse(text, out _, out _));

    [Fact]
    public void Web_shortcut_opens_escaped_url()
    {
        var shell = new FakeShell();

        new WebShortcutSearchSource(WebShortcutSearchSource.Defaults, shell, T).Search(Q("gg:kedi köpek")).Single().Execute();

        Assert.Equal(["open https://www.google.com/search?q=kedi%20k%C3%B6pek"], shell.Log);
    }

    [Fact]
    public void Unknown_web_key_gives_nothing() => Assert.Empty(new WebShortcutSearchSource(WebShortcutSearchSource.Defaults, new FakeShell(), T).Search(Q("zz:kedi")));

    [Fact]
    public void Web_fallback_searches_in_default_chromium_browser_with_its_own_engine()
    {
        var shell = new FakeShell();
        var chrome = DefaultBrowserSearch.FromOpenCommand(@"""C:\Chrome\chrome.exe"" --single-argument %1");

        var row = new WebShortcutSearchSource(WebShortcutSearchSource.Defaults, shell, T, () => chrome).Fallback(Q("kedi"))!;
        row.Execute();

        Assert.Equal([@"run C:\Chrome\chrome.exe [--single-argument|? kedi]"], shell.Log);
    }

    [Fact]
    public void Web_fallback_uses_duckduckgo_when_browser_is_unknown()
    {
        var shell = new FakeShell();

        new WebShortcutSearchSource(WebShortcutSearchSource.Defaults, shell, T, () => null).Fallback(Q("kedi"))!.Execute();

        Assert.Equal(["open https://duckduckgo.com/?q=kedi"], shell.Log);
    }

    [Fact]
    public void Web_fallback_uses_duckduckgo()
    {
        var shell = new FakeShell();

        new WebShortcutSearchSource(WebShortcutSearchSource.Defaults, shell, T).Fallback(Q("kedi"))!.Execute();

        Assert.Equal(["open https://duckduckgo.com/?q=kedi"], shell.Log);
    }

    // --- Yer imleri ---

    private const string BookmarksJson = """
        {"roots":{"bookmark_bar":{"type":"folder","children":[
          {"type":"url","name":"Koy Sitesi","url":"https://koy.dev"},
          {"type":"folder","name":"İç","children":[{"type":"url","name":"Haber","url":"http://haber.example"}]},
          {"type":"url","name":"Yerel","url":"chrome://settings"}
        ]},"other":{"type":"folder","children":[]}}}
        """;

    [Fact]
    public void Chromium_bookmarks_are_collected_recursively_and_only_web_links()
    {
        var list = ChromiumBookmarks.Parse(BookmarksJson);

        Assert.Equal(["Koy Sitesi", "Haber"], list.Select(b => b.Title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bozuk {")]
    [InlineData("[]")]
    [InlineData("""{"roots":5}""")]
    public void Broken_bookmark_files_give_nothing(string json) => Assert.Empty(ChromiumBookmarks.Parse(json));

    [Fact]
    public async Task Bookmark_source_loads_in_background_and_lists_all_with_keyword()
    {
        var shell = new FakeShell();
        var source = new BookmarksSearchSource(() => ChromiumBookmarks.Parse(BookmarksJson), shell, T);
        var changed = new TaskCompletionSource();
        source.Changed += (_, _) => changed.TrySetResult();

        source.Prepare();
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, source.Search(Q("yer imleri")).Count());
        Assert.Empty(source.Search(Q("ko")));
        source.Search(Q("koy")).Single().Execute();
        Assert.Equal(["open https://koy.dev"], shell.Log);
    }

    // --- Tarih, pano, yardım ---

    [Fact]
    public void Date_and_time_keywords()
    {
        var shell = new FakeShell();
        var source = new DateTimeSearchSource(shell, T, () => new DateTime(2026, 11, 8, 14, 5, 0), Tr);

        var date = source.Search(Q("tarih")).Single();
        date.Execute();

        Assert.Contains("2026", date.Title);
        Assert.Equal(["copy " + date.Title], shell.Log);
        Assert.Contains("14:05", source.Search(Q("saat")).Single().Title);
        Assert.Empty(source.Search(Q("tari")));
    }

    private sealed class FakeClipboard(params ClipboardItem[] items) : IClipboardHistoryService
    {
        public List<Guid> Copied { get; } = [];
        public bool IsRunning => true;
#pragma warning disable CS0067
        public event EventHandler? Changed;
#pragma warning restore CS0067
        public void Start() { }
        public void Stop() { }
        public IReadOnlyList<ClipboardItem> Query(ClipboardFilter filter = ClipboardFilter.All, string? search = null) => items;
        public bool SetPinned(Guid id, bool pinned) => true;
        public bool Remove(Guid id) => true;
        public void Clear() { }
        public bool CopyBack(Guid id) { Copied.Add(id); return true; }
        public void Dispose() { }
    }

    private static ClipboardItem Clip(string text) => new(Guid.NewGuid(), text, ClipboardKind.Text, false, DateTimeOffset.Now);

    [Fact]
    public void Clipboard_needs_three_letters_and_is_capped()
    {
        var source = new ClipboardSearchSource(new FakeClipboard(Clip("a"), Clip("b"), Clip("c"), Clip("d")), T);

        Assert.Empty(source.Search(Q("ab")));
        Assert.Equal(3, source.Search(Q("abc")).Count());
    }

    [Fact]
    public void Clipboard_result_copies_back_and_flattens_long_text()
    {
        var item = Clip("satır bir\nsatır iki " + new string('x', 200));
        var service = new FakeClipboard(item);

        var result = new ClipboardSearchSource(service, T).Search(Q("satır")).Single();
        result.Execute();

        Assert.DoesNotContain("\n", result.Title);
        Assert.True(result.Title.Length <= 81);
        Assert.True(result.RestoresFocus);
        Assert.Equal([item.Id], service.Copied);
    }

    private static HelpSearchSource Help(params ISearchSource[] sources)
    {
        HelpSearchSource? help = null;
        help = new HelpSearchSource(() => [help!, .. sources], T);
        return help;
    }

    private sealed class WithSyntax : SearchSource
    {
        public WithSyntax(string id, string name, params SearchSyntax[] syntaxes) : base(id, name) => Syntaxes = syntaxes;

        public override IEnumerable<SearchResult> Search(SearchQuery query) => [];
    }

    [Fact]
    public void Help_lists_documented_sources_and_row_expands_to_details()
    {
        var help = Help(new WithSyntax("web", "Web", new SearchSyntax("gg:ara", "Google")), new WithSyntax("hidden", "Gizli"));

        var row = help.Search(Q("?")).Single();

        Assert.Equal("gg:ara", row.Title);
        Assert.Equal("?Web", row.ReplacesQuery);
        Assert.True(row.SkipHistory);
        Assert.Empty(help.Search(Q("web")));
    }

    [Fact]
    public void Help_keyword_works_like_question_mark() => Assert.Single(Help(new WithSyntax("a", "A", new SearchSyntax("x", "y"))).Search(Q("yardım")));

    [Fact]
    public void Help_shows_every_example_for_a_named_source_in_order()
    {
        var help = Help(new WithSyntax("web", "Web", new SearchSyntax("gg:ara", "Google"), new SearchSyntax("yt:ara", "YouTube")));

        var rows = help.Search(Q("?web")).ToList();

        Assert.Equal(["gg:ara", "yt:ara"], rows.Select(r => r.Title));
        Assert.True(rows[0].Relevance > rows[1].Relevance);
        Assert.Equal("yt:ara", rows[1].ReplacesQuery);
    }
}
