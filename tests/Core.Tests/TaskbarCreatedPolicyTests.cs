using Koy.Core.Tray;

namespace Koy.Core.Tests;

public class TaskbarCreatedPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 17, 30, 0, DateTimeKind.Utc);
    private static readonly nint Tray = 0x1234;

    [Fact]
    public void Broadcast_just_after_our_own_with_same_explorer_tray_is_an_echo()
        => Assert.True(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TimeSpan.FromMilliseconds(50), Now, Tray, Tray));

    [Fact]
    public void Broadcast_at_the_edge_of_the_window_is_still_an_echo()
        => Assert.True(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TaskbarCreatedPolicy.OwnBroadcastWindow, Now, Tray, Tray));

    [Fact]
    public void Broadcast_long_after_ours_is_a_real_explorer_restart()
        => Assert.False(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TimeSpan.FromSeconds(10), Now, Tray, Tray));

    [Fact]
    public void Coincident_real_restart_is_not_swallowed_when_the_explorer_tray_changed()
        => Assert.False(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TimeSpan.FromMilliseconds(200), Now, Tray, 0x9999));

    [Fact]
    public void Missing_explorer_tray_right_now_counts_as_restart()
        => Assert.False(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TimeSpan.FromMilliseconds(200), Now, Tray, 0));

    [Fact]
    public void Unknown_tray_at_broadcast_time_is_never_an_echo()
        => Assert.False(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TimeSpan.FromMilliseconds(200), Now, 0, 0));

    [Fact]
    public void Without_any_broadcast_of_ours_nothing_is_an_echo()
        => Assert.False(TaskbarCreatedPolicy.IsOwnBroadcast(DateTime.MinValue, Now, Tray, Tray));
}
