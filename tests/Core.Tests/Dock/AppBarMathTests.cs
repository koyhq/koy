using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Dock;

public class AppBarMathTests
{
    private static readonly WindowRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Owned_edge_ignores_the_upward_push_of_the_explorer_taskbar()
    {
        // QUERYPOS Gezgin'in 48 px'ini sayıp alt sınırı 1032'ye çekti: 46 px'lik pay 986..1032 olurdu (94 px toplam).
        var pushed = new WindowRect(0, 986, 1920, 1032);

        var r = AppBarMath.FinalRect(Monitor, 46, pushed, ownsBottomEdge: true);

        Assert.Equal(new WindowRect(0, 1034, 1920, 1080), r);
    }

    [Theory]
    [InlineData(46, 1034)]
    [InlineData(48, 1032)]
    [InlineData(60, 1020)]
    public void Owned_edge_always_hugs_the_monitor_bottom(int height, int expectedTop)
    {
        var r = AppBarMath.FinalRect(Monitor, height, new WindowRect(0, 0, 1920, 900), ownsBottomEdge: true);

        Assert.Equal(1080, r.Bottom);
        Assert.Equal(expectedTop, r.Top);
        Assert.Equal(0, r.Left);
        Assert.Equal(1920, r.Right);
    }

    [Fact]
    public void Visible_taskbar_stacks_above_what_windows_proposed()
    {
        var queried = new WindowRect(0, 0, 1920, 1032);

        var r = AppBarMath.FinalRect(Monitor, 46, queried, ownsBottomEdge: false);

        Assert.Equal(new WindowRect(0, 986, 1920, 1032), r);
    }

    [Fact]
    public void Secondary_monitor_uses_its_own_bounds()
    {
        var second = new WindowRect(1920, -200, 3840, 880);

        var r = AppBarMath.FinalRect(second, 48, new WindowRect(1920, 0, 3840, 832), ownsBottomEdge: true);

        Assert.Equal(new WindowRect(1920, 832, 3840, 880), r);
    }

    [Fact]
    public void Top_edge_allocates_from_queried_top()
    {
        var queried = new WindowRect(0, 0, 1920, 1080);
        var r = AppBarMath.FinalRect(DockEdge.Top, Monitor, 48, queried, ownsBottomEdge: false);
        Assert.Equal(new WindowRect(0, 0, 1920, 48), r);
    }

    [Fact]
    public void Left_edge_allocates_from_queried_left()
    {
        var queried = new WindowRect(0, 0, 1920, 1080);
        var r = AppBarMath.FinalRect(DockEdge.Left, Monitor, 48, queried, ownsBottomEdge: false);
        Assert.Equal(new WindowRect(0, 0, 48, 1080), r);
    }

    [Fact]
    public void Right_edge_allocates_from_queried_right()
    {
        var queried = new WindowRect(0, 0, 1920, 1080);
        var r = AppBarMath.FinalRect(DockEdge.Right, Monitor, 48, queried, ownsBottomEdge: false);
        Assert.Equal(new WindowRect(1920 - 48, 0, 1920, 1080), r);
    }

    [Fact]
    public void NeedsReassert_WorksOnAllEdges()
    {
        // Top edge: expected work.Top is 48. If work.Top < 48 (e.g. 0), needs reassert
        Assert.True(AppBarOwnership.NeedsReassert(DockEdge.Top, Monitor, new WindowRect(0, 0, 1920, 1080), 48, false));
        Assert.False(AppBarOwnership.NeedsReassert(DockEdge.Top, Monitor, new WindowRect(0, 48, 1920, 1080), 48, false));

        // Left edge: expected work.Left is 48. If work.Left < 48 (e.g. 0), needs reassert
        Assert.True(AppBarOwnership.NeedsReassert(DockEdge.Left, Monitor, new WindowRect(0, 0, 1920, 1080), 48, false));
        Assert.False(AppBarOwnership.NeedsReassert(DockEdge.Left, Monitor, new WindowRect(48, 0, 1920, 1080), 48, false));

        // Right edge: expected work.Right is 1920 - 48 = 1872. If work.Right > 1872 (e.g. 1920), needs reassert
        Assert.True(AppBarOwnership.NeedsReassert(DockEdge.Right, Monitor, new WindowRect(0, 0, 1920, 1080), 48, false));
        Assert.False(AppBarOwnership.NeedsReassert(DockEdge.Right, Monitor, new WindowRect(0, 0, 1872, 1080), 48, false));
    }
}
