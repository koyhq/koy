using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class SettingsPropertyEffectsTests
{
    // Ayar alanı olup düz v3 adı SettingsKeyMap'te olmayan, ViewModel'e özgü adlı özellikler (ayar yine yazılır).
    // Bunlar dışında etkisi olan her ad SettingsKeyMap.V3ToV4 içinde bulunmak zorunda: adı yanlış yazılan ya da ayarı
    // olmayan bir ad burada yakalanır.
    private static readonly HashSet<string> ViewModelOnly = new(StringComparer.Ordinal)
    {
        "MotionNotchOpenClose", "MotionTabTransitions", "MotionOsd", "MotionDockMagnify", "MotionFolderPanel",
        "UiFontFamily", "ClockFontFamily", "BaseFontSize", "UiFontWeight", "UiScalePercent", "UserStyleOverridesEnabled",
        "IconPack", "IconPackFolder",
        "NotchOpenDelayMs", "NotchCloseDelayMs", "NotchTabBarStyle", "NotchTabOrderVersion", "HomeCardOrderVersion",
        "VisualizerStyle", "VisualizerColorSource", "VisualizerBarCount",
        "LiveBackgroundTarget", "LiveBackgroundSource", "LiveBackgroundPreset", "LiveBackgroundFilePath", "LiveBackgroundScrimPercent",
        "PanelFocus", "PanelShelf", "PanelCalendar", "PanelControlCenter", "PanelClipboard", "PanelSystem",
        "ClipboardHistoryEnabled", "SearchSourcesVersion",
        "ClockHourFormat", "ClockShowSeconds", "ClockShowWeekNumber", "ClockFirstDayOfWeek", "ClockCustomDateFormat",
        "ClockShowDate", "ClockSecondTimeZoneId", "TemperatureUnit", "WindSpeedUnit", "NetworkSpeedUnit",
        "NotchFollowsMaterial", "MiddleClickAction", "WheelAction", "HideWindowsTaskbar",
    };

    private static bool IsSettingsBacked(string name) =>
        SettingsKeyMap.V3ToV4.ContainsKey(name) || ViewModelOnly.Contains(name);

    [Fact]
    public void Properties_that_are_not_saved_never_map_to_a_settings_key()
    {
        Assert.All(SettingsPropertyEffects.NotSaved, name =>
        {
            Assert.DoesNotContain(name, SettingsKeyMap.V3ToV4.Keys);
            Assert.DoesNotContain(name, ViewModelOnly);
        });
    }

    [Fact]
    public void Every_name_that_triggers_an_effect_is_a_saved_property()
    {
        foreach (var name in SettingsPropertyEffects.AppliesTheme
                     .Concat(SettingsPropertyEffects.RebuildsNotch)
                     .Concat(SettingsPropertyEffects.DockExtraNames))
        {
            Assert.True(IsSettingsBacked(name), $"{name}: ne SettingsKeyMap'te ne de ViewModel'e özgü listede");
            Assert.True(SettingsPropertyEffects.IsSaved(name), $"{name}: kaydedilmeyen özellik etki tetikliyor");
        }
    }

    [Theory]
    [InlineData("SelectedPageIndex", false)]
    [InlineData("SettingsSearchQuery", false)]
    [InlineData("MemoryUsageText", false)]
    [InlineData("DownloadProgressText", false)]
    [InlineData("Theme", true)]
    [InlineData("NotchVisibility", true)]
    [InlineData("DockEdge", true)]
    [InlineData("SomethingNew", true)]
    [InlineData(null, true)]
    public void Only_ui_state_is_skipped_when_saving(string? name, bool saved) =>
        Assert.Equal(saved, SettingsPropertyEffects.IsSaved(name));

    [Theory]
    [InlineData("Theme", true)]
    [InlineData("UiScalePercent", true)]
    [InlineData("UserStyleOverridesEnabled", true)]
    [InlineData("NotchVisibility", false)]
    [InlineData(null, false)]
    public void Theme_is_reapplied_for_appearance_properties_only(string? name, bool expected) =>
        Assert.Equal(expected, SettingsPropertyEffects.AppliesThemeTo(name));

    [Theory]
    [InlineData("DockEdge", true)]
    [InlineData("DockShowTray", true)]
    [InlineData("DockEnabled", false)]
    [InlineData("HideWindowsTaskbar", true)]
    [InlineData("Material", true)]
    [InlineData("IconPackFolder", true)]
    [InlineData("NotchStyle", false)]
    [InlineData(null, false)]
    public void Dock_is_rebuilt_for_dock_prefixed_names_and_a_few_shared_ones(string? name, bool expected) =>
        Assert.Equal(expected, SettingsPropertyEffects.RebuildsDock(name));

    [Theory]
    [InlineData("NotchStyle", true)]
    [InlineData("MusicShowWaveform", true)]
    [InlineData("VoiceTimeoutSeconds", true)]
    [InlineData("NotchEnabled", false)]
    [InlineData("DockEdge", false)]
    [InlineData("Theme", false)]
    [InlineData(null, false)]
    public void Notch_is_rebuilt_for_notch_music_voice_and_related_names(string? name, bool expected) =>
        Assert.Equal(expected, SettingsPropertyEffects.RebuildsNotchFor(name));

    [Fact]
    public void Enable_switches_use_their_own_path_and_do_not_rebuild()
    {
        Assert.False(SettingsPropertyEffects.RebuildsDock("DockEnabled"));
        Assert.False(SettingsPropertyEffects.RebuildsNotchFor("NotchEnabled"));
    }

    [Fact]
    public void Set_sizes_match_the_previous_hand_written_lists()
    {
        Assert.Equal(44, SettingsPropertyEffects.NotSaved.Count);
        Assert.Equal(21, SettingsPropertyEffects.AppliesTheme.Count);
        Assert.Equal(9, SettingsPropertyEffects.DockExtraNames.Count);
        Assert.Equal(65, SettingsPropertyEffects.RebuildsNotch.Count);
    }
}
