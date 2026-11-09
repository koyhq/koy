using System.Windows.Interop;
using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Koy.Core.Shortcuts;
using Koy.Platform.Shell;

namespace Koy.App.Features.Shortcuts;

/// <summary>
/// Genel kısayolların yaşam döngüsü: gizli bir pencere WM_HOTKEY mesajlarını alır, <see cref="ShortcutManager"/> kayıt
/// ve çakışma kararlarını verir. Çentik ve dock kapalıyken de çalışır (kısayollar özellikten bağımsızdır).
/// </summary>
public sealed class ShortcutController : IDisposable
{
    private HwndSource? _source;
    private HotkeyService? _hotkeys;
    private KeyboardHookService? _hook;
    private ShortcutManager? _manager;

    /// <summary>Son kayıt denemesinin eylem başına sonucu. Sayfa hata satırlarını bundan çizer.</summary>
    public IReadOnlyDictionary<ShortcutAction, ShortcutResult> Results { get; private set; } =
        new Dictionary<ShortcutAction, ShortcutResult>();

    public event Action? ResultsChanged;
    public event Action<ShortcutAction>? Triggered;

    public void Start(AppSettings settings)
    {
        if (_manager is not null)
            return;

        _source = new HwndSource(new HwndSourceParameters("KoyHotkeyListener")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0
        });
        _source.AddHook(WndProc);

        _hotkeys = new HotkeyService(_source.Handle);
        var dispatcher = _source.Dispatcher;
        _hook = new KeyboardHookService(action => dispatcher.BeginInvoke(action));
        _manager = new ShortcutManager(_hotkeys, _hook);
        _manager.Triggered += (_, action) => CrashLog.Guard("Kısayol " + action, () => Triggered?.Invoke(action));

        Apply(settings);
    }

    /// <summary>Ayarlardaki kısayolları yeniden kaydeder (açılışta ve her değişiklikte).</summary>
    public void Apply(AppSettings settings)
    {
        if (_manager is null)
            return;

        Results = _manager.Apply(ShortcutBindings.ReadAll(settings));
        ResultsChanged?.Invoke();
    }

    /// <summary>Ayarlardaki kutu için bir sonraki kombinasyonu yakalar; kanca kurulamazsa false (kutu WPF tuş olaylarıyla çalışır).</summary>
    public bool BeginCapture(Action<Hotkey> onCaptured) => _hook?.BeginCapture(onCaptured) ?? false;

    public void EndCapture() => _hook?.EndCapture();

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // WndProc'tan sızan istisna süreci düşürür; logla ve yut.
        try
        {
            if (_hotkeys?.HandleMessage(msg, wParam) == true)
                handled = true;
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShortcutController.WndProc", ex);
        }

        return 0;
    }

    public void Dispose()
    {
        _manager?.Dispose();
        _manager = null;
        _hotkeys = null;
        _hook = null;

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }
}
