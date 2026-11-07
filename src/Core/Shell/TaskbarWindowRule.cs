namespace Koy.Core.Shell;

/// <summary>Bir üst düzey pencerenin görev çubuğuna girip girmeyeceğini belirleyen olgular (Win32'den okunur).</summary>
/// <param name="IsVisible">WS_VISIBLE (IsWindowVisible).</param>
/// <param name="IsCloaked">DWM gizlemesi (başka sanal masaüstü, askıdaki Mağaza uygulaması).</param>
/// <param name="IsShellWindow">Masaüstü, Progman, WorkerW, görev çubuğu penceresi.</param>
/// <param name="HasOwner">GW_OWNER boş değil (diyalog, alt pencere).</param>
/// <param name="IsAppWindow">WS_EX_APPWINDOW.</param>
/// <param name="IsToolWindow">WS_EX_TOOLWINDOW.</param>
/// <param name="IsNoActivate">WS_EX_NOACTIVATE.</param>
public readonly record struct TaskbarWindowFacts(
    bool IsVisible,
    bool IsCloaked,
    bool IsShellWindow,
    bool HasOwner,
    bool IsAppWindow,
    bool IsToolWindow,
    bool IsNoActivate);

/// <summary>
/// Windows görev çubuğunun kuralı: görünür ve gizlenmemiş pencere, WS_EX_APPWINDOW ise her zaman girer; değilse
/// sahipsiz olmalı ve araç penceresi ya da etkinleşmeyen pencere olmamalı. Başlığın boş olması tek başına eleme
/// sebebi değildir (başlığı sonradan konan pencereler eskiden listeden düşüyordu).
/// </summary>
public static class TaskbarWindowRule
{
    public static bool ShouldShow(in TaskbarWindowFacts facts)
    {
        if (!facts.IsVisible || facts.IsCloaked || facts.IsShellWindow)
            return false;

        if (facts.IsAppWindow)
            return true;

        return !facts.HasOwner && !facts.IsToolWindow && !facts.IsNoActivate;
    }
}
