using Koy.Core.Dock;

namespace Koy.Core.Tests;

public class DockDragPlanTests
{
    private const double Slot = 50;
    private static readonly Func<int, bool> All = _ => true;
    private static readonly double[] Widths = [Slot, Slot, Slot, Slot, Slot];

    // 5 yuva (0..4), simge 1'den sürükleniyor.
    private static DockDragPlan Plan(double offset, Func<int, bool>? merge = null, int original = 1) =>
        DockReorderMath.Evaluate(original, offset, Widths, 0, 4, merge ?? All);

    [Theory]
    [InlineData(0.0)]
    [InlineData(20.0)]
    public void Kendi_yuvasinda_kayma_yok(double offset)
    {
        var p = Plan(offset);
        Assert.Equal(1, p.HoveredIndex);
        Assert.Equal(1, p.ShiftTarget);
        Assert.False(p.CanMerge);
    }

    [Fact]
    public void Saga_giderken_hedefin_sol_kenari_ve_orta_bandi_komsuyu_kaydirmaz()
    {
        // Eski davranış 0,5 yuvada (25 px) komşuyu kaydırıyordu; imleç 2. yuvanın sol kenarına (Before) girer.
        var left = Plan(0.6 * Slot);     // merkez 1,6 -> yuva 2, kesir -0,4 -> Before
        Assert.Equal(2, left.HoveredIndex);
        Assert.Equal(DockDropZone.Before, left.Zone);
        Assert.Equal(1, left.ShiftTarget);
        Assert.Empty(DockReorderMath.ShiftedIndices(1, left.ShiftTarget));
        Assert.False(left.CanMerge);

        var mid = Plan(1.0 * Slot);      // tam yuvanın ortası -> Merge
        Assert.Equal(2, mid.HoveredIndex);
        Assert.Equal(DockDropZone.Merge, mid.Zone);
        Assert.True(mid.CanMerge);
        Assert.Equal(1, mid.ShiftTarget);

        var bandEdge = Plan(1.24 * Slot);
        Assert.True(bandEdge.CanMerge);
        Assert.Equal(1, bandEdge.ShiftTarget);
    }

    [Fact]
    public void Saga_giderken_komsu_yalniz_sag_kenar_bolgesinde_kayar()
    {
        var after = Plan(1.3 * Slot);    // merkez 2,3 -> kesir +0,3 -> After
        Assert.Equal(DockDropZone.After, after.Zone);
        Assert.False(after.CanMerge);
        Assert.Equal(2, after.ShiftTarget);
        Assert.Equal([2], DockReorderMath.ShiftedIndices(1, after.ShiftTarget));
    }

    [Fact]
    public void Sola_giderken_ayna_gorunum()
    {
        // Simge 3'ten sola; yuva 2'nin sağ kenarı (After) kaydırmaz, orta bant birleştirir, sol kenar (Before) kaydırır.
        var right = Plan(-0.6 * Slot, original: 3);    // merkez 2,4 -> yuva 2, kesir +0,4 After
        Assert.Equal(2, right.HoveredIndex);
        Assert.Equal(3, right.ShiftTarget);
        var mid = Plan(-1.0 * Slot, original: 3);
        Assert.True(mid.CanMerge);
        Assert.Equal(3, mid.ShiftTarget);
        var leftEdge = Plan(-1.3 * Slot, original: 3); // merkez 1,7 -> yuva 2, kesir -0,3 Before
        Assert.Equal(DockDropZone.Before, leftEdge.Zone);
        Assert.Equal(2, leftEdge.ShiftTarget);
        Assert.Equal([2], DockReorderMath.ShiftedIndices(3, leftEdge.ShiftTarget));
    }

    [Fact]
    public void Birlestirilemeyen_yuvada_eski_davranis_yuvaya_girince_kayar()
    {
        var p = Plan(0.6 * Slot, merge: i => i != 2);   // yuva 2 ek öğe gibi: hedef 2
        Assert.Equal(2, p.HoveredIndex);
        Assert.Equal(2, p.ShiftTarget);
        Assert.False(p.CanMerge);
    }

    [Fact]
    public void Sinirlar_kiskaclanir()
    {
        var p = Plan(10 * Slot);
        Assert.Equal(4, p.HoveredIndex);
        Assert.Equal(4, p.ShiftTarget);
        Assert.Equal(0, Plan(-10 * Slot).HoveredIndex);
    }

    [Fact]
    public void Gecersiz_girdi_yerinde_birakir()
    {
        Assert.Equal(1, DockReorderMath.Evaluate(1, 100, [0, 0, 0, 0, 0], 0, 4, All).ShiftTarget);
        Assert.Equal(1, DockReorderMath.Evaluate(1, 100, Widths, 3, 2, All).ShiftTarget);
    }

    [Fact]
    public void Kayma_kumesi_ShiftFor_ile_uyumlu()
    {
        for (var target = 0; target < 5; target++)
        {
            var moved = DockReorderMath.ShiftedIndices(2, target).ToHashSet();
            for (var i = 0; i < 5; i++)
                Assert.Equal(moved.Contains(i), DockReorderMath.ShiftFor(i, 2, target, Slot) != 0);
        }
    }

    [Fact]
    public void Esit_olmayan_yuvalarda_hedef_gercek_kenarlara_gore_bulunur()
    {
        // Uygulama (50), saat (80), uygulama (50), pil (60), uygulama (50); 0'dan sürükleniyor.
        double[] widths = [50, 80, 50, 60, 50];
        // 0'ın merkezi 25; 2. yuvanın merkezi 50 + 80 + 25 = 155.
        var onApp = DockReorderMath.Evaluate(0, 130, widths, 0, 4, i => i is 0 or 2 or 4);
        Assert.Equal(2, onApp.HoveredIndex);
        Assert.True(onApp.CanMerge);
        Assert.Equal(1, onApp.ShiftTarget);

        // Saatin üstünde (merkez 90): birleştirilemez, yuvaya girince kayar.
        var onClock = DockReorderMath.Evaluate(0, 65, widths, 0, 4, i => i is 0 or 2 or 4);
        Assert.Equal(1, onClock.HoveredIndex);
        Assert.False(onClock.CanMerge);
        Assert.Equal(1, onClock.ShiftTarget);
    }

    [Fact]
    public void Ofset_sinirlari_ilk_ve_son_yuvanin_merkezidir()
    {
        double[] widths = [50, 80, 50];
        var (min, max) = DockReorderMath.OffsetRange(widths, 1, 0, 2);
        Assert.Equal(-65, min);   // 25 - 90
        Assert.Equal(65, max);    // 155 - 90
    }

    [Fact]
    public void Birakinca_ekrandaki_konumlar_korunur()
    {
        // 0 sürüklendi: 2. yuvaya bırakıldı; sürüklenen imleçte (ofset 130), komşular 1 yuva kaymış (-50).
        double[] widths = [50, 80, 50];
        double[] offsets = [130, -50, -50];
        var next = DockReorderMath.CommitOffsets(widths, offsets, 0, 2);
        // Yeni sıra: eski 1, eski 2, eski 0. Yeni sol kenarlar: 0, 80, 130.
        // eski 1: sol 50 - 50 = 0 -> ofset 0; eski 2: sol 130 - 50 = 80 -> yeni sol 80 -> ofset 0; eski 0: 0 + 130 - 130 = 0.
        Assert.Equal([0.0, 0.0, 0.0], next);
    }

    [Fact]
    public void Birakinca_yarida_biraktiysa_ofset_kalir_ve_yay_sifira_ceker()
    {
        double[] widths = [50, 50, 50];
        var next = DockReorderMath.CommitOffsets(widths, [20, 0, 0], 0, 0);
        Assert.Equal([20.0, 0.0, 0.0], next);
    }
}
