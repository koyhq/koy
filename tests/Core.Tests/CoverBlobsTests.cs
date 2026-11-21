using Koy.Core.Appearance;
using Koy.Core.Media;

namespace Koy.Core.Tests;

public class CoverBlobsTests
{
    private static readonly Rgb Card = new(40, 20, 60);
    private static readonly Rgb Light = new(200, 150, 230);

    [Fact]
    public void Single_tone_cover_gives_three_related_colors()
    {
        var colors = CoverBlobs.ColorsFrom(new CoverPalette(Card, Light, Light));

        Assert.Equal(CoverBlobs.Count, colors.Length);
        Assert.Equal(Light, colors[0]);
        Assert.NotEqual(Card, colors[1]);
        Assert.NotEqual(Light, colors[2]);
    }

    [Fact]
    public void Multi_color_cover_uses_blend_colors_cycling()
    {
        Rgb[] blend = [new(255, 0, 0), new(0, 0, 255)];
        var colors = CoverBlobs.ColorsFrom(new CoverPalette(Card, Light, Light, blend));

        Assert.Equal([blend[0], blend[1], blend[0]], colors);
    }

    [Fact]
    public void Blobs_start_at_rest_and_return()
    {
        for (var i = 0; i < CoverBlobs.Count; i++)
        {
            var start = CoverBlobs.FrameAt(i, 0);
            Assert.Equal(0, start.OffsetX, 9);
            Assert.Equal(0, start.OffsetY, 9);
        }

        // İlk leke 22 sn'de başa döner, yarısında en uzakta.
        Assert.Equal(0, CoverBlobs.FrameAt(0, 22).OffsetX, 6);
        Assert.Equal(120, CoverBlobs.FrameAt(0, 11).OffsetX, 6);
        Assert.Equal(1.25, CoverBlobs.FrameAt(0, 11).Scale, 6);
    }

    [Fact]
    public void Motion_is_slow()
    {
        // 1/15 sn'lik bir karede leke 3 DIP'ten fazla kaymaz: 15 fps yeterli, takılma görünmez.
        for (var i = 0; i < CoverBlobs.Count; i++)
            for (var t = 0.0; t < 30; t += 0.5)
            {
                var a = CoverBlobs.FrameAt(i, t);
                var b = CoverBlobs.FrameAt(i, t + 1.0 / 15);
                Assert.True(Math.Abs(a.OffsetX - b.OffsetX) < 3 && Math.Abs(a.OffsetY - b.OffsetY) < 3);
            }
    }

    [Fact]
    public void Blend_clamps()
    {
        Assert.Equal(Card, CoverBlobs.Blend(Card, Light, -1));
        Assert.Equal(Light, CoverBlobs.Blend(Card, Light, 2));
    }
}
