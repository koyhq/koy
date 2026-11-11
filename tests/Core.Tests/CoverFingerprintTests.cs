using Koy.Core.Media;

namespace Koy.Core.Tests;

public class CoverFingerprintTests
{
    private static byte[] Bytes(int length, int seed)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    [Fact]
    public void Same_bytes_give_the_same_fingerprint() =>
        Assert.Equal(CoverFingerprint.Of(Bytes(5000, 1)), CoverFingerprint.Of(Bytes(5000, 1)));

    [Fact]
    public void Different_covers_differ()
    {
        Assert.NotEqual(CoverFingerprint.Of(Bytes(5000, 1)), CoverFingerprint.Of(Bytes(5000, 2)));
        Assert.NotEqual(CoverFingerprint.Of(Bytes(5000, 1)), CoverFingerprint.Of(Bytes(5001, 1)));
    }

    [Fact]
    public void Last_bytes_are_part_of_the_fingerprint()
    {
        var a = Bytes(5000, 3);
        var b = (byte[])a.Clone();
        b[^1] ^= 0xFF;
        Assert.NotEqual(CoverFingerprint.Of(a), CoverFingerprint.Of(b));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(100)]
    public void Short_inputs_do_not_throw(int length) =>
        Assert.Equal(length, CoverFingerprint.Of(Bytes(length, 4)).Length);
}
