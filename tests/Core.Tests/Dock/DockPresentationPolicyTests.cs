using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class DockPresentationPolicyTests
{
    private static DockResolvedState State(bool fullWidth = false, bool snapped = false, bool hidden = false, bool handle = false,
        double tx = 3, double ty = 4, double opacity = 1) =>
        new(LeftDip: 0, TopDip: 0, WidthDip: 500, HeightDip: 60,
            CornerRadiusTopLeft: 1, CornerRadiusTopRight: 2, CornerRadiusBottomRight: 3, CornerRadiusBottomLeft: 4,
            TranslationY: ty, Opacity: opacity, IsSnapped: snapped, IsHandleVisible: handle, IsHidden: hidden,
            IsFullWidth: fullWidth, AppBarReservedHeightDip: 0, TranslationX: tx);

    private static DockPresentationInput Input(DockResolvedState state, DockEdge edge = DockEdge.Bottom) =>
        new(state, edge, DockProximityMode.ReserveSpace, HideInFullscreen: true, HiddenEdgeLineEnabled: false,
            EdgeOffsetDip: 0, IsForegroundFullscreen: false, IsMouseAtEdge: false, ControllerVisible: null);

    [Theory]
    [InlineData(DockEdge.Bottom, 12, 12, 12, 10)]
    [InlineData(DockEdge.Top, 12, 10, 12, 12)]
    [InlineData(DockEdge.Left, 10, 12, 12, 12)]
    [InlineData(DockEdge.Right, 12, 12, 10, 12)]
    public void Floating_capsule_leaves_a_small_gap_on_the_docked_edge(DockEdge edge, double l, double t, double r, double b)
    {
        var d = DockPresentationPolicy.Decide(Input(State(), edge));

        Assert.Equal(new DockInsets(l, t, r, b), d.Margin);
        Assert.Equal(DockCorners.Uniform(18), d.Corners);
    }

    [Theory]
    [InlineData(DockEdge.Bottom, 12, 12, 12, 0)]
    [InlineData(DockEdge.Top, 12, 0, 12, 12)]
    [InlineData(DockEdge.Left, 0, 12, 12, 12)]
    [InlineData(DockEdge.Right, 12, 12, 0, 12)]
    public void Snapped_dock_touches_the_edge_and_takes_the_resolved_corners(DockEdge edge, double l, double t, double r, double b)
    {
        var d = DockPresentationPolicy.Decide(Input(State(snapped: true), edge));

        Assert.Equal(new DockInsets(l, t, r, b), d.Margin);
        Assert.Equal(new DockCorners(1, 2, 3, 4), d.Corners);
    }

    [Fact]
    public void Full_width_has_no_margin_or_corners()
    {
        var d = DockPresentationPolicy.Decide(Input(State(fullWidth: true)));

        Assert.Equal(new DockInsets(0, 0, 0, 0), d.Margin);
        Assert.Equal(DockCorners.Uniform(0), d.Corners);
    }

    [Fact]
    public void Visible_dock_uses_the_resolved_translation_and_opacity()
    {
        var d = DockPresentationPolicy.Decide(Input(State(opacity: 0.8)));

        Assert.True(d.ShouldShow);
        Assert.Equal((3d, 4d), (d.TargetX, d.TargetY));
        Assert.Equal(0.8, d.TargetOpacity);
        Assert.False(d.NeedsEdgeWatch);
    }

    [Fact]
    public void Resolver_hidden_dock_keeps_the_resolver_translation_and_watches_the_edge()
    {
        var d = DockPresentationPolicy.Decide(Input(State(hidden: true, tx: 0, ty: 70, opacity: 0.5)));

        Assert.False(d.ShouldShow);
        Assert.Equal((0d, 70d), (d.TargetX, d.TargetY));
        Assert.Equal(0, d.TargetOpacity);
        Assert.True(d.NeedsEdgeWatch);
    }

    [Theory]
    [InlineData(DockEdge.Bottom, 0, 130)]
    [InlineData(DockEdge.Top, 0, -130)]
    [InlineData(DockEdge.Left, -130, 0)]
    [InlineData(DockEdge.Right, 130, 0)]
    public void Fullscreen_hidden_floating_dock_slides_out_by_height_margin_offset_and_ten(DockEdge edge, double x, double y)
    {
        // Yüzen: 60 kalınlık + (10 alt boşluk + 50 ofset) + 10.
        var input = Input(State(), edge) with { IsForegroundFullscreen = true, EdgeOffsetDip = 50 };

        var d = DockPresentationPolicy.Decide(input);

        Assert.False(d.ShouldShow);
        Assert.Equal((x, y), (d.TargetX, d.TargetY));
        Assert.False(d.NeedsEdgeWatch);
    }

    [Fact]
    public void Slide_distance_depends_on_the_dock_style()
    {
        var hide = Input(State(), DockEdge.Bottom) with { IsForegroundFullscreen = true, EdgeOffsetDip = 5 };

        Assert.Equal(48 + 5 + 10, DockPresentationPolicy.Decide(hide with { Resolved = State(fullWidth: true) }).TargetY);
        Assert.Equal(46 + 5 + 10, DockPresentationPolicy.Decide(hide with { Resolved = State(snapped: true) }).TargetY);
        Assert.Equal(60 + 10 + 5 + 10, DockPresentationPolicy.Decide(hide).TargetY);
    }

    [Fact]
    public void Fullscreen_does_not_hide_when_the_setting_is_off_but_still_blocks_edge_watching()
    {
        var d = DockPresentationPolicy.Decide(Input(State()) with { HideInFullscreen = false, IsForegroundFullscreen = true, IsMouseAtEdge = true });

        Assert.True(d.ShouldShow);
        Assert.False(d.NeedsEdgeWatch);
    }

    [Theory]
    [InlineData(DockProximityMode.ReserveSpace, false, true)]
    [InlineData(DockProximityMode.Dodge, false, false)]
    [InlineData(DockProximityMode.Dodge, true, true)]
    public void Controller_narrows_visibility_unless_the_dock_reserves_space(DockProximityMode mode, bool controllerVisible, bool expected)
    {
        var d = DockPresentationPolicy.Decide(Input(State()) with { Proximity = mode, ControllerVisible = controllerVisible });

        Assert.Equal(expected, d.ShouldShow);
    }

    [Fact]
    public void Missing_controller_never_narrows_visibility()
    {
        var d = DockPresentationPolicy.Decide(Input(State()) with { Proximity = DockProximityMode.Dodge, ControllerVisible = null });

        Assert.True(d.ShouldShow);
    }

    [Fact]
    public void Mouse_at_edge_keeps_the_watch_on_while_visible()
    {
        var d = DockPresentationPolicy.Decide(Input(State()) with { IsMouseAtEdge = true });

        Assert.True(d.NeedsEdgeWatch);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    public void Hidden_edge_line_shows_only_for_a_hidden_dock_without_fullscreen_or_handle(bool lineOn, bool hidden, bool handle, bool expected)
    {
        var d = DockPresentationPolicy.Decide(Input(State(hidden: hidden, handle: handle)) with { HiddenEdgeLineEnabled = lineOn });

        Assert.Equal(expected, d.IsEdgeLineVisible);
    }
}
