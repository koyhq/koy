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
    /// Uyuşmazlık bir önceki yeniden iddiadan sonra da aynı kaldıysa (<paramref name="unresolvedWorkBottom"/>, ör. başka bir
    /// uygulamanın çubuğu) tekrar denenmez: kendi SETPOS'umuzla döngüye girilmez.
    /// </summary>
    public static bool NeedsReassert(WindowRect monitor, WindowRect work, int heightPx, bool ownsBottomEdge, int? unresolvedWorkBottom = null)
    {
        var expected = monitor.Bottom - heightPx;
        var mismatch = ownsBottomEdge ? work.Bottom != expected : work.Bottom > expected;
        return mismatch && unresolvedWorkBottom != work.Bottom;
    }
}
