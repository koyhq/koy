using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Dock;

public class AppBarPlacementCacheTests
{
    private static readonly WindowRect Rect = new(0, 1034, 1920, 1080);

    [Fact]
    public void First_registration_always_sends_setpos_at_the_monitor_bottom()
    {
        var cache = new AppBarPlacementCache();

        Assert.True(cache.ShouldSend(1, Rect, ownsBottomEdge: true, force: false));

        // Gönderilecek alan alt kenara yaslı ve istenen yükseklikte (kayıt anında Gezgin itmesi olsa bile).
        var final = AppBarMath.FinalRect(new WindowRect(0, 0, 1920, 1080), 46, new WindowRect(0, 986, 1920, 1032), ownsBottomEdge: true);
        Assert.Equal(Rect, final);
    }

    [Fact]
    public void Same_rect_is_skipped_only_while_we_own_the_edge()
    {
        var cache = new AppBarPlacementCache();
        cache.Record(1, Rect);

        Assert.False(cache.ShouldSend(1, Rect, ownsBottomEdge: true, force: false));
        Assert.True(cache.ShouldSend(1, Rect, ownsBottomEdge: false, force: false));
        Assert.True(cache.ShouldSend(1, Rect, ownsBottomEdge: true, force: true));
    }

    [Fact]
    public void Changed_rect_or_cleared_cache_sends_again()
    {
        var cache = new AppBarPlacementCache();
        cache.Record(1, Rect);

        Assert.True(cache.ShouldSend(1, Rect with { Top = 1032 }, true, false));

        cache.Clear();   // sahiplik değişti
        Assert.True(cache.ShouldSend(1, Rect, true, false));
    }

    [Fact]
    public void Other_windows_are_independent()
    {
        var cache = new AppBarPlacementCache();
        cache.Record(1, Rect);

        Assert.True(cache.ShouldSend(2, Rect, true, false));
        cache.Forget(1);
        Assert.True(cache.ShouldSend(1, Rect, true, false));
    }
}
