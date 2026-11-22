using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class SettingsPageResetTests
{
    private static AppSettings Customized() => new()
    {
        General = new GeneralSettings { Language = "en", FirstRunCompleted = true, LastSeenVersion = "0.2.7", AutoCheckUpdates = false },
        Appearance = new AppearanceSettings { AnimationSpeed = Koy.Core.Appearance.AnimationSpeed.Fast },
        Notch = new NotchSettings { ShowVolumeOsd = false, CollapsedBackground = Koy.Core.Notch.NotchCollapsedBackground.AlbumColor },
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
    [InlineData("DockItems")]
    [InlineData("QuickLaunch")]
    [InlineData("Weather")]
    [InlineData("Weather.Latitude")]
    [InlineData("Nope")]
    public void Sections_with_user_data_and_unknown_names_cannot_be_reset(string section) =>
        Assert.Throws<ArgumentException>(() => SettingsPageReset.Reset(new AppSettings(), [section]));

    [Fact]
    public void Dock_reset_keeps_enabled_and_taskbar_switches_so_the_taskbar_is_untouched()
    {
        var current = Customized();
        current.Dock.HideWindowsTaskbar = false;
        current.Dock.Edge = Koy.Core.Dock.DockEdge.Top;
        current.DockItems.PinnedOrder = ["A.lnk"];

        var reset = SettingsPageReset.Reset(current, ["Dock"]);

        Assert.False(reset.Dock.Enabled);
        Assert.False(reset.Dock.HideWindowsTaskbar);
        Assert.Equal(new DockSettings().Edge, reset.Dock.Edge);
        Assert.True(reset.Dock.ShowTray);
        Assert.Equal(["A.lnk"], reset.DockItems.PinnedOrder);
    }

    [Fact]
    public void Performance_page_resets_only_its_two_fields()
    {
        var current = Customized();
        current.Appearance.BatterySaverReduceMotion = false;
        current.Weather.RefreshInterval = Koy.Core.Weather.WeatherRefreshInterval.OnStartup;
        current.Weather.City = "Ankara";

        var reset = SettingsPageReset.Reset(current, SettingsPageReset.TargetsFor(7));

        Assert.True(reset.Appearance.BatterySaverReduceMotion);
        Assert.Equal(new WeatherSettings().RefreshInterval, reset.Weather.RefreshInterval);
        Assert.Equal("Ankara", reset.Weather.City);
        Assert.Equal(Koy.Core.Appearance.AnimationSpeed.Fast, reset.Appearance.AnimationSpeed);
    }

    [Fact]
    public void Displays_and_hot_corners_reset_to_defaults()
    {
        var current = Customized();
        current.Displays.NewScreenDefault = Koy.Core.Display.NewScreenMode.None;
        current.HotCorners.DwellMs = 999;

        var reset = SettingsPageReset.Reset(current, ["Displays", "HotCorners"]);

        Assert.Equal(new DisplaysSettings(), reset.Displays);
        Assert.Equal(new HotCornersSettings(), reset.HotCorners);
    }

    [Theory]
    [InlineData(0, "General")]
    [InlineData(1, "Notch")]
    [InlineData(2, "Dock")]
    [InlineData(3, "Displays")]
    [InlineData(4, "Appearance")]
    [InlineData(5, "Shortcuts")]
    [InlineData(6, "Voice")]
    [InlineData(7, "Weather.RefreshInterval")]
    public void Every_page_with_settings_has_a_reset_target(int page, string expectedTarget) =>
        Assert.Contains(expectedTarget, SettingsPageReset.TargetsFor(page));

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Pages_without_own_settings_have_no_reset_target(int page) =>
        Assert.Empty(SettingsPageReset.TargetsFor(page));

    [Fact]
    public void Every_page_target_is_resettable()
    {
        for (var page = 0; page <= 10; page++)
        {
            foreach (var target in SettingsPageReset.TargetsFor(page))
                Assert.True(SettingsPageReset.ResettableSections.Contains(target) || SettingsPageReset.ResettableFields.Contains(target), target);
        }
    }

    [Fact]
    public void Every_resettable_section_is_a_real_settings_section()
    {
        var names = typeof(AppSettings).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.All(SettingsPageReset.ResettableSections, s => Assert.Contains(s, names));
        Assert.All(SettingsPageReset.ResettableFields, f => Assert.Contains(f.Split('.')[0], names));
        // Her biri gerçekten sıfırlanır (switch'te eksik dal kalmasın).
        foreach (var section in SettingsPageReset.ResettableSections)
        {
            var prop = typeof(AppSettings).GetProperty(section)!;
            var reset = SettingsPageReset.Reset(new AppSettings(), [section]);
            Assert.NotNull(prop.GetValue(reset));
        }
    }
}
