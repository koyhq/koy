using Koy.Core.Appearance;
using Koy.Core.Settings;

namespace Koy.Core.Tests;

// Simge paketi, uygulama başına simge değişimi ve hangisinin kazandığı (AGENTS: dosya okuma ve çizim Core dışında).
public class AppIconOverrideTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-icon-tests-" + Guid.NewGuid());

    [Theory]
    [InlineData("Notepad.lnk", true)]
    [InlineData("  Chrome  ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("a\\b.lnk", false)]
    [InlineData("a/b.lnk", false)]
    [InlineData("c:x", false)]
    [InlineData("bad\nname", false)]
    public void Key_must_be_a_plain_shortcut_name(string? key, bool expected)
    {
        Assert.Equal(expected, AppIconOverrides.IsValidKey(key));
    }

    [Fact]
    public void Key_length_is_capped()
    {
        Assert.True(AppIconOverrides.IsValidKey(new string('a', AppIconOverrides.MaxLength)));
        Assert.False(AppIconOverrides.IsValidKey(new string('a', AppIconOverrides.MaxLength + 1)));
    }

    [Theory]
    [InlineData("icon.png", true, "icon.png")]
    [InlineData("ICON.SVG", true, "ICON.SVG")]
    [InlineData("  icon.ico  ", true, "icon.ico")]
    [InlineData("C:\\Users\\me\\icons\\app.png", true, "C:\\Users\\me\\icons\\app.png")]
    [InlineData("C:\\Users\\me\\..\\evil.png", false, null)]
    [InlineData("..\\icons\\app.png", false, null)]
    [InlineData("icons\\app.png", false, null)]
    [InlineData("C:\\app.exe", false, null)]
    [InlineData("C:\\run.ps1", false, null)]
    [InlineData("icon.png|calc", false, null)]
    [InlineData("//server/share/a.png", false, null)]
    [InlineData("\\\\server\\share\\a.png", false, null)]
    [InlineData("\\\\?\\C:\\a.png", false, null)]
    [InlineData("\\\\.\\C:\\a.png", false, null)]
    [InlineData("C:\\x.exe:evil.png", false, null)]
    [InlineData("C:\\a.png:x.png", false, null)]
    [InlineData("C:\\.\\a.png", false, null)]
    [InlineData("C:\\icons\\.\\app.png", false, null)]
    [InlineData("C:/icons/app.png", false, null)]
    [InlineData("C:app.png", false, null)]
    [InlineData("con.png", false, null)]
    [InlineData("NUL.svg", false, null)]
    [InlineData("com1.ico", false, null)]
    [InlineData("LPT9.tar.png", false, null)]
    [InlineData("COM\u00B9.png", false, null)]
    [InlineData("C:\\icons\\aux.png", false, null)]
    [InlineData("C:\\nul\\a.png", false, null)]
    [InlineData("C:\\icons.\\a.png", false, null)]
    [InlineData("C:\\icons \\a.png", false, null)]
    [InlineData("C:\\...\\a.png", false, null)]
    [InlineData("C:\\icons\\\\a.png", false, null)]
    [InlineData("console.png", true, "console.png")]
    [InlineData("com10.png", true, "com10.png")]
    [InlineData("my.con.png", true, "my.con.png")]
    [InlineData("C:\\icons/mixed.png", true, "C:\\icons/mixed.png")]
    [InlineData("", false, null)]
    [InlineData(null, false, null)]
    public void Value_must_be_an_image_path_or_name_and_never_a_script(string? value, bool expected, string? normalized)
    {
        var ok = AppIconOverrides.TryValidateSource(value, out var result);

        Assert.Equal(expected, ok);
        if (expected) Assert.Equal(normalized, result);
    }

    [Theory]
    [InlineData("calc.svg", true)]
    [InlineData("C:\\icons\\calc.svg", false)]
    [InlineData("\\\\server\\share\\calc.svg", false)]
    [InlineData("//server/share/calc.svg", false)]
    [InlineData("..\\calc.svg", false)]
    [InlineData("icons/calc.svg", false)]
    [InlineData("calc.svg:evil.png", false)]
    [InlineData("C:\\icons\\.\\calc.svg", false)]
    public void Pack_entry_must_be_a_bare_file_name(string value, bool expected)
    {
        Assert.Equal(expected, AppIconOverrides.TryValidatePackFile(value, out _));
    }

    [Fact]
    public void Device_named_pack_file_falls_back_to_default()
    {
        var choice = IconResolver.Resolve("calc.lnk", null, IconPack.Folder, "nul.png");

        Assert.Equal(new IconChoice(IconSourceKind.Default, null), choice);
    }

    [Fact]
    public void Lookup_is_case_insensitive_on_the_key()
    {
        var map = new ValueDictionary<string>();
        Assert.True(AppIconOverrides.TrySet(map, "Notepad.lnk", "note.png"));

        Assert.Equal("note.png", AppIconOverrides.Lookup(map, "notepad.LNK"));
        Assert.Null(AppIconOverrides.Lookup(map, "calc.lnk"));
    }

    [Fact]
    public void Invalid_set_leaves_the_map_unchanged()
    {
        var map = new ValueDictionary<string> { ["calc.lnk"] = "calc.png" };

        Assert.False(AppIconOverrides.TrySet(map, "calc.lnk", "run.exe"));
        Assert.False(AppIconOverrides.TrySet(map, "", "ok.png"));

        Assert.Single(map);
        Assert.Equal("calc.png", map["calc.lnk"]);
    }

    [Fact]
    public void Reset_removes_the_override_regardless_of_key_case()
    {
        var map = new ValueDictionary<string>();
        AppIconOverrides.TrySet(map, "Notepad.lnk", "note.png");

        AppIconOverrides.Reset(map, "NOTEPAD.lnk");

        Assert.Empty(map);
    }

    [Fact]
    public void Lookup_ignores_a_stored_value_that_became_invalid()
    {
        // Elle düzenlenmiş settings.json: geçersiz değer haritada olsa bile çözümlemede yok sayılır.
        var map = new ValueDictionary<string> { ["calc.lnk"] = "C:\\Tools\\calc.exe" };

        Assert.Null(AppIconOverrides.Lookup(map, "calc.lnk"));
    }

    [Fact]
    public void Resolve_prefers_app_override_then_pack_then_default()
    {
        var overrides = new ValueDictionary<string> { ["notepad.lnk"] = "note.png" };

        var app = IconResolver.Resolve("Notepad.lnk", overrides, IconPack.Folder, "notepad.svg");
        Assert.Equal(new IconChoice(IconSourceKind.AppOverride, "note.png"), app);

        var pack = IconResolver.Resolve("calc.lnk", overrides, IconPack.Folder, "calc.svg");
        Assert.Equal(new IconChoice(IconSourceKind.Pack, "calc.svg"), pack);

        var fallback = IconResolver.Resolve("calc.lnk", overrides, IconPack.Folder, null);
        Assert.Equal(new IconChoice(IconSourceKind.Default, null), fallback);
    }

    [Fact]
    public void Pack_entry_with_absolute_path_falls_back_to_default()
    {
        var choice = IconResolver.Resolve("calc.lnk", null, IconPack.Folder, "C:\\Tools\\calc.svg");

        Assert.Equal(new IconChoice(IconSourceKind.Default, null), choice);
    }

    [Fact]
    public void Builtin_pack_ignores_the_pack_file()
    {
        var choice = IconResolver.Resolve("calc.lnk", null, IconPack.Builtin, "calc.svg");

        Assert.Equal(new IconChoice(IconSourceKind.Default, null), choice);
    }

    [Fact]
    public void Invalid_pack_file_falls_back_to_default()
    {
        var choice = IconResolver.Resolve("calc.lnk", null, IconPack.Folder, "..\\calc.svg");

        Assert.Equal(new IconChoice(IconSourceKind.Default, null), choice);
    }

    [Fact]
    public void Null_app_key_skips_the_override_map()
    {
        var overrides = new ValueDictionary<string> { ["calc.lnk"] = "calc.png" };

        var choice = IconResolver.Resolve(null, overrides, IconPack.Builtin, null);

        Assert.Equal(new IconChoice(IconSourceKind.Default, null), choice);
    }

    [Fact]
    public void Icon_settings_default_to_builtin_and_no_overrides()
    {
        var settings = new AppSettings();

        Assert.Equal(IconPack.Builtin, settings.Appearance.IconPack);
        Assert.Empty(settings.Appearance.AppIconOverrides);
    }

    [Fact]
    public void Override_maps_with_same_entries_in_any_case_and_order_are_equal()
    {
        var a = new AppSettings { Appearance = new() { AppIconOverrides = new() { ["A.lnk"] = "a.png", ["b.lnk"] = "b.png" } } };
        var b = new AppSettings { Appearance = new() { AppIconOverrides = new() { ["b.LNK"] = "b.png", ["a.lnk"] = "a.png" } } };
        var c = new AppSettings { Appearance = new() { AppIconOverrides = new() { ["a.lnk"] = "other.png", ["b.lnk"] = "b.png" } } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.NotEqual(a.GetHashCode(), c.GetHashCode());
    }

    [Fact]
    public void Icon_settings_survive_a_save_and_load_round_trip()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        var store = new SettingsStore(path);
        var settings = new AppSettings
        {
            Appearance = new() { IconPack = IconPack.Folder, AppIconOverrides = new() { ["Notepad.lnk"] = "C:\\icons\\note.png" } }
        };

        store.Save(settings);
        var loaded = new SettingsStore(path).Load();

        Assert.Equal(IconPack.Folder, loaded.Appearance.IconPack);
        Assert.Equal("C:\\icons\\note.png", loaded.Appearance.AppIconOverrides["notepad.lnk"]);
    }

    [Theory]
    [InlineData("""{ "Appearance": { "AppIconOverrides": null } }""")]
    [InlineData("""{ "Appearance": null }""")]
    public void Null_override_map_in_the_file_is_restored_to_an_empty_map(string json)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, json);

        var loaded = new SettingsStore(path).Load();

        Assert.NotNull(loaded.Appearance.AppIconOverrides);
        Assert.Empty(loaded.Appearance.AppIconOverrides);
        Assert.Null(AppIconOverrides.Lookup(loaded.Appearance.AppIconOverrides, "calc.lnk"));
    }

    [Fact]
    public void Lookup_ignores_a_null_stored_value()
    {
        // Elle düzenlenmiş dosyadan null değer gelebilir; çözümleme patlamadan yok saymalı.
        var map = new ValueDictionary<string> { ["calc.lnk"] = null! };

        Assert.Null(AppIconOverrides.Lookup(map, "calc.lnk"));
    }

    [Fact]
    public void Undefined_icon_pack_number_resolves_to_the_default_icon()
    {
        // Elle yazılmış sayı ("IconPack": 7) enum'a olduğu gibi girer; çözümleme paketi yok sayıp yerleşik simgeye düşmeli.
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, """{ "Appearance": { "IconPack": 7 } }""");

        var loaded = new SettingsStore(path).Load();
        var choice = IconResolver.Resolve("calc.lnk", null, loaded.Appearance.IconPack, "calc.svg");

        Assert.Equal(new IconChoice(IconSourceKind.Default, null), choice);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
