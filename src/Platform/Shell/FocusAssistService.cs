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
    /// Rahatsız etmeyin ya da Odaklanma açık mı. Windows 11'de tek belgeli yol yok, üç işaret birleşir:
    /// 1) SHQueryUserNotificationState: QUIET_TIME (Win10 Sessiz saatler) ve sunum modu; Win11 "Rahatsız etmeyin"i vermez.
    /// 2) <c>FocusSessionManager.IsFocusActive</c> (belgeli WinRT): Odaklanma oturumu.
    /// 3) HKCU\...\Notifications\Settings\NOC_GLOBAL_SETTING_TOASTS_ENABLED = 0: Win11'in Rahatsız etmeyin düğmesi bu değeri
    ///    yazar (belgesiz, Windows sürümüyle değişebilir; okunamazsa sessizce "kapalı" sayılır).
    /// <see cref="IsActive"/> bunları kapsamaz (o tam ekran "meşgul" durumunu okur).
    /// </summary>
    public bool IsDoNotDisturb => ReadQuns() || ReadFocusSession() || ReadToastsDisabled();

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

    private static bool ReadToastsDisabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings");
            return key?.GetValue("NOC_GLOBAL_SETTING_TOASTS_ENABLED") is int enabled && enabled == 0;
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
