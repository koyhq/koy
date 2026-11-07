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
    /// Monitörün çalışma alanı AppBar'ın istediği payı karşılamıyorsa true (Gezgin çalışma alanını yeniden hesaplayıp
    /// payı düşürmüş). Karşılanıyorsa yeniden iddia gereksizdir; bildirim fırtınasında kendi SETPOS'umuzla döngüye girmeyi önler.
    /// </summary>
    public static bool NeedsReassert(WindowRect monitor, WindowRect work, int heightPx) =>
        work.Bottom > monitor.Bottom - heightPx;
}
