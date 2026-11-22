using Koy.Core.Dock;
using Koy.Core.Media;
using Koy.Core.Notch;

namespace Koy.Core.Tests;

// Paket: müzik sayfası ve çentik maliyeti (ortak kare ızgarası, örtülü zemin, aynı kapak, dock gecikmesi).
public class MusicCostTests
{
    [Theory]
    [InlineData(30, 1)]
    [InlineData(15, 2)]
    [InlineData(10, 3)]
    [InlineData(8, 4)]
    [InlineData(0, FrameGrid.BaseHz)]
    [InlineData(500, 1)]
    public void FrameGrid_divisor_matches_requested_rate(double fps, int divisor) =>
        Assert.Equal(divisor, FrameGrid.DivisorFor(fps));

    [Fact]
    public void FrameGrid_slow_consumer_fires_on_every_third_tick_only()
    {
        var fired = new List<long>();
        long prev = 0;
        for (long tick = 1; tick <= 12; tick++)
        {
            if (FrameGrid.IsDue(prev, tick, 3))
                fired.Add(tick);
            prev = tick;
        }

        Assert.Equal([3L, 6L, 9L, 12L], fired);
    }

    [Fact]
    public void FrameGrid_consumers_share_ticks_so_frames_land_together()
    {
        // 10 fps ve 30 fps aynı vuruşta çizer: ayrı sunum yok.
        for (long tick = 1; tick <= 30; tick++)
        {
            var blobs = FrameGrid.IsDue(tick - 1, tick, FrameGrid.DivisorFor(10));
            var vinyl = FrameGrid.IsDue(tick - 1, tick, FrameGrid.DivisorFor(30));
            if (blobs)
                Assert.True(vinyl);
        }
    }

    [Fact]
    public void FrameGrid_skipped_ticks_fire_once_and_same_tick_does_not()
    {
        Assert.True(FrameGrid.IsDue(1, 8, 3));
        Assert.False(FrameGrid.IsDue(8, 8, 3));
        Assert.False(FrameGrid.IsDue(7, 5, 3));
    }

    [Fact]
    public void FrameGrid_tick_follows_seconds()
    {
        Assert.Equal(0, FrameGrid.TickAt(-1));
        Assert.Equal(30, FrameGrid.TickAt(1.0));
        Assert.Equal(15, FrameGrid.TickAt(0.5));
    }

    [Theory]
    [InlineData(true, false, 255, true)]   // albüm rengi, Katı
    [InlineData(true, false, 230, true)]
    [InlineData(true, false, 219, false)]  // Buzlu: altı kısmen görünür
    [InlineData(true, false, 184, false)]  // Cam
    [InlineData(false, true, 100, true)]   // Kapak resmi stili her zaman opak
    [InlineData(false, false, 255, false)] // Düz kart ya da palet yok
    public void MusicCard_opacity_rules(bool tint, bool backdrop, int alpha, bool expected) =>
        Assert.Equal(expected, MusicCardCoverage.IsOpaque(tint, backdrop, (byte)alpha));

    [Fact]
    public void MusicCard_covers_background_only_while_music_face_shown()
    {
        Assert.True(MusicCardCoverage.CoversNotchBackground(true, true, false, 255));
        Assert.False(MusicCardCoverage.CoversNotchBackground(false, true, false, 255));
    }

    [Theory]
    [InlineData(true, LiveBackgroundSurface.Notch, LiveBackgroundPlaybackMode.StaticFirstFrame)]
    [InlineData(false, LiveBackgroundSurface.Notch, LiveBackgroundPlaybackMode.Animate)]
    public void Decide_covered_notch_backdrop_does_not_animate(bool covered, LiveBackgroundSurface surface, LiveBackgroundPlaybackMode expected) =>
        Assert.Equal(expected,
            LiveBackgroundPlaybackPolicy.Decide(true, true, false, false, LiveBackgroundTarget.Both, surface, isVideo: true, isBackdropCovered: covered));

    [Fact]
    public void Decide_covered_does_not_turn_hidden_notch_on_and_leaves_music_card_alone()
    {
        Assert.Equal(LiveBackgroundPlaybackMode.Off,
            LiveBackgroundPlaybackPolicy.Decide(false, true, false, false, LiveBackgroundTarget.Both, LiveBackgroundSurface.Notch, isBackdropCovered: true));
        Assert.Equal(LiveBackgroundPlaybackMode.Animate,
            LiveBackgroundPlaybackPolicy.Decide(true, true, false, false, LiveBackgroundTarget.MusicCard, LiveBackgroundSurface.MusicCard, isBackdropCovered: true));
    }

    [Fact]
    public void CoverChangeTracker_same_cover_is_not_a_change()
    {
        var tracker = new CoverChangeTracker();
        var cover = new byte[] { 1, 2, 3, 4 };

        Assert.True(tracker.HasChanged(cover));
        Assert.False(tracker.HasChanged(cover));                 // oynat/duraklat: aynı dizi
        Assert.False(tracker.HasChanged([1, 2, 3, 4]));          // aynı albüm: yeni dizi, aynı parmak izi
        Assert.True(tracker.HasChanged([9, 9, 9]));
    }

    [Fact]
    public void CoverChangeTracker_no_cover_and_reset()
    {
        var tracker = new CoverChangeTracker();
        Assert.False(tracker.HasChanged(null));                  // kapak yoktu, hâlâ yok
        Assert.True(tracker.HasChanged([1, 2]));
        Assert.True(tracker.HasChanged(null));                   // kapak gitti
        Assert.True(tracker.HasChanged([1, 2]));                 // geri geldi
        tracker.Reset();
        Assert.True(tracker.HasChanged([1, 2]));                 // çözme başarısızsa yeniden denenir
    }

    [Fact]
    public void Dock_window_event_storm_collapses_to_one_recheck()
    {
        var time = new RefreshDebouncerTests.ManualTimeProvider();
        var count = 0;
        using var debouncer = new RefreshDebouncer(() => count++, DockWindowEventPolicy.RecheckDelay, timeProvider: time);

        for (var i = 0; i < 50; i++)
            debouncer.Request();
        time.FireLast();

        Assert.Equal(1, count);
        Assert.InRange(DockWindowEventPolicy.RecheckDelay.TotalMilliseconds, 50, 100);
    }
}

public class LyricsEmphasisSettledTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(2.9, false)]
    [InlineData(3, true)]
    [InlineData(-7, true)]
    [InlineData(double.NaN, true)]
    public void Far_rows_are_settled(double distance, bool settled) =>
        Assert.Equal(settled, Koy.Core.Lyrics.LyricsEmphasis.IsSettled(distance));

    [Fact]
    public void Settled_rows_have_constant_values()
    {
        Assert.Equal(Koy.Core.Lyrics.LyricsEmphasis.OpacityAt(3), Koy.Core.Lyrics.LyricsEmphasis.OpacityAt(40));
        Assert.Equal(Koy.Core.Lyrics.LyricsEmphasis.ScaleAt(3), Koy.Core.Lyrics.LyricsEmphasis.ScaleAt(40));
    }
}
