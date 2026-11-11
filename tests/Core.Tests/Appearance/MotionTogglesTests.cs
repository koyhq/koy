using Koy.Core.Appearance;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Appearance;

/// <summary>Animasyon başına açma/kapama (issue #79): kapalı animasyon anında biter, genel kurallar her zaman üstündedir.</summary>
public class MotionTogglesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-motion-" + Guid.NewGuid().ToString("N"));

    public MotionTogglesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    public static IEnumerable<object[]> Kinds() =>
        Enum.GetValues<MotionPolicy.MotionKind>().Select(k => new object[] { k });

    /// <summary>Her tür için ilgili anahtarı kapatır: anahtar adı türle birebir aynı, değişen yer tek yer.</summary>
    private static MotionSettings WithOnly(MotionPolicy.MotionKind disabled) => disabled switch
    {
        MotionPolicy.MotionKind.NotchOpenClose => new MotionSettings { NotchOpenClose = false },
        MotionPolicy.MotionKind.DockMagnify => new MotionSettings { DockMagnify = false },
        MotionPolicy.MotionKind.FolderPanel => new MotionSettings { FolderPanel = false },
        MotionPolicy.MotionKind.Osd => new MotionSettings { Osd = false },
        MotionPolicy.MotionKind.BadgeBounce => new MotionSettings { BadgeBounce = false },
        MotionPolicy.MotionKind.TabTransitions => new MotionSettings { TabTransitions = false },
        _ => throw new ArgumentOutOfRangeException(nameof(disabled)),
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void With_default_toggles_every_animation_gets_the_global_scale(MotionPolicy.MotionKind kind)
        => Assert.Equal(1.0, MotionPolicy.ScaleFor(kind, new MotionSettings(), 1.0));

    [Theory]
    [MemberData(nameof(Kinds))]
    public void A_disabled_toggle_makes_only_its_own_animation_instant(MotionPolicy.MotionKind kind)
    {
        var toggles = WithOnly(kind);

        Assert.Equal(0.0, MotionPolicy.ScaleFor(kind, toggles, 1.0));
        foreach (var other in Enum.GetValues<MotionPolicy.MotionKind>().Where(k => k != kind))
            Assert.Equal(1.0, MotionPolicy.ScaleFor(other, toggles, 1.0));
    }

    [Fact]
    public void A_zero_global_scale_wins_over_enabled_toggles()
        => Assert.Equal(0.0, MotionPolicy.ScaleFor(MotionPolicy.MotionKind.Osd, new MotionSettings(), 0.0));

    [Fact]
    public void A_nan_global_scale_means_no_animation()
        => Assert.Equal(0.0, MotionPolicy.ScaleFor(MotionPolicy.MotionKind.Osd, new MotionSettings(), double.NaN));

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
    public void A_saved_toggle_is_read_back_after_a_save_and_load_round_trip()
    {
        var path = Path.Combine(_dir, "settings.json");
        var store = new SettingsStore(path);
        var original = new AppSettings { Appearance = new AppearanceSettings { Motion = new MotionSettings { Osd = false } } };

        store.Save(original);
        var reloaded = store.Load();

        Assert.False(reloaded.Appearance.Motion.Osd);
        Assert.True(reloaded.Appearance.Motion.TabTransitions);
        Assert.Equal(original, reloaded);
    }

    [Fact]
    public void An_invalid_motion_value_resets_only_that_flag()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "Appearance": { "Theme": "Light", "Motion": { "Osd": "yanlis", "DockMagnify": false } } }""");

        Assert.True(loaded.Appearance.Motion.Osd);
        Assert.False(loaded.Appearance.Motion.DockMagnify);
        Assert.Equal(AppTheme.Light, loaded.Appearance.Theme);
    }

    [Fact]
    public void Changing_a_motion_toggle_makes_settings_unequal()
    {
        var changed = new AppSettings { Appearance = new AppearanceSettings { Motion = new MotionSettings { Osd = false } } };

        Assert.NotEqual(new AppSettings(), changed);
    }
}
