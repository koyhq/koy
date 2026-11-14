using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Shell;
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
    private const uint ABE_LEFT = 0;
    private const uint ABE_TOP = 1;
    private const uint ABE_RIGHT = 2;
    private const uint ABE_BOTTOM = 3;

    private static uint ToAppBarEdge(DockEdge edge) => edge switch
    {
        DockEdge.Left => ABE_LEFT,
        DockEdge.Top => ABE_TOP,
        DockEdge.Right => ABE_RIGHT,
        _ => ABE_BOTTOM
    };

    private readonly HashSet<nint> _registeredHandles = [];
    private readonly Dictionary<nint, DockEdge> _registeredEdges = [];

    // Her pencere için en son uygulanan alan: aynı alan için SETPOS tekrarlanmaz (yerleşim fırtınasında ~20 çağrı oluyordu).
    private readonly AppBarPlacementCache _placements = new();

    // Yeniden iddiadan sonra da uyuşmayan çalışma alanı kenarı (başka çubuk): aynı değer için kısa süre tekrar denenmez.
    private readonly Dictionary<nint, (int Coord, long Ticks)> _unresolvedWorkEdge = [];
    private static readonly TimeSpan UnresolvedRetryAfter = TimeSpan.FromSeconds(2);
    private bool _disposed;

    /// <summary>
    /// Windows görev çubuğu gizliyken true: alt kenar Koy'undur ve ABM_QUERYPOS'un Gezgin yüzünden yukarı itmesi kabul
    /// edilmez (AppBarMath). Aksi halde görev çubuğu hâlâ sayılırken kayıt yapılınca pay 94 px olup boşluk bırakıyordu.
    /// </summary>
    public bool OwnsBottomEdge
    {
        get => _ownsBottomEdge;
        set
        {
            if (_ownsBottomEdge == value)
                return;

            _ownsBottomEdge = value;
            _unresolvedWorkEdge.Clear();
            _placements.Clear();   // sahiplik değişince eski alan geçersiz: sıradaki güncelleme mutlaka SETPOS gönderir
        }
    }

    private bool _ownsBottomEdge;

    public unsafe void Register(nint windowHandle, double heightDip, double dpiScale = 1.0, DockEdge edge = DockEdge.Bottom)
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
            _registeredEdges[windowHandle] = edge;
            UpdatePositionInternal(hwnd, heightDip, dpiScale, force: true, edge);
        }
    }

    public unsafe void UpdatePosition(nint windowHandle, double heightDip, double dpiScale = 1.0, DockEdge edge = DockEdge.Bottom)
    {
        if (windowHandle == 0 || !_registeredHandles.Contains(windowHandle))
            return;

        UpdatePositionInternal(new HWND(windowHandle), heightDip, dpiScale, force: false, edge);
    }

    /// <summary>
    /// Alan aynı olsa da yeniden iddia eder: Gezgin çalışma alanını yeniden hesapladıysa (ABN_POSCHANGED, görev çubuğu
    /// gösterildi/gizlendi) dock'un payı o an düşmüş olabilir.
    /// </summary>
    /// <param name="onlyIfNeeded">true ise monitörün çalışma alanı payı zaten karşılıyorsa SETPOS gönderilmez (bildirim yolu).</param>
    /// <returns>SETPOS gönderildi mi.</returns>
    public unsafe bool Reassert(nint windowHandle, double heightDip, double dpiScale = 1.0, bool onlyIfNeeded = false, DockEdge edge = DockEdge.Bottom)
    {
        if (windowHandle == 0 || !_registeredHandles.Contains(windowHandle))
            return false;

        var hwnd = new HWND(windowHandle);
        if (onlyIfNeeded)
        {
            var hMonitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFOEXW { monitorInfo = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFOEXW) } };
            if (hMonitor != 0 && PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&mi))
            {
                var bounds = mi.monitorInfo.rcMonitor;
                var work = mi.monitorInfo.rcWork;
                var monitorRect = new WindowRect(bounds.left, bounds.top, bounds.right, bounds.bottom);
                var heightPx = WindowPlacementMath.ToPixels(heightDip <= 0 ? 46 : heightDip, dpiScale);
                int? unresolvedCoord = null;
                if (_unresolvedWorkEdge.TryGetValue(windowHandle, out var unresolved)
                    && TimeSpan.FromTicks(Environment.TickCount64 * TimeSpan.TicksPerMillisecond - unresolved.Ticks) < UnresolvedRetryAfter)
                    unresolvedCoord = unresolved.Coord;

                var needed = AppBarOwnership.NeedsReassert(
                    edge, monitorRect, new WindowRect(work.left, work.top, work.right, work.bottom), heightPx, OwnsBottomEdge,
                    unresolvedCoord);
                if (!needed)
                    return false;

                UpdatePositionInternal(hwnd, heightDip, dpiScale, force: true, edge);

                // Düzelmediyse (başka bir çubuk payı etkiliyor) aynı çalışma alanı için tekrar denenmez.
                var after = new MONITORINFOEXW { monitorInfo = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFOEXW) } };
                if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&after))
                {
                    var stillWrong = AppBarOwnership.NeedsReassert(
                        edge,
                        monitorRect,
                        new WindowRect(after.monitorInfo.rcWork.left, after.monitorInfo.rcWork.top, after.monitorInfo.rcWork.right, after.monitorInfo.rcWork.bottom),
                        heightPx, OwnsBottomEdge);
                    if (stillWrong)
                    {
                        var coord = edge switch
                        {
                            DockEdge.Top => after.monitorInfo.rcWork.top,
                            DockEdge.Left => after.monitorInfo.rcWork.left,
                            DockEdge.Right => after.monitorInfo.rcWork.right,
                            _ => after.monitorInfo.rcWork.bottom
                        };
                        _unresolvedWorkEdge[windowHandle] = (coord, Environment.TickCount64 * TimeSpan.TicksPerMillisecond);
                    }
                    else
                    {
                        _unresolvedWorkEdge.Remove(windowHandle);
                    }
                }

                return true;
            }
        }

        UpdatePositionInternal(hwnd, heightDip, dpiScale, force: true, edge);
        return true;
    }

    public void Reregister(nint windowHandle, double heightDip, double dpiScale = 1.0, DockEdge edge = DockEdge.Bottom)
    {
        if (windowHandle == 0)
            return;

        // Eski Explorer'ın kaydı yok olmuştur; ABM_REMOVE zararsızdır ve yerel kayıt kümesini de temizler.
        Unregister(windowHandle);
        Register(windowHandle, heightDip, dpiScale, edge);
    }

    public unsafe void Unregister(nint windowHandle)
    {
        if (windowHandle == 0 || !_registeredHandles.Remove(windowHandle))
            return;

        _registeredEdges.Remove(windowHandle);
        var hwnd = new HWND(windowHandle);
        _placements.Forget(windowHandle);
        _unresolvedWorkEdge.Remove(windowHandle);
        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = hwnd
        };

        PInvoke.SHAppBarMessage(ABM_REMOVE, ref data);
    }

    private unsafe void UpdatePositionInternal(HWND hwnd, double heightDip, double dpiScale, bool force, DockEdge edge = DockEdge.Bottom)
    {
        var handle = (nint)hwnd.Value;
        if (_registeredEdges.TryGetValue(handle, out var prevEdge) && prevEdge != edge)
        {
            // Win32 AppBar: Kenar değiştiğinde eski kenarın çalışma alanını geri vermek için ABM_REMOVE
            // ve yeni kenarda ABM_NEW zorunludur; aksi halde eski kenar payı Windows'ta asılı kalır.
            Reregister(handle, heightDip, dpiScale, edge);
            return;
        }

        _registeredEdges[handle] = edge;

        if (heightDip <= 0)
            heightDip = 46;

        var screenLeft = 0;
        var screenTop = 0;
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
                screenTop = mi.monitorInfo.rcMonitor.top;
                screenRight = mi.monitorInfo.rcMonitor.right;
                screenBottom = mi.monitorInfo.rcMonitor.bottom;
            }
        }

        // Pencere yüksekliğiyle aynı piksel kuralı (WindowPlacementMath.ToPixels): çubuk ile çalışma alanı arasında boşluk kalmaz.
        var heightPx = WindowPlacementMath.ToPixels(heightDip, dpiScale);
        var uEdge = ToAppBarEdge(edge);

        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = hwnd,
            uEdge = uEdge,
            rc = edge switch
            {
                DockEdge.Top => new RECT { left = screenLeft, top = screenTop, right = screenRight, bottom = screenTop + heightPx },
                DockEdge.Left => new RECT { left = screenLeft, top = screenTop, right = screenLeft + heightPx, bottom = screenBottom },
                DockEdge.Right => new RECT { left = screenRight - heightPx, top = screenTop, right = screenRight, bottom = screenBottom },
                _ => new RECT { left = screenLeft, top = screenBottom - heightPx, right = screenRight, bottom = screenBottom }
            }
        };

        var requested = new WindowRect(data.rc.left, data.rc.top, data.rc.right, data.rc.bottom);

        // Alt kenar bizimken sonuç Gezgin'den bağımsızdır: aynı alan için tekrar SETPOS gerekmez (kurallar: AppBarPlacementCache).
        if (!_placements.ShouldSend(handle, requested, OwnsBottomEdge, force))
            return;

        PInvoke.SHAppBarMessage(ABM_QUERYPOS, ref data);
        var queried = new WindowRect(data.rc.left, data.rc.top, data.rc.right, data.rc.bottom);
        var final = AppBarMath.FinalRect(edge, new WindowRect(screenLeft, screenTop, screenRight, screenBottom), heightPx, queried, OwnsBottomEdge);
        data.rc = new RECT { left = final.Left, top = final.Top, right = final.Right, bottom = final.Bottom };
        PInvoke.SHAppBarMessage(ABM_SETPOS, ref data);
        _placements.Record(handle, requested);
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
