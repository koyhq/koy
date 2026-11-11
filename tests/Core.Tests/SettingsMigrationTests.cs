using System.Reflection;
using System.Text.Json.Nodes;
using Koy.Core.Display;
using Koy.Core.Settings;
using Xunit;

namespace Koy.Core.Tests;

public class SettingsMigrationTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), "koy-mig-" + Guid.NewGuid().ToString("N") + ".json");

    [Theory]
    [InlineData("Primary")]
    [InlineData("All")]
    [InlineData("")]
    public void Version2_primary_or_all_becomes_all_screens_open(string target)
    {
        var migrated = SettingsFile.Load("{ \"Version\": 2, \"MonitorTarget\": \"" + target + "\" }");

        Assert.Equal(AppSettings.CurrentVersion, migrated.Version);
        Assert.Empty(migrated.Displays.Monitors);
        Assert.Equal(NewScreenMode.NotchAndDock, migrated.Displays.NewScreenDefault);
    }

    [Fact]
    public void Version2_specific_monitor_keeps_only_that_screen_open()
    {
        var migrated = SettingsFile.Load("""{ "Version": 2, "MonitorTarget": "\\\\.\\DISPLAY2" }""");

        Assert.True(migrated.Displays.Monitors[@"\\.\DISPLAY2"].NotchEnabled);
        Assert.True(migrated.Displays.Monitors[@"\\.\DISPLAY2"].DockEnabled);
        Assert.Equal(NewScreenMode.None, migrated.Displays.NewScreenDefault);
    }

    [Fact]
    public void Version2_specific_monitor_replaces_an_existing_record_of_the_same_screen()
    {
        var migrated = SettingsFile.Load("""
            { "Version": 2, "MonitorTarget": "ofis",
              "Monitors": { "OFIS": { "NotchEnabled": false, "DockEnabled": false, "Name": "Eski" } } }
            """);

        var record = Assert.Single(migrated.Displays.Monitors);
        Assert.True(record.Value.NotchEnabled && record.Value.DockEnabled);
    }

    [Fact]
    public void Current_version_is_untouched()
    {
        var root = JsonNode.Parse("""{ "Version": 4, "MonitorTarget": "Elle", "NewScreenDefault": "NotchOnly" }""")!.AsObject();

        var original = SettingsMigration.Migrate(root);

        Assert.Equal(AppSettings.CurrentVersion, original);
        Assert.Equal("Elle", (string?)root["MonitorTarget"]);
        Assert.Equal("NotchOnly", (string?)root["NewScreenDefault"]);
        Assert.Null(root["Monitors"]);
    }

    [Fact]
    public void Missing_version_is_treated_as_the_pre_v4_default_and_returns_original_version()
    {
        var root = JsonNode.Parse("""{ "DockEnabled": true }""")!.AsObject();

        Assert.Equal(3, SettingsMigration.Migrate(root));
    }

    [Fact]
    public void Migration_preserves_other_settings()
    {
        var migrated = SettingsFile.Load("""{ "Version": 2, "Language": "en", "DockIconSize": 55 }""");

        Assert.Equal("en", migrated.General.Language);
        Assert.Equal(55, migrated.Dock.IconSize);
    }

    [Fact]
    public void Store_migrates_a_version2_file_on_load()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{ \"Version\": 2, \"MonitorTarget\": \"\\\\\\\\.\\\\DISPLAY2\" }");

            var loaded = new SettingsStore(path).Load();

            Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
            Assert.True(loaded.Displays.Monitors.ContainsKey(@"\\.\DISPLAY2"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Monitor_settings_round_trip_through_json_including_disconnected_ones()
    {
        var path = TempPath();
        try
        {
            var store = new SettingsStore(path);
            var settings = new AppSettings { Displays = new DisplaysSettings { NewScreenDefault = NewScreenMode.None, Monitors = new()
                {
                    ["kimlik-1"] = new MonitorSettings { NotchEnabled = true, DockEnabled = false, Name = "Ofis" },
                    ["kimlik-2"] = new MonitorSettings { NotchEnabled = false, DockEnabled = true }
                } } };
            store.Save(settings);

            var loaded = store.Load();

            Assert.Equal(settings, loaded);
            Assert.Equal("Ofis", loaded.Displays.Monitors["kimlik-1"].Name);
            Assert.Equal(NewScreenMode.None, loaded.Displays.NewScreenDefault);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Golden_v3_fixture_has_every_field_different_from_the_default()
    {
        // Altın dosya her alanı varsayılandan farklı tutmalı; yeni alan eklenirse dosya da güncellenmeli.
        var loaded = SettingsFile.Load(SettingsFile.FixtureV3);
        var defaults = new AppSettings();

        foreach (var property in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Version zaten güncel; Language varsayılanı makinenin diline bağlı (aynı değere denk gelebilir).
            if (property.Name is nameof(AppSettings.Version) or nameof(AppSettings.General.Language))
                continue;
            var value = property.GetValue(loaded);
            var def = property.GetValue(defaults);
            Assert.False(Equals(value, def) || SequenceEqual(value, def), $"{property.Name} altın dosyada varsayılan değerde.");
        }
    }

    [Fact]
    public void Golden_v3_fixture_survives_migration_save_and_reload_unchanged()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, SettingsFile.FixtureV3);
            var store = new SettingsStore(path);
            var migrated = store.Load();

            store.Save(migrated);

            // Yazılan dosya v4'tür ve yeniden okununca hiçbir değer kaybolmaz ya da kaymaz.
            Assert.Equal(AppSettings.CurrentVersion, migrated.Version);
            Assert.Equal(migrated, store.Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool SequenceEqual(object? a, object? b) =>
        a is System.Collections.IEnumerable x && b is System.Collections.IEnumerable y && a is not string &&
        x.Cast<object>().SequenceEqual(y.Cast<object>());
}
