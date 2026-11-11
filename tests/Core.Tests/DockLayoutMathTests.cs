using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Settings;
using Koy.Core.Shell;
using Xunit;

namespace Koy.Core.Tests;

public class DockLayoutMathTests
{
    private static readonly DockRectDip Screen1080p = new(0, 0, 1920, 1080);

    // Görev çubuğu ve dock dışındaki alan: alttan 48 px dock için çalışma alanı kısalmış olsun.
    private static readonly DockRectDip WorkArea = new(0, 0, 1920, 1032);

    [Fact]
    public void Default_bottom_layout_matches_full_width_dock_geometry()
    {
        // Varsayılan ayarlar (alt, %100, ofset 0, dolgu 0) bugünkü tam genişlik dock ile aynı kutuyu vermeli.
        var monitor = new DisplayMonitor(
            Id: "M1",
            Name: "Primary",
            Bounds: new WindowRect(0, 0, 1920, 1080),
            WorkArea: new WindowRect(0, 0, 1920, 1080),
            IsPrimary: true);

        var resolved = DockPositionResolver.Resolve(
            monitor: monitor,
            dockStyle: DockStyleMode.FullWidth,
            proximityMode: DockProximityMode.ReserveSpace,
            contentWidthDip: 1920,
            foregroundWindowBoundsDip: WindowRect.Empty);

        var layout = DockLayoutMath.Compute(
            DockEdge.Bottom,
            DockLayoutLimits.DefaultLengthPercent,
            DockLayoutLimits.DefaultEdgeOffsetDip,
            DockLayoutLimits.DefaultInnerPaddingDip,
            DockPositionResolver.FullWidthHeightDip,
            Screen1080p);

        Assert.Equal(resolved.LeftDip, layout.Outer.Left);
        Assert.Equal(resolved.TopDip, layout.Outer.Top);
        Assert.Equal(resolved.WidthDip, layout.Outer.Width);
        Assert.Equal(resolved.HeightDip, layout.Outer.Height);
        Assert.Equal(layout.Outer, layout.Content);
    }

    [Fact]
    public void Bottom_edge_sits_on_work_area_bottom_minus_offset_and_thickness()
    {
        var result = DockLayoutMath.Compute(DockEdge.Bottom, 100, 12, 0, 48, Screen1080p);

        Assert.Equal(new DockRectDip(0, 1080 - 12 - 48, 1920, 48), result.Outer);
    }

    [Fact]
    public void Top_edge_sits_on_work_area_top_plus_offset()
    {
        var result = DockLayoutMath.Compute(DockEdge.Top, 100, 8, 0, 48, Screen1080p);

        Assert.Equal(new DockRectDip(0, 8, 1920, 48), result.Outer);
    }

    [Fact]
    public void Left_edge_is_full_height_and_thickness_wide()
    {
        var result = DockLayoutMath.Compute(DockEdge.Left, 100, 0, 0, 60, Screen1080p);

        Assert.Equal(new DockRectDip(0, 0, 60, 1080), result.Outer);
    }

    [Fact]
    public void Right_edge_sits_on_work_area_right_minus_offset_and_thickness()
    {
        var result = DockLayoutMath.Compute(DockEdge.Right, 100, 10, 0, 60, Screen1080p);

        Assert.Equal(new DockRectDip(1920 - 10 - 60, 0, 60, 1080), result.Outer);
    }

    [Fact]
    public void Length_is_a_centered_percentage_of_the_parallel_side()
    {
        var bottom = DockLayoutMath.Compute(DockEdge.Bottom, 50, 0, 0, 48, Screen1080p);
        Assert.Equal(new DockRectDip(480, 1032, 960, 48), bottom.Outer);

        var left = DockLayoutMath.Compute(DockEdge.Left, 50, 0, 0, 48, Screen1080p);
        Assert.Equal(new DockRectDip(0, 270, 48, 540), left.Outer);
    }

    [Fact]
    public void Work_area_with_nonzero_origin_is_respected()
    {
        // İkinci monitör: çalışma alanı x=1920'den başlar, üstten 40 px görev çubuğu var.
        var secondary = new DockRectDip(1920, 40, 1440, 1400);

        var bottom = DockLayoutMath.Compute(DockEdge.Bottom, 100, 0, 0, 48, secondary);
        Assert.Equal(new DockRectDip(1920, 40 + 1400 - 48, 1440, 48), bottom.Outer);

        var right = DockLayoutMath.Compute(DockEdge.Right, 100, 0, 0, 48, secondary);
        Assert.Equal(new DockRectDip(1920 + 1440 - 48, 40, 48, 1400), right.Outer);
    }

    [Fact]
    public void Inner_padding_insets_the_content_rectangle_only()
    {
        var result = DockLayoutMath.Compute(DockEdge.Bottom, 100, 0, 6, 48, Screen1080p);

        Assert.Equal(new DockRectDip(0, 1032, 1920, 48), result.Outer);
        Assert.Equal(new DockRectDip(6, 1038, 1908, 36), result.Content);
    }

    [Fact]
    public void Inset_never_produces_negative_size()
    {
        var content = DockLayoutMath.Compute(DockEdge.Bottom, 100, 0, 24, 20, Screen1080p).Content;

        Assert.Equal(0.0, content.Height);
        Assert.True(content.Width >= 0.0);
    }

    [Fact]
    public void Content_stays_inside_outer_when_padding_exceeds_half_the_thickness()
    {
        var result = DockLayoutMath.Compute(DockEdge.Bottom, 100, 0, 24, 20, Screen1080p);

        Assert.True(result.Content.Top >= result.Outer.Top);
        Assert.True(result.Content.Bottom <= result.Outer.Bottom);
        Assert.Equal(result.Outer.Top + 10, result.Content.Top);
    }

    [Fact]
    public void Monitor_with_negative_coordinates_is_respected()
    {
        // Birincil monitörün solundaki ve üstündeki ekran negatif koordinatta durur.
        var left = new DockRectDip(-1920, -200, 1920, 1080);

        var bottom = DockLayoutMath.Compute(DockEdge.Bottom, 50, 4, 0, 48, left);
        Assert.Equal(new DockRectDip(-1920 + 480, -200 + 1080 - 4 - 48, 960, 48), bottom.Outer);

        var right = DockLayoutMath.Compute(DockEdge.Right, 100, 0, 0, 48, left);
        Assert.Equal(new DockRectDip(-48, -200, 48, 1080), right.Outer);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-100, -50)]
    [InlineData(double.NaN, double.NaN)]
    [InlineData(double.PositiveInfinity, 100)]
    public void Degenerate_work_area_never_gives_negative_or_non_finite_sizes(double width, double height)
    {
        foreach (var edge in Enum.GetValues<DockEdge>())
        {
            var r = DockLayoutMath.Compute(edge, 100, 10, 6, 48, new DockRectDip(0, 0, width, height));

            foreach (var v in new[] { r.Outer.Left, r.Outer.Top, r.Outer.Width, r.Outer.Height, r.Content.Width, r.Content.Height })
                Assert.True(double.IsFinite(v), $"{edge}: sonlu olmalı");
            Assert.True(r.Outer.Width >= 0 && r.Outer.Height >= 0 && r.Content.Width >= 0 && r.Content.Height >= 0);
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-30.0)]
    public void Invalid_thickness_gives_zero_thickness_not_NaN(double thickness)
    {
        var r = DockLayoutMath.Compute(DockEdge.Bottom, 100, 0, 0, thickness, Screen1080p);

        Assert.Equal(0.0, r.Outer.Height);
        Assert.Equal(1080.0, r.Outer.Top);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public void Undefined_edge_number_is_treated_as_bottom(int raw)
    {
        var undefined = DockLayoutMath.Compute((DockEdge)raw, 100, 5, 0, 48, Screen1080p);
        var bottom = DockLayoutMath.Compute(DockEdge.Bottom, 100, 5, 0, 48, Screen1080p);

        Assert.Equal(bottom, undefined);
    }

    [Fact]
    public void Undefined_edge_number_in_settings_json_loads_as_bottom()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-dock-layout-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{ \"Version\": 4, \"Dock\": { \"Edge\": 7, \"LengthPercent\": 40 } }");

            var back = new SettingsStore(path).Load().Dock;

            Assert.Equal(DockEdge.Bottom, back.Edge);
            Assert.Equal(40, back.LengthPercent);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Out_of_range_length_is_clamped_into_limits()
    {
        var tooShort = DockLayoutMath.Compute(DockEdge.Bottom, 0, 0, 0, 48, Screen1080p);
        Assert.Equal(1920 * 0.10, tooShort.Outer.Width, precision: 6);

        var tooLong = DockLayoutMath.Compute(DockEdge.Bottom, 500, 0, 0, 48, Screen1080p);
        Assert.Equal(1920.0, tooLong.Outer.Width);
    }

    [Fact]
    public void Out_of_range_offset_and_padding_are_clamped()
    {
        var result = DockLayoutMath.Compute(DockEdge.Top, 100, 10_000, 10_000, 48, Screen1080p);

        Assert.Equal(DockLayoutLimits.MaxEdgeOffsetDip, result.Outer.Top);
        Assert.Equal(DockLayoutLimits.MaxInnerPaddingDip, result.Content.Left);
    }

    [Fact]
    public void Negative_offset_and_padding_fall_back_to_zero()
    {
        var result = DockLayoutMath.Compute(DockEdge.Top, 100, -5, -3, 48, Screen1080p);

        Assert.Equal(0.0, result.Outer.Top);
        Assert.Equal(0.0, result.Content.Left);
    }

    [Fact]
    public void Non_finite_values_use_defaults()
    {
        Assert.Equal(DockLayoutLimits.DefaultEdgeOffsetDip, DockLayoutLimits.ClampEdgeOffsetDip(double.NaN));
        Assert.Equal(DockLayoutLimits.DefaultInnerPaddingDip, DockLayoutLimits.ClampInnerPaddingDip(double.PositiveInfinity));
        Assert.Equal(DockLayoutLimits.DefaultIconSpacingDip, DockLayoutLimits.ClampIconSpacingDip(double.NaN));
    }

    [Fact]
    public void Icon_spacing_is_clamped_to_its_range()
    {
        Assert.Equal(0.0, DockLayoutLimits.ClampIconSpacingDip(-1));
        Assert.Equal(DockLayoutLimits.MaxIconSpacingDip, DockLayoutLimits.ClampIconSpacingDip(99));
        Assert.Equal(6.0, DockLayoutLimits.ClampIconSpacingDip(6));
    }

    [Fact]
    public void Default_dock_settings_keep_bottom_edge_and_full_length()
    {
        var dock = new DockSettings();

        Assert.Equal(DockEdge.Bottom, dock.Edge);
        Assert.Equal(100, dock.LengthPercent);
        Assert.Equal(0.0, dock.EdgeOffsetDip);
        Assert.Equal(0.0, dock.InnerPaddingDip);
        Assert.Equal(2.0, dock.IconSpacingDip);
    }

    [Fact]
    public void Sanitize_clamps_every_geometry_field_of_dock_settings()
    {
        var dock = new DockSettings
        {
            LengthPercent = 3,
            EdgeOffsetDip = 500,
            InnerPaddingDip = -4,
            IconSpacingDip = double.NaN,
        };

        DockLayoutLimits.Sanitize(dock);

        Assert.Equal(DockLayoutLimits.MinLengthPercent, dock.LengthPercent);
        Assert.Equal(DockLayoutLimits.MaxEdgeOffsetDip, dock.EdgeOffsetDip);
        Assert.Equal(DockLayoutLimits.MinInnerPaddingDip, dock.InnerPaddingDip);
        Assert.Equal(DockLayoutLimits.DefaultIconSpacingDip, dock.IconSpacingDip);
    }

    [Fact]
    public void Edge_is_written_by_name_and_read_back()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-dock-layout-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            store.Save(new AppSettings { Dock = new DockSettings { Edge = DockEdge.Left, LengthPercent = 70 } });

            var text = File.ReadAllText(store.Path);
            var back = store.Load().Dock;

            Assert.Contains("\"Left\"", text);
            Assert.Equal(DockEdge.Left, back.Edge);
            Assert.Equal(70, back.LengthPercent);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Settings_file_without_geometry_fields_loads_as_bottom_full_length()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-dock-layout-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{ \"Version\": 4, \"Dock\": { \"Enabled\": true } }");

            var back = new SettingsStore(path).Load().Dock;

            Assert.Equal(DockEdge.Bottom, back.Edge);
            Assert.Equal(100, back.LengthPercent);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Thickness_and_offset_stay_inside_a_small_work_area()
    {
        var small = new DockRectDip(0, 0, 1280, 40);

        var result = DockLayoutMath.Compute(DockEdge.Bottom, 100, 96, 0, 60, small);

        Assert.True(result.Outer.Top >= small.Top, "üst kenar çalışma alanının dışına taşmamalı");
        Assert.True(result.Outer.Top + result.Outer.Height <= small.Top + small.Height + 1e-9);
        Assert.True(result.Outer.Height <= small.Height);
    }
}
