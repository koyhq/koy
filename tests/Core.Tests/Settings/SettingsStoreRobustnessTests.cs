using Koy.Core.Dock.Items;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

/// <summary>Bozuk ya da eksik dosyalar, göç yedeği ve kayıt/okuma döngüsü.</summary>
public class SettingsStoreRobustnessTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-robust-" + Guid.NewGuid().ToString("N"));

    public SettingsStoreRobustnessTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private string PathFor(string name = "settings.json") => Path.Combine(_dir, name);

    [Fact]
    public void Dock_items_beyond_MaxItemCount_are_cut_on_load_keeping_order()
    {
        // 40 öğe: yüklenen model yalnız ilk 32'yi, aynı sırayla tutmalı (Normalize çağrılmadan da).
        var items = string.Join(",", Enumerable.Range(0, 40).Select(i => $$"""{ "Kind": "app", "Id": "app{{i}}.lnk" }"""));
        var loaded = SettingsFile.Load($$"""{ "Version": 4, "DockItems": { "Items": [{{items}}] } }""");

        Assert.Equal(DockItemRules.MaxItemCount, loaded.DockItems.Items.Count);
        Assert.Equal(Enumerable.Range(0, DockItemRules.MaxItemCount).Select(i => $"app{i}.lnk"), loaded.DockItems.Items.Select(e => e.Id));
    }

    [Fact]
    public void Unknown_value_in_an_enum_list_drops_only_that_value()
    {
        var loaded = SettingsFile.Load("""
            { "Version": 4, "Notch": { "HideInFullscreen": false, "HomeCards": ["Weather", "Gelecek", "Music"] } }
            """);

        Assert.False(loaded.Notch.HideInFullscreen);
        Assert.Equal([Koy.Core.Notch.HomeCard.Weather, Koy.Core.Notch.HomeCard.Music], loaded.Notch.HomeCards);
    }

    [Fact]
    public void Null_dock_item_entries_are_dropped_before_the_cap()
    {
        // Boş girdiler geçerli öğenin yerini yememeli; listede null kalırsa okuyan her tüketici çöker.
        var nulls = string.Join(",", Enumerable.Repeat("null", DockItemRules.MaxItemCount));
        var loaded = SettingsFile.Load($$"""{ "Version": 4, "DockItems": { "Items": [{{nulls}}, { "Kind": "app", "Id": "a.lnk" }, null] } }""");

        Assert.All(loaded.DockItems.Items, e => Assert.NotNull(e));
        Assert.Equal("a.lnk", Assert.Single(loaded.DockItems.Items).Id);
    }

    [Fact]
    public void Dock_item_entry_with_null_fields_does_not_break_normalize()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "DockItems": { "Items": [{ "Kind": null, "Id": null, "Command": null }, { "Kind": "spacer", "Id": null }] } }""");

        var specs = DockItemRules.Normalize(loaded.DockItems.Items);

        Assert.Equal(DockItemKind.Spacer, Assert.Single(specs).Kind);
    }

    [Fact]
    public void Null_sections_are_replaced_with_defaults()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "Dock": null, "Notch": null, "DockItems": null }""");

        Assert.NotNull(loaded.Dock);
        Assert.True(loaded.Dock.Enabled);
        Assert.NotNull(loaded.Notch);
        Assert.True(loaded.Notch.Enabled);
        Assert.NotNull(loaded.DockItems);
        Assert.Empty(loaded.DockItems.PinnedOrder);
        Assert.Empty(loaded.DockItems.Folders);
    }

    [Fact]
    public void Null_lists_inside_sections_become_empty_lists()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "DockItems": { "PinnedOrder": null, "Folders": null }, "QuickLaunch": { "Items": null } }""");

        Assert.NotNull(loaded.DockItems.PinnedOrder);
        Assert.Empty(loaded.DockItems.PinnedOrder);
        Assert.NotNull(loaded.DockItems.Folders);
        Assert.Empty(loaded.DockItems.Folders);
        Assert.Empty(loaded.QuickLaunch.Items);
    }

    [Fact]
    public void Unknown_top_level_key_is_ignored_without_losing_other_values()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "Gelecek": 1, "Dock": { "Enabled": false } }""");

        Assert.False(loaded.Dock.Enabled);
        Assert.True(loaded.Dock.HideWindowsTaskbar);
    }

    [Fact]
    public void Migration_backup_is_written_once_and_never_overwritten()
    {
        var path = PathFor();
        var original = SettingsFile.FixtureV3;
        File.WriteAllText(path, original);

        new SettingsStore(path).Load();
        var backup = new SettingsStore(path).BackupPath;
        Assert.True(File.Exists(backup));
        Assert.Equal(original, File.ReadAllText(backup));

        // Yeni bir v3 dosyası gelse de (örn. eski sürüm yeniden yazdı) mevcut yedek üzerine yazılmaz.
        File.WriteAllText(path, """{ "Version": 3, "Language": "en" }""");
        new SettingsStore(path).Load();

        Assert.Equal(original, File.ReadAllText(backup));
    }

    [Fact]
    public void No_backup_is_written_for_a_current_version_file()
    {
        var path = PathFor();
        File.WriteAllText(path, """{ "Version": 4 }""");

        new SettingsStore(path).Load();

        Assert.False(File.Exists(new SettingsStore(path).BackupPath));
    }

    [Fact]
    public void Full_golden_settings_survive_save_and_load()
    {
        var path = PathFor();
        var original = SettingsFile.Load(SettingsFile.FixtureV3);
        var store = new SettingsStore(path);

        store.Save(original);
        var reloaded = store.Load();

        Assert.Equal(original, reloaded);
        Assert.Equal(original.GetHashCode(), reloaded.GetHashCode());
    }

    [Fact]
    public void Saved_file_is_version_4_with_nested_sections()
    {
        var path = PathFor();
        new SettingsStore(path).Save(SettingsFile.Load(SettingsFile.FixtureV3));

        var text = File.ReadAllText(path);
        Assert.Contains("\"Version\": 4", text);
        Assert.Contains("\"Dock\": {", text);
        Assert.False(System.Text.Json.Nodes.JsonNode.Parse(text)!.AsObject().ContainsKey("DockEnabled"));
    }

    [Fact]
    public void Invalid_enum_value_resets_only_its_own_field()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "General": { "Language": "tr" }, "Dock": { "Enabled": false, "Style": "Bogus" } }""");

        Assert.Equal("tr", loaded.General.Language);
        Assert.False(loaded.Dock.Enabled);
        Assert.Equal(new DockSettings().Style, loaded.Dock.Style);
    }

    [Fact]
    public void Invalid_value_in_a_migrated_v3_file_keeps_the_other_settings()
    {
        var loaded = SettingsFile.Load("""{ "Version": 3, "Language": "tr", "NotchEnabled": false, "DockStyle": "Bogus" }""");

        Assert.Equal("tr", loaded.General.Language);
        Assert.False(loaded.Notch.Enabled);
        Assert.Equal(new DockSettings().Style, loaded.Dock.Style);
    }

    [Fact]
    public void Invalid_value_inside_a_dictionary_resets_that_dictionary_but_keeps_other_sections()
    {
        // Sınırlama: JsonException.Path sözlüğün kendisini gösterir (girdiyi değil), bu yüzden sözlüğün tamamı sıfırlanır.
        // Diğer bölümler (burada Language) korunur.
        var loaded = SettingsFile.Load("""{ "Version": 4, "General": { "Language": "tr" }, "Displays": { "Monitors": { "DISPLAY1.x": { "NotchEnabled": "yanlis" } } } }""");

        Assert.Equal("tr", loaded.General.Language);
        Assert.Empty(loaded.Displays.Monitors);
    }
    [Fact]
    public void Invalid_list_entry_drops_only_that_entry_and_keeps_the_section()
    {
        // Tek bozuk klasör kaydı, aynı bölümdeki sabitlenmiş sırayı ve diğer klasörleri silmemeli.
        var loaded = SettingsFile.Load("""
            { "Version": 4,
              "DockItems": { "PinnedOrder": ["code.exe"],
                             "Folders": [ { "Id": "a", "Name": "A", "Items": [] }, { "Id": 5, "Name": "B", "Items": [] }, { "Id": "c", "Name": "C", "Items": [] } ] } }
            """);

        Assert.Equal(["code.exe"], loaded.DockItems.PinnedOrder);
        Assert.Equal(["a", "c"], loaded.DockItems.Folders.Select(f => f.Id));
    }

    [Fact]
    public void Invalid_value_inside_a_nested_list_drops_the_innermost_entry()
    {
        var loaded = SettingsFile.Load("""
            { "Version": 4,
              "DockItems": { "Folders": [ { "Id": "a", "Name": "A", "Items": ["x.exe", 7, "y.exe"] } ] } }
            """);

        Assert.Equal(["x.exe", "y.exe"], Assert.Single(loaded.DockItems.Folders).Items);
    }
}
