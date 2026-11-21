using Koy.Core.Notch;

namespace Koy.Core.Tests;

public class MirrorWaveTests
{
    private static readonly float[] Bands = [1f, 0.8f, 0.6f, 0.4f, 0.2f];

    [Fact]
    public void Wave_is_symmetric()
    {
        const int count = MirrorWave.DefaultBarCount;
        for (var i = 0; i < count / 2; i++)
            Assert.Equal(MirrorWave.LevelFor(Bands, i, count), MirrorWave.LevelFor(Bands, count - 1 - i, count), 9);
    }

    [Fact]
    public void Center_reads_bass_and_edges_read_treble()
    {
        // Tek sayıda çubukta tam orta çubuk ilk bandı taper olmadan okur.
        Assert.Equal(1.0, MirrorWave.LevelFor(Bands, 2, 5), 6);
        // Kenar: son bant, kenar kısaltmasıyla.
        Assert.Equal(0.2 * 0.65, MirrorWave.LevelFor(Bands, 0, 5), 6);
    }

    [Fact]
    public void Distance_runs_from_zero_to_one()
    {
        Assert.Equal(1, MirrorWave.DistanceFromCenter(0, 9));
        Assert.Equal(0, MirrorWave.DistanceFromCenter(4, 9));
        Assert.Equal(0.5, MirrorWave.DistanceFromCenter(2, 9));
        Assert.Equal(0, MirrorWave.DistanceFromCenter(0, 1));
    }

    [Fact]
    public void Levels_stay_in_range_and_out_of_range_input_is_silent()
    {
        float[] loud = [2f, -1f, 5f];
        for (var i = 0; i < 10; i++)
            Assert.InRange(MirrorWave.LevelFor(loud, i, 10), 0, 1);

        Assert.Equal(0, MirrorWave.LevelFor([], 0, 10));
        Assert.Equal(0, MirrorWave.LevelFor(Bands, -1, 10));
        Assert.Equal(0, MirrorWave.LevelFor(Bands, 10, 10));
    }
}
