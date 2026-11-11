using Koy.Core.Appearance;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Appearance;

/// <summary>Animasyon başına açma/kapama (issue #79): kapalı animasyon anında biter, genel kurallar her zaman üstündedir.</summary>
public class MotionTogglesTests
{
    public static IEnumerable<object[]> Kinds() =>
        Enum.GetValues<MotionPolicy.MotionKind>().Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Kinds))]
    public void With_default_toggles_every_animation_gets_the_global_scale(MotionPolicy.MotionKind kind)
        => Assert.Equal(1.0, MotionPolicy.ScaleFor(kind, new MotionSettings(), 1.0));

    [Theory]
    [MemberData(nameof(Kinds))]
    public void A_disabled_toggle_makes_only_its_own_animation_instant(MotionPolicy.MotionKind kind)
    {
        var one = new MotionSettings();
        var field = typeof(MotionSettings).GetProperty(kind.ToString() switch
        {
            "NotchOpenClose" => nameof(MotionSettings.NotchOpenClose),
            "DockMagnify" => nameof(MotionSettings.DockMagnify),
            "FolderPanel" => nameof(MotionSettings.FolderPanel),
            "Osd" => nameof(MotionSettings.Osd),
            "BadgeBounce" => nameof(MotionSettings.BadgeBounce),
            _ => nameof(MotionSettings.TabTransitions),
        })!;
        field.SetValue(one, false);

        Assert.Equal(0.0, MotionPolicy.ScaleFor(kind, one, 1.0));
        foreach (var other in Enum.GetValues<MotionPolicy.MotionKind>().Where(k => k != kind))
            Assert.Equal(1.0, MotionPolicy.ScaleFor(other, one, 1.0));
    }

    [Fact]
    public void A_zero_global_scale_wins_over_enabled_toggles()
        => Assert.Equal(0.0, MotionPolicy.ScaleFor(MotionPolicy.MotionKind.Osd, new MotionSettings(), 0.0));

    [Fact]
    public void Scaled_speed_is_kept_when_the_toggle_is_on()
        => Assert.Equal(1.5, MotionPolicy.ScaleFor(MotionPolicy.MotionKind.NotchOpenClose, new MotionSettings(), 1.5));

    [Fact]
    public void Missing_motion_section_defaults_to_everything_on()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "Appearance": { "Theme": "Light" } }""");

        Assert.True(loaded.Appearance.Motion.Osd);
        Assert.True(loaded.Appearance.Motion.NotchOpenClose);
    }

    [Fact]
    public void Null_motion_section_is_replaced_with_defaults()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "Appearance": { "Motion": null } }""");

        Assert.NotNull(loaded.Appearance.Motion);
        Assert.True(loaded.Appearance.Motion.DockMagnify);
    }

    [Fact]
    public void A_saved_toggle_is_read_back()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "Appearance": { "Motion": { "Osd": false } } }""");

        Assert.False(loaded.Appearance.Motion.Osd);
        Assert.True(loaded.Appearance.Motion.TabTransitions);
    }

    [Fact]
    public void Changing_a_motion_toggle_makes_settings_unequal()
    {
        var changed = new AppSettings { Appearance = new AppearanceSettings { Motion = new MotionSettings { Osd = false } } };

        Assert.NotEqual(new AppSettings(), changed);
        Assert.NotEqual(new AppSettings().GetHashCode(), changed.GetHashCode());
    }
}
