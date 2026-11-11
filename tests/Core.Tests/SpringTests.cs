using Koy.Core.Animation;

namespace Koy.Core.Tests;

public class SpringTests
{
    [Fact]
    public void Settles_on_target()
    {
        var spring = new Spring(0) { Target = 100 };

        for (var i = 0; i < 120 && spring.Step(1.0 / 60); i++) { }

        Assert.True(spring.IsAtRest);
        Assert.Equal(100, spring.Value);
    }

    [Fact]
    public void Result_does_not_depend_on_frame_size()
    {
        var small = new Spring(0) { Target = 100 };
        var large = new Spring(0) { Target = 100 };

        for (var i = 0; i < 6; i++) small.Step(1.0 / 60);
        large.Step(6.0 / 60);

        Assert.Equal(small.Value, large.Value, precision: 6);
    }

    [Fact]
    public void Step_returns_false_when_already_at_rest()
    {
        Assert.False(new Spring(42).Step(1.0 / 60));
    }

    [Fact]
    public void SnapTo_stops_motion()
    {
        var spring = new Spring(0) { Target = 100 };
        spring.Step(0.05);

        spring.SnapTo(10);

        Assert.Equal(10, spring.Value);
        Assert.Equal(0, spring.Velocity);
        Assert.True(spring.IsAtRest);
    }

    [Fact]
    public void Default_damping_overshoots_when_closing()
    {
        var spring = new Spring(620) { Target = 236 };

        Assert.True(MinimumWhileSettling(spring) < 235);
    }

    [Fact]
    public void Critical_damping_closes_without_overshoot()
    {
        var spring = new Spring(620) { Target = 236 };
        spring.Damping = spring.CriticalDamping;

        Assert.True(MinimumWhileSettling(spring) > 235.99);
        Assert.Equal(236, spring.Value);
    }

    [Fact]
    public void Kick_moves_away_and_returns_to_target()
    {
        var spring = new Spring(0) { Stiffness = 260, Damping = 14 };

        spring.Kick(30);
        var peak = 0.0;
        for (var i = 0; i < 240 && spring.Step(1.0 / 60); i++)
            peak = Math.Max(peak, spring.Value);

        Assert.InRange(peak, 0.8, 1.2);
        Assert.True(spring.IsAtRest);
        Assert.Equal(0, spring.Value);
    }

    private static double MinimumWhileSettling(Spring spring)
    {
        var min = spring.Value;
        for (var i = 0; i < 240 && spring.Step(1.0 / 60); i++)
            min = Math.Min(min, spring.Value);
        return min;
    }
}
