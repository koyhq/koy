using Koy.Core.Diagnostics;
using Koy.Core.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// Köşe takibi için WH_MOUSE_LL kancası. <see cref="KeyboardHookService"/> gibi kendi iş parçacığında çalışır: arayüz
/// takılsa bile sistemin faresi gecikmez, Windows da yavaş kancayı sessizce kaldırmaz. Hiçbir olayı yutmaz; her harekette
/// yalnız köşe kutularıyla karşılaştırır, arayüze yalnız köşe değişince haber verir. İmleç durunca hiç uyanmaz.
/// Yalnız en az bir köşeye eylem bağlıyken kurulur.
/// </summary>
public sealed unsafe class CornerHookService : ICornerHookService
{
    private const uint WmMouseMove = 0x0200;
    private const uint WmQuit = 0x0012;

    private readonly Action<Action> _post;
    private readonly HOOKPROC _proc;
    private readonly object _gate = new();

    private Thread? _thread;
    private uint _threadId;
    private bool _installed;
    private bool _disposed;
    private volatile ScreenRect[] _screens = [];
    private volatile int _cornerSize;
    // Yalnız kanca iş parçacığı okur/yazar.
    private CornerHit? _last;

    public event EventHandler<CornerHit?>? CornerChanged;

    /// <param name="post">Olayları arayüz iş parçacığına taşır; kanca içinde beklememesi gerekir.</param>
    public CornerHookService(Action<Action> post)
    {
        _post = post;
        _proc = Callback;
    }

    public bool SetScreens(IReadOnlyList<ScreenRect> screens, int cornerSizePx)
    {
        lock (_gate)
        {
            if (_disposed)
                return false;

            _cornerSize = cornerSizePx;
            _screens = screens.ToArray();
            if (_screens.Length > 0)
                return EnsureRunning();

            StopHook();
            return true;
        }
    }

    private bool EnsureRunning()
    {
        if (_thread is not null)
            return _installed;

        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Koy kose kancasi" };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
        return _installed;
    }

    private void StopHook()
    {
        if (_thread is null)
            return;

        PInvoke.PostThreadMessage(_threadId, WmQuit, default, default);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _installed = false;
    }

    private void Run(ManualResetEventSlim ready)
    {
        try
        {
            _last = null;
            _threadId = PInvoke.GetCurrentThreadId();
            using var module = PInvoke.GetModuleHandle((string?)null);
            using var hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, _proc, module, 0);
            _installed = !hook.IsInvalid;
            ready.Set();

            if (!_installed)
            {
                CrashLog.Write("CornerHookService", new System.ComponentModel.Win32Exception());
                return;
            }

            while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
            {
                PInvoke.TranslateMessage(msg);
                PInvoke.DispatchMessage(msg);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("CornerHookService.Run", ex);
            ready.Set();
        }
    }

    private LRESULT Callback(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && (uint)wParam.Value == WmMouseMove)
        {
            try
            {
                var info = (MSLLHOOKSTRUCT*)lParam.Value;
                var hit = HotCornerMath.HitTest(info->pt.X, info->pt.Y, _screens, _cornerSize);
                if (hit != _last)
                {
                    _last = hit;
                    _post(() => CornerChanged?.Invoke(this, hit));
                }
            }
            catch (Exception ex)
            {
                CrashLog.Write("CornerHookService.Callback", ex);
            }
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            CornerChanged = null;
            StopHook();
        }
    }
}
