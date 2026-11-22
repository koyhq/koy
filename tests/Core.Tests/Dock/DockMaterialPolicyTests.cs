using Koy.Core.Appearance;
using Koy.Core.Dock;

namespace Koy.Core.Tests;

public class DockMaterialPolicyTests
{
    [Theory]
    [InlineData(DockEdge.Bottom)]
    [InlineData(DockEdge.Top)]
    [InlineData(DockEdge.Left)]
    [InlineData(DockEdge.Right)]
    public void Solid_has_no_cues_on_any_edge_and_stays_opaque(DockEdge edge)
    {
        var full = DockMaterialPolicy.For(MaterialLevel.Solid, edge, fullWidth: true);
        var capsule = DockMaterialPolicy.For(MaterialLevel.Solid, edge, fullWidth: false);

        Assert.False(full.HasCues);
        Assert.False(capsule.HasCues);
        Assert.False(full.HasTile);
        Assert.Equal(0, full.TileBorderAlpha);
        Assert.Equal(255, full.SurfaceAlpha);
        Assert.Equal(0xE6, capsule.SurfaceAlpha); // kapsül Katı'da da %90
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Frosted_and_glass_have_sheen_and_highlight_and_glass_is_stronger(bool fullWidth)
    {
        var frosted = DockMaterialPolicy.For(MaterialLevel.Frosted, DockEdge.Bottom, fullWidth);
        var glass = DockMaterialPolicy.For(MaterialLevel.Glass, DockEdge.Bottom, fullWidth);

        Assert.True(frosted.HasCues);
        Assert.True(frosted.SheenLighten > 0 && frosted.HighlightAlpha > 0);
        Assert.True(glass.SheenLighten > frosted.SheenLighten);
        Assert.True(glass.SheenLighten < 0.2); // ince kalmalı
        Assert.True(glass.HasTile && frosted.HasTile);
        Assert.True(glass.TileBorderAlpha > frosted.TileBorderAlpha);
    }

    [Theory]
    [InlineData(MaterialLevel.Solid)]
    [InlineData(MaterialLevel.Frosted)]
    [InlineData(MaterialLevel.Glass)]
    public void Surface_alpha_follows_layered_alpha_table(MaterialLevel level)
    {
        Assert.Equal(MaterialSetting.LayeredAlpha(level), DockMaterialPolicy.For(level, DockEdge.Bottom, fullWidth: true).SurfaceAlpha);
        Assert.Equal(Math.Min((byte)0xE6, MaterialSetting.LayeredAlpha(level)),
            DockMaterialPolicy.For(level, DockEdge.Bottom, fullWidth: false).SurfaceAlpha);
    }

    [Theory]
    [InlineData(DockEdge.Bottom, 0.5, 0, 0.5, 1)]
    [InlineData(DockEdge.Top, 0.5, 1, 0.5, 0)]
    [InlineData(DockEdge.Left, 1, 0.5, 0, 0.5)]
    [InlineData(DockEdge.Right, 0, 0.5, 1, 0.5)]
    public void Gradient_starts_on_the_edge_facing_the_screen_center(DockEdge edge, double sx, double sy, double ex, double ey)
    {
        var look = DockMaterialPolicy.For(MaterialLevel.Glass, edge, fullWidth: true);

        Assert.Equal(new DockUnitPoint(sx, sy), look.GradientStart);
        Assert.Equal(new DockUnitPoint(ex, ey), look.GradientEnd);
    }

    [Fact]
    public void Full_width_line_fades_toward_the_ends_along_the_bar_axis_and_glass_more_than_frosted()
    {
        var glass = DockMaterialPolicy.For(MaterialLevel.Glass, DockEdge.Bottom, fullWidth: true);
        var frosted = DockMaterialPolicy.For(MaterialLevel.Frosted, DockEdge.Bottom, fullWidth: true);
        var vertical = DockMaterialPolicy.For(MaterialLevel.Glass, DockEdge.Left, fullWidth: true);

        Assert.True(glass.LineEndsFade < frosted.LineEndsFade && frosted.LineEndsFade < 1);
        Assert.Equal(new DockUnitPoint(0, 0.5), glass.RimStart);
        Assert.Equal(new DockUnitPoint(1, 0.5), glass.RimEnd);
        Assert.Equal(new DockUnitPoint(0.5, 0), vertical.RimStart);
        Assert.Equal(new DockUnitPoint(0.5, 1), vertical.RimEnd);
    }

    [Fact]
    public void Capsule_rim_is_diagonal_and_dims_in_the_middle()
    {
        var look = DockMaterialPolicy.For(MaterialLevel.Glass, DockEdge.Bottom, fullWidth: false);

        Assert.Equal(new DockUnitPoint(0, 0), look.RimStart);
        Assert.Equal(new DockUnitPoint(1, 1), look.RimEnd);
        Assert.True(look.RimMidAlpha < 1);
    }

    [Fact]
    public void Full_width_glass_sheen_is_a_band_that_fades_before_the_far_edge_and_the_line_peaks_bright()
    {
        var glass = DockMaterialPolicy.For(MaterialLevel.Glass, DockEdge.Bottom, fullWidth: true);
        var frosted = DockMaterialPolicy.For(MaterialLevel.Frosted, DockEdge.Bottom, fullWidth: true);
        var capsule = DockMaterialPolicy.For(MaterialLevel.Glass, DockEdge.Bottom, fullWidth: false);

        Assert.InRange(glass.SheenLighten, 0.14, 0.18);
        Assert.InRange(glass.SheenReach, 0.5, 0.6);
        Assert.True(glass.HighlightAlpha >= 0x70);
        Assert.True(frosted.SheenLighten < glass.SheenLighten && frosted.SheenLighten > 0);
        Assert.Equal(1, capsule.SheenReach); // kapsül değişmedi
        Assert.Equal(0x5C, capsule.HighlightAlpha);
    }
}
