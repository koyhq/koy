using Koy.Core.Appearance;

namespace Koy.Core.Tests;

public class MaterialTests
{
    [Theory]
    [InlineData(MaterialLevel.Solid, 1.0, 255)]
    [InlineData(MaterialLevel.Frosted, 0.86, 219)]
    [InlineData(MaterialLevel.Glass, 0.72, 184)]
    public void SurfaceOpacity_matches_demo_levels(MaterialLevel value, double opacity, byte alpha)
    {
        Assert.Equal(opacity, MaterialSetting.SurfaceOpacity(value));
        Assert.Equal(alpha, MaterialSetting.SurfaceAlpha(value));
        Assert.Equal(value != MaterialLevel.Solid, MaterialSetting.IsTranslucent(value));
    }

    // Demodaki duvar kağıdı ortalamaları.
    private static readonly Rgb DarkWall = new(24, 30, 44);
    private static readonly Rgb LightWall = new(236, 240, 246);

    [Fact]
    public void Guard_keeps_glass_on_dark_wallpaper()
    {
        Assert.Equal(MaterialLevel.Glass, ReadabilityGuard.Effective(MaterialLevel.Glass, true, DarkWall, ReadabilityGuard.Notch));
        Assert.Equal(MaterialLevel.Glass, ReadabilityGuard.Effective(MaterialLevel.Glass, true, DarkWall, ReadabilityGuard.WindowDark));
    }

    [Fact]
    public void Guard_raises_glass_to_frosted_on_light_wallpaper_like_the_demo()
    {
        // Onay notu: açık duvar kağıdında Cam → Buzlu (demodaki %72 / %86, bugün Koy penceresinin değerleri).
        Assert.True(ReadabilityGuard.SecondaryContrast(MaterialSetting.GlassOpacity, LightWall, ReadabilityGuard.WindowDark) < 4.5);
        Assert.True(ReadabilityGuard.SecondaryContrast(MaterialSetting.FrostedOpacity, LightWall, ReadabilityGuard.WindowDark) >= 4.5);
        Assert.Equal(MaterialLevel.Frosted, ReadabilityGuard.Effective(MaterialLevel.Glass, true, LightWall, ReadabilityGuard.WindowDark));
    }

    [Theory]
    [InlineData(MaterialLevel.Solid, 1.0, 255)]
    [InlineData(MaterialLevel.Frosted, 0.94, 240)]
    [InlineData(MaterialLevel.Glass, 0.88, 224)]
    public void Layered_surfaces_are_more_opaque_than_the_window(MaterialLevel value, double opacity, byte alpha)
    {
        // Çentik ve dock bulanıklık alamaz; elle sınamada %72 arkadaki metni okunur bırakıyordu.
        Assert.Equal(opacity, MaterialSetting.LayeredOpacity(value));
        Assert.Equal(alpha, MaterialSetting.LayeredAlpha(value));
        Assert.True(MaterialSetting.LayeredOpacity(value) >= MaterialSetting.SurfaceOpacity(value));
    }

    [Fact]
    public void Notch_guard_uses_layered_opacity()
    {
        Assert.True(ReadabilityGuard.Notch.Layered);
        Assert.Equal(MaterialSetting.LayeredGlassOpacity, ReadabilityGuard.Notch.OpacityOf(MaterialLevel.Glass));
        Assert.Equal(MaterialSetting.GlassOpacity, ReadabilityGuard.WindowDark.OpacityOf(MaterialLevel.Glass));
        // Açık duvar kağıdında çentik yine eşiği geçen bir seviyede kalır.
        var level = ReadabilityGuard.Effective(MaterialLevel.Glass, true, LightWall, ReadabilityGuard.Notch);
        Assert.True(level == MaterialLevel.Solid
            || ReadabilityGuard.SecondaryContrast(ReadabilityGuard.Notch.OpacityOf(level), LightWall, ReadabilityGuard.Notch) >= 4.5);
    }

    [Fact]
    public void Guard_result_is_solid_or_passes_threshold()
    {
        var white = new Rgb(255, 255, 255);
        foreach (var surface in new[] { ReadabilityGuard.Notch, ReadabilityGuard.WindowDark, ReadabilityGuard.WindowLight })
        {
            var effective = ReadabilityGuard.Effective(MaterialLevel.Glass, true, white, surface);
            Assert.True(effective == MaterialLevel.Solid
                || ReadabilityGuard.SecondaryContrast(surface.OpacityOf(effective), white, surface) >= 4.5);
        }
    }

    [Fact]
    public void Guard_off_or_unknown_wallpaper_returns_requested()
    {
        Assert.Equal(MaterialLevel.Glass, ReadabilityGuard.Effective(MaterialLevel.Glass, false, LightWall, ReadabilityGuard.Notch));
        Assert.Equal(MaterialLevel.Glass, ReadabilityGuard.Effective(MaterialLevel.Glass, true, null, ReadabilityGuard.Notch));
        Assert.Equal(MaterialLevel.Solid, ReadabilityGuard.Effective(MaterialLevel.Solid, true, LightWall, ReadabilityGuard.Notch));
    }

    [Fact]
    public void Guard_never_lowers_opacity()
    {
        Assert.Equal(MaterialLevel.Frosted, ReadabilityGuard.Effective(MaterialLevel.Frosted, true, DarkWall, ReadabilityGuard.Notch));
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
