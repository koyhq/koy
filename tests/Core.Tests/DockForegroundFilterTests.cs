using Koy.Core.Dock;
using Koy.Core.Shell;
using Xunit;

namespace Koy.Core.Tests;

public class DockForegroundFilterTests
{
    private const nint Primary = 101;
    private const nint Secondary = 202;

    // Ana ekranda (0–1920) büyütülmüş pencere: Windows kenarlardan 8 px taşırır.
    private static readonly WindowRect MaximizedOnPrimary = new(-8, -8, 1928, 1048);

    [Fact]
    public void Yan_ekrandaki_pencere_bu_dock_icin_bos_sayilir()
    {
        var bounds = DockForegroundFilter.BoundsForDock(MaximizedOnPrimary, Primary, Secondary);
        Assert.True(bounds.IsEmpty);
        Assert.False(DockForegroundFilter.AppliesToDock(Primary, Secondary));
    }

    [Fact]
    public void Kendi_ekranindaki_pencere_oldugu_gibi_verilir()
    {
        Assert.Equal(MaximizedOnPrimary, DockForegroundFilter.BoundsForDock(MaximizedOnPrimary, Primary, Primary));
        Assert.True(DockForegroundFilter.AppliesToDock(Primary, Primary));
    }

    [Fact]
    public void Ekrani_bilinmeyen_pencere_her_dock_a_verilir()
    {
        Assert.Equal(MaximizedOnPrimary, DockForegroundFilter.BoundsForDock(MaximizedOnPrimary, 0, Secondary));
        Assert.True(DockForegroundFilter.AppliesToDock(0, Secondary));
    }

    [Fact]
    public void Tasma_yan_ekrana_bos_sinir_verilince_dock_cakisma_gormez()
    {
        // Yan ekran 1920'den başlıyor: filtre olmadan 1928'lik sağ kenar bu ekranın alt şeridiyle kesişirdi.
        var secondary = new Koy.Core.Display.DisplayMonitor(
            Id: "MONITOR_2",
            Name: "Yan ekran",
            Bounds: new WindowRect(1920, 0, 3456, 864),
            WorkArea: new WindowRect(1920, 0, 3456, 864),
            IsPrimary: false,
            Handle: Secondary);
        var bounds = DockForegroundFilter.BoundsForDock(MaximizedOnPrimary, Primary, Secondary);

        var state = DockPositionResolver.Resolve(
            secondary, DockStyleMode.FloatingCapsule, DockProximityMode.Dodge, 400, bounds);

        Assert.False(state.IsHidden);
        Assert.False(state.IsSnapped);
    }

    [Fact]
    public void ResolveMonitorWindow_AyniMonitorde_OnPlanPenceresiniDondurur()
    {
        var fg = new WindowInfo(999, MaximizedOnPrimary);
        var result = DockForegroundFilter.ResolveMonitorWindow(Primary, fg, Primary, true, null);

        Assert.Equal(MaximizedOnPrimary, result.Bounds);
        Assert.True(result.IsMaximized);
        Assert.Equal(999, result.Handle);
    }

    [Fact]
    public void ResolveMonitorWindow_FarkliMonitorde_KendiMonitorundekiEnUstAdayiDondurur()
    {
        var fg = new WindowInfo(999, MaximizedOnPrimary);
        var candidate1 = new DockCandidateWindow(10, new WindowRect(0, 0, 800, 600), Primary, IsMaximized: false);
        var candidate2 = new DockCandidateWindow(20, new WindowRect(1920, 500, 2920, 1000), Secondary, IsMaximized: true);
        var candidate3 = new DockCandidateWindow(30, new WindowRect(1920, 200, 2500, 700), Secondary, IsMaximized: false);

        var result = DockForegroundFilter.ResolveMonitorWindow(
            Secondary, fg, Primary, true, [candidate1, candidate2, candidate3]);

        Assert.Equal(candidate2.Bounds, result.Bounds);
        Assert.True(result.IsMaximized);
        Assert.Equal(20, result.Handle);
    }

    [Fact]
    public void ResolveMonitorWindow_KucultulmusAdaylarAtlanir()
    {
        var fg = new WindowInfo(999, MaximizedOnPrimary);
        var minimized = new DockCandidateWindow(20, new WindowRect(1920, 0, 2500, 500), Secondary, IsMaximized: false, IsMinimized: true);
        var visible = new DockCandidateWindow(30, new WindowRect(1920, 500, 2920, 1000), Secondary, IsMaximized: false, IsMinimized: false);

        var result = DockForegroundFilter.ResolveMonitorWindow(
            Secondary, fg, Primary, true, [minimized, visible]);

        Assert.Equal(visible.Bounds, result.Bounds);
        Assert.False(result.IsMaximized);
        Assert.Equal(30, result.Handle);
    }

    [Fact]
    public void ResolveMonitorWindow_KendiMonitorundeAdayYoksa_BosDoner()
    {
        var fg = new WindowInfo(999, MaximizedOnPrimary);
        var candidateOnPrimary = new DockCandidateWindow(10, new WindowRect(0, 0, 800, 600), Primary, IsMaximized: false);

        var result = DockForegroundFilter.ResolveMonitorWindow(
            Secondary, fg, Primary, true, [candidateOnPrimary]);

        Assert.Equal(WindowRect.Empty, result.Bounds);
        Assert.False(result.IsMaximized);
        Assert.Equal(0, result.Handle);
    }

    [Fact]
    public void ResolveMonitorWindow_SinirlariBosAdaylarAtlanir()
    {
        var fg = new WindowInfo(999, MaximizedOnPrimary);
        var emptyWindow = new DockCandidateWindow(20, WindowRect.Empty, Secondary, IsMaximized: false, IsMinimized: false);
        var zeroSizeWindow = new DockCandidateWindow(25, new WindowRect(100, 100, 100, 100), Secondary, IsMaximized: false, IsMinimized: false);
        var validWindow = new DockCandidateWindow(30, new WindowRect(1920, 500, 2920, 1000), Secondary, IsMaximized: false, IsMinimized: false);

        var result = DockForegroundFilter.ResolveMonitorWindow(
            Secondary, fg, Primary, true, [emptyWindow, zeroSizeWindow, validWindow]);

        Assert.Equal(validWindow.Bounds, result.Bounds);
        Assert.False(result.IsMaximized);
        Assert.Equal(30, result.Handle);
    }
}
