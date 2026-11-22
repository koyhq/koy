using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Dock;

public class AppBarMathTests
{
    private static readonly WindowRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Owned_edge_ignores_the_upward_push_of_the_explorer_taskbar()
    {
        // QUERYPOS Gezgin'in 48 px'ini sayıp alt sınırı 1032'ye çekti: 46 px'lik pay 986..1032 olurdu (94 px toplam).
        var pushed = new WindowRect(0, 986, 1920, 1032);

        var r = AppBarMath.FinalRect(Monitor, 46, pushed, ownsBottomEdge: true);

        Assert.Equal(new WindowRect(0, 1034, 1920, 1080), r);
    }

    [Theory]
    [InlineData(46, 1034)]
    [InlineData(48, 1032)]
    [InlineData(60, 1020)]
    public void Owned_edge_always_hugs_the_monitor_bottom(int height, int expectedTop)
    {
        var r = AppBarMath.FinalRect(Monitor, height, new WindowRect(0, 0, 1920, 900), ownsBottomEdge: true);

        Assert.Equal(1080, r.Bottom);
        Assert.Equal(expectedTop, r.Top);
        Assert.Equal(0, r.Left);
        Assert.Equal(1920, r.Right);
    }

    [Fact]
    public void Visible_taskbar_stacks_above_what_windows_proposed()
    {
        var queried = new WindowRect(0, 0, 1920, 1032);

        var r = AppBarMath.FinalRect(Monitor, 46, queried, ownsBottomEdge: false);

        Assert.Equal(new WindowRect(0, 986, 1920, 1032), r);
    }

    [Fact]
    public void Secondary_monitor_uses_its_own_bounds()
    {
        var second = new WindowRect(1920, -200, 3840, 880);

        var r = AppBarMath.FinalRect(second, 48, new WindowRect(1920, 0, 3840, 832), ownsBottomEdge: true);

        Assert.Equal(new WindowRect(1920, 832, 3840, 880), r);
    }

    [Fact]
    public void Top_edge_allocates_from_queried_top()
    {
        var queried = new WindowRect(0, 0, 1920, 1080);
        var r = AppBarMath.FinalRect(DockEdge.Top, Monitor, 48, queried, ownsBottomEdge: false);
        Assert.Equal(new WindowRect(0, 0, 1920, 48), r);
    }

    [Fact]
    public void Left_edge_allocates_from_queried_left()
    {
        var queried = new WindowRect(0, 0, 1920, 1080);
        var r = AppBarMath.FinalRect(DockEdge.Left, Monitor, 48, queried, ownsBottomEdge: false);
        Assert.Equal(new WindowRect(0, 0, 48, 1080), r);
    }

    [Fact]
    public void Right_edge_allocates_from_queried_right()
    {
        var queried = new WindowRect(0, 0, 1920, 1080);
        var r = AppBarMath.FinalRect(DockEdge.Right, Monitor, 48, queried, ownsBottomEdge: false);
        Assert.Equal(new WindowRect(1920 - 48, 0, 1920, 1080), r);
    }

    [Fact]
    public void NeedsReassert_WorksOnAllEdges()
    {
        // Top edge: expected work.Top is 48. If work.Top < 48 (e.g. 0), needs reassert
        Assert.True(AppBarOwnership.NeedsReassert(DockEdge.Top, Monitor, new WindowRect(0, 0, 1920, 1080), 48, false));
        Assert.False(AppBarOwnership.NeedsReassert(DockEdge.Top, Monitor, new WindowRect(0, 48, 1920, 1080), 48, false));

        // Left edge: expected work.Left is 48. If work.Left < 48 (e.g. 0), needs reassert
        Assert.True(AppBarOwnership.NeedsReassert(DockEdge.Left, Monitor, new WindowRect(0, 0, 1920, 1080), 48, false));
        Assert.False(AppBarOwnership.NeedsReassert(DockEdge.Left, Monitor, new WindowRect(48, 0, 1920, 1080), 48, false));

        // Right edge: expected work.Right is 1920 - 48 = 1872. If work.Right > 1872 (e.g. 1920), needs reassert
        Assert.True(AppBarOwnership.NeedsReassert(DockEdge.Right, Monitor, new WindowRect(0, 0, 1920, 1080), 48, false));
        Assert.False(AppBarOwnership.NeedsReassert(DockEdge.Right, Monitor, new WindowRect(0, 0, 1872, 1080), 48, false));
    }

    // ---- Piksel kuralı: ayrılan pay = pencerenin fiziksel kalınlığı + kenar boşluğu (aynı yuvarlama) ----

    [Theory]
    [InlineData(1.00, 46.0, 46)]
    [InlineData(1.25, 46.0, 58)]    // 57,5 -> 58 (yarım yukarı)
    [InlineData(1.50, 46.0, 69)]
    [InlineData(1.75, 46.0, 81)]    // 80,5 -> 81
    [InlineData(1.00, 48.0, 48)]
    [InlineData(1.25, 48.0, 60)]
    [InlineData(1.50, 48.0, 72)]
    [InlineData(1.75, 48.0, 84)]
    public void ReservedPixels_without_offset_rounds_half_up(double scale, double thicknessDip, int expected) =>
        Assert.Equal(expected, AppBarMath.ReservedPixels(thicknessDip, 0, scale));

    [Theory]
    [InlineData(1.00)]
    [InlineData(1.25)]
    [InlineData(1.50)]
    [InlineData(1.75)]
    public void Reserved_pixels_equal_the_dock_window_height_plus_offset_for_both_styles(double scale)
    {
        var monitor = new DisplayMonitor("m", "m", Monitor, Monitor, true, scale, scale);
        foreach (var offset in new[] { 0.0, 3.0, 10.0, 17.0 })
        {
            // Tam genişlik (48 DIP) ve Yer ayır kapsülü (46 DIP): pencere kenardan offset px içeride, kalınlığı thickness px.
            foreach (var thickness in new[] { DockPositionResolver.FullWidthHeightDip, DockPositionResolver.SnappedHeightDip })
            {
                var bar = WindowPlacementMath.EdgeBar(monitor, DockEdge.Bottom, thickness, 100, offset);
                var capsule = WindowPlacementMath.EdgeCapsule(monitor, DockEdge.Bottom, 400, thickness, offset);
                var reserved = AppBarMath.ReservedPixels(thickness, offset, scale);

                // Ayrılan alanın üst sınırı = pencerenin üst kenarı: büyütülmüş pencere dock'a tam dayanır, boşluk ya da çakışma yok.
                Assert.Equal(Monitor.Bottom - reserved, bar.Top);
                Assert.Equal(Monitor.Bottom - reserved, capsule.Top);
            }
        }
    }

    [Fact]
    public void ReservedPixels_ignores_invalid_offset()
    {
        Assert.Equal(60, AppBarMath.ReservedPixels(48, -20, 1.25));
        Assert.Equal(60, AppBarMath.ReservedPixels(48, double.NaN, 1.25));
        Assert.Equal(60 + 120, AppBarMath.ReservedPixels(48, 5000, 1.25));   // ofset üst sınıra (96 DIP = 120 px) kenetlenir
    }

    // ---- Başlangıç durumları ve kenarlar: görev çubuğu görünür / gizlenecek ----

    [Theory]
    [InlineData(1.00, DockEdge.Bottom)]
    [InlineData(1.25, DockEdge.Bottom)]
    [InlineData(1.50, DockEdge.Bottom)]
    [InlineData(1.75, DockEdge.Bottom)]
    [InlineData(1.25, DockEdge.Left)]
    [InlineData(1.25, DockEdge.Right)]
    [InlineData(1.25, DockEdge.Top)]
    public void Taskbar_about_to_hide_registers_from_the_monitor_edge_not_on_top_of_explorer(double scale, DockEdge edge)
    {
        // Açılış: Gezgin'in 48 DIP'lik çubuğu hâlâ görünür; ABM_QUERYPOS alanı onun üstüne itti ya da kısalttı.
        var explorer = AppBarMath.ReservedPixels(48, 0, scale);
        var queried = new WindowRect(0, 0, 1920, 1080 - explorer);
        var px = AppBarMath.ReservedPixels(46, 0, scale);

        var owns = AppBarOwnership.OwnsBottomEdgeAtStart(hideTaskbarSetting: true, explorerTaskbarReady: true, restoredManually: false);
        var rect = AppBarMath.FinalRect(edge, Monitor, px, queried, owns);

        switch (edge)
        {
            case DockEdge.Bottom:
                Assert.Equal(new WindowRect(0, 1080 - px, 1920, 1080), rect);   // 118 px'lik toplam (60 + 58) değil
                break;
            case DockEdge.Top:
                Assert.Equal(new WindowRect(0, 0, 1920, px), rect);
                break;
            case DockEdge.Left:
                Assert.Equal(new WindowRect(0, 0, px, 1080), rect);             // Gezgin'in 60 px'i kadar kısalmaz
                break;
            default:
                Assert.Equal(new WindowRect(1920 - px, 0, 1920, 1080), rect);
                break;
        }
    }

    [Theory]
    [InlineData(true, true, false, true)]     // ayar açık, Gezgin hazır: baştan sahip
    [InlineData(true, false, false, false)]   // Gezgin çubuğu yok: gizlenemez, sahip değil
    [InlineData(false, true, false, false)]   // ayar kapalı: dokunulmaz
    [InlineData(true, true, true, false)]     // kullanıcı elle geri getirdi
    public void OwnsBottomEdgeAtStart_follows_setting_readiness_and_manual_restore(bool setting, bool ready, bool restored, bool expected) =>
        Assert.Equal(expected, AppBarOwnership.OwnsBottomEdgeAtStart(setting, ready, restored));

    [Fact]
    public void Failed_hide_falls_back_to_stacking_above_the_visible_taskbar()
    {
        // Baştan sahip varsayıldı ama Hide başarısız: OwnsBottomEdge(taskbarHidden: false) false -> alan görünür çubuğun üstüne yığılır.
        var owns = AppBarOwnership.OwnsBottomEdge(hideTaskbarSetting: true, taskbarHidden: false, restoredManually: false);
        var queried = new WindowRect(0, 0, 1920, 1020);
        var rect = AppBarMath.FinalRect(DockEdge.Bottom, Monitor, 58, queried, owns);
        Assert.Equal(new WindowRect(0, 962, 1920, 1020), rect);
    }

    [Fact]
    public void Visible_taskbar_does_not_stay_registered_as_owner_when_setting_is_off()
    {
        var rect = AppBarMath.FinalRect(DockEdge.Bottom, Monitor, 58, new WindowRect(0, 0, 1920, 1020),
            AppBarOwnership.OwnsBottomEdgeAtStart(false, true, false));
        Assert.Equal(1020, rect.Bottom);
    }

    [Fact]
    public void Owned_start_pay_is_accepted_by_reassert_check_when_work_area_matches()
    {
        // Gezgin çubuğu gizlenip çalışma alanı 1080 - 58'e oturunca ek SETPOS gerekmez; 94 px'te (fazla ayrılmış) gerekir.
        Assert.False(AppBarOwnership.NeedsReassert(Monitor, new WindowRect(0, 0, 1920, 1022), 58, ownsBottomEdge: true));
        Assert.True(AppBarOwnership.NeedsReassert(Monitor, new WindowRect(0, 0, 1920, 962), 58, ownsBottomEdge: true));
    }
}
