using Koy.Core.Extensions;
using Koy.Core.Extensions.Store;
using static Koy.Core.Tests.Extensions.Store.StoreTestKit;

namespace Koy.Core.Tests.Extensions.Store;

public class InstalledRegistryTests
{
    [Fact]
    public void Build_orders_by_id_and_rejects_duplicates()
    {
        Assert.True(InstalledRegistry.TryBuild([Installed("zeta"), Installed("alpha")], out var registry, out _));
        Assert.Equal(["alpha", "zeta"], registry.Entries.Select(e => e.Id));

        Assert.False(InstalledRegistry.TryBuild([Installed("aa"), Installed("aa")], out _, out var error));
        Assert.Equal(InstalledRegistryError.DuplicateId, error);
    }

    [Fact]
    public void Build_enforces_the_64_limit()
    {
        var ok = Enumerable.Range(0, InstalledExtensionList.MaxInstalled).Select(i => Installed("ext-" + i));
        var tooMany = Enumerable.Range(0, InstalledExtensionList.MaxInstalled + 1).Select(i => Installed("ext-" + i));

        Assert.True(InstalledRegistry.TryBuild(ok, out _, out _));
        Assert.False(InstalledRegistry.TryBuild(tooMany, out _, out var error));
        Assert.Equal(InstalledRegistryError.TooManyExtensions, error);
    }

    [Fact]
    public void Build_rejects_invalid_entries()
    {
        Assert.False(InstalledRegistry.TryBuild([Installed("Bad Id")], out _, out var error));
        Assert.Equal(InstalledRegistryError.InvalidEntry, error);
        Assert.False(InstalledRegistry.TryBuild([Installed("ok") with { Version = "1.0" }], out _, out _));
        Assert.False(InstalledRegistry.TryBuild([Installed("ok") with { Kind = (ExtensionKind)99 }], out _, out _));
        Assert.False(InstalledRegistry.TryBuild([Installed("ok") with { License = "" }], out _, out _));
    }

    [Fact]
    public void Upsert_replaces_same_id_and_adds_new()
    {
        var registry = Registry(Installed("aa", version: "1.0.0"));

        Assert.True(registry.TryUpsert(Installed("aa", version: "1.1.0"), out var updated, out _));
        Assert.Equal("1.1.0", Assert.Single(updated.Entries).Version);

        Assert.True(updated.TryUpsert(Installed("bb"), out var two, out _));
        Assert.Equal(2, two.Entries.Count);
        Assert.Equal("1.0.0", registry.Find("aa")!.Version);
    }

    [Fact]
    public void Upsert_of_new_id_fails_at_limit_but_update_of_existing_does_not()
    {
        var full = Registry(Enumerable.Range(0, 64).Select(i => Installed("ext-" + i)).ToArray());

        Assert.False(full.TryUpsert(Installed("new-one"), out _, out var error));
        Assert.Equal(InstalledRegistryError.TooManyExtensions, error);
        Assert.True(full.TryUpsert(Installed("ext-3", version: "2.0.0"), out _, out _));
    }

    [Fact]
    public void Remove_and_find()
    {
        var registry = Registry(Installed("aa"), Installed("bb"));

        Assert.Null(registry.Remove("aa").Find("aa"));
        Assert.NotNull(registry.Remove("aa").Find("bb"));
        Assert.Same(registry.Find("aa"), registry.Find("aa"));
        Assert.Null(registry.Find(null));
    }

    [Fact]
    public void WithApplied_is_exclusive_within_a_kind_only()
    {
        var registry = Registry(
            Installed("theme-a", ExtensionKind.Theme, applied: true),
            Installed("theme-b", ExtensionKind.Theme),
            Installed("icons-a", ExtensionKind.IconPack, applied: true));

        var next = registry.WithApplied("theme-b");

        Assert.False(next.Find("theme-a")!.Applied);
        Assert.True(next.Find("theme-b")!.Applied);
        Assert.True(next.Find("icons-a")!.Applied);
        Assert.False(next.WithoutApplied("theme-b").Find("theme-b")!.Applied);
    }

    [Fact]
    public void Profiles_are_never_marked_applied()
    {
        var registry = Registry(Installed("prof", ExtensionKind.Profile));

        Assert.False(registry.WithApplied("prof").Find("prof")!.Applied);
        Assert.Same(registry, registry.WithApplied("yok"));
    }

    // Dosya gidiş-dönüşü

    [Fact]
    public void Store_round_trips_all_fields_through_a_temp_folder()
    {
        using var dir = new TempDir();
        var store = new InstalledRegistryStore(dir.Combine("sub\\extensions.json"));
        var entry = new InstalledEntry(
            "koyhq.ornek", "1.2.3", ExtensionKind.Background, "Örnek ş", "Yazar", "GPL-3.0-or-later",
            new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.FromHours(3)), ExtensionSource.File, true, "background.gif");

        Assert.True(store.Save(Registry(entry)));
        var loaded = store.Load();

        Assert.Equal(entry, Assert.Single(loaded.Entries));
        Assert.False(File.Exists(store.Path + ".tmp"));
    }

    [Fact]
    public void Saved_file_uses_camel_case_and_string_enums()
    {
        using var dir = new TempDir();
        var store = new InstalledRegistryStore(dir.Combine("extensions.json"));
        store.Save(Registry(Installed("aa", ExtensionKind.IconPack)));

        var text = File.ReadAllText(store.Path);

        Assert.Contains("\"extensions\"", text);
        Assert.Contains("\"kind\": \"IconPack\"", text);
        Assert.Contains("\"source\": \"Store\"", text);
    }

    [Fact]
    public void Missing_file_loads_empty()
    {
        using var dir = new TempDir();

        Assert.Empty(new InstalledRegistryStore(dir.Combine("yok.json")).Load().Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ bozuk")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{ \"version\": 99, \"extensions\": [] }")]
    [InlineData("{ \"version\": 1, \"extensions\": 5 }")]
    public void Corrupt_file_loads_empty_without_throwing(string content)
    {
        using var dir = new TempDir();
        var path = dir.Combine("extensions.json");
        File.WriteAllText(path, content);
        var errors = new List<string>();

        var loaded = new InstalledRegistryStore(path, (s, _) => errors.Add(s)).Load();

        Assert.Empty(loaded.Entries);
    }

    [Fact]
    public void Bad_entries_and_duplicates_are_dropped_but_good_ones_survive()
    {
        using var dir = new TempDir();
        var path = dir.Combine("extensions.json");
        const string good = "{ \"id\": \"aa\", \"version\": \"1.0.0\", \"kind\": \"Theme\", \"name\": \"A\", \"author\": \"X\"," +
            " \"license\": \"MIT\", \"installedAt\": \"2026-11-01T00:00:00+00:00\", \"source\": \"Store\", \"applied\": false }";
        var badId = good.Replace("\"aa\"", "\"../evil\"");
        var dup = good.Replace("\"A\"", "\"Ikinci\"");
        File.WriteAllText(path, "{ \"version\": 1, \"extensions\": [" + good + "," + badId + "," + dup + ", null] }");

        var loaded = new InstalledRegistryStore(path).Load();

        var entry = Assert.Single(loaded.Entries);
        Assert.Equal("A", entry.Name);
    }

    [Fact]
    public void File_with_more_than_64_entries_is_truncated()
    {
        using var dir = new TempDir();
        var path = dir.Combine("extensions.json");
        string One(int i) => "{ \"id\": \"ext-" + i + "\", \"version\": \"1.0.0\", \"kind\": \"Theme\", \"name\": \"A\", \"author\": \"X\"," +
            " \"license\": \"MIT\", \"installedAt\": \"2026-11-01T00:00:00+00:00\", \"source\": \"Store\", \"applied\": false }";
        File.WriteAllText(path, "{ \"version\": 1, \"extensions\": [" + string.Join(",", Enumerable.Range(0, 70).Select(One)) + "] }");

        Assert.Equal(64, new InstalledRegistryStore(path).Load().Entries.Count);
    }

    [Fact]
    public void Update_is_read_modify_write_and_null_means_no_change()
    {
        using var dir = new TempDir();
        var store = new InstalledRegistryStore(dir.Combine("extensions.json"));

        Assert.True(store.Update(r => r.TryUpsert(Installed("aa"), out var next, out _) ? next : null));
        Assert.False(store.Update(_ => null));
        Assert.Equal("aa", Assert.Single(store.Load().Entries).Id);
    }

    [Fact]
    public void Save_to_unwritable_location_returns_false_and_reports()
    {
        using var dir = new TempDir();
        // Yolun üst parçası bir dosya: klasör oluşturulamaz.
        File.WriteAllText(dir.Combine("blocker"), "x");
        var errors = new List<string>();
        var store = new InstalledRegistryStore(dir.Combine("blocker\\extensions.json"), (s, _) => errors.Add(s));

        Assert.False(store.Save(Registry(Installed("aa"))));
        Assert.Single(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ bozuk")]
    [InlineData("null")]
    [InlineData("{ \"version\": 0, \"extensions\": [] }")]
    public void Update_moves_corrupt_file_aside_then_writes(string content)
    {
        using var dir = new TempDir();
        var path = dir.Combine("extensions.json");
        File.WriteAllText(path, content);
        var store = new InstalledRegistryStore(path, (_, _) => { });

        Assert.True(store.Update(r => r.TryUpsert(Installed("aa"), out var next, out _) ? next : null));

        Assert.Equal(content, File.ReadAllText(path + ".bad"));
        Assert.Equal("aa", Assert.Single(store.Load().Entries).Id);
    }

    [Fact]
    public void Update_keeps_an_earlier_bad_backup()
    {
        using var dir = new TempDir();
        var path = dir.Combine("extensions.json");
        File.WriteAllText(path + ".bad", "ilk");
        File.WriteAllText(path, "{ bozuk");
        var store = new InstalledRegistryStore(path, (_, _) => { });

        Assert.True(store.Update(r => r.TryUpsert(Installed("aa"), out var next, out _) ? next : null));

        Assert.Equal("ilk", File.ReadAllText(path + ".bad"));
        Assert.Equal(2, Directory.GetFiles(dir.Path, "extensions.json*.bad").Length);
    }

    [Fact]
    public void Update_never_overwrites_a_newer_version_file()
    {
        using var dir = new TempDir();
        var path = dir.Combine("extensions.json");
        const string newer = "{ \"version\": 99, \"extensions\": [] }";
        File.WriteAllText(path, newer);
        var store = new InstalledRegistryStore(path);

        Assert.False(store.Update(r => r.TryUpsert(Installed("aa"), out var next, out _) ? next : null));
        Assert.False(store.Save(Registry(Installed("aa"))));

        Assert.Equal(newer, File.ReadAllText(path));
        Assert.False(File.Exists(path + ".bad"));
    }

    [Fact]
    public void Update_does_not_write_when_the_file_is_unreadable()
    {
        using var dir = new TempDir();
        var path = dir.Combine("extensions.json");
        var store = new InstalledRegistryStore(path, (_, _) => { });
        Assert.True(store.Save(Registry(Installed("aa"))));
        var before = File.ReadAllText(path);

        // Dosya başka bir süreçte özel kilitli gibi: okuma IOException verir.
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(store.Update(r => r.Remove("aa")));
        }

        Assert.Equal(before, File.ReadAllText(path));
        Assert.Equal("aa", Assert.Single(store.Load().Entries).Id);
    }
}
