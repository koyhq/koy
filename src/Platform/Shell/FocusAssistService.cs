using System.Diagnostics;
using Koy.Core.Shell;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Koy.Platform.Shell;

public sealed class FocusAssistService : IFocusAssistService
{
    public bool IsActive
    {
        get
        {
            try
            {
                unsafe
                {
                    QUERY_USER_NOTIFICATION_STATE state;
                    var hr = PInvoke.SHQueryUserNotificationState(&state);
                    if (hr.Succeeded)
                    {
                        return state is QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY or
                                       QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }

    /// <summary>
    /// Odaklanma oturumu ya da sunum modu açık mı (bildirim kartları bu durumda susar). İki işaret birleşir:
    /// 1) SHQueryUserNotificationState: QUIET_TIME (Win10 Sessiz saatler) ve sunum modu.
    /// 2) <c>FocusSessionManager.IsFocusActive</c> (belgeli WinRT): Odaklanma oturumu; paketsiz süreçte de çalışır.
    /// Bilinen kısıt: Windows 11'in "Rahatsız etmeyin" düğmesi tek başına okunamıyor (belgelenmiş API yok; kayıt defterinde
    /// işaret bulunmadı, WNF kullanılmıyor); düğme tek başına açıkken bu değer false döner.
    /// <see cref="IsActive"/> bunları kapsamaz (o tam ekran "meşgul" durumunu okur).
    /// </summary>
    public bool IsDoNotDisturb => ReadQuns() || ReadFocusSession();

    private static bool ReadQuns()
    {
        try
        {
            unsafe
            {
                QUERY_USER_NOTIFICATION_STATE state;
                var hr = PInvoke.SHQueryUserNotificationState(&state);
                return hr.Succeeded && state is QUERY_USER_NOTIFICATION_STATE.QUNS_QUIET_TIME or
                                                QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool ReadFocusSession()
    {
        try
        {
            return Windows.UI.Shell.FocusSessionManager.IsSupported && Windows.UI.Shell.FocusSessionManager.GetDefault().IsFocusActive;
        }
        catch
        {
            return false;
        }
    }

    private EventHandler<bool>? _stateChanged;
    private Windows.UI.Shell.FocusSessionManager? _focusManager;

    public event EventHandler<bool>? StateChanged
    {
        add
        {
            _stateChanged += value;
            if (_focusManager is not null)
                return;

            try
            {
                if (!Windows.UI.Shell.FocusSessionManager.IsSupported)
                    return;

                _focusManager = Windows.UI.Shell.FocusSessionManager.GetDefault();
                _focusManager.IsFocusActiveChanged += OnFocusActiveChanged;
            }
            catch
            {
                // Paketsiz süreçte olay kurulamayabilir (NotificationChanged gibi): çağıran açılışta okumaya düşer.
                _focusManager = null;
            }
        }
        remove
        {
            _stateChanged -= value;
            if (_stateChanged is not null)
                return;

            DetachFocusManager();
        }
    }

    private void OnFocusActiveChanged(Windows.UI.Shell.FocusSessionManager sender, object args) =>
        _stateChanged?.Invoke(this, IsDoNotDisturb);

    private void DetachFocusManager()
    {
        try
        {
            if (_focusManager is not null)
                _focusManager.IsFocusActiveChanged -= OnFocusActiveChanged;
        }
        catch
        {
        }

        _focusManager = null;
    }

    public void OpenSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:quiethours") { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true });
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        _stateChanged = null;
        DetachFocusManager();
    }
}
