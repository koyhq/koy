using Koy.Core.Appearance;
using Koy.Core.Dock;
using Koy.Core.Media;
using Koy.Core.Notch;
using Koy.Core.Settings;
using Koy.Core.Weather;

namespace Koy.Core.Tests.Settings;

/// <summary>v3 → v4: sayı ve dile bağlı metin değerleri enum adına çevrilir; tanınmayan değer yalnız kendi alanını bozar.</summary>
public class LegacyValueMigrationTests
{
    private static AppSettings LoadWith(string key, string jsonValue) =>
        SettingsFile.Load("{ \"Version\": 3, \"" + key + "\": " + jsonValue + " }");

    [Fact]
    public void Golden_v3_file_lands_every_converted_value_on_the_right_enum()
    {
        var s = SettingsFile.Load(SettingsFile.FixtureV3);

        Assert.Equal(AppSettings.CurrentVersion, s.Version);
        Assert.Equal(AppTheme.Light, s.Appearance.Theme);                                   // "Light"
        Assert.Equal(AnimationSpeed.Fast, s.Appearance.AnimationSpeed);                     // "Hızlı"
        Assert.Equal(UpdateChannel.Preview, s.General.UpdateChannel);                    // "Önizleme"
        Assert.Equal(WeatherRefreshInterval.Hourly, s.Weather.RefreshInterval);   // "1h"
        Assert.Equal(MaterialLevel.Glass, s.Appearance.Material);                           // "Cam"
        Assert.Equal(NotchStyleMode.Floating, s.Notch.Style);                     // "Yüzen"
        Assert.Equal(MusicCardStyle.Artwork, s.Music.CardStyle);                  // "Artwork"
        Assert.Equal(IdleDisplayMode.RingClockDay, s.Notch.IdleDisplay);           // 3
        Assert.Equal(NotchOpenBehavior.Click, s.Notch.OpenBehavior);              // 1
        Assert.Equal(WheelAction.Volume, s.Notch.WheelAction);                         // 1
        Assert.Equal(MiddleClickAction.Close, s.Dock.MiddleClickAction);              // 1
        Assert.Equal(DockIndicatorStyle.Line, s.Dock.IndicatorStyle);             // 1

        // Dönüştürülmeyen alanlar olduğu gibi kalır.
        Assert.Equal("#FF8800", s.Appearance.AccentColor);
        Assert.Equal(36.0, s.Dock.IconSize);
        Assert.Equal(["bookmarks", "files"], s.Search.DisabledSources);
    }

    [Theory]
    [InlineData("System", AppTheme.System)]
    [InlineData("sistem", AppTheme.System)]
    [InlineData("Dark", AppTheme.Dark)]
    [InlineData("Koyu", AppTheme.Dark)]
    [InlineData("Light", AppTheme.Light)]
    [InlineData("Açık", AppTheme.Light)]
    [InlineData("acik", AppTheme.Light)]
    public void Theme_legacy_values(string stored, AppTheme expected) =>
        Assert.Equal(expected, LoadWith("Theme", "\"" + stored + "\"").Appearance.Theme);

    [Theory]
    [InlineData("Katı", MaterialLevel.Solid)]
    [InlineData("Buzlu", MaterialLevel.Frosted)]
    [InlineData("Yarı saydam", MaterialLevel.Frosted)] // iki seviyeli dönemden kalan
    [InlineData("Translucent", MaterialLevel.Frosted)]
    [InlineData("acrylic", MaterialLevel.Frosted)]
    [InlineData("Cam", MaterialLevel.Glass)]
    [InlineData(" glass ", MaterialLevel.Glass)]
    public void Material_legacy_values(string stored, MaterialLevel expected) =>
        Assert.Equal(expected, LoadWith("Material", "\"" + stored + "\"").Appearance.Material);

    [Theory]
    [InlineData("Sakin", AnimationSpeed.Calm)]
    [InlineData("Normal", AnimationSpeed.Normal)]
    [InlineData("Hızlı", AnimationSpeed.Fast)]
    [InlineData("fast", AnimationSpeed.Fast)]
    [InlineData("Kapalı", AnimationSpeed.Off)]
    public void Animation_speed_legacy_values(string stored, AnimationSpeed expected) =>
        Assert.Equal(expected, LoadWith("AnimationSpeed", "\"" + stored + "\"").Appearance.AnimationSpeed);

    [Theory]
    [InlineData("Kararlı", UpdateChannel.Stable)]
    [InlineData("Önizleme", UpdateChannel.Preview)]
    public void Update_channel_legacy_values(string stored, UpdateChannel expected) =>
        Assert.Equal(expected, LoadWith("UpdateChannel", "\"" + stored + "\"").General.UpdateChannel);

    [Theory]
    [InlineData("Yapışık", NotchStyleMode.Pinned)]
    [InlineData("Yüzen", NotchStyleMode.Floating)]
    [InlineData("yuzen", NotchStyleMode.Floating)]
    [InlineData("Floating", NotchStyleMode.Floating)]
    public void Notch_style_legacy_values(string stored, NotchStyleMode expected) =>
        Assert.Equal(expected, LoadWith("NotchStyle", "\"" + stored + "\"").Notch.Style);

    [Theory]
    [InlineData("Neutral", MusicCardStyle.Neutral)]
    [InlineData("Düz", MusicCardStyle.Neutral)]
    [InlineData("Tinted", MusicCardStyle.Tinted)]
    [InlineData("Artwork", MusicCardStyle.Artwork)]
    [InlineData("Albüm kapağı", MusicCardStyle.Artwork)]
    public void Music_card_style_legacy_values(string stored, MusicCardStyle expected) =>
        Assert.Equal(expected, LoadWith("MusicCardStyle", "\"" + stored + "\"").Music.CardStyle);

    [Theory]
    [InlineData("30m", WeatherRefreshInterval.Every30Minutes)]
    [InlineData("1h", WeatherRefreshInterval.Hourly)]
    [InlineData("startup", WeatherRefreshInterval.OnStartup)]
    public void Weather_refresh_legacy_values(string stored, WeatherRefreshInterval expected) =>
        Assert.Equal(expected, LoadWith("WeatherRefreshInterval", "\"" + stored + "\"").Weather.RefreshInterval);

    [Theory]
    [InlineData(0, IdleDisplayMode.ClockAndBattery)]
    [InlineData(1, IdleDisplayMode.ClockOnly)]
    [InlineData(2, IdleDisplayMode.Nothing)]
    [InlineData(3, IdleDisplayMode.RingClockDay)]
    public void Idle_display_ints(int stored, IdleDisplayMode expected) =>
        Assert.Equal(expected, LoadWith("IdleDisplayMode", stored.ToString(System.Globalization.CultureInfo.InvariantCulture)).Notch.IdleDisplay);

    [Theory]
    [InlineData(0, NotchOpenBehavior.Hover)]
    [InlineData(1, NotchOpenBehavior.Click)]
    public void Open_behavior_ints(int stored, NotchOpenBehavior expected) =>
        Assert.Equal(expected, LoadWith("NotchOpenBehavior", stored.ToString(System.Globalization.CultureInfo.InvariantCulture)).Notch.OpenBehavior);

    [Theory]
    [InlineData(0, WheelAction.SwitchPanel)]
    [InlineData(1, WheelAction.Volume)]
    public void Wheel_action_ints(int stored, WheelAction expected) =>
        Assert.Equal(expected, LoadWith("WheelAction", stored.ToString(System.Globalization.CultureInfo.InvariantCulture)).Notch.WheelAction);

    [Theory]
    [InlineData(0, MiddleClickAction.NewWindow)]
    [InlineData(1, MiddleClickAction.Close)]
    public void Middle_click_ints(int stored, MiddleClickAction expected) =>
        Assert.Equal(expected, LoadWith("MiddleClickAction", stored.ToString(System.Globalization.CultureInfo.InvariantCulture)).Dock.MiddleClickAction);

    [Theory]
    [InlineData(0, DockIndicatorStyle.Dot)]
    [InlineData(1, DockIndicatorStyle.Line)]
    public void Dock_indicator_ints(int stored, DockIndicatorStyle expected) =>
        Assert.Equal(expected, LoadWith("DockIndicatorStyle", stored.ToString(System.Globalization.CultureInfo.InvariantCulture)).Dock.IndicatorStyle);

    [Fact]
    public void An_unknown_value_resets_only_its_own_field_and_keeps_the_rest_of_the_file()
    {
        var s = SettingsFile.Load("""
            {
              "Version": 3,
              "Theme": "saçma",
              "Material": "bilinmeyen",
              "UpdateChannel": 42,
              "IdleDisplayMode": 99,
              "WheelAction": "yok",
              "AnimationSpeed": null,
              "DockIconSize": 55,
              "AccentColor": "#112233",
              "Material2": "yok"
            }
            """);

        var defaults = new AppSettings();
        Assert.Equal(defaults.Appearance.Theme, s.Appearance.Theme);
        Assert.Equal(defaults.Appearance.Material, s.Appearance.Material);
        Assert.Equal(defaults.General.UpdateChannel, s.General.UpdateChannel);
        Assert.Equal(defaults.Notch.IdleDisplay, s.Notch.IdleDisplay);
        Assert.Equal(defaults.Notch.WheelAction, s.Notch.WheelAction);
        Assert.Equal(defaults.Appearance.AnimationSpeed, s.Appearance.AnimationSpeed);
        Assert.Equal(55.0, s.Dock.IconSize);
        Assert.Equal("#112233", s.Appearance.AccentColor);
    }

    [Fact]
    public void A_v4_file_is_not_translated_again()
    {
        // Sürüm 4 dosyasında enum adları zaten yazılıdır; çeviri yalnız eski sürümlere uygulanır.
        var s = SettingsFile.Load("""{ "Version": 4, "Appearance": { "Material": "Glass" }, "Notch": { "IdleDisplay": "Nothing" } }""");

        Assert.Equal(MaterialLevel.Glass, s.Appearance.Material);
        Assert.Equal(IdleDisplayMode.Nothing, s.Notch.IdleDisplay);
    }
}
