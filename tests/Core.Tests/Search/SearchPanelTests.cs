using Koy.Core.Search;

namespace Koy.Core.Tests.Search;

public sealed class SearchPanelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private sealed class FakeSource(params SearchResult[] results) : SearchSource("fake", "Fake")
    {
        public int Prepared { get; private set; }
        public int Released { get; private set; }
        public SearchResult[] Items { get; set; } = results;
        public bool Throws { get; set; }
        public bool Busy { get; set; }

        public override bool IsBusy => Busy;
        public override void Prepare() => Prepared++;
        public override void Release() => Released++;

        public override IEnumerable<SearchResult> Search(SearchQuery query)
        {
            if (Throws)
                throw new InvalidOperationException("kaynak hatası");
            return Items;
        }

        public void Raise() => RaiseChanged();
    }

    private static SearchResult R(string id, string category = "A", int categoryRelevance = SearchRelevance.Moderate, double relevance = 0.5, Action? execute = null) =>
        new(id, category, id, execute ?? (() => { })) { CategoryRelevance = categoryRelevance, Relevance = relevance };

    private SearchHistory History() => new(Path.Combine(_dir, "history.txt"));

    private static SearchPanelController Open(params SearchResult[] results)
    {
        var panel = new SearchPanelController([new FakeSource(results)]);
        panel.Open();
        panel.SetQuery("x");
        return panel;
    }

    // --- Sıralama ---

    [Fact]
    public void Categories_sort_by_importance_and_stay_together()
    {
        var list = SearchLayout.Arrange([
            R("low1", "L", SearchRelevance.Low, 0.9),
            R("high1", "H", SearchRelevance.High, 0.1),
            R("low2", "L", SearchRelevance.Low, 0.95),
            R("high2", "H", SearchRelevance.Moderate, 0.8)]);

        Assert.Equal(["high2", "high1", "low2", "low1"], list.Select(r => r.Id));
    }

    [Fact]
    public void Apps_stay_on_top_and_files_at_the_bottom_whatever_their_importance()
    {
        var list = SearchLayout.Arrange([
            R("file", "F", SearchRelevance.Highest, 0.9) with { Placement = SearchCategoryPlacement.Bottom },
            R("session", "S", SearchRelevance.Low, 0.9),
            R("calc", "C", SearchRelevance.High, 0.9),
            R("app", "A", SearchRelevance.Moderate, 0.1) with { Placement = SearchCategoryPlacement.Top }]);

        Assert.Equal(["app", "calc", "session", "file"], list.Select(r => r.Id));
    }

    [Fact]
    public void Single_category_may_use_whole_limit()
    {
        var list = SearchLayout.Arrange(Enumerable.Range(0, 40).Select(i => R("a" + i)));

        Assert.Equal(SearchLayout.MaxResults, list.Count);
    }

    [Fact]
    public void First_category_gets_at_most_half_and_every_category_is_shown()
    {
        var results = new[] { "A", "B", "C" }.SelectMany(c => Enumerable.Range(0, 20).Select(i => R(c + i, c,
            c == "A" ? SearchRelevance.High : c == "B" ? SearchRelevance.Moderate : SearchRelevance.Low)));

        var list = SearchLayout.Arrange(results);

        Assert.Equal(8, list.Count(r => r.Category == "A")); // 15 / 2 yukarı
        Assert.True(list.Count(r => r.Category == "C") >= 1);
        Assert.True(list.Count <= SearchLayout.MaxResults);
    }

    // --- Panel yaşam döngüsü ---

    [Fact]
    public void Open_prepares_and_close_releases_sources()
    {
        var source = new FakeSource();
        var panel = new SearchPanelController([source]);

        panel.Open();
        panel.Open();
        Assert.Equal(1, source.Prepared);

        panel.Close();
        panel.Close();
        Assert.Equal(1, source.Released);
    }

    [Fact]
    public void Prior_search_is_restored_by_default()
    {
        var panel = Open(R("a"));
        panel.SetQuery("son arama");

        panel.Close();
        panel.Open();

        Assert.Equal("son arama", panel.Query);
        Assert.Single(panel.Results);
    }

    [Fact]
    public void Prior_search_can_be_turned_off()
    {
        var panel = new SearchPanelController([new FakeSource(R("a"))]) { RetainPriorSearch = false };
        panel.Open();
        panel.SetQuery("gizli");

        panel.Close();
        panel.Open();

        Assert.Equal("", panel.Query);
        Assert.Empty(panel.Results);
    }

    [Fact]
    public void Empty_query_shows_nothing()
    {
        var panel = Open(R("a"));

        panel.SetQuery("   ");

        Assert.Empty(panel.Results);
        Assert.Equal(-1, panel.SelectedIndex);
    }

    // --- Seçim ve eylemler ---

    [Fact]
    public void First_result_is_selected_and_selection_wraps()
    {
        var panel = Open(R("a", relevance: 0.9), R("b", relevance: 0.5));
        Assert.Equal(0, panel.SelectedIndex);

        panel.MoveSelection(-1);
        Assert.Equal(1, panel.SelectedIndex);

        panel.MoveSelection(1);
        Assert.Equal(0, panel.SelectedIndex);
    }

    [Fact]
    public void Category_jump_goes_to_next_group_and_wraps()
    {
        var panel = Open(R("a1", "A", SearchRelevance.High, 0.9), R("a2", "A", SearchRelevance.High, 0.8), R("b1", "B", SearchRelevance.Low));

        panel.MoveCategory(1);
        Assert.Equal("b1", panel.Selected?.Id);

        panel.MoveCategory(1);
        Assert.Equal("a1", panel.Selected?.Id);

        panel.MoveCategory(-1);
        Assert.Equal("b1", panel.Selected?.Id);
    }

    [Fact]
    public void Activate_runs_selected_result()
    {
        var ran = 0;
        var result = Open(R("a", execute: () => ran++)).Activate();

        Assert.Equal(SearchActivation.Executed, result.Outcome);
        Assert.Equal(1, ran);
    }

    [Fact]
    public void Activate_with_no_result_does_nothing() =>
        Assert.Equal(SearchActivation.None, new SearchPanelController([]).Activate().Outcome);

    [Fact]
    public void Tab_cycles_actions_then_reports_end()
    {
        var item = R("a") with { Actions = [new SearchAction("x", "X", () => { }), new SearchAction("y", "Y", () => { })] };
        var panel = Open(item);

        Assert.True(panel.CycleAction(1));
        Assert.Equal(0, panel.ActiveAction);
        Assert.True(panel.CycleAction(1));
        Assert.Equal(1, panel.ActiveAction);
        Assert.False(panel.CycleAction(1));

        Assert.True(panel.CycleAction(-1));
        Assert.True(panel.CycleAction(-1));
        Assert.Equal(-1, panel.ActiveAction);
        Assert.False(panel.CycleAction(-1));
    }

    [Fact]
    public void Enter_on_highlighted_action_runs_it_instead_of_the_row()
    {
        var row = 0;
        var action = 0;
        var panel = Open(R("a", execute: () => row++) with { Actions = [new SearchAction("x", "X", () => action++)] });

        panel.CycleAction(1);
        panel.Activate();

        Assert.Equal((0, 1), (row, action));
    }

    [Fact]
    public void Shift_enter_runs_first_action()
    {
        var row = 0;
        var action = 0;
        var panel = Open(R("a", execute: () => row++) with { Actions = [new SearchAction("x", "X", () => action++)] });

        panel.Activate(firstAction: true);

        Assert.Equal((0, 1), (row, action));
    }

    [Fact]
    public void Moving_selection_resets_highlighted_action()
    {
        var item = R("a", relevance: 0.9) with { Actions = [new SearchAction("x", "X", () => { })] };
        var panel = Open(item, R("b"));
        panel.CycleAction(1);

        panel.MoveSelection(1);

        Assert.Equal(-1, panel.ActiveAction);
    }

    // --- Onay ---

    [Fact]
    public void Dangerous_result_needs_second_activation()
    {
        var ran = 0;
        var panel = Open(R("kapat", execute: () => ran++) with { RequiresConfirmation = true });

        Assert.Equal(SearchActivation.NeedsConfirmation, panel.Activate().Outcome);
        Assert.Equal(0, ran);
        Assert.Equal("kapat", panel.PendingConfirmationId);

        Assert.Equal(SearchActivation.Executed, panel.Activate().Outcome);
        Assert.Equal(1, ran);
    }

    [Fact]
    public void Moving_selection_cancels_confirmation()
    {
        var ran = 0;
        var panel = Open(R("kapat", relevance: 0.9, execute: () => ran++) with { RequiresConfirmation = true }, R("b"));
        panel.Activate();

        panel.MoveSelection(1);
        panel.MoveSelection(-1);

        Assert.Null(panel.PendingConfirmationId);
        Assert.Equal(SearchActivation.NeedsConfirmation, panel.Activate().Outcome);
        Assert.Equal(0, ran);
    }

    [Fact]
    public void Typing_cancels_confirmation()
    {
        var panel = Open(R("kapat") with { RequiresConfirmation = true });
        panel.Activate();

        panel.SetQuery("xy");

        Assert.Null(panel.PendingConfirmationId);
    }

    // --- Kaynak olayları ---

    [Fact]
    public void Failing_source_does_not_lose_other_results()
    {
        var panel = new SearchPanelController([new FakeSource { Throws = true }, new FakeSource(R("a"))]);
        panel.Open();

        panel.SetQuery("x");

        Assert.Single(panel.Results);
    }

    [Fact]
    public void Duplicate_targets_keep_the_most_important_one()
    {
        var panel = Open(R("path:x", "Recent", SearchRelevance.Low), R("path:x", "Places", SearchRelevance.High));

        Assert.Equal("Places", Assert.Single(panel.Results).Category);
    }

    [Fact]
    public void Untouched_selection_follows_new_top_result()
    {
        var source = new FakeSource(R("a", relevance: 0.5));
        var panel = new SearchPanelController([source]);
        panel.Open();
        panel.SetQuery("x");

        source.Items = [R("yeni", relevance: 0.9), R("a", relevance: 0.5)];
        source.Raise();

        Assert.Equal("yeni", panel.Selected?.Id);
    }

    [Fact]
    public void Moved_selection_survives_a_refresh()
    {
        var source = new FakeSource(R("a", relevance: 0.9), R("b", relevance: 0.5));
        var panel = new SearchPanelController([source]);
        panel.Open();
        panel.SetQuery("x");
        panel.MoveSelection(1);

        source.Items = [R("yeni", relevance: 0.95), R("a", relevance: 0.9), R("b", relevance: 0.5)];
        source.Raise();

        Assert.Equal("b", panel.Selected?.Id);
    }

    [Fact]
    public void Source_events_go_through_post_and_stop_after_close()
    {
        var posted = 0;
        var source = new FakeSource(R("a"));
        var panel = new SearchPanelController([source], action => { posted++; action(); });
        panel.Open();
        source.Raise();
        Assert.Equal(1, posted);

        panel.Close();
        source.Raise();
        Assert.Equal(1, posted);
    }

    [Fact]
    public void Enter_before_results_runs_first_result_when_it_arrives()
    {
        var ran = 0;
        var source = new FakeSource { Busy = true };
        var panel = new SearchPanelController([source]);
        SearchResult? auto = null;
        panel.AutoActivated += (_, r) => auto = r;
        panel.Open();
        panel.SetQuery("x");

        Assert.Equal(SearchActivation.Deferred, panel.Activate().Outcome);

        source.Items = [R("a", execute: () => ran++)];
        source.Busy = false;
        source.Raise();

        Assert.Equal(1, ran);
        Assert.Equal("a", auto?.Id);
    }

    [Fact]
    public void Typing_cancels_deferred_enter()
    {
        var ran = 0;
        var source = new FakeSource { Busy = true };
        var panel = new SearchPanelController([source]);
        panel.Open();
        panel.SetQuery("x");
        panel.Activate();

        panel.SetQuery("xy");
        source.Items = [R("a", execute: () => ran++)];
        source.Raise();

        Assert.Equal(0, ran);
    }

    [Fact]
    public void Fallback_is_used_only_when_nothing_found_and_search_finished()
    {
        var source = new FakeSource { Busy = true };
        var panel = new SearchPanelController([source], fallback: _ => R("web"));
        panel.Open();
        panel.SetQuery("x");
        Assert.Empty(panel.Results);

        source.Busy = false;
        source.Raise();
        Assert.Equal("web", Assert.Single(panel.Results).Id);

        source.Items = [R("a")];
        source.Raise();
        Assert.Equal("a", Assert.Single(panel.Results).Id);
    }

    [Fact]
    public void Replacing_result_updates_query_and_keeps_panel_open()
    {
        var panel = Open(R("a") with { ReplacesQuery = "96" });
        var cursor = -1;
        panel.QueryReplaced += (_, c) => cursor = c;

        var result = panel.Activate();

        Assert.Equal(SearchActivation.QueryUpdated, result.Outcome);
        Assert.Equal("96", panel.Query);
        Assert.Equal(2, cursor);
    }

    // --- Geçmiş ---

    [Fact]
    public void History_keeps_newest_first_without_duplicates_and_caps()
    {
        var history = History();
        for (var i = 0; i < SearchHistory.MaxEntries + 5; i++)
            history.Add("q" + i);
        history.Add("q10");

        Assert.Equal(SearchHistory.MaxEntries, history.Entries.Count);
        Assert.Equal("q10", history.Entries[0]);
        Assert.Single(history.Entries, e => e == "q10");
    }

    [Fact]
    public void History_persists_between_instances()
    {
        History().Add("kalıcı");

        Assert.Equal("kalıcı", History().Entries[0]);
    }

    [Fact]
    public void History_suggests_newest_match_ignoring_case_and_turkish_letters()
    {
        var history = History();
        history.Add("Şirket raporu");
        history.Add("şirket planı");

        Assert.Equal("şirket planı", history.Suggest("sir"));
        Assert.Null(history.Suggest("yok"));
        Assert.Null(history.Suggest(""));
    }

    private SearchPanelController OpenWithHistory(SearchHistory history, params SearchResult[] results)
    {
        var panel = new SearchPanelController([new FakeSource(results)], history: history, historyCategory: "Son", removeLabel: "Sil");
        panel.Open();
        return panel;
    }

    [Fact]
    public void Executed_search_is_remembered_trimmed()
    {
        var history = History();
        var panel = OpenWithHistory(history, R("a"));
        panel.SetQuery("  chrome ");
        panel.SetQuery("chrome ");

        panel.Activate();

        Assert.Equal("chrome", history.Entries[0]);
    }

    [Fact]
    public void Search_starting_with_space_is_not_remembered()
    {
        var history = History();
        var panel = OpenWithHistory(history, R("a"));
        panel.SetQuery(" gizli");

        panel.Activate();

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void Disabled_mode_records_and_suggests_nothing()
    {
        var history = History();
        history.Add("eski");
        var panel = OpenWithHistory(history, R("a"));
        panel.HistoryMode = SearchHistoryMode.Disabled;
        panel.SetQuery("eski ara");

        panel.Activate();

        Assert.Single(history.Entries);
        Assert.Equal("", panel.Suggestion);
        Assert.False(panel.HasHistory);
    }

    [Fact]
    public void Result_marked_skip_is_not_remembered()
    {
        var history = History();
        var panel = OpenWithHistory(history, R("a") with { SkipHistory = true });
        panel.SetQuery("?");

        panel.Activate();

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void Suggestion_comes_from_history()
    {
        var history = History();
        history.Add("notepad");
        var panel = OpenWithHistory(history);

        panel.SetQuery("not");

        Assert.Equal("notepad", panel.Suggestion);
    }

    [Fact]
    public void History_list_toggles_on_empty_box_and_entry_fills_query()
    {
        var history = History();
        history.Add("eski");
        var panel = OpenWithHistory(history, R("a"));

        panel.ToggleHistory();
        Assert.True(panel.ShowingHistory);
        var row = Assert.Single(panel.Results);
        Assert.Equal("eski", row.Title);

        var result = panel.Activate();

        Assert.Equal(SearchActivation.QueryUpdated, result.Outcome);
        Assert.Equal("eski", panel.Query);
        Assert.False(panel.ShowingHistory);
    }

    [Fact]
    public void History_list_shows_at_most_twenty()
    {
        var history = History();
        for (var i = 0; i < 40; i++)
            history.Add("q" + i);
        var panel = OpenWithHistory(history);

        panel.ToggleHistory();

        Assert.Equal(SearchHistory.VisibleEntries, panel.Results.Count);
    }

    [Fact]
    public void History_entry_can_be_removed_and_list_stays_open()
    {
        var history = History();
        history.Add("bir");
        history.Add("iki");
        var panel = OpenWithHistory(history);
        panel.ToggleHistory();

        var result = panel.Activate(firstAction: true);

        Assert.Equal(SearchActivation.QueryUpdated, result.Outcome);
        Assert.Equal(["bir"], history.Entries);
        Assert.True(panel.ShowingHistory);
        Assert.Single(panel.Results);
    }

    [Fact]
    public void Removing_last_history_entry_closes_the_list()
    {
        var history = History();
        history.Add("tek");
        var panel = OpenWithHistory(history);
        panel.ToggleHistory();

        panel.Activate(firstAction: true);

        Assert.False(panel.ShowingHistory);
        Assert.Empty(panel.Results);
    }

    [Fact]
    public void Clear_history_empties_store_and_list()
    {
        var history = History();
        history.Add("a");
        var panel = OpenWithHistory(history);
        panel.ToggleHistory();

        panel.ClearHistory();

        Assert.Empty(history.Entries);
        Assert.False(panel.ShowingHistory);
    }

    // --- Geçmiş gizliliği ---

    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\u0007b")]
    [InlineData("")]
    public void History_never_saves_control_characters(string term)
    {
        var history = History();
        history.Add("güvenli");
        history.Add(term);

        Assert.Equal(["güvenli"], history.Entries);
        Assert.Equal(["güvenli"], new SearchHistory(Path.Combine(_dir, "history.txt")).Entries);
    }

    [Fact]
    public void History_skips_overlong_entries()
    {
        var history = History();
        history.Add(new string('x', SearchHistory.MaxEntryLength + 1));
        history.Add(new string('y', SearchHistory.MaxEntryLength));

        Assert.Single(history.Entries);
    }

    [Fact]
    public void History_ignores_corrupted_lines_when_loading()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "history.txt"), "iyi\n\u0007kötü\nbaşka\n");

        Assert.Equal(["iyi", "başka"], History().Entries);
    }

    [Fact]
    public void History_is_off_by_default_for_new_settings() =>
        Assert.Equal(SearchHistoryMode.Disabled, new Koy.Core.Settings.AppSettings().SearchHistoryMode);

    [Fact]
    public void Clipboard_query_is_not_saved_to_history()
    {
        var history = History();
        var panel = new SearchPanelController([new FakeSource(R("a"))], history: history) { HistoryMode = SearchHistoryMode.Suggestion };
        panel.Open();
        panel.ReplaceQuery("gizli parola", fromClipboard: true);

        panel.Activate();

        Assert.Empty(history.Entries);
    }
}
