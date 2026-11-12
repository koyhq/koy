using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Tests;

public class EdgeRevealTrackerTests
{
    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void KenardaBeklemedenAcmaz_GecikmeSonraAcar()
    {
        var tracker = new EdgeRevealTracker(Ms(300));

        Assert.False(tracker.Update(true, false, Ms(0)));
        Assert.False(tracker.Update(true, false, Ms(100)));
        Assert.False(tracker.Update(true, false, Ms(299)));
        Assert.True(tracker.Update(true, false, Ms(300)));
        Assert.True(tracker.IsArmed);
    }

    [Fact]
    public void KenardanAyrilincaSifirlanir_YenidenBeklenir()
    {
        var tracker = new EdgeRevealTracker(Ms(300));
        tracker.Update(true, false, Ms(0));
        tracker.Update(true, false, Ms(400));
        Assert.True(tracker.IsArmed);

        Assert.False(tracker.Update(false, false, Ms(450)));
        Assert.False(tracker.IsArmed);

        Assert.False(tracker.Update(true, false, Ms(500)));
        Assert.False(tracker.Update(true, false, Ms(799)));
        Assert.True(tracker.Update(true, false, Ms(800)));
    }

    [Fact]
    public void FareTusuBasiliykenCikmaz_VeSayaciSifirlar()
    {
        var tracker = new EdgeRevealTracker(Ms(300));
        tracker.Update(true, false, Ms(0));

        // Pencere sürüklerken kenara inmek dock'u açmamalı.
        Assert.False(tracker.Update(true, true, Ms(350)));
        Assert.False(tracker.Update(true, true, Ms(900)));

        // Tuş bırakıldıktan sonra bekleme baştan başlar.
        Assert.False(tracker.Update(true, false, Ms(1000)));
        Assert.True(tracker.Update(true, false, Ms(1300)));
    }

    [Fact]
    public void AcikkenTusaBasilirsaKapanir()
    {
        var tracker = new EdgeRevealTracker(Ms(300));
        tracker.Update(true, false, Ms(0));
        tracker.Update(true, false, Ms(300));

        Assert.False(tracker.Update(true, true, Ms(350)));
    }

    [Fact]
    public void Reset_SilahiBosaltir()
    {
        var tracker = new EdgeRevealTracker(Ms(0));
        Assert.True(tracker.Update(true, false, Ms(5)));

        tracker.Reset();

        Assert.False(tracker.IsArmed);
    }

    [Theory]
    [InlineData(500, 1079, 0, 1920, 1080, false, 1.0, true)]
    [InlineData(500, 1076, 0, 1920, 1080, false, 1.0, true)]
    [InlineData(500, 1070, 0, 1920, 1080, false, 1.0, false)]
    [InlineData(500, 1005, 0, 1920, 1080, true, 1.0, true)]
    [InlineData(500, 990, 0, 1920, 1080, true, 1.0, false)]
    [InlineData(500, 970, 0, 1920, 1080, true, 1.5, true)]
    [InlineData(2000, 1079, 0, 1920, 1080, false, 1.0, false)]
    [InlineData(-1, 1079, 0, 1920, 1080, false, 1.0, false)]
    public void IsInZone_MonitorBasinaKenarBolgesi(int x, int y, int left, int right, int bottom, bool revealed, double dpi, bool expected)
    {
        Assert.Equal(expected, EdgeRevealTracker.IsInZone(x, y, left, right, bottom, revealed, dpi));
    }

    [Theory]
    [InlineData(DockEdge.Top, 500, 2, false, true)]
    [InlineData(DockEdge.Top, 500, 10, false, false)]
    [InlineData(DockEdge.Top, 500, 50, true, true)]
    [InlineData(DockEdge.Left, 2, 500, false, true)]
    [InlineData(DockEdge.Left, 10, 500, false, false)]
    [InlineData(DockEdge.Left, 50, 500, true, true)]
    [InlineData(DockEdge.Right, 1918, 500, false, true)]
    [InlineData(DockEdge.Right, 1900, 500, false, false)]
    [InlineData(DockEdge.Right, 1850, 500, true, true)]
    [InlineData(DockEdge.Bottom, 500, 1078, false, true)]
    [InlineData(DockEdge.Bottom, 500, 1050, false, false)]
    [InlineData(DockEdge.Bottom, 500, 1010, true, true)]
    public void IsInZone_FourEdges(DockEdge edge, int x, int y, bool revealed, bool expected)
    {
        var bounds = new WindowRect(0, 0, 1920, 1080);
        Assert.Equal(expected, EdgeRevealTracker.IsInZone(edge, x, y, bounds, revealed, 1.0, 1.0));
    }
}

public class FullscreenRuleStateTests
{
    private static readonly WindowRect Monitor = new(0, 0, 1920, 1080);
    private static readonly WindowRect MaximizedOverflow = new(-8, -8, 1928, 1088);
    private static readonly WindowRect Small = new(100, 100, 900, 700);

    [Theory]
    [InlineData(ShellNotificationState.D3dFullScreen)]
    [InlineData(ShellNotificationState.Presentation)]
    public void OzelKipOyunVeSunumModu_KesinTamEkran(ShellNotificationState state)
    {
        // Geometri ne olursa olsun.
        Assert.True(FullscreenRule.IsFullscreen(Small, Monitor, hasCaption: true, isMaximized: false, state: state));
    }

    [Fact]
    public void WindowsAcikcaTamEkranYokDiyorsa_GeometriYetmez()
    {
        Assert.False(FullscreenRule.IsFullscreen(Monitor, Monitor, hasCaption: false, isMaximized: false, state: ShellNotificationState.Normal));
    }

    [Fact]
    public void MesguPlusBasliksizPlusKaplama_TamEkran()
    {
        Assert.True(FullscreenRule.IsFullscreen(Monitor, Monitor, hasCaption: false, isMaximized: false, state: ShellNotificationState.Busy));
    }

    [Fact]
    public void SessizSaatlerdeMesguDonupGeometriTutmuyorsa_TamEkranDegil()
    {
        // Odaklanma Yardımı "meşgul" derken küçük pencere: tek başına sistem işareti yetmez.
        Assert.False(FullscreenRule.IsFullscreen(Small, Monitor, hasCaption: false, isMaximized: false, state: ShellNotificationState.Busy));
        Assert.False(FullscreenRule.IsFullscreen(Monitor, Monitor, hasCaption: true, isMaximized: false, state: ShellNotificationState.Busy));
    }

    [Fact]
    public void BuyutulmusNormalPencere_MesguOlsaBileTamEkranDegil()
    {
        Assert.False(FullscreenRule.IsFullscreen(MaximizedOverflow, Monitor, hasCaption: false, isMaximized: true, state: ShellNotificationState.Busy));
        Assert.False(FullscreenRule.IsFullscreen(MaximizedOverflow, Monitor, hasCaption: true, isMaximized: true, state: ShellNotificationState.Busy));
    }

    [Fact]
    public void F11TarayiciMonitoreTamOturur_BuyutulmusBayragiylaTamEkran()
    {
        Assert.True(FullscreenRule.IsFullscreen(Monitor, Monitor, hasCaption: false, isMaximized: true, state: ShellNotificationState.Busy));
    }

    [Fact]
    public void BuyutulmusTamOturanPencereyiWindowsOnaylamazsa_TamEkranDegil()
    {
        Assert.False(FullscreenRule.IsFullscreen(Monitor, Monitor, hasCaption: false, isMaximized: true, state: ShellNotificationState.Normal));
    }

    [Fact]
    public void BaskaEkrandakiPencereIcinDurumBilinmez_GeometriKarar()
    {
        Assert.True(FullscreenRule.IsFullscreen(Monitor, Monitor, hasCaption: false, isMaximized: false, state: ShellNotificationState.Unknown));
        Assert.False(FullscreenRule.IsFullscreen(MaximizedOverflow, Monitor, hasCaption: false, isMaximized: true, state: ShellNotificationState.Unknown));
    }

    [Theory]
    [InlineData(2, ShellNotificationState.Busy)]
    [InlineData(7, ShellNotificationState.Busy)]
    [InlineData(3, ShellNotificationState.D3dFullScreen)]
    [InlineData(4, ShellNotificationState.Presentation)]
    [InlineData(5, ShellNotificationState.Normal)]
    [InlineData(1, ShellNotificationState.Unknown)]
    [InlineData(6, ShellNotificationState.Unknown)]
    [InlineData(99, ShellNotificationState.Unknown)]
    public void FromQuns_SayisalDegeriSadelestirir(int quns, ShellNotificationState expected)
    {
        Assert.Equal(expected, ShellNotificationStates.FromQuns(quns));
    }
}

public class DockEdgeRevealResolverTests
{
    private static DisplayMonitor Monitor() => new(
        Id: "m1", Name: "M",
        Bounds: new WindowRect(0, 0, 1920, 1080), WorkArea: new WindowRect(0, 0, 1920, 1080),
        IsPrimary: true, DpiScaleX: 1.0, DpiScaleY: 1.0, Handle: 1);

    [Fact]
    public void BuyutulmusPencere_DockGizli_KenardaAcilir()
    {
        var hidden = DockPositionResolver.Resolve(Monitor(), DockStyleMode.FloatingCapsule, DockProximityMode.Dodge, 500,
            WindowRect.Empty, isWindowMaximized: true, isMouseAtBottomEdge: false, edgeRevealEnabled: true);
        var revealed = DockPositionResolver.Resolve(Monitor(), DockStyleMode.FloatingCapsule, DockProximityMode.Dodge, 500,
            WindowRect.Empty, isWindowMaximized: true, isMouseAtBottomEdge: true, edgeRevealEnabled: true);

        Assert.True(hidden.IsHidden);
        Assert.False(revealed.IsHidden);
    }

    [Fact]
    public void KenardanAcmaKapaliysa_BuyutulmusPencerede_FareKenardaOlsaDaGizliKalir()
    {
        var state = DockPositionResolver.Resolve(Monitor(), DockStyleMode.FloatingCapsule, DockProximityMode.Dodge, 500,
            WindowRect.Empty, isWindowMaximized: true, isMouseAtBottomEdge: true, edgeRevealEnabled: false);

        Assert.True(state.IsHidden);
    }

    [Fact]
    public void Kapsul_AsagiYapis_BuyutulmusPencerede_GizlenmezAltaYapisirYerAyirmaz()
    {
        var state = DockPositionResolver.Resolve(Monitor(), DockStyleMode.FloatingCapsule, DockProximityMode.DockDown, 500,
            WindowRect.Empty, isWindowMaximized: true, isMouseAtBottomEdge: false, edgeRevealEnabled: true);

        Assert.False(state.IsHidden);
        Assert.Equal(1.0, state.Opacity);
        Assert.Equal(0.0, state.TranslationY);
        Assert.True(state.IsSnapped);
        Assert.False(state.IsHandleVisible);
        Assert.Equal(DockPositionResolver.SnappedHeightDip, state.HeightDip);
        Assert.Equal(DockPositionResolver.SnappedCornerRadiusDip, state.CornerRadiusTopLeft);
        Assert.Equal(0.0, state.CornerRadiusBottomLeft);
        Assert.Equal(1080 - DockPositionResolver.SnappedHeightDip, state.TopDip);
        Assert.Equal(0, state.AppBarReservedHeightDip);
    }

    [Fact]
    public void Kapsul_AsagiYapis_TamEkranda_YineGizlenir()
    {
        var state = DockPositionResolver.Resolve(Monitor(), DockStyleMode.FloatingCapsule, DockProximityMode.DockDown, 500,
            WindowRect.Empty, isWindowMaximized: true, isWindowFullscreen: true, hideInFullscreen: true);

        Assert.True(state.IsHidden);
    }

    [Theory]
    [InlineData(DockProximityMode.Dodge)]
    [InlineData(DockProximityMode.AlwaysHidden)]
    public void Kapsul_CekilVeHepGizli_BuyutulmusPencerede_GizliKalir(DockProximityMode mode)
    {
        var state = DockPositionResolver.Resolve(Monitor(), DockStyleMode.FloatingCapsule, mode, 500,
            WindowRect.Empty, isWindowMaximized: true, isMouseAtBottomEdge: false, edgeRevealEnabled: true);

        Assert.True(state.IsHidden);
    }

    [Fact]
    public void TamEkranda_KenardaFareOlsaDaDockCikmaz()
    {
        foreach (var style in new[] { DockStyleMode.FloatingCapsule, DockStyleMode.FullWidth })
        foreach (var mode in Enum.GetValues<DockProximityMode>())
        {
            var state = DockPositionResolver.Resolve(Monitor(), style, mode, 500, WindowRect.Empty,
                isWindowMaximized: true, isMouseAtBottomEdge: true, isMouseOverDock: false,
                isWindowFullscreen: true, hideInFullscreen: true, edgeRevealEnabled: true);

            Assert.True(state.IsHidden, $"{style}/{mode}");
        }
    }

    [Fact]
    public void FullWidth_BuyutulmusPencere_KenardaAcilir()
    {
        var state = DockPositionResolver.Resolve(Monitor(), DockStyleMode.FullWidth, DockProximityMode.DockDown, 500,
            WindowRect.Empty, isWindowMaximized: true, isMouseAtBottomEdge: true, edgeRevealEnabled: true);

        Assert.False(state.IsHidden);
    }
}
