using Koy.Core.Appearance;

namespace Koy.Core.Tests;

public class MaterialTests
{
    [Theory]
    [InlineData("Katı", "Katı")]
    [InlineData(null, "Katı")]
    [InlineData("", "Katı")]
    [InlineData("bilinmeyen", "Katı")]
    [InlineData("Buzlu", "Buzlu")]
    [InlineData("frosted", "Buzlu")]
    [InlineData("Yarı saydam", "Buzlu")] // eski iki seviyeli dönem
    [InlineData("Translucent", "Buzlu")]
    [InlineData("acrylic", "Buzlu")]
    [InlineData("Cam", "Cam")]
    [InlineData(" glass ", "Cam")]
    public void Normalize_maps_new_and_legacy_values(string? value, string expected)
    {
        Assert.Equal(expected, MaterialSetting.Normalize(value));
    }

    [Theory]
    [InlineData("Katı", 1.0, 255)]
    [InlineData("Buzlu", 0.86, 219)]
    [InlineData("Cam", 0.72, 184)]
    [InlineData("Yarı saydam", 0.86, 219)]
    public void SurfaceOpacity_matches_demo_levels(string value, double opacity, byte alpha)
    {
        Assert.Equal(opacity, MaterialSetting.SurfaceOpacity(value));
        Assert.Equal(alpha, MaterialSetting.SurfaceAlpha(value));
        Assert.Equal(value != "Katı", MaterialSetting.IsTranslucent(value));
    }

    // Demodaki duvar kağıdı ortalamaları.
    private static readonly Rgb DarkWall = new(24, 30, 44);
    private static readonly Rgb LightWall = new(236, 240, 246);

    [Fact]
    public void Guard_keeps_glass_on_dark_wallpaper()
    {
        Assert.Equal(MaterialSetting.Glass, ReadabilityGuard.Effective("Cam", true, DarkWall, ReadabilityGuard.Notch));
        Assert.Equal(MaterialSetting.Glass, ReadabilityGuard.Effective("Cam", true, DarkWall, ReadabilityGuard.WindowDark));
    }

    [Fact]
    public void Guard_raises_glass_to_frosted_on_light_wallpaper_like_the_demo()
    {
        // Onay notu: açık duvar kağıdında Cam → Buzlu.
        Assert.True(ReadabilityGuard.SecondaryContrast(MaterialSetting.GlassOpacity, LightWall, ReadabilityGuard.Notch) < 4.5);
        Assert.True(ReadabilityGuard.SecondaryContrast(MaterialSetting.FrostedOpacity, LightWall, ReadabilityGuard.Notch) >= 4.5);
        Assert.Equal(MaterialSetting.Frosted, ReadabilityGuard.Effective("Cam", true, LightWall, ReadabilityGuard.Notch));
    }

    [Fact]
    public void Guard_result_is_solid_or_passes_threshold()
    {
        var white = new Rgb(255, 255, 255);
        foreach (var surface in new[] { ReadabilityGuard.Notch, ReadabilityGuard.WindowDark, ReadabilityGuard.WindowLight })
        {
            var effective = ReadabilityGuard.Effective("Cam", true, white, surface);
            Assert.True(effective == MaterialSetting.Solid
                || ReadabilityGuard.SecondaryContrast(MaterialSetting.SurfaceOpacity(effective), white, surface) >= 4.5);
        }
    }

    [Fact]
    public void Guard_off_or_unknown_wallpaper_returns_requested()
    {
        Assert.Equal(MaterialSetting.Glass, ReadabilityGuard.Effective("Cam", false, LightWall, ReadabilityGuard.Notch));
        Assert.Equal(MaterialSetting.Glass, ReadabilityGuard.Effective("Cam", true, null, ReadabilityGuard.Notch));
        Assert.Equal(MaterialSetting.Solid, ReadabilityGuard.Effective("Katı", true, LightWall, ReadabilityGuard.Notch));
    }

    [Fact]
    public void Guard_never_lowers_opacity()
    {
        Assert.Equal(MaterialSetting.Frosted, ReadabilityGuard.Effective("Buzlu", true, DarkWall, ReadabilityGuard.Notch));
    }

    [Fact]
    public void AverageBgra_averages_channels_and_ignores_alpha()
    {
        byte[] pixels = [0, 0, 255, 255, 255, 0, 0, 0]; // kırmızı, ardından alfası 0 olan mavi
        Assert.Equal(new Rgb(127, 0, 127), ReadabilityGuard.AverageBgra(pixels));
        Assert.Null(ReadabilityGuard.AverageBgra([]));
        Assert.Null(ReadabilityGuard.AverageBgra([1, 2, 3]));
    }
}
