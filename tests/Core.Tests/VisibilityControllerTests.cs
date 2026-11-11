using Koy.Core.Shell;
using Koy.Core.Visibility;
using Xunit;

namespace Koy.Core.Tests;

public class VisibilityControllerTests
{
    [Fact]
    public void FixedMode_InitiallyVisibleAndNotClickThrough()
    {
        using var controller = new VisibilityController(VisibilityMode.Fixed);
        Assert.True(controller.IsVisible);
        Assert.False(controller.IsClickThrough);
    }

    [Fact]
    public void FixedMode_HidesWhenFullscreen_AndRestoresWhenFullscreenExits()
    {
        using var controller = new VisibilityController(VisibilityMode.Fixed);
        Assert.True(controller.IsVisible);

        var events = new List<VisibilityResult>();
        controller.Changed += (_, res) => events.Add(res);

        controller.SetFullscreen(true);
        Assert.False(controller.IsVisible);
        Assert.True(controller.IsClickThrough);
        Assert.Single(events);
        Assert.False(events[0].IsVisible);
        Assert.True(events[0].IsClickThrough);

        controller.SetFullscreen(false);
        Assert.True(controller.IsVisible);
        Assert.False(controller.IsClickThrough);
        Assert.Equal(2, events.Count);
        Assert.True(events[1].IsVisible);
    }

    [Fact]
    public void FixedMode_DoesNotHideOnWindowOverlap()
    {
        using var controller = new VisibilityController(VisibilityMode.Fixed);
        controller.SetWindowOverlapping(true);
        Assert.True(controller.IsVisible);
        Assert.False(controller.IsClickThrough);
    }

    [Fact]
    public void SmartMode_InitiallyVisible_HidesWhenWindowOverlaps()
    {
        using var controller = new VisibilityController(VisibilityMode.Smart);
        Assert.True(controller.IsVisible);

        controller.SetWindowOverlapping(true);
        Assert.False(controller.IsVisible);
        Assert.True(controller.IsClickThrough);

        controller.SetWindowOverlapping(false);
        Assert.True(controller.IsVisible);
        Assert.False(controller.IsClickThrough);
    }

    [Fact]
    public void SmartMode_WhenOverlapped_ShowsOnMouseAtEdgeOrMouseOver()
    {
        using var controller = new VisibilityController(VisibilityMode.Smart);
        controller.SetWindowOverlapping(true);
        Assert.False(controller.IsVisible);

        // Edge hover triggers visibility
        controller.SetMouseAtEdge(true);
        Assert.True(controller.IsVisible);

        // Mouse enters notch, leaves edge
        controller.SetMouseOver(true);
        controller.SetMouseAtEdge(false);
        Assert.True(controller.IsVisible);

        // Mouse leaves notch
        controller.SetMouseOver(false);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void SmartMode_WhenOverlapped_FullscreenOverridesMouse()
    {
        using var controller = new VisibilityController(VisibilityMode.Smart);
        controller.SetWindowOverlapping(true);
        controller.SetMouseAtEdge(true);
        Assert.True(controller.IsVisible);

        controller.SetFullscreen(true);
        Assert.False(controller.IsVisible);
        Assert.True(controller.IsClickThrough);

        // Even with mouse over, fullscreen remains hidden
        controller.SetMouseOver(true);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void PeekMode_InitiallyHidden_ShowsOnMouseAtEdgeOrOver()
    {
        using var controller = new VisibilityController(VisibilityMode.Peek);
        Assert.False(controller.IsVisible);
        Assert.True(controller.IsClickThrough);

        controller.SetMouseAtEdge(true);
        Assert.True(controller.IsVisible);

        controller.SetMouseAtEdge(false);
        Assert.False(controller.IsVisible);

        controller.SetMouseOver(true);
        Assert.True(controller.IsVisible);

        controller.SetMouseOver(false);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void Hold_KeepsNotchVisibleAfterPeekTimeoutUntilReleased()
    {
        using var controller = new VisibilityController(VisibilityMode.Peek);
        controller.OnMediaActivity();
        controller.SetHold(true);
        controller.OnMediaPeekTimeout();
        Assert.True(controller.IsVisible);

        controller.SetHold(false);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void Hold_DoesNotOverrideFullscreenHide()
    {
        using var controller = new VisibilityController(VisibilityMode.Smart);
        controller.SetHold(true);
        controller.SetFullscreen(true);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void PeekMode_MediaActivity_PeeksAndTimesOut()
    {
        using var controller = new VisibilityController(VisibilityMode.Peek);
        Assert.False(controller.IsVisible);

        controller.OnMediaActivity();
        Assert.True(controller.IsVisible);
        Assert.True(controller.IsMediaPeekActive);

        // Timeout expires without mouse interaction
        controller.OnMediaPeekTimeout();
        Assert.False(controller.IsVisible);
        Assert.False(controller.IsMediaPeekActive);
    }

    [Fact]
    public void PeekMode_MediaActivity_StaysVisibleIfMouseEntered()
    {
        using var controller = new VisibilityController(VisibilityMode.Peek);
        controller.OnMediaActivity();
        Assert.True(controller.IsVisible);

        // User moves mouse over notch during peek
        controller.SetMouseOver(true);
        controller.OnMediaPeekTimeout();

        // Still visible because mouse is over!
        Assert.True(controller.IsVisible);

        controller.SetMouseOver(false);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void SmartMode_MediaActivity_PeeksEvenWhenOverlapped()
    {
        using var controller = new VisibilityController(VisibilityMode.Smart);
        controller.SetWindowOverlapping(true);
        Assert.False(controller.IsVisible);

        // Media activity triggers temporary peek
        controller.OnMediaActivity();
        Assert.True(controller.IsVisible);

        controller.OnMediaPeekTimeout();
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void ModeSwitching_DynamicallyRecalculatesVisibility()
    {
        using var controller = new VisibilityController(VisibilityMode.Fixed);
        controller.SetWindowOverlapping(true);
        Assert.True(controller.IsVisible);

        // Switching to Smart hides immediately because of overlap
        controller.Mode = VisibilityMode.Smart;
        Assert.False(controller.IsVisible);

        // Switching to Peek stays hidden
        controller.Mode = VisibilityMode.Peek;
        Assert.False(controller.IsVisible);

        // Switching back to Fixed shows immediately
        controller.Mode = VisibilityMode.Fixed;
        Assert.True(controller.IsVisible);
    }

    [Fact]
    public void UpdateForegroundWindow_CalculatesOverlapWithNotchBounds()
    {
        using var controller = new VisibilityController(VisibilityMode.Smart);
        controller.NotchBounds = new WindowRect(800, 0, 1120, 40);
        controller.NotchHandle = 1000;

        // Maximized window covering top of monitor -> overlaps
        controller.UpdateForegroundWindow(2000, new WindowRect(0, 0, 1920, 1080));
        Assert.True(controller.IsWindowOverlapping);
        Assert.False(controller.IsVisible);

        // Window below notch (e.g. Top = 100) -> no overlap
        controller.UpdateForegroundWindow(2001, new WindowRect(200, 100, 1200, 800));
        Assert.False(controller.IsWindowOverlapping);
        Assert.True(controller.IsVisible);

        // Own notch window handle -> ignored, no overlap
        controller.UpdateForegroundWindow(1000, new WindowRect(800, 0, 1120, 40));
        Assert.False(controller.IsWindowOverlapping);
        Assert.True(controller.IsVisible);

        // Zero handle or empty bounds -> no overlap
        controller.UpdateForegroundWindow(0, WindowRect.Empty);
        Assert.False(controller.IsWindowOverlapping);
        Assert.True(controller.IsVisible);
    }

    [Fact]
    public void WindowRect_GeometryMethodsWorkCorrectly()
    {
        var rect = new WindowRect(10, 20, 110, 120);
        Assert.Equal(100, rect.Width);
        Assert.Equal(100, rect.Height);
        Assert.False(rect.IsEmpty);

        Assert.True(rect.Contains(50, 50));
        Assert.False(rect.Contains(5, 50));
        Assert.False(rect.Contains(50, 150));

        var overlapping = new WindowRect(50, 50, 150, 150);
        Assert.True(rect.Intersects(overlapping));

        var disjoint = new WindowRect(200, 200, 300, 300);
        Assert.False(rect.Intersects(disjoint));

        var empty = WindowRect.Empty;
        Assert.Equal(0, empty.Width);
        Assert.Equal(0, empty.Height);
        Assert.True(empty.IsEmpty);
    }

    [Fact]
    public void SmartMode_WhenOverlapped_ExpandedKeepsVisibleDuringMouseLeave()
    {
        using var controller = new VisibilityController(VisibilityMode.Smart);
        controller.SetWindowOverlapping(true);
        Assert.False(controller.IsVisible);

        // Hover & expand
        controller.SetMouseOver(true);
        controller.SetExpanded(true);
        Assert.True(controller.IsVisible);

        // Mouse leaves temporarily within 400ms grace period (IsExpanded remains true)
        controller.SetMouseOver(false);
        Assert.True(controller.IsVisible);

        // Grace period expires / collapsed
        controller.SetExpanded(false);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void PeekMode_ExpandedKeepsVisibleDuringMouseLeave()
    {
        using var controller = new VisibilityController(VisibilityMode.Peek);
        Assert.False(controller.IsVisible);

        controller.SetMouseOver(true);
        controller.SetExpanded(true);
        Assert.True(controller.IsVisible);

        controller.SetMouseOver(false);
        Assert.True(controller.IsVisible);

        controller.SetExpanded(false);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void Fullscreen_OverridesExpandedState()
    {
        using var controller = new VisibilityController(VisibilityMode.Fixed);
        controller.SetExpanded(true);
        Assert.True(controller.IsVisible);

        controller.SetFullscreen(true);
        Assert.False(controller.IsVisible);
    }

    [Fact]
    public void UpdateForegroundWindow_DesktopEmptyBounds_NeverOverlaps()
    {
        using var controller = new VisibilityController(VisibilityMode.Smart);
        controller.NotchBounds = new WindowRect(750, 0, 1170, 140);

        // First an application window overlaps
        controller.UpdateForegroundWindow(1234, new WindowRect(0, 0, 1920, 1080));
        Assert.True(controller.IsWindowOverlapping);
        Assert.False(controller.IsVisible);

        // User clicks desktop / taskbar (bounds reported as Empty)
        controller.UpdateForegroundWindow(5678, WindowRect.Empty);
        Assert.False(controller.IsWindowOverlapping);
        Assert.True(controller.IsVisible);
    }
}
