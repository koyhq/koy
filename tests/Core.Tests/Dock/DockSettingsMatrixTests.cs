using System.Reflection;
using Koy.Core.Dock;
using Koy.Core.Dock.SystemArea;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

public class DockSettingsMatrixTests
{
    private static IEnumerable<PropertyInfo> DockProperties => typeof(DockSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanWrite);

    [Fact]
    public void Every_dock_setting_has_a_scope()
    {
        var missing = DockProperties.Select(p => p.Name).Where(n => !DockSettingsMatrix.Keys.Contains(n)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Matrix_has_no_unknown_setting_names()
    {
        var names = DockProperties.Select(p => p.Name).ToHashSet();
        foreach (var key in DockSettingsMatrix.Keys)
            Assert.Contains(key, names);
    }

    [Fact]
    public void Settings_pinned_by_the_windows_style_are_capsule_only()
    {
        // Alanlar varsayılandan farklı değerlere getirilip ApplyWindowsStyle'ın hangilerini değiştirdiğine bakılır.
        var user = new DockSettings
        {
            IconSize = 40, IconSpacingDip = 0, HoverMagnify = true, IndicatorStyle = DockIndicatorStyle.Highlight,
            IndicatorColor = DockColorSource.Custom, IndicatorOnePerWindow = true, LabelMode = DockLabelMode.Always,
            ShowStartButton = false, StartIcon = DockStartIcon.Koy, ShowFolders = true, ShowRecents = true,
            GroupingMode = DockGroupingMode.Never, LeftClickAction = DockLeftClickAction.CycleWindows, IconAlignment = DockIconAlignment.Right
        };
        var pinned = TaskbarLayout.ApplyWindowsStyle(user);

        foreach (var p in DockProperties)
        {
            if (Equals(p.GetValue(user), p.GetValue(pinned)))
                continue;

            // Hizalama Orta/Sol'a eşlenir (tam genişlikte geçerli ayar); öteki sabitlenenler tam genişlikte geçersizdir.
            if (p.Name == nameof(DockSettings.IconAlignment))
                continue;

            Assert.True(DockSettingsMatrix.ScopeOf(p.Name) == DockSettingScope.CapsuleOnly, $"{p.Name} tam genişlikte sabitleniyor ama yalnız kapsül değil");
        }
    }

    [Theory]
    [InlineData(nameof(DockSettings.LengthPercent), false, true)]      // kapsülde etkisizdi: kapsülde gizle
    [InlineData(nameof(DockSettings.InnerPaddingDip), true, false)]    // tam genişlikte etkisizdi: tam genişlikte gizle
    [InlineData(nameof(DockSettings.HoverMagnify), true, false)]
    [InlineData(nameof(DockSettings.TaskbarSearch), false, true)]
    [InlineData(nameof(DockSettings.IconAlignment), false, true)]
    [InlineData(nameof(DockSettings.Edge), true, true)]
    [InlineData(nameof(DockSettings.ShowBadges), true, true)]
    public void Applicability_follows_the_scope(string setting, bool capsule, bool fullWidth)
    {
        Assert.Equal(capsule, DockSettingsMatrix.IsApplicable(setting, DockStyleMode.FloatingCapsule));
        Assert.Equal(fullWidth, DockSettingsMatrix.IsApplicable(setting, DockStyleMode.FullWidth));
    }

    [Fact]
    public void Unknown_setting_is_treated_as_applicable_everywhere()
    {
        Assert.True(DockSettingsMatrix.IsApplicable("YeniAyar", DockStyleMode.FullWidth));
        Assert.True(DockSettingsMatrix.IsApplicable("YeniAyar", DockStyleMode.FloatingCapsule));
    }

    [Fact]
    public void Windows_taskbar_settings_default_to_the_windows_defaults()
    {
        var d = new DockSettings();
        Assert.Equal(TaskbarSearchMode.Box, d.TaskbarSearch);
        Assert.True(d.TaskbarShowTaskView);
        Assert.True(d.TaskbarShowWidgets);
        Assert.True(d.TaskbarShowFlashing);
        Assert.False(d.TaskbarClockSeconds);
        Assert.True(d.TaskbarShowDesktopCorner);
    }

    // ---- Görev çubuğu ayarlarının düzene etkisi ----

    [Theory]
    [InlineData(TaskbarSearchMode.Hidden, false)]
    [InlineData(TaskbarSearchMode.IconOnly, true)]
    [InlineData(TaskbarSearchMode.IconAndLabel, true)]
    [InlineData(TaskbarSearchMode.Box, true)]
    public void Search_modes_decide_visibility_and_width(TaskbarSearchMode mode, bool shown)
    {
        var l = TaskbarLayout.Resolve(true, false, DockIconAlignment.Center, true, true, new TaskbarOptions { Search = mode });

        Assert.Equal(shown, l.ShowSearch);
        Assert.Equal(mode == TaskbarSearchMode.Box, l.ShowSearchBox);
        Assert.Equal(mode == TaskbarSearchMode.IconAndLabel, l.ShowSearchLabel);
        Assert.Equal(mode == TaskbarSearchMode.IconOnly, l.ShowSearchIconOnly);
        Assert.Equal(shown, TaskbarLayout.SearchWidthFor(mode) > 0);
    }

    [Fact]
    public void Task_view_widgets_and_desktop_corner_follow_their_settings()
    {
        var off = new TaskbarOptions { Search = TaskbarSearchMode.Box, ShowTaskView = false, ShowWidgets = false, ShowDesktopCorner = false };
        var l = TaskbarLayout.Resolve(true, false, DockIconAlignment.Center, true, true, off);

        Assert.False(l.ShowTaskView);
        Assert.Equal(WidgetsPlacement.None, l.Widgets);
        Assert.False(l.SystemArea.ShowDesktopStrip);
        Assert.True(l.SystemArea.ShowClock);   // saat ve zil kapatılamaz

        var def = TaskbarLayout.Resolve(true, false, DockIconAlignment.Center, true, true);
        Assert.True(def.ShowTaskView);
        Assert.Equal(WidgetsPlacement.Leading, def.Widgets);
        Assert.True(def.SystemArea.ShowDesktopStrip);
        Assert.Equal(TaskbarSearchMode.Box, def.Search);
    }

    [Fact]
    public void Leading_width_shrinks_when_search_and_task_view_are_hidden()
    {
        var full = TaskbarLayout.Resolve(true, false, DockIconAlignment.Center, true, true);
        var slim = TaskbarLayout.Resolve(true, false, DockIconAlignment.Center, true, true,
            new TaskbarOptions { Search = TaskbarSearchMode.Hidden, ShowTaskView = false, ShowWidgets = true, ShowDesktopCorner = true });

        Assert.Equal(TaskbarLayout.ButtonWidthDip, TaskbarLayout.LeadingWidthDip(slim));
        Assert.True(TaskbarLayout.LeadingWidthDip(full) > TaskbarLayout.LeadingWidthDip(slim));
    }

    [Fact]
    public void Options_are_read_from_the_dock_settings()
    {
        var o = TaskbarOptions.From(new DockSettings
        {
            TaskbarSearch = TaskbarSearchMode.IconOnly, TaskbarShowTaskView = false, TaskbarShowWidgets = false, TaskbarShowDesktopCorner = false
        });

        Assert.Equal(TaskbarSearchMode.IconOnly, o.Search);
        Assert.False(o.ShowTaskView);
        Assert.False(o.ShowWidgets);
        Assert.False(o.ShowDesktopCorner);
    }

    [Fact]
    public void Clock_seconds_change_the_format_and_the_tick()
    {
        var now = new DateTime(2026, 11, 22, 18, 34, 5, 250);
        var tr = new System.Globalization.CultureInfo("tr-TR");

        Assert.Equal("18:34", SystemClockText.Time(now, tr));
        Assert.Equal("18:34:05", SystemClockText.Time(now, tr, showSeconds: true));
        Assert.True(SystemClockText.DelayToNextTick(now, showSeconds: true) < TimeSpan.FromSeconds(1.1));
        Assert.True(SystemClockText.DelayToNextTick(now, showSeconds: false) > TimeSpan.FromSeconds(50));
    }

    [Fact]
    public void Windows_style_pins_grouping_to_always_and_the_windows_left_click()
    {
        var w = TaskbarLayout.ApplyWindowsStyle(new DockSettings { GroupingMode = DockGroupingMode.Never, LeftClickAction = DockLeftClickAction.CycleWindows });

        Assert.Equal(DockGroupingMode.Always, w.GroupingMode);
        Assert.Equal(DockLeftClickAction.MinimizeIfActive, w.LeftClickAction);
    }
}
