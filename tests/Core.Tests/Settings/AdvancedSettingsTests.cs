using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class AdvancedSettingsTests
{
    [Fact]
    public void Lists_simple_fields_without_dedicated_ui_and_skips_internal_ones()
    {
        var rows = AdvancedSettings.List(new AppSettings(), ["Dock.ShowTray"]);
        var keys = rows.Select(r => r.Key).ToHashSet();

        Assert.Contains("Notch.ClockShowSeconds", keys);
        Assert.Contains("Appearance.UiScalePercent", keys);
        Assert.DoesNotContain("Dock.ShowTray", keys);             // kendi arayüzü var
        Assert.DoesNotContain("General.FirstRunCompleted", keys); // uygulamanın kendi durumu
        Assert.DoesNotContain("Version", keys);
        Assert.DoesNotContain(keys, k => k.StartsWith("DockItems.", StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.StartsWith("Displays.", StringComparison.Ordinal));
        Assert.All(rows, r => Assert.False(r.IsChanged));
    }

    [Fact]
    public void Kinds_and_choices_come_from_the_model()
    {
        var rows = AdvancedSettings.List(new AppSettings(), []).ToDictionary(r => r.Key);

        Assert.Equal(AdvancedSettingKind.Bool, rows["Notch.ClockShowSeconds"].Kind);
        Assert.Equal(AdvancedSettingKind.Integer, rows["Appearance.UiScalePercent"].Kind);
        Assert.Equal(AdvancedSettingKind.Number, rows["Appearance.BaseFontSize"].Kind);
        Assert.Equal(AdvancedSettingKind.Text, rows["Notch.ClockCustomDateFormat"].Kind);
        Assert.Equal(AdvancedSettingKind.Choice, rows["Notch.ClockHourFormat"].Kind);
        Assert.Contains("Hour24", rows["Notch.ClockHourFormat"].Choices);
    }

    [Theory]
    [InlineData("Notch.ClockShowSeconds", "true")]
    [InlineData("Appearance.UiScalePercent", "120")]
    [InlineData("Appearance.BaseFontSize", "14.5")]
    [InlineData("Notch.ClockHourFormat", "hour24")]
    [InlineData("Notch.ClockCustomDateFormat", "dd.MM.yyyy")]
    public void Valid_values_are_written_and_read_back(string key, string text)
    {
        var error = AdvancedSettings.TrySet(new AppSettings(), key, text, out var updated);

        Assert.Equal(AdvancedSettingError.None, error);
        var row = AdvancedSettings.List(updated, []).Single(r => r.Key == key);
        Assert.True(row.IsChanged);
        Assert.Equal(text, row.Value, ignoreCase: true);
    }

    [Theory]
    [InlineData("Notch.ClockShowSeconds", "yes")]
    [InlineData("Appearance.UiScalePercent", "12.5")]
    [InlineData("Appearance.UiScalePercent", "99999999999")]
    [InlineData("Appearance.BaseFontSize", "NaN")]
    [InlineData("Appearance.BaseFontSize", "14,5")]
    [InlineData("Notch.ClockHourFormat", "3")]
    [InlineData("Notch.ClockHourFormat", "Hour13")]
    [InlineData("Notch.ClockCustomDateFormat", "a\nb")]
    public void Invalid_values_are_never_written(string key, string text)
    {
        var current = new AppSettings();

        var error = AdvancedSettings.TrySet(current, key, text, out var updated);

        Assert.Equal(AdvancedSettingError.InvalidValue, error);
        Assert.Same(current, updated);
    }

    [Theory]
    [InlineData("Nope.Thing")]
    [InlineData("Notch")]
    [InlineData("General.FirstRunCompleted")]
    [InlineData("DockItems.PinnedOrder")]
    public void Unknown_or_protected_keys_are_rejected(string key) =>
        Assert.Equal(AdvancedSettingError.UnknownKey, AdvancedSettings.TrySet(new AppSettings(), key, "1", out _));

    [Fact]
    public void Writing_one_field_keeps_every_other_setting()
    {
        var current = new AppSettings { General = new GeneralSettings { Language = "en", FirstRunCompleted = true } };

        AdvancedSettings.TrySet(current, "Notch.ClockShowSeconds", "true", out var updated);

        Assert.Equal(current.General, updated.General);
        Assert.Equal(current.Dock, updated.Dock);
        Assert.True(updated.Notch.ClockShowSeconds);
    }

    [Fact]
    public void Reset_returns_a_row_to_its_default()
    {
        AdvancedSettings.TrySet(new AppSettings(), "Appearance.UiScalePercent", "130", out var changed);

        Assert.Equal(AdvancedSettingError.None, AdvancedSettings.Reset(changed, "Appearance.UiScalePercent", out var reset));
        Assert.Equal(new AppSettings().Appearance.UiScalePercent, reset.Appearance.UiScalePercent);
    }

    [Fact]
    public void AdvancedSetting_populates_section_and_property_name_correctly()
    {
        var rows = AdvancedSettings.List(new AppSettings(), []);
        var fontRow = rows.Single(r => r.Key == "Appearance.BaseFontSize");

        Assert.Equal("Appearance", fontRow.Section);
        Assert.Equal("BaseFontSize", fontRow.PropertyName);
        Assert.Equal("Taban Yazı Boyutu", fontRow.GetFriendlyTitle("tr"));
        Assert.Equal("Base Font Size", fontRow.GetFriendlyTitle("en"));
        Assert.Equal("Görünüm", fontRow.GetSectionTitle("tr"));
        Assert.Equal("Appearance", fontRow.GetSectionTitle("en"));
    }

    [Theory]
    [InlineData("Appearance", "Görünüm", "Appearance")]
    [InlineData("Dock", "Dock", "Dock")]
    [InlineData("Notch", "Çentik", "Notch")]
    [InlineData("Music", "Müzik", "Music")]
    [InlineData("General", "Genel", "General")]
    public void Section_titles_match_expected_translations(string section, string expectedTr, string expectedEn)
    {
        Assert.Equal(expectedTr, AdvancedSettings.GetSectionTitle(section, "tr"));
        Assert.Equal(expectedEn, AdvancedSettings.GetSectionTitle(section, "en"));
    }

    [Theory]
    [InlineData("BaseFontSize", "Taban Yazı Boyutu", "Base Font Size")]
    [InlineData("ClockFontFamily", "Saat Yazı Tipi", "Clock Font")]
    [InlineData("FontFamily", "Yazı Tipi Ailesi", "Font Family")]
    [InlineData("FontWeight", "Yazı Kalınlığı", "Font Weight")]
    [InlineData("IconPack", "Simge Paketi", "Icon Pack")]
    [InlineData("IconPackFolder", "Simge Paketi Klasörü", "Icon Pack Folder")]
    [InlineData("UiScalePercent", "Arayüz Ölçeği (%)", "UI Scale (%)")]
    [InlineData("UserStyleOverridesEnabled", "Kullanıcı Stil Dosyası", "User Style Overrides")]
    [InlineData("DeveloperMode", "Geliştirici Modu", "Developer Mode")]
    public void Property_titles_match_expected_translations(string propertyName, string expectedTr, string expectedEn)
    {
        Assert.Equal(expectedTr, AdvancedSettings.GetFriendlyTitle(propertyName, "tr"));
        Assert.Equal(expectedEn, AdvancedSettings.GetFriendlyTitle(propertyName, "en"));
        // Also supports full key format e.g. "Appearance.BaseFontSize"
        Assert.Equal(expectedTr, AdvancedSettings.GetFriendlyTitle($"Section.{propertyName}", "tr"));
    }

    [Theory]
    [InlineData("SomeFutureCustomSetting", "Some Future Custom Setting")]
    [InlineData("EnableFastRenderingMode", "Enable Fast Rendering Mode")]
    [InlineData("Simple", "Simple")]
    public void Fallback_formatter_splits_pascal_case_into_words(string input, string expected)
    {
        Assert.Equal(expected, AdvancedSettings.SplitPascalCase(input));
        // When untranslated, GetFriendlyTitle falls back to SplitPascalCase
        Assert.Equal(expected, AdvancedSettings.GetFriendlyTitle(input, "tr"));
        Assert.Equal(expected, AdvancedSettings.GetFriendlyTitle(input, "en"));
    }

    [Fact]
    public void GroupBySection_and_ListGrouped_organize_items_by_section()
    {
        var groups = AdvancedSettings.ListGrouped(new AppSettings(), [], "tr");

        Assert.NotEmpty(groups);
        var appearanceGroup = groups.FirstOrDefault(g => g.Section == "Appearance");
        Assert.NotNull(appearanceGroup);
        Assert.Equal("Görünüm", appearanceGroup.Title);
        Assert.Equal("Görünüm", appearanceGroup.SectionTitle);
        Assert.Contains(appearanceGroup.Items, i => i.PropertyName == "BaseFontSize");
    }

    [Fact]
    public void DeveloperMode_setting_defaults_to_false_and_is_mapped_in_settings_key_map()
    {
        var general = new GeneralSettings();
        Assert.False(general.DeveloperMode);

        general.DeveloperMode = true;
        Assert.True(general.DeveloperMode);

        Assert.True(SettingsKeyMap.V3ToV4.ContainsKey("DeveloperMode"));
        var (section, name) = SettingsKeyMap.V3ToV4["DeveloperMode"];
        Assert.Equal("General", section);
        Assert.Equal("DeveloperMode", name);
    }
}
