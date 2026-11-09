using System.Diagnostics;
using Koy.Core.Diagnostics;
using Koy.Core.Shortcuts;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// WH_KEYBOARD_LL kancası. Kendi iş parçacığında çalışır: arayüz takılsa bile bütün sistemin klavyesi gecikmesin,
/// Windows da yavaş kancayı sessizce kaldırmasın. Karar <see cref="KeyboardHookEngine"/>'de, burada yalnız Windows bağlantısı var.
/// </summary>
public sealed unsafe class KeyboardHookService : IKeyboardHookService
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmQuit = 0x0012;

    // Atanmamış tuş: Win ya da Alt "tek başına basıldı" sayılmasın diye araya girer, hiçbir uygulamada iş yapmaz.
    private const VIRTUAL_KEY MaskKey = (VIRTUAL_KEY)0xE8;

    private readonly KeyboardHookEngine _engine = new();
    private readonly Action<Action> _post;
    private readonly HOOKPROC _proc;
    private readonly object _gate = new();

    private Thread? _thread;
    private uint _threadId;
    private bool _installed;
    private bool _hasBindings;
    private bool _capturing;
    private bool _disposed;
    private volatile Action<Hotkey>? _onCaptured;

    public event EventHandler<int>? HotkeyPressed;

    /// <param name="post">Olayları arayüz iş parçacığına taşır; kanca içinde beklememesi gerekir.</param>
    public KeyboardHookService(Action<Action> post)
    {
        _post = post;
        _proc = Callback;
    }

    public bool SetBindings(IReadOnlyDictionary<int, Hotkey> bindings)
    {
        lock (_gate)
        {
            if (_disposed)
                return false;

            _engine.SetBindings(bindings);
            _hasBindings = bindings.Count > 0;
            return _hasBindings ? EnsureRunning() : StopIfIdle();
        }
    }

    public bool BeginCapture(Action<Hotkey> onCaptured)
    {
        lock (_gate)
        {
            if (_disposed || !EnsureRunning())
                return false;

            _capturing = true;
            _onCaptured = onCaptured;
            _engine.BeginCapture(Environment.TickCount64);
            return true;
        }
    }

    public void EndCapture()
    {
        lock (_gate)
        {
            _onCaptured = null;
            _capturing = false;
            _engine.EndCapture();
            if (!_disposed)
                StopIfIdle();
        }
    }

    private bool EnsureRunning()
    {
        if (_thread is not null)
            return _installed;

        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Koy klavye kancasi" };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
        return _installed;
    }

    private bool StopIfIdle()
    {
        if (_hasBindings || _capturing || _thread is null)
            return true;

        PInvoke.PostThreadMessage(_threadId, WmQuit, default, default);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _installed = false;
        return true;
    }

    private void Run(ManualResetEventSlim ready)
    {
        try
        {
            _threadId = PInvoke.GetCurrentThreadId();
            using var module = PInvoke.GetModuleHandle((string?)null);
            using var hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _proc, module, 0);
            _installed = !hook.IsInvalid;
            ready.Set();

            if (!_installed)
            {
                CrashLog.Write("KeyboardHookService", new System.ComponentModel.Win32Exception());
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
            CrashLog.Write("KeyboardHookService.Run", ex);
            ready.Set();
        }
    }

    private LRESULT Callback(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code < 0)
            return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);

        try
        {
            var info = (KBDLLHOOKSTRUCT*)lParam.Value;
            var message = (uint)wParam.Value;
            var isDown = message is WmKeyDown or WmSysKeyDown;
            var known = isDown || message is WmKeyUp or WmSysKeyUp;

            // Koy'un kendi gönderdiği tuşlar (Win+S, boş tuş) yeniden yorumlanmaz.
            if (known && (info->flags & KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED) == 0)
            {
                var outcome = _engine.Process((int)info->vkCode, isDown, Environment.TickCount64, IsPhysicallyDown);
                Publish(outcome);

                if (outcome.MaskModifier)
                    SendMaskKey();
                if (outcome.Swallow)
                    return new LRESULT(1);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("KeyboardHookService.Callback", ex);
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }

    private static bool IsPhysicallyDown(int vk) => (PInvoke.GetAsyncKeyState(vk) & 0x8000) != 0;

    private void Publish(HookOutcome outcome)
    {
        if (outcome.FiredId is { } id)
            _post(() => HotkeyPressed?.Invoke(this, id));

        if (outcome.Captured is { } hotkey && _onCaptured is { } onCaptured)
            _post(() => onCaptured(hotkey));
    }

    private static void SendMaskKey()
    {
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[0].Anonymous.ki.wVk = MaskKey;
        inputs[1].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[1].Anonymous.ki.wVk = MaskKey;
        inputs[1].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        PInvoke.SendInput(inputs, sizeof(INPUT));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _hasBindings = false;
            _capturing = false;
            _onCaptured = null;
            StopIfIdle();
        }
    }
}
