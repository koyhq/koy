using Koy.Core.Appearance;
using Koy.Core.Dock;
using Koy.Core.Settings;
using System.Text.Json.Nodes;
using Koy.Core.Settings.Profiles;

namespace Koy.Core.Tests.Settings;

public class SettingsProfileCodecTests
{
    private static AppSettings Source() => new()
    {
        General = new GeneralSettings { Language = "tr", FirstRunCompleted = true },
        Appearance = new AppearanceSettings { Theme = AppTheme.Light, CornerRadius = 17.0 },
        Dock = new DockSettings { Style = DockStyleMode.FullWidth, StartIconPath = @"C:\Users\ornek\simge.png" },
        Music = new MusicSettings { ShowWaveform = false },
    };

    private static ImportedProfile Profile(string json) => SettingsProfileCodec.Read(json).Profile!;

    [Fact]
    public void Export_then_read_round_trips_the_selected_sections()
    {
        var json = SettingsProfileCodec.Export(Source(), "Sunum", [ProfileSection.Appearance, ProfileSection.Dock]);

        var result = SettingsProfileCodec.Read(json);

        Assert.True(result.IsValid);
        Assert.Equal("Sunum", result.Profile!.Name);
        Assert.Equal([ProfileSection.Appearance, ProfileSection.Dock], result.Profile.Sections);
        Assert.Equal(Source().Appearance, result.Profile.Settings.Appearance);
        Assert.Equal(Source().Dock with { StartIconPath = "" }, result.Profile.Settings.Dock);
    }

    [Fact]
    public void Export_clears_the_local_start_icon_path_from_the_file()
    {
        var json = SettingsProfileCodec.Export(Source(), "Sunum", [ProfileSection.Dock]);

        Assert.Equal("", JsonNode.Parse(json)!["Settings"]!["Dock"]!["StartIconPath"]!.GetValue<string>());
        Assert.DoesNotContain("simge.png", json);
        Assert.DoesNotContain("ornek", json);
    }

    [Fact]
    public void Export_writes_no_unselected_or_machine_specific_section()
    {
        var json = SettingsProfileCodec.Export(Source(), "Sunum", [ProfileSection.Appearance]);

        Assert.Contains("\"Appearance\"", json);
        Assert.DoesNotContain("\"General\"", json);
        Assert.DoesNotContain("\"FirstRunCompleted\"", json);
        Assert.DoesNotContain("\"Dock\"", json);
        Assert.DoesNotContain("\"Displays\"", json);
        Assert.DoesNotContain("\"Weather\"", json);
    }

    [Fact]
    public void Export_sorts_sections_and_removes_duplicates()
    {
        var json = SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Dock, ProfileSection.Appearance, ProfileSection.Dock]);

        Assert.Equal([ProfileSection.Appearance, ProfileSection.Dock], Profile(json).Sections);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\nb")]
    public void Export_rejects_an_invalid_name(string name)
    {
        Assert.Throws<ArgumentException>(() => SettingsProfileCodec.Export(new AppSettings(), name, [ProfileSection.Appearance]));
    }

    [Fact]
    public void Export_rejects_a_name_longer_than_the_limit_and_accepts_the_limit()
    {
        Assert.True(SettingsProfileCodec.IsValidName(new string('a', SettingsProfileCodec.MaxNameLength)));
        Assert.False(SettingsProfileCodec.IsValidName(new string('a', SettingsProfileCodec.MaxNameLength + 1)));
    }

    [Fact]
    public void Export_rejects_an_empty_section_list()
    {
        Assert.Throws<ArgumentException>(() => SettingsProfileCodec.Export(new AppSettings(), "P", []));
    }

    [Fact]
    public void Read_rejects_malformed_json()
    {
        Assert.Equal(ProfileImportError.MalformedJson, SettingsProfileCodec.Read("{").Error);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"Version\":4}")]
    [InlineData("{\"Format\":\"baska\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"Dock\"],\"Settings\":{}}")]
    [InlineData("{\"Format\":\"koy-profile\",\"Name\":\"A\",\"Sections\":[\"Dock\"],\"Settings\":{}}")]
    [InlineData("{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"Dock\"]}")]
    public void Read_rejects_content_that_is_not_a_profile(string json)
    {
        Assert.Equal(ProfileImportError.NotAProfile, SettingsProfileCodec.Read(json).Error);
    }

    [Fact]
    public void Read_rejects_a_newer_format_version()
    {
        const string json = "{\"Format\":\"koy-profile\",\"FormatVersion\":2,\"Name\":\"A\",\"Sections\":[\"Dock\"],\"Settings\":{}}";

        Assert.Equal(ProfileImportError.UnsupportedFormatVersion, SettingsProfileCodec.Read(json).Error);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"a\\u0007b\"")]
    [InlineData("5")]
    public void Read_rejects_an_invalid_name(string jsonName)
    {
        var json = $"{{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":{jsonName},\"Sections\":[\"Dock\"],\"Settings\":{{}}}}";

        Assert.Equal(ProfileImportError.InvalidName, SettingsProfileCodec.Read(json).Error);
    }

    [Theory]
    [InlineData("[\"DockItems\"]")]
    [InlineData("[\"General\"]")]
    [InlineData("[\"Displays\"]")]
    [InlineData("[\"Weather\"]")]
    [InlineData("[\"Bogus\"]")]
    [InlineData("[5]")]
    public void Read_rejects_unknown_or_machine_specific_sections(string sections)
    {
        var json = $"{{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":{sections},\"Settings\":{{}}}}";

        Assert.Equal(ProfileImportError.UnknownSection, SettingsProfileCodec.Read(json).Error);
    }

    [Fact]
    public void Read_rejects_an_empty_section_list()
    {
        const string json = "{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[],\"Settings\":{}}";

        Assert.Equal(ProfileImportError.NoSections, SettingsProfileCodec.Read(json).Error);
    }

    [Fact]
    public void Read_rejects_an_invalid_value_in_a_declared_section()
    {
        const string json = "{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"Dock\"],\"Settings\":{\"Dock\":{\"Style\":\"Bogus\"}}}";

        Assert.Equal(ProfileImportError.InvalidValues, SettingsProfileCodec.Read(json).Error);
    }

    [Fact]
    public void Read_ignores_an_invalid_value_in_a_section_the_file_does_not_declare()
    {
        const string json = "{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"Dock\"],\"Settings\":{\"Dock\":{},\"General\":{\"Language\":5}}}";

        var result = SettingsProfileCodec.Read(json);

        Assert.True(result.IsValid);
        Assert.Equal(new DockSettings(), result.Profile!.Settings.Dock);
    }

    [Fact]
    public void Read_ignores_unknown_keys_inside_a_declared_section()
    {
        const string json = "{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"Dock\"],\"Settings\":{\"Dock\":{\"Gelecek\":true}}}";

        var result = SettingsProfileCodec.Read(json);

        Assert.True(result.IsValid);
        Assert.Equal(new DockSettings(), result.Profile!.Settings.Dock);
    }

    [Fact]
    public void Read_fills_a_declared_section_missing_from_the_settings_with_defaults()
    {
        const string json = "{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"Music\"],\"Settings\":{}}";

        Assert.Equal(new MusicSettings(), Profile(json).Settings.Music);
    }

    [Fact]
    public void Read_rejects_text_over_the_length_limit_before_parsing()
    {
        var oversized = new string(' ', SettingsProfileCodec.MaxLength + 1);

        Assert.Equal(ProfileImportError.TooLarge, SettingsProfileCodec.Read(oversized).Error);
    }

    [Fact]
    public void Apply_changes_only_the_chosen_sections_the_file_declares()
    {
        var profile = Profile(SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Appearance, ProfileSection.Dock]));
        var current = new AppSettings { Appearance = new AppearanceSettings { Theme = AppTheme.Dark } };

        var applied = SettingsProfileCodec.Apply(current, profile, [ProfileSection.Appearance]);

        Assert.Equal(AppTheme.Light, applied.Appearance.Theme);
        Assert.Equal(current.Dock, applied.Dock);
        Assert.Equal(current.General, applied.General);
    }

    [Fact]
    public void Apply_does_not_import_a_section_the_file_does_not_declare()
    {
        var profile = Profile(SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Appearance]));
        var current = new AppSettings();

        var applied = SettingsProfileCodec.Apply(current, profile, [ProfileSection.Appearance, ProfileSection.Dock]);

        Assert.Equal(current.Dock, applied.Dock);
        Assert.Equal(AppTheme.Light, applied.Appearance.Theme);
    }

    [Fact]
    public void Apply_keeps_the_local_start_icon_path()
    {
        var profile = Profile(SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Dock]));
        var current = new AppSettings { Dock = new DockSettings { StartIconPath = @"D:\yerel.png" } };

        var applied = SettingsProfileCodec.Apply(current, profile, [ProfileSection.Dock]);

        Assert.Equal(DockStyleMode.FullWidth, applied.Dock.Style);
        Assert.Equal(@"D:\yerel.png", applied.Dock.StartIconPath);
    }

    [Fact]
    public void Export_clears_live_background_file_path_but_keeps_other_live_background_choices()
    {
        var source = Source() with
        {
            Notch = Source().Notch with
            {
                LiveBackgroundTarget = Koy.Core.Notch.LiveBackgroundTarget.Both,
                LiveBackgroundSource = Koy.Core.Notch.LiveBackgroundSourceKind.LocalFile,
                LiveBackgroundFilePath = @"C:\Users\ornek\loop.gif",
            },
        };

        var json = SettingsProfileCodec.Export(source, "P", [ProfileSection.Notch]);

        Assert.Equal("", JsonNode.Parse(json)!["Settings"]!["Notch"]!["LiveBackgroundFilePath"]!.GetValue<string>());
        Assert.Equal("Both", JsonNode.Parse(json)!["Settings"]!["Notch"]!["LiveBackgroundTarget"]!.GetValue<string>());
    }

    [Fact]
    public void Apply_keeps_the_local_live_background_file_path()
    {
        var source = Source() with
        {
            Notch = Source().Notch with { LiveBackgroundTarget = Koy.Core.Notch.LiveBackgroundTarget.Notch },
        };
        var profile = Profile(SettingsProfileCodec.Export(source, "P", [ProfileSection.Notch]));
        var current = new AppSettings { Notch = new NotchSettings { LiveBackgroundFilePath = @"D:\yerel.gif" } };

        var applied = SettingsProfileCodec.Apply(current, profile, [ProfileSection.Notch]);

        Assert.Equal(Koy.Core.Notch.LiveBackgroundTarget.Notch, applied.Notch.LiveBackgroundTarget);
        Assert.Equal(@"D:\yerel.gif", applied.Notch.LiveBackgroundFilePath);
    }

    [Fact]
    public void Export_clears_local_app_icon_override_paths_from_the_file()
    {
        var source = Source() with
        {
            Appearance = Source().Appearance with { AppIconOverrides = new() { ["notepad"] = @"C:\Users\ornek\not.png" } },
        };

        var json = SettingsProfileCodec.Export(source, "Sunum", [ProfileSection.Appearance]);

        Assert.DoesNotContain("not.png", json);
        Assert.DoesNotContain("ornek", json);
    }

    [Fact]
    public void Export_clears_the_local_icon_pack_folder_and_apply_keeps_the_local_one()
    {
        var source = Source() with
        {
            Appearance = Source().Appearance with { IconPack = IconPack.Folder, IconPackFolder = @"C:\Users\ornek\Paket" },
        };

        var json = SettingsProfileCodec.Export(source, "Sunum", [ProfileSection.Appearance]);
        Assert.DoesNotContain("ornek", json);

        var current = new AppSettings { Appearance = new AppearanceSettings { IconPackFolder = @"D:\yerel" } };
        var applied = SettingsProfileCodec.Apply(current, Profile(json), [ProfileSection.Appearance]);

        Assert.Equal(IconPack.Folder, applied.Appearance.IconPack);
        Assert.Equal(@"D:\yerel", applied.Appearance.IconPackFolder);
    }

    [Fact]
    public void Apply_keeps_the_local_app_icon_overrides()
    {
        var profile = Profile(SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Appearance]));
        var current = new AppSettings
        {
            Appearance = new AppearanceSettings { AppIconOverrides = new() { ["notepad"] = @"D:\yerel.png" } },
        };

        var applied = SettingsProfileCodec.Apply(current, profile, [ProfileSection.Appearance]);

        Assert.Equal(AppTheme.Light, applied.Appearance.Theme);
        Assert.Equal(@"D:\yerel.png", applied.Appearance.AppIconOverrides["notepad"]);
    }

    [Fact]
    public void Apply_does_not_mutate_the_current_settings()
    {
        var profile = Profile(SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Appearance]));
        var current = new AppSettings();
        var snapshot = current with { };

        _ = SettingsProfileCodec.Apply(current, profile, [ProfileSection.Appearance]);

        Assert.Equal(snapshot, current);
    }

    [Fact]
    public void ChangedSections_lists_declared_sections_that_differ_and_is_empty_after_apply()
    {
        var profile = Profile(SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Appearance, ProfileSection.Dock]));
        var current = new AppSettings();

        Assert.Equal([ProfileSection.Appearance, ProfileSection.Dock], SettingsProfileCodec.ChangedSections(current, profile));

        var applied = SettingsProfileCodec.Apply(current, profile, [ProfileSection.Appearance, ProfileSection.Dock]);

        Assert.Empty(SettingsProfileCodec.ChangedSections(applied, profile));
    }

    [Fact]
    public void ChangedSections_ignores_sections_the_file_does_not_declare()
    {
        var profile = Profile(SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Music]));

        Assert.Equal([ProfileSection.Music], SettingsProfileCodec.ChangedSections(new AppSettings(), profile));
    }

    // ---- Tolerantlı okumanın kaçırdığı değerler ----

    private static string ProfileJson(string section, string sectionJson) =>
        $"{{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"{section}\"],\"Settings\":{{\"{section}\":{sectionJson}}}}}";

    // Taşınabilir bölümlerin (ProfileSection) AppSettings'teki kayıt türleri: yeni alanı doğrulamaya eklemeyen olursa test düşer.
    private static IEnumerable<(string Section, System.Reflection.PropertyInfo Property)> PortableProperties() =>
        Enum.GetValues<ProfileSection>().SelectMany(section =>
            typeof(AppSettings).GetProperty(section.ToString())!.PropertyType.GetProperties()
                .Select(property => (section.ToString(), property)));

    [Fact]
    public void Read_rejects_an_undefined_enum_number_in_every_portable_enum_field()
    {
        var checkedFields = 0;
        foreach (var (section, property) in PortableProperties().Where(p => p.Property.PropertyType.IsEnum))
        {
            foreach (var value in new[] { "999", "-1", "\"999\"" })
            {
                var result = SettingsProfileCodec.Read(ProfileJson(section, $"{{\"{property.Name}\":{value}}}"));
                Assert.True(result.Error == ProfileImportError.InvalidValues, $"{section}.{property.Name}={value} kabul edildi");
            }
            checkedFields++;
        }
        Assert.True(checkedFields >= 14);
    }

    [Fact]
    public void Read_accepts_every_default_value_of_every_portable_section()
    {
        var json = SettingsProfileCodec.Export(new AppSettings(), "P", Enum.GetValues<ProfileSection>());

        Assert.True(SettingsProfileCodec.Read(json).IsValid);
    }

    [Fact]
    public void Read_rejects_an_infinite_number_in_every_portable_double_field()
    {
        var checkedFields = 0;
        foreach (var (section, property) in PortableProperties().Where(p => p.Property.PropertyType == typeof(double)))
        {
            // 1e400 System.Text.Json'da hata vermeden +Infinity okunur.
            var result = SettingsProfileCodec.Read(ProfileJson(section, $"{{\"{property.Name}\":1e400}}"));
            Assert.True(result.Error == ProfileImportError.InvalidValues, $"{section}.{property.Name} sonsuz değeri kabul etti");
            checkedFields++;
        }
        Assert.True(checkedFields >= 2);
    }

    [Fact]
    public void Read_rejects_oversized_lists_and_null_items_in_every_portable_list_field()
    {
        var oversized = "[" + string.Join(",", Enumerable.Repeat("\"x\"", SettingsProfileCodec.MaxListItems + 1)) + "]";
        var checkedFields = 0;
        foreach (var (section, property) in PortableProperties().Where(p => p.Property.PropertyType == typeof(ValueList<string>)))
        {
            foreach (var list in new[] { oversized, "[null,\"home\"]" })
            {
                var result = SettingsProfileCodec.Read(ProfileJson(section, $"{{\"{property.Name}\":{list}}}"));
                Assert.True(result.Error == ProfileImportError.InvalidValues, $"{section}.{property.Name} geçersiz listeyi kabul etti");
            }
            checkedFields++;
        }
        Assert.True(checkedFields >= 2);
    }

    [Fact]
    public void Read_accepts_a_list_at_the_item_limit()
    {
        var list = "[" + string.Join(",", Enumerable.Repeat("\"home\"", SettingsProfileCodec.MaxListItems)) + "]";

        Assert.True(SettingsProfileCodec.Read(ProfileJson("Notch", $"{{\"EnabledPanels\":{list}}}")).IsValid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("181")]
    [InlineData("2147483647")]
    public void Read_rejects_pomodoro_minutes_outside_the_runtime_range(string minutes)
    {
        foreach (var field in new[] { "PomodoroWorkMinutes", "PomodoroShortBreakMinutes", "PomodoroLongBreakMinutes" })
            Assert.Equal(ProfileImportError.InvalidValues, SettingsProfileCodec.Read(ProfileJson("Focus", $"{{\"{field}\":{minutes}}}")).Error);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("180")]
    public void Read_accepts_pomodoro_minutes_at_the_range_edges(string minutes)
    {
        Assert.True(SettingsProfileCodec.Read(ProfileJson("Focus", $"{{\"PomodoroWorkMinutes\":{minutes}}}")).IsValid);
    }

    [Theory]
    [InlineData("{\"ToggleNotch\":\"zzz\"}")]
    [InlineData("{\"ToggleNotch\":\"Win+L\"}")]
    [InlineData("{\"ToggleNotch\":\"Ctrl+Alt+Del\"}")]
    [InlineData("{\"ToggleNotch\":\"Ctrl+Alt+R\"}")]
    [InlineData("{\"ToggleNotch\":\"Ctrl+Alt+X\",\"OpenShelf\":\"ctrl+alt+x\"}")]
    public void Read_rejects_an_unparsable_reserved_or_duplicate_shortcut(string shortcuts)
    {
        Assert.Equal(ProfileImportError.InvalidValues, SettingsProfileCodec.Read(ProfileJson("Shortcuts", shortcuts)).Error);
    }

    [Fact]
    public void Read_accepts_cleared_and_valid_shortcuts()
    {
        var result = SettingsProfileCodec.Read(ProfileJson("Shortcuts", "{\"ToggleNotch\":\"\",\"OpenShelf\":\"Ctrl+Shift+F9\"}"));

        Assert.True(result.IsValid);
        Assert.Equal("", result.Profile!.Settings.Shortcuts.ToggleNotch);
    }

    [Fact]
    public void Read_does_not_validate_values_of_sections_the_file_does_not_declare()
    {
        const string json = "{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"Music\"],\"Settings\":{\"Dock\":{\"Style\":999},\"Shortcuts\":{\"ToggleNotch\":\"zzz\"}}}";

        Assert.True(SettingsProfileCodec.Read(json).IsValid);
    }

    [Theory]
    [InlineData("{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Name\":\"A\",\"Sections\":[\"Dock\"],\"Settings\":{\"Dock\":{},\"Dock\":{}}}")]
    [InlineData("{\"Format\":\"koy-profile\",\"FormatVersion\":1,\"Format\":\"koy-profile\",\"Name\":\"A\",\"Sections\":[\"Dock\"],\"Settings\":{}}")]
    public void Read_rejects_duplicate_keys_without_throwing(string json)
    {
        Assert.Equal(ProfileImportError.MalformedJson, SettingsProfileCodec.Read(json).Error);
    }

    [Theory]
    [InlineData(0x202E)]
    [InlineData(0x2066)]
    [InlineData(0x2028)]
    [InlineData(0x2029)]
    public void Names_with_line_separators_or_bidi_overrides_are_invalid(int codePoint)
    {
        Assert.False(SettingsProfileCodec.IsValidName("a" + (char)codePoint + "b"));
    }

    [Fact]
    public void Names_with_emoji_joiners_stay_valid()
    {
        // Erkek + ZWJ (U+200D) + dizüstü bilgisayar: tek görünen emoji; ZWJ yasak sayılmamalı.
        var name = char.ConvertFromUtf32(0x1F468) + (char)0x200D + char.ConvertFromUtf32(0x1F4BB) + " Is";

        Assert.True(SettingsProfileCodec.IsValidName(name));
    }

    [Fact]
    public void Export_rejects_an_undefined_section()
    {
        Assert.Throws<ArgumentException>(() => SettingsProfileCodec.Export(new AppSettings(), "P", [(ProfileSection)99]));
    }

    [Fact]
    public void Apply_result_shares_no_mutable_state_with_the_profile()
    {
        var profile = Profile(SettingsProfileCodec.Export(Source(), "P", [ProfileSection.Notch, ProfileSection.Appearance]));
        var first = SettingsProfileCodec.Apply(new AppSettings(), profile, [ProfileSection.Notch, ProfileSection.Appearance]);

        // Uygulama ayarları yerinde değiştirir (ControlCenterViewModel): profil etkilenmemeli.
        first.Notch.ControlCenterLayout = ["donotdisturb"];
        first.Notch.EnabledPanels.Clear();
        first.Notch.Enabled = !first.Notch.Enabled;
        first.Appearance.Theme = AppTheme.Dark;

        Assert.Equal(new NotchSettings(), profile.Settings.Notch);
        Assert.Equal(AppTheme.Light, profile.Settings.Appearance.Theme);
        var second = SettingsProfileCodec.Apply(new AppSettings(), profile, [ProfileSection.Notch]);
        Assert.Equal(new NotchSettings(), second.Notch);
        Assert.NotSame(profile.Settings.Notch.EnabledPanels, second.Notch.EnabledPanels);
    }
}
