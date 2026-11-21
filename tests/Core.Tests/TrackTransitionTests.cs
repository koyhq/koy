using Koy.Core.Media;

namespace Koy.Core.Tests;

public class TrackTransitionTests
{
    private const double Cover = 200;

    [Fact]
    public void Idle_transition_shows_the_final_state()
    {
        var t = new TrackTransition();

        Assert.False(t.IsRunning);
        var f = t.Frame(Cover);
        Assert.Equal(0, f.IncomingOffsetX);
        Assert.Equal(1, f.IncomingScale);
        Assert.Equal(1, f.IncomingOpacity);
        Assert.Equal(0, f.OutgoingOpacity);
        Assert.Equal(0, f.TitleOffsetY);
        Assert.Equal(1, f.TitleOpacity);
    }

    [Fact]
    public void Start_puts_new_cover_right_and_small_and_old_cover_in_place()
    {
        var f = TrackTransition.FrameAt(0, Cover);

        Assert.True(f.IncomingOffsetX > 0);
        Assert.Equal(TrackTransition.IncomingStartScale, f.IncomingScale);
        Assert.Equal(0, f.IncomingOpacity);
        Assert.Equal(0, f.OutgoingOffsetX);
        Assert.Equal(1, f.OutgoingOpacity);
        Assert.Equal(TrackTransition.TitleRise, f.TitleOffsetY);
        Assert.Equal(0, f.TitleOpacity);
    }

    [Fact]
    public void Old_cover_moves_left_while_new_cover_comes_in()
    {
        var f = TrackTransition.FrameAt(0.5, Cover);

        Assert.True(f.OutgoingOffsetX < 0);
        Assert.True(f.IncomingOffsetX > 0 && f.IncomingOffsetX < Cover * TrackTransition.SlideFraction);
        Assert.True(f.OutgoingOpacity < 1);
        Assert.True(f.IncomingOpacity > 0);
    }

    [Fact]
    public void Title_lags_behind_the_cover()
    {
        var f = TrackTransition.FrameAt(0.1, Cover);

        Assert.True(f.IncomingOpacity > 0);
        Assert.Equal(0, f.TitleOpacity);
    }

    [Fact]
    public void Overshoot_never_pushes_opacity_out_of_range()
    {
        var f = TrackTransition.FrameAt(1.03, Cover);

        Assert.InRange(f.IncomingOpacity, 0, 1);
        Assert.InRange(f.OutgoingOpacity, 0, 1);
        Assert.InRange(f.TitleOpacity, 0, 1);
    }

    [Fact]
    public void Transition_settles_within_half_a_second()
    {
        var t = new TrackTransition();
        t.Start();
        Assert.True(t.IsRunning);

        var elapsed = 0.0;
        while (t.Step(1.0 / 60) && elapsed < 2)
            elapsed += 1.0 / 60;

        Assert.False(t.IsRunning);
        Assert.InRange(elapsed, 0.3, 0.6);
        Assert.Equal(1, t.Progress);
    }

    [Fact]
    public void Restart_mid_transition_begins_from_zero()
    {
        var t = new TrackTransition();
        t.Start();
        t.Step(0.2);
        Assert.True(t.Progress > 0.3);

        t.Start();
        Assert.Equal(0, t.Progress);
    }

    [Fact]
    public void Finish_jumps_to_the_end()
    {
        var t = new TrackTransition();
        t.Start();
        t.Step(0.05);

        t.Finish();

        Assert.False(t.IsRunning);
        Assert.Equal(1, t.Progress);
    }
}
