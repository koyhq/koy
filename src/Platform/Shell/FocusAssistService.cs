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
    /// Rahatsız Etme (Sessiz saatler) ya da sunum modu açık mı: bildirim kartı bu durumda gösterilmez.
    /// <see cref="IsActive"/> bunu kapsamaz (o tam ekran "meşgul" durumunu okur); QUNS_QUIET_TIME burada ayrıca denetlenir.
    /// </summary>
    public bool IsDoNotDisturb
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
                        return state is QUERY_USER_NOTIFICATION_STATE.QUNS_QUIET_TIME or
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

    public event EventHandler<bool>? StateChanged
    {
        add { }
        remove { }
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
    }
}
