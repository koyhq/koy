using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class SettingsPageResetTests
{
    private static AppSettings Customized() => new()
    {
        General = new GeneralSettings { Language = "en", FirstRunCompleted = true, LastSeenVersion = "0.2.7", AutoCheckUpdates = false },
        Appearance = new AppearanceSettings { AnimationSpeed = Koy.Core.Appearance.AnimationSpeed.Fast },
        Notch = new NotchSettings { ShowVolumeOsd = false },
        Music = new MusicSettings { ShowWaveform = false },
        Dock = new DockSettings { Enabled = false, ShowTray = false },
    };

    [Fact]
    public void Resets_only_the_given_sections()
    {
        var current = Customized();

        var reset = SettingsPageReset.Reset(current, ["Notch", "Music"]);

        Assert.Equal(new NotchSettings(), reset.Notch);
        Assert.Equal(new MusicSettings(), reset.Music);
        Assert.Equal(current.Appearance, reset.Appearance);
        Assert.Equal(current.Dock, reset.Dock);
        Assert.Equal(current.General, reset.General);
    }

    [Fact]
    public void General_reset_keeps_first_run_and_last_seen_version()
    {
        var reset = SettingsPageReset.Reset(Customized(), ["General"]);

        Assert.True(reset.General.FirstRunCompleted);
        Assert.Equal("0.2.7", reset.General.LastSeenVersion);
        Assert.Equal(new GeneralSettings().Language, reset.General.Language);
        Assert.True(reset.General.AutoCheckUpdates);
    }

    [Fact]
    public void Reset_page_reports_no_changes_for_that_section()
    {
        var reset = SettingsPageReset.Reset(Customized(), ["General", "Appearance"]);
        var changed = SettingsChanges.ChangedKeys(reset, new AppSettings());

        Assert.False(SettingsChanges.HasChanges(changed, "General"));
        Assert.False(SettingsChanges.HasChanges(changed, "Appearance"));
        Assert.True(SettingsChanges.HasChanges(changed, "Dock"));
    }

    [Theory]
    [InlineData("Dock")]
    [InlineData("Displays")]
    [InlineData("DockItems")]
    [InlineData("Nope")]
    public void Dock_and_display_sections_cannot_be_reset(string section) =>
        Assert.Throws<ArgumentException>(() => SettingsPageReset.Reset(new AppSettings(), [section]));

    [Fact]
    public void Every_resettable_section_is_a_real_settings_section()
    {
        var names = typeof(AppSettings).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.All(SettingsPageReset.ResettableSections, s => Assert.Contains(s, names));
        // Her biri gerçekten sıfırlanır (switch'te eksik dal kalmasın).
        foreach (var section in SettingsPageReset.ResettableSections)
        {
            var prop = typeof(AppSettings).GetProperty(section)!;
            var reset = SettingsPageReset.Reset(new AppSettings(), [section]);
            Assert.NotNull(prop.GetValue(reset));
        }
    }
}
