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
}
