using Koy.Core.Dock;
using Koy.Core.Dock.SystemArea;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

public class TaskbarLayoutTests
{
    [Theory]
    [InlineData(DockIconAlignment.Center, TaskbarAlignment.Center)]
    [InlineData(DockIconAlignment.Left, TaskbarAlignment.Left)]
    [InlineData(DockIconAlignment.Right, TaskbarAlignment.Center)]   // Windows'ta Sağ yok: Orta
    public void Alignment_maps_the_icon_alignment_setting_to_the_two_windows_options(DockIconAlignment setting, TaskbarAlignment expected) =>
        Assert.Equal(expected, TaskbarLayout.EffectiveAlignment(setting));

    [Fact]
    public void Centered_taskbar_puts_widgets_at_the_far_left_and_groups_start_search_task_view_with_the_apps()
    {
        var l = TaskbarLayout.Resolve(true, false, DockIconAlignment.Center, hasTray: true, hasBattery: true);

        Assert.True(l.IsActive);
        Assert.Equal(TaskbarAlignment.Center, l.Alignment);
        Assert.True(l.ShowStart);
        Assert.True(l.ShowSearch);
        Assert.True(l.ShowTaskView);
        Assert.Equal(WidgetsPlacement.Leading, l.Widgets);
        Assert.True(l.ShowWidgetsLeading);
        Assert.False(l.ShowWidgetsAfterItems);
        Assert.True(l.SystemArea.IsActive);
    }

    [Fact]
    public void Left_aligned_taskbar_moves_widgets_to_the_right_of_the_apps()
    {
        var l = TaskbarLayout.Resolve(true, false, DockIconAlignment.Left, hasTray: true, hasBattery: false);

        Assert.Equal(TaskbarAlignment.Left, l.Alignment);
        Assert.Equal(WidgetsPlacement.AfterItems, l.Widgets);
        Assert.True(l.ShowWidgetsAfterItems);
        Assert.False(l.ShowWidgetsLeading);
        Assert.False(l.SystemArea.ShowBattery);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void Capsule_and_vertical_docks_keep_their_own_layout(bool fullWidth, bool vertical)
    {
        var l = TaskbarLayout.Resolve(fullWidth, vertical, DockIconAlignment.Center, true, true);

        Assert.False(l.IsActive);
        Assert.Equal(default, l);
        Assert.Equal(0.0, TaskbarLayout.LeadingWidthDip(l));
    }

    [Fact]
    public void Leading_width_counts_start_search_task_view_and_widgets_only_when_left_aligned()
    {
        var center = TaskbarLayout.Resolve(true, false, DockIconAlignment.Center, true, true);
        var left = TaskbarLayout.Resolve(true, false, DockIconAlignment.Left, true, true);

        var basic = TaskbarLayout.ButtonWidthDip * 2 + TaskbarLayout.SearchWidthDip;
        Assert.Equal(basic, TaskbarLayout.LeadingWidthDip(center));
        Assert.Equal(basic + TaskbarLayout.WidgetsAfterItemsWidthDip, TaskbarLayout.LeadingWidthDip(left));
    }

    [Fact]
    public void Windows_style_fixes_icon_size_slot_indicators_and_hides_koy_only_buttons()
    {
        var user = new DockSettings
        {
            IconSize = 40,
            IconSpacingDip = 0,
            HoverMagnify = true,
            IndicatorStyle = DockIndicatorStyle.Highlight,
            IndicatorColor = DockColorSource.Custom,
            IndicatorOnePerWindow = true,
            LabelMode = DockLabelMode.Always,
            ShowStartButton = false,
            StartIcon = DockStartIcon.Koy,
            ShowFolders = true,
            ShowRecents = true,
            IconAlignment = DockIconAlignment.Right
        };

        var w = TaskbarLayout.ApplyWindowsStyle(user);

        Assert.Equal(24.0, w.IconSize);
        Assert.Equal(44.0, DockFitMath.SlotWidth(w.IconSize, w.IconSpacingDip), 3);   // yuva ~44 DIP
        Assert.False(w.HoverMagnify);
        Assert.Equal(DockIndicatorStyle.Dot, w.IndicatorStyle);
        Assert.Equal(DockColorSource.Accent, w.IndicatorColor);
        Assert.False(w.IndicatorOnePerWindow);
        Assert.Equal(DockLabelMode.Never, w.LabelMode);
        Assert.True(w.ShowStartButton);
        Assert.Equal(DockStartIcon.Windows, w.StartIcon);
        Assert.False(w.ShowFolders);
        Assert.False(w.ShowRecents);
        Assert.Equal(DockIconAlignment.Center, w.IconAlignment);
    }

    [Fact]
    public void Windows_style_keeps_left_alignment_and_does_not_touch_the_user_settings()
    {
        var user = new DockSettings { IconAlignment = DockIconAlignment.Left, IconSize = 36, HoverMagnify = true };

        var w = TaskbarLayout.ApplyWindowsStyle(user);

        Assert.Equal(DockIconAlignment.Left, w.IconAlignment);
        Assert.Equal(36.0, user.IconSize);       // kopya döner, kaynak değişmez
        Assert.True(user.HoverMagnify);
    }

    [Fact]
    public void Spacing_stays_inside_the_allowed_range()
    {
        var w = TaskbarLayout.ApplyWindowsStyle(new DockSettings());

        Assert.InRange(w.IconSpacingDip, DockLayoutLimits.MinIconSpacingDip, DockLayoutLimits.MaxIconSpacingDip);
    }
}
