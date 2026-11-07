using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Dock;

public class AppBarOwnershipTests
{
    [Theory]
    [InlineData(true, true, false, true)]     // gizlendi
    [InlineData(true, false, false, false)]   // ayar açık ama henüz gizlenmedi (kayıt anı): Gezgin hâlâ sayılır
    [InlineData(true, true, true, false)]     // elle geri getirildi
    [InlineData(false, true, false, false)]   // ayar kapandı
    [InlineData(false, false, false, false)]
    public void Ownership_follows_the_real_hidden_state(bool setting, bool hidden, bool manual, bool expected)
        => Assert.Equal(expected, AppBarOwnership.OwnsBottomEdge(setting, hidden, manual));

    [Fact]
    public void Manual_restore_then_rehide_gives_the_edge_back()
    {
        var owns = AppBarOwnership.OwnsBottomEdge(true, true, false);
        var afterManualRestore = AppBarOwnership.OwnsBottomEdge(true, true, true);
        var afterRehide = AppBarOwnership.OwnsBottomEdge(true, true, false);

        Assert.True(owns);
        Assert.False(afterManualRestore);
        Assert.True(afterRehide);
    }

    private static readonly WindowRect Monitor = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(1080, 46, true)]    // hiçbir şey ayrılmamış
    [InlineData(1034, 46, false)]   // tam pay
    [InlineData(1010, 46, false)]   // daha fazlası ayrılmış (başka çubuk)
    [InlineData(1032, 48, false)]
    [InlineData(1080, 48, true)]
    public void Reassert_only_when_work_area_is_missing_the_reservation(int workBottom, int heightPx, bool expected)
        => Assert.Equal(expected, AppBarOwnership.NeedsReassert(Monitor, new WindowRect(0, 0, 1920, workBottom), heightPx));
}
