using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// Notch penceresinin Windows stil ayarları. Alt+Tab'dan gizler ve tıklamada odak çalmasını önler.
/// </summary>
public static class WindowStyles
{
    public static void ApplyToolWindowAndNoActivate(nint hwnd)
    {
        var handle = new HWND(hwnd);
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);

        PInvoke.SetWindowPos(
            handle,
            HWND.Null,
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Odak çalmama (WS_EX_NOACTIVATE) stilini açar ya da kapatır. Çentikte yazı yazılacak bir alana tıklanınca
    /// geçici olarak kapatılır (etkinliksiz pencere klavye girdisi alamaz), odak gidince yeniden açılır.
    /// </summary>
    public static void SetNoActivate(nint hwnd, bool noActivate)
    {
        var handle = new HWND(hwnd);
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (noActivate)
            exStyle |= WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        else
            exStyle &= ~WINDOW_EX_STYLE.WS_EX_NOACTIVATE;

        _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
    }

    /// <summary>
    /// Pencereyi tıklama geçirgen (WS_EX_TRANSPARENT) yapar ya da tık alabilir hale getirir.
    /// </summary>
    public static void SetClickThrough(nint hwnd, bool clickThrough)
    {
        var handle = new HWND(hwnd);
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (clickThrough)
            exStyle |= WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
        else
            exStyle &= ~WINDOW_EX_STYLE.WS_EX_TRANSPARENT;

        _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
        PInvoke.SetWindowPos(
            handle,
            HWND.Null,
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Pencereyi fiziksel piksel koordinatlarıyla yerleştirir (z sırası ve odak değişmez). Farklı DPI'lı ekranlar
    /// arasında WPF'in DIP tabanlı Left/Top alanı yanlış yere düşebildiği için çoklu ekranda bu kullanılır.
    /// <paramref name="resize"/> false ise yalnız konum verilir, boyutu WPF belirler (SizeToContent).
    /// </summary>
    public static void SetBounds(nint hwnd, Koy.Core.Shell.WindowRect rect, bool resize)
    {
        var flags = SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        if (!resize)
            flags |= SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

        PInvoke.SetWindowPos(new HWND(hwnd), HWND.Null, rect.Left, rect.Top, rect.Width, rect.Height, flags);
    }

    /// <summary>
    /// Odak çalmadan pencereyi en üste (Topmost) taşır.
    /// </summary>
    public static void ReassertTopmost(nint hwnd)
    {
        var handle = new HWND(hwnd);
        PInvoke.SetWindowPos(
            handle,
            new HWND((nint)(-1)), // HWND_TOPMOST
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Pencereyi ön plana (foreground) taşır.
    /// </summary>
    public static void SetForeground(nint hwnd)
    {
        if (hwnd == 0) return;
        PInvoke.SetForegroundWindow(new HWND(hwnd));
    }

    /// <summary>
    /// Geçerli ön plan penceresinin tanıtıcısını (HWND) döndürür.
    /// </summary>
    public static unsafe nint GetForeground() => (nint)PInvoke.GetForegroundWindow().Value;

    /// <summary>
    /// Pencerenin ekranı kaplayıp kaplamadığını (WS_MAXIMIZE) denetler.
    /// </summary>
    public static bool IsWindowMaximized(nint hwnd)
    {
        if (hwnd == 0) return false;
        var handle = new HWND(hwnd);
        var style = (uint)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        const uint WS_MAXIMIZE = 0x01000000;
        return (style & WS_MAXIMIZE) != 0;
    }

    /// <summary>
    /// Pencerenin simge durumuna küçültülüp küçültülmediğini (IsIconic / WS_MINIMIZE) denetler.
    /// </summary>
    public static bool IsWindowMinimized(nint hwnd)
    {
        if (hwnd == 0) return false;
        return PInvoke.IsIconic(new HWND(hwnd));
    }

    /// <summary>
    /// Verilen pencerenin ait olduğu süreç kimliğini (PID) döndürür.
    /// </summary>
    public static uint GetWindowProcessId(nint hwnd)
    {
        if (hwnd == 0) return 0;
        uint pid = 0;
        unsafe { _ = PInvoke.GetWindowThreadProcessId(new HWND(hwnd), &pid); }
        return pid;
    }

    /// <summary>
    /// Pencerenin süreç exe adı, uzantısız (ör. "chrome"). Sınırlı sorgu hakkıyla, yönetici süreçlerde de çalışır;
    /// çözülemezse null. Bağlam kuralları ön plan olayında bir kez çağırır (yoklama yok).
    /// </summary>
    public static string? GetWindowProcessName(nint hwnd)
    {
        var pid = GetWindowProcessId(hwnd);
        var path = WindowTracker.GetProcessPath(pid);
        return string.IsNullOrEmpty(path) ? null : System.IO.Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// Verilen pencerenin bulunduğu monitörün tanıtıcısını (HMONITOR) döndürür.
    /// </summary>
    public static unsafe nint GetWindowMonitor(nint hwnd)
    {
        if (hwnd == 0) return 0;
        return (nint)PInvoke.MonitorFromWindow(new HWND(hwnd), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST).Value;
    }
}
