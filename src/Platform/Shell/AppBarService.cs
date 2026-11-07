using Koy.Core.Display;
using Koy.Core.Dock;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// Dock penceresini Windows AppBar olarak kaydeder, ekranın altında masaüstü çalışma alanı ayırır
/// ve kapanışta (ABM_REMOVE) eski çalışma alanını geri yükler.
/// </summary>
public sealed class AppBarService : IAppBarService
{
    private const uint ABM_NEW = 0x00000000;
    private const uint ABM_REMOVE = 0x00000001;
    private const uint ABM_QUERYPOS = 0x00000002;
    private const uint ABM_SETPOS = 0x00000003;
    private const uint ABE_BOTTOM = 3;

    private readonly HashSet<nint> _registeredHandles = [];

    // Her pencere için en son uygulanan alan: aynı alan için SETPOS tekrarlanmaz (yerleşim fırtınasında ~20 çağrı oluyordu).
    private readonly Dictionary<nint, RECT> _lastApplied = [];
    private bool _disposed;

    public unsafe void Register(nint windowHandle, double heightDip, double dpiScale = 1.0)
    {
        if (windowHandle == 0 || _registeredHandles.Contains(windowHandle))
            return;

        if (heightDip <= 0)
            heightDip = 46;

        var hwnd = new HWND(windowHandle);

        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = hwnd,
            uCallbackMessage = 0x0400 + 100 // WM_USER + 100
        };

        var registered = PInvoke.SHAppBarMessage(ABM_NEW, ref data);
        if (registered != 0)
        {
            _registeredHandles.Add(windowHandle);
            UpdatePositionInternal(hwnd, heightDip, dpiScale, force: true);
        }
    }

    public unsafe void UpdatePosition(nint windowHandle, double heightDip, double dpiScale = 1.0)
    {
        if (windowHandle == 0 || !_registeredHandles.Contains(windowHandle))
            return;

        UpdatePositionInternal(new HWND(windowHandle), heightDip, dpiScale, force: false);
    }

    /// <summary>
    /// Alan aynı olsa da yeniden iddia eder: Gezgin çalışma alanını yeniden hesapladıysa (ABN_POSCHANGED, görev çubuğu
    /// gösterildi/gizlendi) dock'un payı o an düşmüş olabilir.
    /// </summary>
    public unsafe void Reassert(nint windowHandle, double heightDip, double dpiScale = 1.0)
    {
        if (windowHandle == 0 || !_registeredHandles.Contains(windowHandle))
            return;

        UpdatePositionInternal(new HWND(windowHandle), heightDip, dpiScale, force: true);
    }

    public void Reregister(nint windowHandle, double heightDip, double dpiScale = 1.0)
    {
        if (windowHandle == 0)
            return;

        // Eski Explorer'ın kaydı yok olmuştur; ABM_REMOVE zararsızdır ve yerel kayıt kümesini de temizler.
        Unregister(windowHandle);
        Register(windowHandle, heightDip, dpiScale);
    }

    public unsafe void Unregister(nint windowHandle)
    {
        if (windowHandle == 0 || !_registeredHandles.Remove(windowHandle))
            return;

        var hwnd = new HWND(windowHandle);
        _lastApplied.Remove(windowHandle);
        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = hwnd
        };

        PInvoke.SHAppBarMessage(ABM_REMOVE, ref data);
    }

    private unsafe void UpdatePositionInternal(HWND hwnd, double heightDip, double dpiScale, bool force)
    {
        if (heightDip <= 0)
            heightDip = 46;

        var screenLeft = 0;
        var screenRight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var screenBottom = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

        var hMonitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (hMonitor != 0)
        {
            var mi = new MONITORINFOEXW
            {
                monitorInfo = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFOEXW) }
            };
            if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&mi))
            {
                screenLeft = mi.monitorInfo.rcMonitor.left;
                screenRight = mi.monitorInfo.rcMonitor.right;
                screenBottom = mi.monitorInfo.rcMonitor.bottom;
            }
        }

        // Pencere yüksekliğiyle aynı piksel kuralı (WindowPlacementMath.ToPixels): çubuk ile çalışma alanı arasında boşluk kalmaz.
        var heightPx = WindowPlacementMath.ToPixels(heightDip, dpiScale);

        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = hwnd,
            uEdge = ABE_BOTTOM,
            rc = new RECT
            {
                left = screenLeft,
                top = screenBottom - heightPx,
                right = screenRight,
                bottom = screenBottom
            }
        };

        var requested = data.rc;
        if (!force && _lastApplied.TryGetValue(hwnd.Value, out var last)
            && last.left == requested.left && last.top == requested.top && last.right == requested.right && last.bottom == requested.bottom)
            return;

        PInvoke.SHAppBarMessage(ABM_QUERYPOS, ref data);
        data.rc.top = data.rc.bottom - heightPx;
        PInvoke.SHAppBarMessage(ABM_SETPOS, ref data);
        _lastApplied[hwnd.Value] = requested;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var handles = _registeredHandles.ToList();
        foreach (var handle in handles)
        {
            Unregister(handle);
        }
    }
}
