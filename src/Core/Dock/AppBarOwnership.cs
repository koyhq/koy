using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// AppBar'ın alt kenarın sahibi olup olmadığı. Tek yerden hesaplanır; görev çubuğunun gizli durumu her değiştiğinde
/// (Hide başarılı, Restore, elle geri getirme, ayar değişimi) yeniden sorulur ve değişimde AppBar yeniden iddia edilir.
/// </summary>
public static class AppBarOwnership
{
    /// <param name="hideTaskbarSetting">"Windows görev çubuğunu gizle" ayarı.</param>
    /// <param name="taskbarHidden">Görev çubuğu şu an gerçekten gizlendi (TaskbarService.Hide çalıştı, Restore edilmedi).</param>
    /// <param name="restoredManually">Kullanıcı görev çubuğunu elle geri getirdi ve dock yeniden gizlemedi.</param>
    public static bool OwnsBottomEdge(bool hideTaskbarSetting, bool taskbarHidden, bool restoredManually) =>
        hideTaskbarSetting && taskbarHidden && !restoredManually;

    /// <summary>
    /// Monitörün çalışma alanı AppBar'ın istediği payla uyuşmuyorsa true. Alt kenar Koy'undayken pay TAM olmalıdır:
    /// eksik ayrılmış (Gezgin sıfırlamış) da fazla ayrılmış (kayıt anında Gezgin'in 48 px'i de sayılmış, 94 px) da düzeltilir.
    /// Alt kenar Koy'un değilken (görev çubuğu görünür, başka çubuklar yığılır) yalnız eksik pay düzeltilir.
    /// Uyuşmazlık bir önceki yeniden iddiadan sonra da aynı kaldıysa (<paramref name="unresolvedEdge"/>, ör. başka bir
    /// uygulamanın çubuğu) tekrar denenmez: kendi SETPOS'umuzla döngüye girilmez.
    /// </summary>
    public static bool NeedsReassert(
        DockEdge edge,
        WindowRect monitor,
        WindowRect work,
        int thicknessPx,
        bool ownsBottomEdge,
        int? unresolvedEdge = null)
    {
        if (!Enum.IsDefined(edge)) edge = DockEdge.Bottom;

        int expected;
        bool mismatch;
        int currentEdge;

        switch (edge)
        {
            case DockEdge.Top:
                expected = monitor.Top + thicknessPx;
                mismatch = work.Top < expected;
                currentEdge = work.Top;
                break;
            case DockEdge.Left:
                expected = monitor.Left + thicknessPx;
                mismatch = work.Left < expected;
                currentEdge = work.Left;
                break;
            case DockEdge.Right:
                expected = monitor.Right - thicknessPx;
                mismatch = work.Right > expected;
                currentEdge = work.Right;
                break;
            case DockEdge.Bottom:
            default:
                expected = monitor.Bottom - thicknessPx;
                mismatch = ownsBottomEdge ? work.Bottom != expected : work.Bottom > expected;
                currentEdge = work.Bottom;
                break;
        }

        return mismatch && unresolvedEdge != currentEdge;
    }

    public static bool NeedsReassert(
        WindowRect monitor,
        WindowRect work,
        int heightPx,
        bool ownsBottomEdge,
        int? unresolvedWorkBottom = null)
        => NeedsReassert(DockEdge.Bottom, monitor, work, heightPx, ownsBottomEdge, unresolvedWorkBottom);
}
