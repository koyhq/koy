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
    private const uint ABE_BOTTOM = 3;

    private readonly HashSet<nint> _registeredHandles = [];

    // Her pencere için en son uygulanan alan: aynı alan için SETPOS tekrarlanmaz (yerleşim fırtınasında ~20 çağrı oluyordu).
    private readonly AppBarPlacementCache _placements = new();

    // Yeniden iddiadan sonra da uyuşmayan çalışma alanı alt sınırı (başka çubuk): aynı değer için kısa süre tekrar denenmez.
    // Kalıcı değildir: açılışta Gezgin gizlenen görev çubuğunu birkaç saniye hâlâ yer kaplıyor sayar ve o anki SETPOS
    // payı onun üstüne yığar (canlı: 986). Aynı SETPOS birkaç saniye sonra doğru sonucu verir (1034); kalıcı kayıt bunu
    // engelliyordu.
    private readonly Dictionary<nint, (int Bottom, long Ticks)> _unresolvedWorkBottom = [];
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
            _unresolvedWorkBottom.Clear();
            _placements.Clear();   // sahiplik değişince eski alan geçersiz: sıradaki güncelleme mutlaka SETPOS gönderir
        }
    }

    private bool _ownsBottomEdge;

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
    /// <param name="onlyIfNeeded">true ise monitörün çalışma alanı payı zaten karşılıyorsa SETPOS gönderilmez (bildirim yolu).</param>
    /// <returns>SETPOS gönderildi mi.</returns>
    public unsafe bool Reassert(nint windowHandle, double heightDip, double dpiScale = 1.0, bool onlyIfNeeded = false)
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
                int? unresolvedBottom = null;
                if (_unresolvedWorkBottom.TryGetValue(windowHandle, out var unresolved)
                    && TimeSpan.FromTicks(Environment.TickCount64 * TimeSpan.TicksPerMillisecond - unresolved.Ticks) < UnresolvedRetryAfter)
                    unresolvedBottom = unresolved.Bottom;

                var needed = AppBarOwnership.NeedsReassert(
                    monitorRect, new WindowRect(work.left, work.top, work.right, work.bottom), heightPx, OwnsBottomEdge,
                    unresolvedBottom);
                if (!needed)
                    return false;

                UpdatePositionInternal(hwnd, heightDip, dpiScale, force: true);

                // Düzelmediyse (başka bir çubuk payı etkiliyor) aynı çalışma alanı için tekrar denenmez.
                var after = new MONITORINFOEXW { monitorInfo = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFOEXW) } };
                if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&after))
                {
                    var stillWrong = AppBarOwnership.NeedsReassert(
                        monitorRect,
                        new WindowRect(after.monitorInfo.rcWork.left, after.monitorInfo.rcWork.top, after.monitorInfo.rcWork.right, after.monitorInfo.rcWork.bottom),
                        heightPx, OwnsBottomEdge);
                    if (stillWrong)
                        _unresolvedWorkBottom[windowHandle] = (after.monitorInfo.rcWork.bottom, Environment.TickCount64 * TimeSpan.TicksPerMillisecond);
                    else
                        _unresolvedWorkBottom.Remove(windowHandle);
                }

                return true;
            }
        }

        UpdatePositionInternal(hwnd, heightDip, dpiScale, force: true);
        return true;
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
        _placements.Forget(windowHandle);
        _unresolvedWorkBottom.Remove(windowHandle);
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

        var requested = new WindowRect(data.rc.left, data.rc.top, data.rc.right, data.rc.bottom);

        // Alt kenar bizimken sonuç Gezgin'den bağımsızdır: aynı alan için tekrar SETPOS gerekmez (kurallar: AppBarPlacementCache).
        if (!_placements.ShouldSend(hwnd.Value, requested, OwnsBottomEdge, force))
            return;

        PInvoke.SHAppBarMessage(ABM_QUERYPOS, ref data);
        var queried = new WindowRect(data.rc.left, data.rc.top, data.rc.right, data.rc.bottom);
        var final = AppBarMath.FinalRect(new WindowRect(screenLeft, 0, screenRight, screenBottom), heightPx, queried, OwnsBottomEdge);
        data.rc = new RECT { left = final.Left, top = final.Top, right = final.Right, bottom = final.Bottom };
        PInvoke.SHAppBarMessage(ABM_SETPOS, ref data);
        _placements.Record(hwnd.Value, requested);
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
