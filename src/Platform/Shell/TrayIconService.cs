using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// Windows bildirim alanı (System Tray) simgesi için Win32 Shell_NotifyIcon sarmalayıcısı.
/// Dock kapalıyken kullanıcının Koy menüsüne erişebilmesini sağlar.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly nint _hwnd;
    private readonly uint _callbackMessage;
    private DestroyIconSafeHandle? _safeIconHandle;
    private bool _isAdded;
    private string _tooltip = "Koy";

    /// <summary>
    /// Explorer (yeniden) başlayıp görev çubuğunu oluşturunca bütün üst düzey pencerelere yayınlanan mesaj.
    /// Tepsi simgeleri Explorer ile birlikte silindiği için bu mesajda simge yeniden eklenmeli.
    /// </summary>
    public static uint TaskbarCreatedMessage { get; } = PInvoke.RegisterWindowMessage("TaskbarCreated");

    public TrayIconService(nint hwnd, uint callbackMessage = 0x8001)
    {
        _hwnd = hwnd;
        _callbackMessage = callbackMessage;
    }

    public unsafe bool Add(string tooltip = "Koy")
    {
        if (_isAdded || _hwnd == 0) return false;
        _tooltip = tooltip;

        // Uygulamanın kendi gömülü ikonunu (Koy.exe) çıkar
        HICON hIcon = default;
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                HICON smallIcon = default;
                uint extracted;
                fixed (char* pExe = exePath)
                    extracted = PInvoke.ExtractIconEx(new PCWSTR(pExe), 0, null, &smallIcon, 1);

                if (extracted > 0 && !smallIcon.IsNull)
                {
                    _safeIconHandle = new DestroyIconSafeHandle((nint)smallIcon.Value, ownsHandle: true);
                    hIcon = smallIcon;
                }
            }
        }
        catch
        {
            // İkon çıkarılamazsa varsayılan devam et
        }

        var flags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
        if (!hIcon.IsNull)
        {
            flags |= NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
        }

        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = new HWND(_hwnd),
            uID = 1001,
            uFlags = flags,
            uCallbackMessage = _callbackMessage,
            hIcon = hIcon
        };

        var pData = &data;
        var tipPtr = (char*)&pData->szTip;
        var chars = tooltip.AsSpan();
        var maxLen = Math.Min(chars.Length, 127);
        for (var i = 0; i < maxLen; i++)
        {
            tipPtr[i] = chars[i];
        }
        tipPtr[maxLen] = '\0';

        _isAdded = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, pData);
        return _isAdded;
    }

    /// <summary>Explorer yeniden başlatıldıktan sonra simgeyi yeniden ekler (eski kayıt zaten silinmiştir).</summary>
    public bool Recreate()
    {
        _isAdded = false;
        _safeIconHandle?.Dispose();
        _safeIconHandle = null;
        return Add(_tooltip);
    }

    public unsafe void Remove()
    {
        if (!_isAdded || _hwnd == 0) return;

        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = new HWND(_hwnd),
            uID = 1001
        };

        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
        _isAdded = false;

        _safeIconHandle?.Dispose();
        _safeIconHandle = null;
    }

    public void Dispose()
    {
        Remove();
    }
}
