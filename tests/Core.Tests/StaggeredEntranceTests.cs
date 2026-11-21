using Koy.Core.Animation;

namespace Koy.Core.Tests;

public class StaggeredEntranceTests
{
    [Fact]
    public void Items_start_hidden_low_and_small()
    {
        var s = new StaggeredEntrance(5);
        var f = s.FrameAt(0, 0);

        Assert.Equal(0, f.Opacity);
        Assert.Equal(StaggeredEntrance.StartScale, f.Scale);
        Assert.Equal(StaggeredEntrance.Rise, f.OffsetY);
    }

    [Fact]
    public void Later_items_wait_their_turn()
    {
        var s = new StaggeredEntrance(5, step: 0.05);

        Assert.True(s.ProgressAt(0, 0.04) > 0);
        Assert.Equal(0, s.ProgressAt(1, 0.04));
        Assert.Equal(0, s.ProgressAt(4, 0.19));
        Assert.True(s.ProgressAt(4, 0.21) > 0);
    }

    [Fact]
    public void Every_item_ends_in_place()
    {
        var s = new StaggeredEntrance(5);
        for (var i = 0; i < 5; i++)
        {
            var f = s.FrameAt(i, s.TotalSeconds);
            Assert.Equal(1, f.Opacity);
            Assert.Equal(1, f.Scale);
            Assert.Equal(0, f.OffsetY);
        }
        Assert.True(s.IsDone(s.TotalSeconds));
        Assert.False(s.IsDone(s.TotalSeconds - 0.01));
    }

    [Fact]
    public void Overshoot_is_slight_and_opacity_stays_in_range()
    {
        var s = new StaggeredEntrance(1);
        var max = 0.0;
        for (var t = 0.0; t <= s.Duration; t += 0.005)
        {
            var f = s.FrameAt(0, t);
            Assert.InRange(f.Opacity, 0, 1);
            max = Math.Max(max, s.ProgressAt(0, t));
        }

        Assert.InRange(max, 1.0, 1.03);
    }

    [Fact]
    public void Whole_entrance_is_short()
    {
        var s = new StaggeredEntrance(5);
        Assert.InRange(s.TotalSeconds, 0.4, 0.6);
    }

    [Fact]
    public void Exit_fades_out_within_its_window()
    {
        Assert.Equal(1, StaggeredEntrance.ExitOpacityAt(0));
        Assert.InRange(StaggeredEntrance.ExitOpacityAt(0.06), 0.4, 0.6);
        Assert.Equal(0, StaggeredEntrance.ExitOpacityAt(StaggeredEntrance.ExitSeconds));
    }

    [Fact]
    public void Empty_entrance_is_done_immediately() => Assert.True(new StaggeredEntrance(0).IsDone(0));
}
