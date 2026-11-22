using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

public sealed class DockWindowGroupingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(500)]
    public void Always_groups_at_any_count(int count) =>
        Assert.True(DockWindowGrouping.ShouldGroup(DockGroupingMode.Always, count));

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(500)]
    public void Never_never_groups(int count) =>
        Assert.False(DockWindowGrouping.ShouldGroup(DockGroupingMode.Never, count));

    [Theory]
    [InlineData(0, false)]
    [InlineData(16, false)]
    [InlineData(17, true)]
    [InlineData(40, true)]
    public void When_space_runs_out_groups_only_above_the_slot_limit(int count, bool expected) =>
        Assert.Equal(expected, DockWindowGrouping.ShouldGroup(DockGroupingMode.WhenSpaceRunsOut, count));

    [Fact]
    public void Slot_limit_keeps_the_previous_value_of_sixteen() =>
        Assert.Equal(16, DockWindowGrouping.SlotLimit);

    [Fact]
    public void Undefined_mode_from_the_file_falls_back_to_no_grouping() =>
        Assert.False(DockWindowGrouping.ShouldGroup((DockGroupingMode)7, 100));
}
