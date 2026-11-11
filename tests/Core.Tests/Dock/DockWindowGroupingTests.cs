using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

public sealed class DockWindowGroupingTests
{
    private static DockWindowFact W(string name, int monitor, long handle) => new(name, monitor, (nint)handle);

    private static readonly DockWindowFact[] Sample =
    [
        W("chrome", 1, 10),
        W("code", 2, 20),
        W("Chrome", 2, 30),
        W("explorer", 1, 40),
        W("chrome", 1, 50),
    ];

    [Fact]
    public void Never_gives_one_group_per_window_in_input_order()
    {
        var groups = DockWindowGrouping.Group(Sample, DockGroupingMode.Never, availableSlots: 0);

        Assert.Equal(5, groups.Count);
        Assert.Equal([(nint)10], groups[0].Handles);
        Assert.Equal([(nint)20], groups[1].Handles);
        Assert.Equal([(nint)30], groups[2].Handles);
        Assert.Equal([(nint)40], groups[3].Handles);
        Assert.Equal([(nint)50], groups[4].Handles);
    }

    [Fact]
    public void Always_groups_case_insensitively_in_first_appearance_order()
    {
        var groups = DockWindowGrouping.Group(Sample, DockGroupingMode.Always, availableSlots: 100);

        Assert.Equal(3, groups.Count);
        Assert.Equal("chrome", groups[0].ProcessName);
        Assert.Equal([(nint)10, (nint)30, (nint)50], groups[0].Handles);
        Assert.Equal("code", groups[1].ProcessName);
        Assert.Equal([(nint)20], groups[1].Handles);
        Assert.Equal("explorer", groups[2].ProcessName);
        Assert.Equal([(nint)40], groups[2].Handles);
    }

    [Fact]
    public void Always_is_deterministic_for_the_same_input()
    {
        var first = DockWindowGrouping.Group(Sample, DockGroupingMode.Always, 0);
        var second = DockWindowGrouping.Group(Sample, DockGroupingMode.Always, 0);

        // Record eşitliği IReadOnlyList için referansa bakar; içerik karşılaştırması elle yapılır.
        static string[] Flatten(IEnumerable<DockWindowGroup> groups) =>
            groups.Select(g => $"{g.ProcessName}:{string.Join(',', g.Handles)}").ToArray();

        Assert.Equal(Flatten(first), Flatten(second));
    }

    [Fact]
    public void WhenSpaceRunsOut_keeps_windows_separate_while_they_fit()
    {
        var groups = DockWindowGrouping.Group(Sample, DockGroupingMode.WhenSpaceRunsOut, availableSlots: 5);

        Assert.Equal(5, groups.Count);
        Assert.All(groups, g => Assert.Single(g.Handles));
    }

    [Fact]
    public void WhenSpaceRunsOut_groups_once_windows_exceed_the_slots()
    {
        var groups = DockWindowGrouping.Group(Sample, DockGroupingMode.WhenSpaceRunsOut, availableSlots: 4);

        Assert.Equal(3, groups.Count);
        Assert.Equal(3, groups[0].Handles.Count);
    }

    [Fact]
    public void Empty_input_gives_no_groups_in_any_mode()
    {
        foreach (var mode in Enum.GetValues<DockGroupingMode>())
            Assert.Empty(DockWindowGrouping.Group([], mode, availableSlots: 3));
    }

    [Fact]
    public void Undefined_mode_from_the_file_keeps_windows_separate()
    {
        var groups = DockWindowGrouping.Group(Sample, (DockGroupingMode)7, availableSlots: 0);

        Assert.Equal(5, groups.Count);
    }

    [Fact]
    public void Missing_process_name_reads_as_empty_in_every_mode()
    {
        DockWindowFact[] windows = [new(null!, 1, 1), new(null!, 1, 2)];

        Assert.All(DockWindowGrouping.Group(windows, DockGroupingMode.Never, 0), g => Assert.Equal("", g.ProcessName));
        Assert.Equal("", Assert.Single(DockWindowGrouping.Group(windows, DockGroupingMode.Always, 0)).ProcessName);
    }

    [Fact]
    public void Negative_slot_count_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DockWindowGrouping.Group(Sample, DockGroupingMode.WhenSpaceRunsOut, availableSlots: -1));
    }

    [Fact]
    public void AllMonitors_keeps_every_window()
    {
        var filtered = DockWindowGrouping.FilterByMonitor(Sample, DockWindowMonitorScope.AllMonitors, currentMonitorId: 2);

        Assert.Equal(Sample, filtered);
    }

    [Fact]
    public void CurrentMonitor_keeps_only_that_monitor_in_input_order()
    {
        var filtered = DockWindowGrouping.FilterByMonitor(Sample, DockWindowMonitorScope.CurrentMonitor, currentMonitorId: 1);

        Assert.Equal([W("chrome", 1, 10), W("explorer", 1, 40), W("chrome", 1, 50)], filtered);
    }

    [Fact]
    public void Filtered_windows_group_into_the_expected_entries()
    {
        var filtered = DockWindowGrouping.FilterByMonitor(Sample, DockWindowMonitorScope.CurrentMonitor, currentMonitorId: 2);
        var groups = DockWindowGrouping.Group(filtered, DockGroupingMode.Always, 0);

        Assert.Equal(2, groups.Count);
        Assert.Equal([(nint)20], groups[0].Handles);
        Assert.Equal([(nint)30], groups[1].Handles);
    }

    [Fact]
    public void Display_settings_default_to_todays_behaviour()
    {
        var dock = new AppSettings().Dock;

        Assert.Equal(DockGroupingMode.Never, dock.GroupingMode);
        Assert.Equal(DockWindowMonitorScope.AllMonitors, dock.WindowMonitorScope);
        Assert.Equal(DockLeftClickAction.BringToFront, dock.LeftClickAction);
        Assert.Equal(DockLabelMode.Never, dock.LabelMode);
    }
}
