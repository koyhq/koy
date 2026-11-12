using Koy.Core.Appearance;
using Koy.Core.Settings;
using Koy.Core.Settings.Profiles;

namespace Koy.Core.Tests.Settings;

public class ProfileLibraryTests
{
    private static readonly IReadOnlyList<SavedProfile> Empty = [];

    private static AppSettings Source() => new()
    {
        Appearance = new AppearanceSettings { Theme = AppTheme.Light, CornerRadius = 17.0 },
        Music = new MusicSettings { ShowWaveform = false },
    };

    private static IReadOnlyList<SavedProfile> One(string name = "Work", string id = "a")
    {
        var result = ProfileLibrary.Create(Empty, Source(), name, [ProfileSection.Appearance, ProfileSection.Music], id);
        Assert.True(result.IsSuccess);
        return result.Profiles;
    }

    [Fact]
    public void Create_stores_a_valid_codec_text_with_the_chosen_sections()
    {
        var list = One();

        var saved = Assert.Single(list);
        Assert.Equal("a", saved.Id);
        Assert.Equal("Work", saved.Name);
        Assert.Equal([ProfileSection.Appearance, ProfileSection.Music], ProfileLibrary.SectionsOf(saved));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\nname")]
    public void Create_rejects_invalid_names(string name)
    {
        var result = ProfileLibrary.Create(Empty, Source(), name, [ProfileSection.Dock], "x");

        Assert.Equal(ProfileLibraryError.InvalidName, result.Error);
        Assert.Empty(result.Profiles);
    }

    [Fact]
    public void Create_rejects_no_sections_and_undefined_sections()
    {
        Assert.Equal(ProfileLibraryError.NoSections, ProfileLibrary.Create(Empty, Source(), "A", [], "x").Error);
        Assert.Equal(ProfileLibraryError.NoSections, ProfileLibrary.Create(Empty, Source(), "A", [(ProfileSection)99], "x").Error);
    }

    [Fact]
    public void Create_rejects_a_duplicate_name_ignoring_case()
    {
        var result = ProfileLibrary.Create(One("Work"), Source(), " work ", [ProfileSection.Dock], "b");

        Assert.Equal(ProfileLibraryError.DuplicateName, result.Error);
        Assert.Single(result.Profiles);
    }

    [Fact]
    public void Create_stops_at_the_profile_limit()
    {
        var full = Enumerable.Range(0, ProfileLibrary.MaxProfiles).Select(i => new SavedProfile("id" + i, "P" + i, "x")).ToList();

        Assert.Equal(ProfileLibraryError.LimitReached, ProfileLibrary.Create(full, Source(), "New", [ProfileSection.Dock], "n").Error);
    }

    [Fact]
    public void Find_prefers_id_then_matches_name_ignoring_case()
    {
        var list = One("Work", "id-1");

        Assert.Equal("id-1", ProfileLibrary.Find(list, "id-1")!.Id);
        Assert.Equal("id-1", ProfileLibrary.Find(list, "  WORK ")!.Id);
        Assert.Null(ProfileLibrary.Find(list, "Play"));
        Assert.Null(ProfileLibrary.Find(list, null));
        Assert.Null(ProfileLibrary.Find(list, " "));
    }

    [Fact]
    public void Apply_layers_only_the_profile_sections_over_current_settings()
    {
        var list = One();
        var current = new AppSettings
        {
            General = new GeneralSettings { Language = "en" },
            Appearance = new AppearanceSettings { Theme = AppTheme.Dark },
            Music = new MusicSettings { ShowWaveform = true },
            Notch = new NotchSettings { Enabled = false },
        };

        var result = ProfileLibrary.Apply(list, current, "Work");

        Assert.True(result.IsSuccess);
        Assert.Equal(AppTheme.Light, result.Settings!.Appearance.Theme);
        Assert.False(result.Settings.Music.ShowWaveform);
        Assert.Equal("en", result.Settings.General.Language);
        Assert.False(result.Settings.Notch.Enabled);
        Assert.Equal([ProfileSection.Appearance, ProfileSection.Music], result.ChangedSections);
    }

    [Fact]
    public void Apply_reports_no_changed_sections_when_settings_already_match()
    {
        var result = ProfileLibrary.Apply(One(), Source(), "a");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.ChangedSections);
    }

    [Fact]
    public void Apply_unknown_profile_is_not_found()
    {
        var result = ProfileLibrary.Apply(One(), Source(), "nope");

        Assert.Equal(ProfileLibraryError.NotFound, result.Error);
        Assert.Null(result.Settings);
    }

    [Fact]
    public void Apply_corrupt_stored_text_is_rejected_without_settings()
    {
        IReadOnlyList<SavedProfile> list = [new SavedProfile("a", "Work", "not json")];

        var result = ProfileLibrary.Apply(list, Source(), "a");

        Assert.Equal(ProfileLibraryError.CorruptProfile, result.Error);
        Assert.Null(result.Settings);
    }

    [Fact]
    public void Apply_does_not_modify_the_input_settings()
    {
        var current = new AppSettings();

        ProfileLibrary.Apply(One(), current, "a");

        Assert.Equal(new AppSettings(), current);
    }

    [Fact]
    public void Rename_keeps_the_id_and_updates_the_name_inside_the_text()
    {
        var result = ProfileLibrary.Rename(One("Work", "id-1"), "id-1", "Office");

        Assert.True(result.IsSuccess);
        Assert.Equal("id-1", result.Profile!.Id);
        Assert.Equal("Office", result.Profile.Name);
        Assert.Equal("Office", SettingsProfileCodec.Read(result.Profile.Json).Profile!.Name);
    }

    [Fact]
    public void Rename_to_its_own_name_with_different_case_is_allowed()
    {
        Assert.True(ProfileLibrary.Rename(One("Work", "id-1"), "id-1", "WORK").IsSuccess);
    }

    [Fact]
    public void Rename_rejects_a_taken_name_an_invalid_name_and_an_unknown_id()
    {
        var two = ProfileLibrary.Create(One("Work", "a"), Source(), "Play", [ProfileSection.Dock], "b").Profiles;

        Assert.Equal(ProfileLibraryError.DuplicateName, ProfileLibrary.Rename(two, "b", "work").Error);
        Assert.Equal(ProfileLibraryError.InvalidName, ProfileLibrary.Rename(two, "b", " ").Error);
        Assert.Equal(ProfileLibraryError.NotFound, ProfileLibrary.Rename(two, "zzz", "X").Error);
    }

    [Fact]
    public void Duplicate_adds_a_copy_with_a_unique_name_and_new_id()
    {
        var once = ProfileLibrary.Duplicate(One("Work", "a"), "a", "b");
        var twice = ProfileLibrary.Duplicate(once.Profiles, "a", "c");

        Assert.Equal("Work (2)", once.Profile!.Name);
        Assert.Equal("Work (3)", twice.Profile!.Name);
        Assert.Equal(3, twice.Profiles.Count);
        Assert.Equal(ProfileLibrary.SectionsOf(twice.Profiles[0]), ProfileLibrary.SectionsOf(twice.Profile));
    }

    [Fact]
    public void UniqueName_keeps_the_result_within_the_name_limit()
    {
        var longName = new string('x', SettingsProfileCodec.MaxNameLength);
        var list = ProfileLibrary.Create(Empty, Source(), longName, [ProfileSection.Dock], "a").Profiles;

        var unique = ProfileLibrary.UniqueName(list, longName);

        Assert.True(SettingsProfileCodec.IsValidName(unique));
        Assert.EndsWith(" (2)", unique);
    }

    [Fact]
    public void Delete_removes_only_the_target()
    {
        var two = ProfileLibrary.Create(One("Work", "a"), Source(), "Play", [ProfileSection.Dock], "b").Profiles;

        var result = ProfileLibrary.Delete(two, "a");

        Assert.Equal("Play", Assert.Single(result.Profiles).Name);
        Assert.Equal(ProfileLibraryError.NotFound, ProfileLibrary.Delete(two, "zzz").Error);
    }

    [Fact]
    public void AddImported_resolves_a_name_clash_instead_of_overwriting()
    {
        var file = SettingsProfileCodec.Export(Source(), "Work", [ProfileSection.Appearance]);
        var imported = SettingsProfileCodec.Read(file).Profile!;

        var result = ProfileLibrary.AddImported(One("Work", "a"), imported, "b");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Profiles.Count);
        Assert.Equal("Work (2)", result.Profile!.Name);
        Assert.Equal([ProfileSection.Appearance], ProfileLibrary.SectionsOf(result.Profile));
    }

    [Fact]
    public void Operations_do_not_mutate_the_input_list()
    {
        var list = One();

        ProfileLibrary.Delete(list, "a");
        ProfileLibrary.Duplicate(list, "a", "b");

        Assert.Single(list);
    }

    [Fact]
    public void Store_round_trips_through_a_file_and_drops_broken_entries()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-profiles-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProfileStore(Path.Combine(dir, "profiles.json"));
            Assert.Empty(store.Load());

            var list = One("Work", "a");
            Assert.True(store.Save(list));
            Assert.Equal(list, store.Load());

            File.WriteAllText(store.Path, """
                [{"Id":"","Name":"x","Json":"x"},{"Id":"k","Name":"ok","Json":"x"},{"Id":"k","Name":"dup","Json":"x"}]
                """);
            Assert.Equal("ok", Assert.Single(store.Load()).Name);

            File.WriteAllText(store.Path, "garbage");
            Assert.Empty(store.Load());
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Store_ignores_null_entries_in_the_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-profiles-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var store = new ProfileStore(Path.Combine(dir, "profiles.json"));
            File.WriteAllText(store.Path, """[null,{"Id":"k","Name":"ok","Json":"x"}]""");

            Assert.Equal("ok", Assert.Single(store.Load()).Name);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
