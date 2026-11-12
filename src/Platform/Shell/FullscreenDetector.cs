using Koy.Core.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// Tam ekran uygulama algılayıcı.
/// SHQueryUserNotificationState ve ön plan penceresinin monitör sınırlarını kaplama durumunu denetler.
/// </summary>
public sealed class FullscreenDetector : IFullscreenDetector
{
    public nint NotchHandle { get; set; }
    public HashSet<nint> IgnoredHandles { get; } = [];

    public bool IsFullscreen() => IsFullscreen(0);

    /// <summary>
    /// Belirtilen monitörde tam ekran var mı (0: ön plan hangi monitördeyse orada). Odak başka ekrandaki bir
    /// pencerede olsa bile bu monitörün en üstündeki gerçek pencere tam ekransa o ekran tam ekran sayılır;
    /// yan ekranda video tam ekranken ana ekrana tıklamak oradaki Koy'u videonun üstüne çıkarmasın.
    /// </summary>
    public bool IsFullscreen(nint targetMonitorHandle = 0)
    {
        if (IsForegroundFullscreen(targetMonitorHandle))
            return true;

        return targetMonitorHandle != 0 && IsTopWindowFullscreenOn(targetMonitorHandle);
    }

    private unsafe bool IsTopWindowFullscreenOn(nint monitorHandle)
    {
        var ownPid = (uint)Environment.ProcessId;
        var result = false;
        var decided = false;

        // EnumWindows üstten alta (z sırası) gezer; hedef monitördeki ilk gerçek pencere karar verir.
        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd) || IgnoredHandles.Contains(hwnd.Value) || hwnd.Value == NotchHandle)
                return true;

            uint pid = 0;
            if (PInvoke.GetWindowThreadProcessId(hwnd, &pid) == 0 || pid == ownPid)
                return true;

            // Gizlenmiş (cloaked) UWP/başka sanal masaüstü pencereleri görünür sayılmaz.
            int cloaked = 0;
            if (PInvoke.DwmGetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded && cloaked != 0)
                return true;

            var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            if (exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) || exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_NOACTIVATE) ||
                exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TRANSPARENT))
                return true;

            Span<char> buffer = stackalloc char[256];
            fixed (char* ptr = buffer)
            {
                var len = PInvoke.GetClassName(hwnd, ptr, 256);
                if (len > 0)
                {
                    var cn = buffer[..len];
                    if (cn.SequenceEqual("Progman") || cn.SequenceEqual("WorkerW") || cn.SequenceEqual("Shell_TrayWnd") ||
                        cn.SequenceEqual("Shell_SecondaryTrayWnd") || cn.SequenceEqual("ScreenClippingHost"))
                        return true;
                }
            }

            if (!PInvoke.GetWindowRect(hwnd, out var wr))
                return true;

            var mon = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            if (mon.Value != monitorHandle)
                return true;

            var mi = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            if (!PInvoke.GetMonitorInfo(mon, ref mi))
                return true;

            var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            result = FullscreenRule.IsFullscreen(
                new WindowRect(wr.left, wr.top, wr.right, wr.bottom),
                new WindowRect(mi.rcMonitor.left, mi.rcMonitor.top, mi.rcMonitor.right, mi.rcMonitor.bottom),
                hasCaption: style.HasFlag(WINDOW_STYLE.WS_CAPTION),
                isMaximized: style.HasFlag(WINDOW_STYLE.WS_MAXIMIZE));
            decided = true;
            return false; // ilk gerçek pencere karar verdi, taramayı bitir
        }, 0);

        return decided && result;
    }

    private bool IsForegroundFullscreen(nint targetMonitorHandle)
    {
        // Ön plandaki pencereyi al
        var fg = PInvoke.GetForegroundWindow();
        if (fg.IsNull || fg == PInvoke.GetDesktopWindow() || fg == PInvoke.GetShellWindow())
            return false;

        if ((NotchHandle != 0 && fg.Value == NotchHandle) || IgnoredHandles.Contains(fg.Value))
            return false;

        // Masaüstü pencerelerini (Progman, WorkerW) veya görev çubuklarını (birincil / ikincil) yoksay
        unsafe
        {
            Span<char> buffer = stackalloc char[256];
            fixed (char* ptr = buffer)
            {
                var len = PInvoke.GetClassName(fg, ptr, 256);
                if (len > 0)
                {
                    var className = buffer[..len];
                    if (className.SequenceEqual("Progman") ||
                        className.SequenceEqual("WorkerW") ||
                        className.SequenceEqual("Shell_TrayWnd") ||
                        className.SequenceEqual("Shell_SecondaryTrayWnd") ||
                        className.SequenceEqual("ScreenClippingHost"))
                    {
                        return false;
                    }
                }
            }
        }

        if (IsScreenClippingHost(fg))
            return false;

        var fgMon = PInvoke.MonitorFromWindow(fg, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (fgMon.Value == 0)
            return false;

        // Belirli bir monitör sorgulanıyorsa ve ön plan penceresi başka bir monitördeyse
        // bu ekran için tam ekran sayılmaz (AGENTS.md / M6.2: Yan ekrandaki tam ekran burayı gizlemez).
        if (targetMonitorHandle != 0 && fgMon.Value != targetMonitorHandle)
            return false;

        // Üç işaret birleşir (FullscreenRule): Windows'un tam ekran bildirimi (özel kip D3D, sunum modu kesin; QUNS_BUSY
        // tek başına güvenilmez, Sessiz saatlerde de dönebilir, bu yüzden geometriyle birlikte okunur), başlık
        // çubuğunun olmayışı ve monitörü tamamen kaplama. Sorgu başarısızsa durum bilinmiyor sayılır.
        var hr = PInvoke.SHQueryUserNotificationState(out var notificationState);
        var shellState = hr.Succeeded
            ? ShellNotificationStates.FromQuns((int)notificationState)
            : ShellNotificationState.Unknown;

        unsafe
        {
            var mi = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            if (!PInvoke.GetMonitorInfo(fgMon, ref mi))
                return false;

            if (!PInvoke.GetWindowRect(fg, out var wr))
                return false;

            var style = (WINDOW_STYLE)PInvoke.GetWindowLong(fg, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            return FullscreenRule.IsFullscreen(
                new WindowRect(wr.left, wr.top, wr.right, wr.bottom),
                new WindowRect(mi.rcMonitor.left, mi.rcMonitor.top, mi.rcMonitor.right, mi.rcMonitor.bottom),
                hasCaption: style.HasFlag(WINDOW_STYLE.WS_CAPTION),
                isMaximized: style.HasFlag(WINDOW_STYLE.WS_MAXIMIZE),
                state: shellState);
        }
    }

    internal static bool IsScreenClippingHost(HWND hwnd) => IsScreenClippingHost((nint)hwnd.Value);

    public static unsafe bool IsScreenClippingHost(nint handle)
    {
        if (handle == 0) return false;
        var hwnd = new HWND(handle);

        Span<char> buffer = stackalloc char[256];
        fixed (char* ptr = buffer)
        {
            var len = PInvoke.GetClassName(hwnd, ptr, 256);
            if (len > 0)
            {
                var className = buffer[..len];
                if (className.SequenceEqual("ScreenClippingHost"))
                    return true;
            }
        }

        uint pid = 0;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        if (pid != 0)
        {
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                var name = proc.ProcessName;
                if (string.Equals(name, "ScreenClippingHost", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "SnippingTool", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }
}
