using Koy.Core.Extensions.Store;
using static Koy.Core.Tests.Extensions.Store.StoreTestKit;

namespace Koy.Core.Tests.Extensions.Store;

public class StoreCatalogTests
{
    private static StoreListState State(
        IEnumerable<StoreItem> items, InstalledRegistry? registry = null, StoreQuery? query = null, params string[] busy) => new(
        new StoreIndex(null, items.ToList()),
        registry ?? InstalledRegistry.Empty,
        busy.ToHashSet(),
        query ?? new StoreQuery());

    private static string[] Ids(StoreListState state) => state.Visible().Select(r => r.Id).ToArray();

    [Fact]
    public void Status_not_installed_installed_update_and_installing()
    {
        var state = State(
            [Item("aa", version: "1.0.0"), Item("bb", version: "2.0.0"), Item("cc", version: "1.0.0"), Item("dd", version: "1.0.0")],
            Registry(Installed("bb", version: "2.0.0"), Installed("cc", version: "0.9.0"), Installed("dd", version: "1.0.0")),
            busy: "dd");

        var rows = state.Visible().ToDictionary(r => r.Id);

        Assert.Equal(StoreItemStatus.NotInstalled, rows["aa"].Status);
        Assert.Equal(StoreItemStatus.Installed, rows["bb"].Status);
        Assert.Equal(StoreItemStatus.UpdateAvailable, rows["cc"].Status);
        Assert.Equal(StoreItemStatus.Installing, rows["dd"].Status);
        Assert.Equal("0.9.0", rows["cc"].InstalledVersion);
        Assert.Equal("1.0.0", rows["cc"].AvailableVersion);
        Assert.Equal(1, state.UpdateCount);
    }

    [Fact]
    public void Older_index_version_is_not_an_update()
    {
        var state = State([Item("aa", version: "1.0.0")], Registry(Installed("aa", version: "2.0.0")));

        Assert.Equal(StoreItemStatus.Installed, state.Visible().Single().Status);
    }

    [Fact]
    public void Same_id_with_other_kind_is_not_an_update()
    {
        var state = State([Item("aa", ExtensionKind.Profile, version: "2.0.0")], Registry(Installed("aa", ExtensionKind.Theme, "1.0.0")));

        Assert.False(state.Visible().Single().HasUpdate);
    }

    [Fact]
    public void Installing_an_installed_update_keeps_it_in_installed_and_updates_filters()
    {
        var items = new[] { Item("aa", version: "2.0.0") };
        var registry = Registry(Installed("aa", version: "1.0.0"));

        Assert.Equal(["aa"], Ids(State(items, registry, new StoreQuery(StoreFilter.Updates), "aa")));
        Assert.Equal(StoreItemStatus.Installing, State(items, registry, busy: "aa").Visible().Single().Status);
    }

    [Fact]
    public void Filters_all_installed_updates()
    {
        var items = new[] { Item("aa"), Item("bb", version: "2.0.0"), Item("cc") };
        var registry = Registry(Installed("bb", version: "1.0.0"), Installed("cc", version: "1.0.0"));

        Assert.Equal(["aa", "bb", "cc"], Ids(State(items, registry, new StoreQuery(StoreFilter.All))).Order());
        Assert.Equal(["bb", "cc"], Ids(State(items, registry, new StoreQuery(StoreFilter.Installed))).Order());
        Assert.Equal(["bb"], Ids(State(items, registry, new StoreQuery(StoreFilter.Updates))));
    }

    [Fact]
    public void File_installed_items_not_in_index_show_under_installed_only()
    {
        var items = new[] { Item("aa") };
        var registry = Registry(Installed("local-one", ExtensionKind.Profile) with { Source = ExtensionSource.File });

        var all = State(items, registry, new StoreQuery(StoreFilter.All)).Visible();
        var installed = State(items, registry, new StoreQuery(StoreFilter.Installed)).Visible();
        var updates = State(items, registry, new StoreQuery(StoreFilter.Updates)).Visible();

        Assert.Contains(all, r => r.Id == "local-one" && r.Item is null && r.Installed is not null);
        Assert.Equal(["local-one"], installed.Select(r => r.Id));
        Assert.Empty(updates);
    }

    [Fact]
    public void Everything_is_visible_without_an_index_when_installed()
    {
        var state = new StoreListState(null, Registry(Installed("aa")), new HashSet<string>(), new StoreQuery());

        Assert.Equal(["aa"], Ids(state));
        Assert.Empty(Ids(state with { Registry = InstalledRegistry.Empty }));
    }

    [Fact]
    public void Category_filters_by_kind_and_null_means_all()
    {
        var items = new[] { Item("aa"), Item("bb", ExtensionKind.IconPack), Item("cc", ExtensionKind.Profile) };

        Assert.Equal(["bb"], Ids(State(items, query: new StoreQuery(Category: ExtensionKind.IconPack))));
        Assert.Equal(3, Ids(State(items, query: new StoreQuery(Category: null))).Length);
    }

    [Fact]
    public void Category_and_filter_combine()
    {
        var items = new[] { Item("aa"), Item("bb", ExtensionKind.IconPack) };
        var registry = Registry(Installed("aa", ExtensionKind.Theme));

        Assert.Empty(Ids(State(items, registry, new StoreQuery(StoreFilter.Installed, ExtensionKind.IconPack))));
        Assert.Equal(["aa"], Ids(State(items, registry, new StoreQuery(StoreFilter.Installed, ExtensionKind.Theme))));
    }

    [Theory]
    [InlineData("nord", "nord")]
    [InlineData("NORD", "nord")]
    [InlineData("ISIK", "isik")]
    [InlineData("ışık", "isik")]
    [InlineData("IŞIK", "isik")]
    [InlineData("İŞİK", "isik")]
    [InlineData("işık", "isik")]
    [InlineData("sifre", "sifre")]
    [InlineData("ŞİFRE", "sifre")]
    public void Search_is_case_and_turkish_letter_insensitive(string query, string target)
    {
        var names = new Dictionary<string, string>
        {
            ["nord"] = "Nord", ["isik"] = "Işık Teması", ["sifre"] = "Şifre", ["other"] = "Baska",
        };
        var items = names.Select(kv => Item("koyhq." + kv.Key, name: kv.Value)).ToArray();

        Assert.Equal(["koyhq." + target], Ids(State(items, query: new StoreQuery(Search: query))));
    }

    [Fact]
    public void Search_matches_summary_and_author_and_requires_every_word()
    {
        var items = new[]
        {
            Item("aa", name: "Alpha", summary: "Koyu mavi tema", author: "Ayşe"),
            Item("bb", name: "Beta", summary: "Acik tema", author: "Mehmet"),
        };

        Assert.Equal(["aa"], Ids(State(items, query: new StoreQuery(Search: "MAVI"))));
        Assert.Equal(["bb"], Ids(State(items, query: new StoreQuery(Search: "mehmet"))));
        Assert.Equal(["aa"], Ids(State(items, query: new StoreQuery(Search: "ayse koyu"))));
        Assert.Empty(Ids(State(items, query: new StoreQuery(Search: "ayse acik"))));
        Assert.Equal(2, Ids(State(items, query: new StoreQuery(Search: "  "))).Length);
        Assert.Equal(2, Ids(State(items, query: new StoreQuery(Search: null))).Length);
    }

    [Fact]
    public void Search_does_not_match_id_or_description()
    {
        var items = new[] { Item("koyhq.secret", name: "Alpha") };

        Assert.Empty(Ids(State(items, query: new StoreQuery(Search: "secret"))));
    }

    [Fact]
    public void Sort_newest_is_updated_descending_with_name_tiebreak()
    {
        var items = new[]
        {
            Item("aa", name: "Zeta", updated: "2026-01-01"),
            Item("bb", name: "Beta", updated: "2026-03-01"),
            Item("cc", name: "Alpha", updated: "2026-03-01"),
        };

        Assert.Equal(["cc", "bb", "aa"], Ids(State(items, query: new StoreQuery(Sort: StoreSort.Newest))));
    }

    [Fact]
    public void Sort_alphabetical_uses_turkish_folding()
    {
        var items = new[]
        {
            Item("aa", name: "Zeta"), Item("bb", name: "Çam"), Item("cc", name: "Arı"), Item("dd", name: "ısı"),
        };

        // Katlanmış: ari, cam, isi, zeta
        Assert.Equal(["cc", "bb", "dd", "aa"], Ids(State(items, query: new StoreQuery(Sort: StoreSort.Alphabetical))));
    }

    [Fact]
    public void Sort_most_downloads_is_descending()
    {
        var items = new[] { Item("aa", name: "A", downloads: 5), Item("bb", name: "B", downloads: 50), Item("cc", name: "C", downloads: 5) };

        Assert.Equal(["bb", "aa", "cc"], Ids(State(items, query: new StoreQuery(Sort: StoreSort.MostDownloads))));
    }

    [Fact]
    public void Applied_flag_comes_from_the_registry()
    {
        var state = State([Item("aa")], Registry(Installed("aa", applied: true)));

        Assert.True(state.Visible().Single().Applied);
    }

    [Fact]
    public void Initial_state_is_empty()
    {
        Assert.Empty(StoreListState.Initial.Visible());
        Assert.Equal(0, StoreListState.Initial.UpdateCount);
    }
}
