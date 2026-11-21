using Koy.Core.Media;

namespace Koy.Core.Tests;

public class VinylMotionTests
{
    private static void Run(VinylMotion v, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
            v.Step(1.0 / 60);
    }

    [Fact]
    public void Starts_still_with_arm_up()
    {
        var v = new VinylMotion();
        Assert.True(v.IsAtRest);
        Assert.Equal(VinylMotion.ArmRestAngle, v.ArmAngle);
    }

    [Fact]
    public void Playing_spins_at_one_turn_per_eight_seconds()
    {
        var v = new VinylMotion();
        v.SetPlaying(true);
        Run(v, 3);

        Assert.False(v.IsAtRest);
        Assert.Equal(360 / VinylMotion.SecondsPerTurn, v.Speed, 1);
        Assert.Equal(VinylMotion.ArmPlayingAngle, v.ArmAngle, 1);
    }

    [Fact]
    public void Spin_up_is_gradual()
    {
        var v = new VinylMotion();
        v.SetPlaying(true);
        Run(v, 0.1);
        Assert.InRange(v.Speed, 0.01, 360 / VinylMotion.SecondsPerTurn * 0.5);
    }

    [Fact]
    public void Pause_slows_to_a_stop_without_reversing()
    {
        var v = new VinylMotion();
        v.SetPlaying(true);
        Run(v, 3);
        v.SetPlaying(false);

        var previous = v.Angle;
        var travelled = 0.0;
        for (var i = 0; i < 300; i++)
        {
            v.Step(1.0 / 60);
            Assert.True(v.Speed >= -0.05);
            var delta = (v.Angle - previous + 360) % 360;
            travelled += delta;
            previous = v.Angle;
        }

        Assert.True(travelled > 1, "plak birden durmamalı");
        Assert.True(v.IsAtRest);
        Assert.Equal(VinylMotion.ArmRestAngle, v.ArmAngle, 1);
    }

    [Fact]
    public void Snap_jumps_to_target()
    {
        var v = new VinylMotion();
        v.SetPlaying(true);
        v.Snap();
        Assert.Equal(360 / VinylMotion.SecondsPerTurn, v.Speed, 6);
        Assert.Equal(VinylMotion.ArmPlayingAngle, v.ArmAngle, 6);
    }
}
