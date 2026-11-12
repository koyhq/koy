using Koy.Core.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// WS_EX_NOACTIVATE pencerelerinde açılan bağlam menülerini dışarı tıklandığında kapatmak için
/// yalnız menü açıkken kurulan kısa ömürlü WH_MOUSE_LL kancası. Menü kapanınca hemen sökülür.
/// </summary>
public sealed unsafe class OutsideClickHook : IDisposable
{
    private const uint WmLButtonDown = 0x0201;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmQuit = 0x0012;

    private readonly Action<int, int> _onMouseDown;
    private readonly HOOKPROC _proc;
    private readonly object _gate = new();

    private Thread? _thread;
    private uint _threadId;
    private bool _disposed;

    public OutsideClickHook(Action<int, int> onMouseDown)
    {
        _onMouseDown = onMouseDown;
        _proc = Callback;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _thread is not null)
                return;

            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Koy dis tiklama kancasi" };
            _thread.Start();
            ready.Wait(TimeSpan.FromSeconds(1));
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_thread is null)
                return;

            if (_threadId != 0)
                PInvoke.PostThreadMessage(_threadId, WmQuit, default, default);

            _thread.Join(TimeSpan.FromSeconds(1));
            _thread = null;
            _threadId = 0;
        }
    }

    private void Run(ManualResetEventSlim ready)
    {
        try
        {
            _threadId = PInvoke.GetCurrentThreadId();
            using var module = PInvoke.GetModuleHandle((string?)null);
            using var hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, _proc, module, 0);
            ready.Set();

            if (hook.IsInvalid)
            {
                CrashLog.Write("OutsideClickHook", new System.ComponentModel.Win32Exception());
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
            CrashLog.Write("OutsideClickHook.Run", ex);
            ready.Set();
        }
    }

    private LRESULT Callback(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0)
        {
            var msg = (uint)wParam.Value;
            if (msg is WmLButtonDown or WmRButtonDown or WmMButtonDown)
            {
                try
                {
                    var info = (MSLLHOOKSTRUCT*)lParam.Value;
                    _onMouseDown(info->pt.X, info->pt.Y);
                }
                catch (Exception ex)
                {
                    CrashLog.Write("OutsideClickHook.Callback", ex);
                }
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
            Stop();
        }
    }
}
