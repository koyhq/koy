using System.ComponentModel;
using System.Diagnostics;
using Koy.Core.Diagnostics;
using Koy.Core.StartMenu;
using Koy.Platform.Shell;
using Windows.Win32;
using Windows.Win32.System.Power;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Koy.Platform.StartMenu;

/// <summary>Başlat sağ tık menüsünün Windows tarafı: araç başlatma, Win+D / Win+S / Win+R ve pil sorgusu. Durumsuzdur, arka plan işi yok.</summary>
public sealed class StartMenuHost : IStartMenuHost
{
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorNoAssociation = 1155;
    private const int ErrorCancelled = 1223; // UAC istemi reddedildi

    private const byte NoSystemBattery = 128;

    public unsafe bool HasBattery
    {
        get
        {
            SYSTEM_POWER_STATUS status;
            // Sorgu başarısızsa pil yok sayılır: Mobilite Merkezi gereksiz yere çıkmasın.
            return PInvoke.GetSystemPowerStatus(&status) && (status.BatteryFlag & NoSystemBattery) == 0;
        }
    }

    public ToolLaunchResult Launch(string target, string? arguments, bool elevated)
    {
        try
        {
            var info = new ProcessStartInfo(target)
            {
                UseShellExecute = true,
                // Çalışma klasörü Koy'un kurulum klasörü olmasın; Windows'un kendi menüsü gibi kullanıcı klasöründe açılır.
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            if (!string.IsNullOrEmpty(arguments))
                info.Arguments = arguments;
            if (elevated)
                info.Verb = "runas";

            Process.Start(info)?.Dispose();
            return ToolLaunchResult.Started;
        }
        catch (Win32Exception ex)
        {
            switch (ex.NativeErrorCode)
            {
                case ErrorCancelled:
                    return ToolLaunchResult.Declined;
                case ErrorFileNotFound or ErrorPathNotFound or ErrorNoAssociation:
                    return ToolLaunchResult.NotFound;
                default:
                    CrashLog.Write("Başlat menüsü aracı: " + target, ex);
                    return ToolLaunchResult.Failed;
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("Başlat menüsü aracı: " + target, ex);
            return ToolLaunchResult.Failed;
        }
    }

    // Masaüstünü göster mevcut bir servis değil; Windows'un kendi kısayolu (Win+D) kullanılır.
    public void ShowDesktop() => ShellActionService.SendWinCombo(VIRTUAL_KEY.VK_D);

    public void OpenSearch() => ShellActionService.SendWinCombo(VIRTUAL_KEY.VK_S);

    public void OpenRunDialog() => ShellActionService.SendWinCombo(VIRTUAL_KEY.VK_R);
}
