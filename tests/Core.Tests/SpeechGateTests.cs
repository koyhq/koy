using Koy.Core.Voice;

namespace Koy.Core.Tests;

public class SpeechGateTests
{
    private const double Chunk = 0.064; // 1024 örnek, 16 kHz

    private static double[] Blocks(int quiet, double quietLevel, int loud, double loudLevel) =>
        [.. Enumerable.Repeat(quietLevel, quiet), .. Enumerable.Repeat(loudLevel, loud)];

    [Fact]
    public void Empty_recording_is_silent()
    {
        var stats = SpeechGate.Analyse([], Chunk);
        Assert.True(SpeechGate.IsSilent(stats));
    }

    [Fact]
    public void Speech_above_the_noise_floor_is_not_silent()
    {
        // 14 sessiz blok (-60 dB), 6 konuşma bloğu (-20 dB): 6 x 0,064 = 0,384 sn sesli.
        var stats = SpeechGate.Analyse(Blocks(14, 0.001, 6, 0.1), Chunk);

        Assert.Equal(-60, stats.NoiseDb, 3);
        Assert.Equal(-20, stats.SpeechDb, 3);
        Assert.Equal(40, stats.DynamicDb, 3);
        Assert.Equal(0.384, stats.VoicedSeconds, 3);
        Assert.False(SpeechGate.IsSilent(stats));
    }

    [Fact]
    public void Steady_loud_noise_is_silent_however_loud_it_is()
    {
        // Sabit fan sesi: yüksek ama kendi tabanının üstüne çıkan blok yok.
        var stats = SpeechGate.Analyse(Blocks(0, 0, 20, 0.1), Chunk);

        Assert.Equal(0.0, stats.VoicedSeconds);
        Assert.True(SpeechGate.IsSilent(stats));
    }

    [Fact]
    public void A_quiet_microphone_still_passes_because_the_test_is_relative()
    {
        // Taban -66 dB, konuşma -46 dB: mutlak tabanın (-55 dB) üstünde, 5 blok = 0,32 sn.
        var stats = SpeechGate.Analyse(Blocks(15, 0.0005, 5, 0.005), Chunk);

        Assert.False(SpeechGate.IsSilent(stats));
    }

    [Fact]
    public void A_burst_too_short_to_be_a_word_is_silent()
    {
        var stats = SpeechGate.Analyse(Blocks(17, 0.001, 3, 0.1), Chunk);

        Assert.True(SpeechGate.IsSilent(stats));
    }

    [Fact]
    public void Everything_below_the_floor_is_silent()
    {
        var stats = SpeechGate.Analyse(Blocks(10, 0.00001, 10, 0.0001), Chunk);

        Assert.True(stats.SpeechDb < SpeechGate.DefaultFloorDb);
        Assert.True(SpeechGate.IsSilent(stats));
    }

    [Theory]
    [InlineData(1.0, 0)]
    [InlineData(0.1, -20)]
    [InlineData(0.0, -120)]
    public void ToDb_converts_and_clamps(double rms, double expected)
    {
        Assert.Equal(expected, SpeechGate.ToDb(rms), 6);
    }
}
