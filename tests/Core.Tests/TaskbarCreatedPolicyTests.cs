using Koy.Core.Tray;

namespace Koy.Core.Tests;

public class TaskbarCreatedPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 17, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Broadcast_just_after_our_own_is_an_echo()
        => Assert.True(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TimeSpan.FromMilliseconds(50), Now));

    [Fact]
    public void Broadcast_at_the_edge_of_the_window_is_still_an_echo()
        => Assert.True(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TaskbarCreatedPolicy.OwnBroadcastWindow, Now));

    [Fact]
    public void Broadcast_long_after_ours_is_a_real_explorer_restart()
        => Assert.False(TaskbarCreatedPolicy.IsOwnBroadcast(Now - TimeSpan.FromSeconds(10), Now));

    [Fact]
    public void Without_any_broadcast_of_ours_nothing_is_an_echo()
        => Assert.False(TaskbarCreatedPolicy.IsOwnBroadcast(DateTime.MinValue, Now));
}
