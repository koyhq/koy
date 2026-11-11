using Koy.Core.Appearance;
using Koy.Core.Media;

namespace Koy.Core.Tests;

public class CoverPaletteExtractorTests
{
    private static byte[] Image(int w, int h, Func<int, int, (byte r, byte g, byte b)> pixel, int stride = 0)
    {
        stride = stride == 0 ? w * 4 : stride;
        var data = new byte[stride * h];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var (r, g, b) = pixel(x, y);
            var o = y * stride + x * 4;
            data[o] = b; data[o + 1] = g; data[o + 2] = r; data[o + 3] = 255;
        }
        return data;
    }

    private static double Hue(Rgb c) => ColorMath.ToHsl(c).H;

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 255, 255)]
    [InlineData(255, 0, 0)]
    [InlineData(34, 197, 94)]
    [InlineData(14, 42, 24)]
    [InlineData(128, 128, 128)]
    [InlineData(244, 244, 245)]
    [InlineData(10, 200, 250)]
    public void Hsl_round_trips(byte r, byte g, byte b)
    {
        var c = new Rgb(r, g, b);
        Assert.Equal(c, ColorMath.FromHsl(ColorMath.ToHsl(c)));
    }

    [Fact]
    public void Hsl_known_values()
    {
        var red = ColorMath.ToHsl(new Rgb(255, 0, 0));
        Assert.Equal(0, red.H, 3);
        Assert.Equal(1, red.S, 3);
        Assert.Equal(0.5, red.L, 3);
        Assert.Equal(240, ColorMath.ToHsl(new Rgb(0, 0, 255)).H, 3);
        Assert.Equal(new Rgb(0, 255, 0), ColorMath.FromHsl(120, 1, 0.5));
    }

    [Fact]
    public void Pure_red_gives_red_card()
    {
        var px = Image(8, 8, (_, _) => (255, 0, 0));
        var p = CoverPaletteExtractor.Extract(px, 8, 8, 32);
        Assert.NotNull(p);
        Assert.True(Hue(p!.Card) is < 5 or > 355);
        Assert.InRange(ColorMath.ToHsl(p.Card).L, 0.05, 0.16);
        Assert.Equal(p.SubText, p.Spectrum);
        Assert.InRange(ColorMath.ToHsl(p.SubText).L, 0.78, 0.82);
    }

    [Fact]
    public void Gray_gradient_gives_null()
    {
        var px = Image(16, 16, (x, _) => ((byte)(x * 16), (byte)(x * 16), (byte)(x * 16)));
        Assert.Null(CoverPaletteExtractor.Extract(px, 16, 16, 64));
    }

    [Fact]
    public void Half_gray_half_blue_gives_blue()
    {
        var px = Image(8, 8, (x, _) => x < 4 ? ((byte)128, (byte)128, (byte)128) : ((byte)0, (byte)0, (byte)255));
        var p = CoverPaletteExtractor.Extract(px, 8, 8, 32);
        Assert.NotNull(p);
        Assert.InRange(Hue(p!.Card), 235, 245);
    }

    [Fact]
    public void Mostly_gray_with_few_colored_pixels_gives_null()
    {
        // 100 pikselin 5'i renkli: %10 eşiğinin altı.
        var px = Image(10, 10, (x, y) => y == 0 && x < 5 ? ((byte)255, (byte)0, (byte)0) : ((byte)120, (byte)120, (byte)120));
        Assert.Null(CoverPaletteExtractor.Extract(px, 10, 10, 40));
    }

    [Theory]
    [InlineData(255, 255, 0)]
    [InlineData(0, 255, 255)]
    [InlineData(255, 0, 255)]
    [InlineData(255, 128, 0)]
    [InlineData(0, 255, 0)]
    public void Card_always_keeps_title_contrast(byte r, byte g, byte b)
    {
        var px = Image(8, 8, (_, _) => (r, g, b));
        var p = CoverPaletteExtractor.Extract(px, 8, 8, 32);
        Assert.NotNull(p);
        Assert.True(ColorMath.Contrast(Rgb.Parse("#F4F4F5"), p!.Card) >= 7.0);
    }

    [Fact]
    public void Stride_padding_is_ignored()
    {
        // Satır sonundaki dolgu baytları (beyaz) palete katılmamalı.
        var px = Image(8, 8, (_, _) => (0, 0, 255), stride: 48);
        for (var y = 0; y < 8; y++)
            for (var i = 32; i < 48; i++)
                px[y * 48 + i] = 255;
        var p = CoverPaletteExtractor.Extract(px, 8, 8, 48);
        Assert.NotNull(p);
        Assert.InRange(Hue(p!.Card), 235, 245);
    }

    [Fact]
    public void Empty_or_invalid_input_gives_null()
    {
        Assert.Null(CoverPaletteExtractor.Extract([], 0, 0, 0));
        Assert.Null(CoverPaletteExtractor.Extract(new byte[4], 0, 1, 4));
        Assert.Null(CoverPaletteExtractor.Extract(new byte[10], 4, 4, 16)); // kısa dizi
        Assert.Null(CoverPaletteExtractor.Extract(new byte[64], 4, 4, 8)); // stride < width*4
        Assert.Null(CoverPaletteExtractor.Extract(new byte[64], 4, 4, 16)); // tamamen saydam
    }

    [Fact]
    public void Single_pixel_works()
    {
        var p = CoverPaletteExtractor.Extract([0, 255, 0, 255], 1, 1, 4);
        Assert.NotNull(p);
        Assert.InRange(Hue(p!.Card), 115, 125);
    }
}
