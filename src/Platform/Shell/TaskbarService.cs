using System.IO;
using Koy.Core.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// Windows görev çubuğunu gizler ve geri getirir.
/// Güvenlik kuralı (AGENTS.md #7): Çökse bile görev çubuğu geri gelmeli.
/// Görev çubuğu gizlendiğinde durum %AppData%\Koy\taskbar-state.json dosyasına yazılır,
/// geri getirildiğinde ise bu dosya silinir. Açılışta bu dosya kontrol edilir.
/// Bu dosya Koy.Watchdog projesine de bağlanarak derlenir (gözcü, Koy ölünce aynı Restore'u çalıştırır); bu yüzden
/// yalnız Core ve Win32'ye bağımlı kalır. Koy'a özel kısım (gözcüyü başlatma/durdurma) TaskbarService.Watchdog.cs
/// içindeki kısmi yöntemlerdedir.
/// </summary>
public static partial class TaskbarService
{
    private const uint ABM_GETSTATE = 0x00000004;
    private const uint ABM_SETSTATE = 0x0000000A;
    private const int ABS_AUTOHIDE = TaskbarStatePolicy.AutoHide;

    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy",
        "taskbar-state.json");

    /// <summary>
    /// Görev çubuğunu gizler. Birden çok kez çağrılabilir (idempotent): zaten gizliyken kayıtlı "önceki durum"
    /// korunur, böylece bizim koyduğumuz AUTOHIDE kullanıcının gerçek durumu sanılıp üzerine yazılmaz.
    /// Explorer yeniden başlayınca da aynı çağrı gizlemeyi yeniden uygular.
    /// </summary>
    public static unsafe void Hide()
    {
        var saved = ReadSavedState();
        var primary = ShellTrayLocator.FindExplorerTray();
        var prevState = saved is { Hidden: true } s ? s.PreviousState : TaskbarStatePolicy.AlwaysOnTop;

        if (primary != HWND.Null)
        {
            var data = new APPBARDATA
            {
                cbSize = (uint)sizeof(APPBARDATA),
                hWnd = primary
            };
            var current = (int)PInvoke.SHAppBarMessage(ABM_GETSTATE, ref data);
            prevState = TaskbarStatePolicy.ChoosePreviousState(saved, current);

            // Zaten otomatik gizlemedeyse yeniden SETSTATE gönderilmez: Gezgin her SETSTATE'te çalışma alanını yeniden
            // hesaplar ve dock'un AppBar payını bir an düşürür (büyütülmüş pencereler sıçrar).
            if ((current & ABS_AUTOHIDE) == 0)
            {
                data.lParam = new LPARAM((nint)ABS_AUTOHIDE);
                PInvoke.SHAppBarMessage(ABM_SETSTATE, ref data);
            }
        }

        SaveState(new TaskbarSavedState(true, prevState));
        SetVisible(false);
        OnHidden();
    }

    /// <summary>
    /// Görev çubuğunu (birincil ve bütün ikincil monitörlerdekileri) geri getirir ve durumu önceki haline döndürür.
    /// Explorer çalışmıyorsa (birincil çubuk yok) durum dosyası silinmez; sonraki açılış yeniden dener.
    /// </summary>
    /// <param name="alsoSkipProcessId">Gözcü için: ölen Koy sürecinin tepsi penceresini atla.</param>
    /// <param name="force">Kurtarma amaçlı: kayıtlı durum olmasa bile ABM_SETSTATE gönder.</param>
    public static unsafe void Restore(uint alsoSkipProcessId = 0, bool force = false)
    {
        // Temiz geri yüklemede gözcü gereksiz: önce durdurulur ki aynı işi ikinci kez yapmasın.
        OnRestoring();

        SetVisible(true);

        var saved = ReadSavedState();
        if (!TaskbarStatePolicy.ShouldWriteState(saved, force))
        {
            DeleteState();
            return;
        }

        var primary = ShellTrayLocator.FindExplorerTray(alsoSkipProcessId);
        if (primary == HWND.Null)
            return;

        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = primary,
            lParam = new LPARAM((nint)TaskbarStatePolicy.ResolveRestoreState(saved))
        };
        PInvoke.SHAppBarMessage(ABM_SETSTATE, ref data);

        DeleteState();
    }

    /// <summary>
    /// Gezgin, otomatik gizleme durumu değişince ya da kendi kendine görev çubuğunu yeniden gösterir;
    /// o zaman alt kenarda bizim dock'la birlikte ikinci bir çubuk çıkar. Birincil veya ikincil
    /// monitörlerdeki görev çubuklarından herhangi biri görünür olduysa tekrar gizler.
    /// </summary>
    public static bool EnsureHidden()
    {
        var primary = ShellTrayLocator.FindExplorerTray();
        if (primary != HWND.Null && PInvoke.IsWindowVisible(primary))
        {
            SetVisible(false);
            return true;
        }

        var secondary = HWND.Null;
        while ((secondary = PInvoke.FindWindowEx(HWND.Null, secondary, "Shell_SecondaryTrayWnd", null)) != HWND.Null)
        {
            if (PInvoke.IsWindowVisible(secondary))
            {
                SetVisible(false);
                return true;
            }
        }

        return false;
    }

    // Koy'da gözcüyü başlatır/durdurur; gözcü projesinde gövde yoktur (derleyici çağrıyı siler).
    static partial void OnHidden();

    static partial void OnRestoring();

    private static void SetVisible(bool visible)
    {
        var command = visible ? SHOW_WINDOW_CMD.SW_SHOW : SHOW_WINDOW_CMD.SW_HIDE;

        var primary = ShellTrayLocator.FindExplorerTray();
        if (primary != HWND.Null)
            PInvoke.ShowWindow(primary, command);

        // Diğer monitörlerdeki görev çubukları.
        var secondary = HWND.Null;
        while ((secondary = PInvoke.FindWindowEx(HWND.Null, secondary, "Shell_SecondaryTrayWnd", null)) != HWND.Null)
            PInvoke.ShowWindow(secondary, command);
    }

    private static void SaveState(TaskbarSavedState state)
    {
        try
        {
            var dir = Path.GetDirectoryName(StateFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(StateFilePath, TaskbarStatePolicy.Serialize(state, DateTime.UtcNow));
        }
        catch
        {
            // Durum dosyası yazılamasa bile ana iş akışı kesilmez
        }
    }

    private static TaskbarSavedState? ReadSavedState()
    {
        try
        {
            if (File.Exists(StateFilePath))
                return TaskbarStatePolicy.Parse(File.ReadAllText(StateFilePath));
        }
        catch
        {
        }
        return null;
    }

    private static void DeleteState()
    {
        try
        {
            if (File.Exists(StateFilePath))
                File.Delete(StateFilePath);
        }
        catch
        {
            // Silme hatası yutulur
        }
    }
}
