using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class DockDropZoneTests
{
    [Theory]
    [InlineData(40.0, 5.0, DockDropZone.Before)]      // 5 < 10 (sol %25)
    [InlineData(40.0, 9.9, DockDropZone.Before)]      // < 10
    [InlineData(40.0, 10.0, DockDropZone.Merge)]      // tam %25 sınır
    [InlineData(40.0, 20.0, DockDropZone.Merge)]      // orta
    [InlineData(40.0, 30.0, DockDropZone.Merge)]      // tam %75 sınır
    [InlineData(40.0, 30.1, DockDropZone.After)]      // > 30 (sağ %25)
    [InlineData(40.0, 39.0, DockDropZone.After)]      // sağ kenar
    public void DetectZone_VarsayilanYuzde50OrtaBolgeyiHesaplar(double width, double relativeX, DockDropZone expected)
    {
        var zone = DockDropZones.DetectZone(width, relativeX, mergeRatio: 0.5);
        Assert.Equal(expected, zone);
    }

    [Fact]
    public void DetectZone_SifirVeyaNegatifGenislikteMergeDoner()
    {
        Assert.Equal(DockDropZone.Merge, DockDropZones.DetectZone(0, 10));
        Assert.Equal(DockDropZone.Merge, DockDropZones.DetectZone(-10, 5));
    }

    [Fact]
    public void ResolveZone_MutlakKoordinatlariDogruBagilKonumaCevirir()
    {
        // Yuva 100..140 arası, imleç 105 (bağıl 5 -> Before)
        Assert.Equal(DockDropZone.Before, DockDropZones.ResolveZone(cursorX: 105, slotLeft: 100, slotWidth: 40));

        // İmleç 120 (bağıl 20 -> Merge)
        Assert.Equal(DockDropZone.Merge, DockDropZones.ResolveZone(cursorX: 120, slotLeft: 100, slotWidth: 40));

        // İmleç 135 (bağıl 35 -> After)
        Assert.Equal(DockDropZone.After, DockDropZones.ResolveZone(cursorX: 135, slotLeft: 100, slotWidth: 40));
    }

    [Fact]
    public void Tracker_400msAltindaSilahlanmaz()
    {
        var tracker = new DockDropZoneTracker();
        var t0 = TimeSpan.FromSeconds(10);

        // İlk giriş: timer başlar, henüz silahlanmadı
        Assert.False(tracker.Update(targetSlot: 1, DockDropZone.Merge, t0));
        Assert.False(tracker.IsMergeArmed);

        // 200 ms sonra: hala silahlanmadı
        Assert.False(tracker.Update(targetSlot: 1, DockDropZone.Merge, t0 + TimeSpan.FromMilliseconds(200)));
        Assert.False(tracker.IsMergeArmed);

        // 399 ms sonra: hala silahlanmadı
        Assert.False(tracker.Update(targetSlot: 1, DockDropZone.Merge, t0 + TimeSpan.FromMilliseconds(399)));
        Assert.False(tracker.IsMergeArmed);
    }

    [Fact]
    public void Tracker_400msTamamlanincaSilahlanir()
    {
        var tracker = new DockDropZoneTracker();
        var t0 = TimeSpan.FromSeconds(10);

        tracker.Update(targetSlot: 2, DockDropZone.Merge, t0);

        // 400 ms doldu
        var armed = tracker.Update(targetSlot: 2, DockDropZone.Merge, t0 + TimeSpan.FromMilliseconds(400));
        Assert.True(armed);
        Assert.True(tracker.IsMergeArmed);

        // Sonrasında merge'de kaldığı sürece armed true kalır
        Assert.True(tracker.Update(targetSlot: 2, DockDropZone.Merge, t0 + TimeSpan.FromMilliseconds(600)));
        Assert.True(tracker.IsMergeArmed);
    }

    [Fact]
    public void Tracker_KenaraKayincaVeyaCikincaSilahlanmaSifirlanir()
    {
        var tracker = new DockDropZoneTracker();
        var t0 = TimeSpan.FromSeconds(10);

        tracker.Update(targetSlot: 3, DockDropZone.Merge, t0);
        tracker.Update(targetSlot: 3, DockDropZone.Merge, t0 + TimeSpan.FromMilliseconds(400));
        Assert.True(tracker.IsMergeArmed);

        // Kenara (Before) kaydı -> hemen sıfırlanmalı
        var armed = tracker.Update(targetSlot: 3, DockDropZone.Before, t0 + TimeSpan.FromMilliseconds(450));
        Assert.False(armed);
        Assert.False(tracker.IsMergeArmed);
    }

    [Fact]
    public void Tracker_HedefYuvaDegisinceBeklemeSuresiBastanBaslar()
    {
        var tracker = new DockDropZoneTracker();
        var t0 = TimeSpan.FromSeconds(10);

        tracker.Update(targetSlot: 1, DockDropZone.Merge, t0);
        tracker.Update(targetSlot: 1, DockDropZone.Merge, t0 + TimeSpan.FromMilliseconds(300));

        // 350. ms'de başka bir yuvaya geçti (yuva 2)
        var t1 = t0 + TimeSpan.FromMilliseconds(350);
        Assert.False(tracker.Update(targetSlot: 2, DockDropZone.Merge, t1));

        // t1'den 100 ms sonra (toplam 450 ms geçti ama yeni yuvada yalnız 100 ms) -> silahlanmamalı
        Assert.False(tracker.Update(targetSlot: 2, DockDropZone.Merge, t1 + TimeSpan.FromMilliseconds(100)));
        Assert.False(tracker.IsMergeArmed);

        // Yeni yuvada 400 ms dolunca silahlanmalı
        Assert.True(tracker.Update(targetSlot: 2, DockDropZone.Merge, t1 + TimeSpan.FromMilliseconds(400)));
        Assert.True(tracker.IsMergeArmed);
    }

    [Fact]
    public void Tracker_AnahtarMetinTabanliTakip()
    {
        var tracker = new DockDropZoneTracker();
        var t0 = TimeSpan.FromSeconds(5);

        Assert.False(tracker.Update("itemA", DockDropZone.Merge, t0));
        Assert.True(tracker.Update("itemA", DockDropZone.Merge, t0 + TimeSpan.FromMilliseconds(450)));
        Assert.Equal("itemA", tracker.TargetKey);

        tracker.Reset();
        Assert.False(tracker.IsMergeArmed);
        Assert.Null(tracker.TargetKey);
    }
}
