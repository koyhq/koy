using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Shell;
using Xunit;

namespace Koy.Core.Tests;

public class DockPositionResolverTests
{
    private static readonly DisplayMonitor Primary1080p = new(
        Id: "MONITOR_1",
        Name: "Primary 1080p",
        Bounds: new WindowRect(0, 0, 1920, 1080),
        WorkArea: new WindowRect(0, 0, 1920, 1080),
        IsPrimary: true,
        DpiScaleX: 1.0,
        DpiScaleY: 1.0,
        Handle: 100);

    private static readonly DisplayMonitor Secondary1440p = new(
        Id: "MONITOR_2",
        Name: "Secondary 1440p",
        Bounds: new WindowRect(1920, 0, 4480, 1440),
        WorkArea: new WindowRect(1920, 0, 4480, 1440),
        IsPrimary: false,
        DpiScaleX: 1.0,
        DpiScaleY: 1.0,
        Handle: 200);

    [Fact]
    public void FloatingCapsule_WhenNoWindowIntersects_FloatsWith10PxMarginAnd18PxRadius()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.DockDown,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: new WindowRect(100, 100, 900, 700), // Far from dock
            isWindowMaximized: false,
            isMouseAtBottomEdge: false,
            isMouseOverDock: false);

        Assert.Equal(60.0, result.HeightDip);
        // Top should be 1080 - 60 - 10 = 1010
        Assert.Equal(1010.0, result.TopDip);
        // Center: (1920 - 500) / 2 = 710
        Assert.Equal(710.0, result.LeftDip);
        Assert.Equal(500.0, result.WidthDip);
        Assert.Equal(18.0, result.CornerRadiusTopLeft);
        Assert.Equal(18.0, result.CornerRadiusTopRight);
        Assert.Equal(18.0, result.CornerRadiusBottomRight);
        Assert.Equal(18.0, result.CornerRadiusBottomLeft);
        Assert.True(result.HasUniformRadius);
        Assert.False(result.IsSnapped);
        Assert.False(result.IsHandleVisible);
        Assert.False(result.IsHidden);
        Assert.False(result.IsFullWidth);
        Assert.Equal(0, result.AppBarReservedHeightDip);
    }

    [Fact]
    public void DockDown_WhenWindowApproachesDock_SnapsToBottomAndShrinksTo46PxWith14PxTopCorners()
    {
        // Window extends into dock proximity area (bottom >= 1080 - 80 = 1000)
        var approachingWindow = new WindowRect(200, 200, 1200, 1020);

        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.DockDown,
            contentWidthDip: 520.0,
            foregroundWindowBoundsDip: approachingWindow,
            isWindowMaximized: false);

        Assert.True(result.IsSnapped);
        Assert.Equal(46.0, result.HeightDip);
        // Top should be exactly at bottom: 1080 - 46 = 1034
        Assert.Equal(1034.0, result.TopDip);
        Assert.Equal(14.0, result.CornerRadiusTopLeft);
        Assert.Equal(14.0, result.CornerRadiusTopRight);
        Assert.Equal(0.0, result.CornerRadiusBottomRight);
        Assert.Equal(0.0, result.CornerRadiusBottomLeft);
        Assert.False(result.HasUniformRadius);
        Assert.False(result.IsHandleVisible);
        Assert.False(result.IsHidden);
    }

    [Fact]
    public void DockDown_WhenWindowLeavesDockArea_SpringsBackToFloatingCapsule()
    {
        var distantWindow = new WindowRect(100, 50, 800, 600);

        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.DockDown,
            contentWidthDip: 520.0,
            foregroundWindowBoundsDip: distantWindow,
            isWindowMaximized: false);

        Assert.False(result.IsSnapped);
        Assert.Equal(60.0, result.HeightDip);
        Assert.Equal(1010.0, result.TopDip);
        Assert.Equal(18.0, result.CornerRadiusTopLeft);
        Assert.Equal(18.0, result.CornerRadiusBottomLeft);
    }

    [Fact]
    public void ReserveSpace_AlwaysSnapsToBottomAndReserves46PxWorkArea()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.ReserveSpace,
            contentWidthDip: 480.0,
            foregroundWindowBoundsDip: WindowRect.Empty,
            isWindowMaximized: false);

        Assert.True(result.IsSnapped);
        Assert.Equal(46.0, result.HeightDip);
        Assert.Equal(1034.0, result.TopDip);
        Assert.Equal(14.0, result.CornerRadiusTopLeft);
        Assert.Equal(14.0, result.CornerRadiusTopRight);
        Assert.Equal(0.0, result.CornerRadiusBottomLeft);
        Assert.Equal(46, result.AppBarReservedHeightDip);
    }

    [Fact]
    public void Dodge_WhenMaximizedWindowCoversDock_SlidesOffscreenAndShowsHandle()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.Dodge,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowMaximized: true,
            isMouseAtBottomEdge: false,
            isMouseOverDock: false);

        Assert.True(result.IsHidden);
        Assert.True(result.IsHandleVisible);
        Assert.True(result.TranslationY > 60.0);
        Assert.Equal(0.0, result.Opacity);
    }

    [Fact]
    public void Dodge_WhenMaximizedWindowCoversDockAndMouseHoversEdge_SlidesBackUp()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.Dodge,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowMaximized: true,
            isMouseAtBottomEdge: true, // Mouse at bottom edge (150ms hover)
            isMouseOverDock: false);

        Assert.False(result.IsHidden);
        Assert.False(result.IsHandleVisible);
        Assert.Equal(0.0, result.TranslationY);
        Assert.Equal(1.0, result.Opacity);
        Assert.Equal(60.0, result.HeightDip);
    }

    [Fact]
    public void AlwaysHidden_WhenMouseNotAtEdge_RemainsHidden()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.AlwaysHidden,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: WindowRect.Empty,
            isMouseAtBottomEdge: false,
            isMouseOverDock: false);

        Assert.True(result.IsHidden);
        Assert.Equal(0.0, result.Opacity);
        Assert.True(result.TranslationY > 0);
        Assert.False(result.IsHandleVisible);
    }

    [Fact]
    public void AlwaysHidden_WhenMouseTouchesBottomEdge_BecomesVisible()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.AlwaysHidden,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: WindowRect.Empty,
            isMouseAtBottomEdge: true,
            isMouseOverDock: false);

        Assert.False(result.IsHidden);
        Assert.Equal(1.0, result.Opacity);
        Assert.Equal(0.0, result.TranslationY);
    }

    [Fact]
    public void FullWidth_SnapsToBottomWith48PxHeightAndFullScreenWidth()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FullWidth,
            proximityMode: DockProximityMode.DockDown,
            contentWidthDip: 600.0,
            foregroundWindowBoundsDip: WindowRect.Empty);

        Assert.True(result.IsFullWidth);
        Assert.True(result.IsSnapped);
        Assert.Equal(0.0, result.LeftDip);
        Assert.Equal(1920.0, result.WidthDip);
        Assert.Equal(48.0, result.HeightDip);
        Assert.Equal(DockPositionResolver.FullWidthHeightDip, result.HeightDip);
        // Top = 1080 - 48 = 1032
        Assert.Equal(1032.0, result.TopDip);
        Assert.Equal(0.0, result.CornerRadiusTopLeft);
        Assert.Equal(0.0, result.CornerRadiusTopRight);
        Assert.Equal(0.0, result.CornerRadiusBottomRight);
        Assert.Equal(0.0, result.CornerRadiusBottomLeft);
    }

    [Fact]
    public void MultiMonitor_SecondaryScreenCoordinates_CalculatesCorrectOffsets()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Secondary1440p, // Left: 1920, Width: 2560, Height: 1440
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.DockDown,
            contentWidthDip: 600.0,
            foregroundWindowBoundsDip: WindowRect.Empty);

        // Center on secondary: 1920 + (2560 - 600) / 2 = 1920 + 980 = 2900
        Assert.Equal(2900.0, result.LeftDip);
        // Top: 1440 - 70 = 1370
        Assert.Equal(1370.0, result.TopDip);
        Assert.Equal(600.0, result.WidthDip);
        Assert.Equal(60.0, result.HeightDip);
    }

    [Fact]
    public void FullWidth_WhenReserveSpace_Reserves48PxWorkArea()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FullWidth,
            proximityMode: DockProximityMode.ReserveSpace,
            contentWidthDip: 600.0,
            foregroundWindowBoundsDip: WindowRect.Empty);

        Assert.True(result.IsFullWidth);
        Assert.Equal(48, result.AppBarReservedHeightDip);
        Assert.Equal(48.0, result.HeightDip);
    }

    [Fact]
    public void Dodge_WhenWindowDoesNotIntersectDock_FloatsNormallyWithoutHandle()
    {
        var distantWindow = new WindowRect(100, 100, 500, 500);

        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.Dodge,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: distantWindow,
            isWindowMaximized: false);

        Assert.False(result.IsHidden);
        Assert.False(result.IsHandleVisible);
        Assert.Equal(0.0, result.TranslationY);
        Assert.Equal(1.0, result.Opacity);
        Assert.Equal(60.0, result.HeightDip);
    }

    [Fact]
    public void Resolve_WithNullMonitor_UsesDefaultResolutionGracefully()
    {
        var result = DockPositionResolver.Resolve(
            monitor: null,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.DockDown,
            contentWidthDip: 520.0,
            foregroundWindowBoundsDip: WindowRect.Empty);

        Assert.Equal(60.0, result.HeightDip);
        // Default 1080 - 70 = 1010
        Assert.Equal(1010.0, result.TopDip);
        Assert.False(result.IsSnapped);
    }

    [Fact]
    public void DockDown_WhenWindowIsOnDifferentMonitor_DoesNotSnap()
    {
        // Window is located completely on secondary monitor (left: 1920 to 2500)
        var windowOnSecondary = new WindowRect(2000, 800, 2500, 1050);

        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p, // primary monitor (left: 0 to 1920)
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.DockDown,
            contentWidthDip: 520.0,
            foregroundWindowBoundsDip: windowOnSecondary,
            isWindowMaximized: false);

        // Should not snap because window is not on this monitor!
        Assert.False(result.IsSnapped);
        Assert.Equal(60.0, result.HeightDip);
        Assert.Equal(1010.0, result.TopDip);
    }

    [Fact]
    public void FullWidth_WhenFullscreen_HidesDock()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FullWidth,
            proximityMode: DockProximityMode.Dodge,
            contentWidthDip: 600.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowFullscreen: true,
            hideInFullscreen: true);

        Assert.True(result.IsHidden);
        Assert.Equal(0.0, result.Opacity);
        Assert.True(result.TranslationY > 40.0);
        Assert.False(result.IsHandleVisible);
    }

    [Fact]
    public void FullWidth_WhenFullscreen_AndHideInFullscreenFalse_DoesNotHide()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FullWidth,
            proximityMode: DockProximityMode.Dodge,
            contentWidthDip: 600.0,
            foregroundWindowBoundsDip: WindowRect.Empty,
            isWindowFullscreen: true,
            hideInFullscreen: false);

        Assert.False(result.IsHidden);
        Assert.Equal(1.0, result.Opacity);
        Assert.Equal(0.0, result.TranslationY);
    }

    [Fact]
    public void FullWidth_WhenDodgeAndMaximized_SlidesOffscreen()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FullWidth,
            proximityMode: DockProximityMode.Dodge,
            contentWidthDip: 600.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowMaximized: true,
            isMouseAtBottomEdge: false,
            isMouseOverDock: false);

        Assert.True(result.IsHidden);
        Assert.Equal(0.0, result.Opacity);
        Assert.True(result.TranslationY > 40.0);
        Assert.False(result.IsHandleVisible);
    }

    [Fact]
    public void FullWidth_WhenDodgeAndMaximizedAndMouseAtEdge_SlidesBackUp()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FullWidth,
            proximityMode: DockProximityMode.Dodge,
            contentWidthDip: 600.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowMaximized: true,
            isMouseAtBottomEdge: true,
            isMouseOverDock: false);

        Assert.False(result.IsHidden);
        Assert.Equal(1.0, result.Opacity);
        Assert.Equal(0.0, result.TranslationY);
    }

    [Fact]
    public void FloatingCapsule_WhenFullscreen_HidesDockWithoutHandle()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.Dodge,
            contentWidthDip: 520.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowFullscreen: true,
            hideInFullscreen: true);

        Assert.True(result.IsHidden);
        Assert.Equal(0.0, result.Opacity);
        Assert.True(result.TranslationY > 60.0);
        Assert.False(result.IsHandleVisible);
    }

    [Fact]
    public void FloatingCapsule_WhenMaximizedAndHideInFullscreen_HidesDock()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.Dodge, // Çekil büyütülmüş pencerede gizler (Aşağı yapış gizlemez)
            contentWidthDip: 520.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowMaximized: true,
            hideInFullscreen: true);

        Assert.True(result.IsHidden);
        Assert.Equal(0.0, result.Opacity);
        Assert.True(result.TranslationY > 60.0);
    }

    [Fact]
    public void FloatingCapsule_WhenMaximizedAndHideInFullscreenAndMouseAtEdge_Peeks()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.DockDown,
            contentWidthDip: 520.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowMaximized: true,
            isMouseAtBottomEdge: true,
            hideInFullscreen: true);

        Assert.False(result.IsHidden);
        Assert.Equal(1.0, result.Opacity);
        Assert.Equal(0.0, result.TranslationY);
    }

    [Theory]
    [InlineData(false, 0, 0, 0, 0)]          // pencere yok
    [InlineData(true, 100, 100, 900, 700)]   // uzakta, büyütülmüş
    [InlineData(false, 100, 960, 900, 1075)] // dock bölgesine binmiş
    [InlineData(true, 0, 0, 1920, 1080)]     // tam büyütülmüş
    public void FloatingCapsule_AlwaysFloating_NeverSnapsHidesOrReserves(bool maximized, int l, int t, int r, int b)
    {
        var bounds = l == 0 && t == 0 && r == 0 && b == 0 ? default : new WindowRect(l, t, r, b);
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.AlwaysFloating,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: bounds,
            isWindowMaximized: maximized,
            hideInFullscreen: true);

        Assert.Equal(60.0, result.HeightDip);
        Assert.Equal(1010.0, result.TopDip); // 1080 - 60 - 10
        Assert.Equal(710.0, result.LeftDip);
        Assert.Equal(500.0, result.WidthDip);
        Assert.Equal(18.0, result.CornerRadiusTopLeft);
        Assert.Equal(18.0, result.CornerRadiusBottomRight);
        Assert.True(result.HasUniformRadius);
        Assert.Equal(0.0, result.TranslationY);
        Assert.Equal(1.0, result.Opacity);
        Assert.False(result.IsSnapped);
        Assert.False(result.IsHidden);
        Assert.False(result.IsHandleVisible);
        Assert.Equal(0, result.AppBarReservedHeightDip);
    }

    [Fact]
    public void FloatingCapsule_AlwaysFloating_IsHiddenInRealFullscreen()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.AlwaysFloating,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowMaximized: true,
            isWindowFullscreen: true,
            hideInFullscreen: true);

        Assert.True(result.IsHidden);
        Assert.Equal(0.0, result.Opacity);
        Assert.True(result.TranslationY > 60.0);
    }

    [Fact]
    public void FloatingCapsule_AlwaysFloating_StaysWhenFullscreenHidingIsOff()
    {
        var result = DockPositionResolver.Resolve(
            monitor: Primary1080p,
            dockStyle: DockStyleMode.FloatingCapsule,
            proximityMode: DockProximityMode.AlwaysFloating,
            contentWidthDip: 500.0,
            foregroundWindowBoundsDip: new WindowRect(0, 0, 1920, 1080),
            isWindowMaximized: true,
            isWindowFullscreen: true,
            hideInFullscreen: false);

        Assert.False(result.IsHidden);
        Assert.Equal(1010.0, result.TopDip);
        Assert.False(result.IsSnapped);
    }

    [Fact]
    public void FloatingCapsule_AlwaysFloating_IgnoresMouseAtEdge()
    {
        var withMouse = DockPositionResolver.Resolve(Primary1080p, DockStyleMode.FloatingCapsule, DockProximityMode.AlwaysFloating,
            500.0, new WindowRect(0, 0, 1920, 1080), isWindowMaximized: true, isMouseAtBottomEdge: true, isMouseOverDock: true);
        var without = DockPositionResolver.Resolve(Primary1080p, DockStyleMode.FloatingCapsule, DockProximityMode.AlwaysFloating,
            500.0, new WindowRect(0, 0, 1920, 1080), isWindowMaximized: true);

        Assert.Equal(without, withMouse);
    }
}
