using Koy.Core.Shell;
using Koy.Platform.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Koy.Watchdog;

/// <summary>
/// Koy'un görev çubuğu bekçisi. `Koy.Watchdog.exe --pid N`: Koy süreci N'i bekler.
/// Koy düzgün kapanırsa (temiz çıkış olayı) hiçbir şey yapmaz; Koy ölürse Windows görev çubuğunu geri getirir
/// (AGENTS.md #7). Penceresi, konsolu ve ayar dosyası yoktur; durumu Koy'un yazdığı taskbar-state.json'dan okur.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch
        {
            // Gözcü hiçbir koşulda hata penceresi göstermemeli.
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (WatchdogPolicy.IsRestoreOnly(args))
            return RestoreOnly();

        if (!WatchdogPolicy.TryParseProcessId(args, out var koyPid))
            return 2;

        // Aynı anda tek gözcü.
        using var singleton = new Mutex(true, WatchdogPolicy.SingletonMutexName, out var createdNew);
        if (!createdNew)
            return 0;

        var result = Wait(koyPid);
        // Koy ölüp hemen yeniden açıldıysa (güncelleme, elle yeniden başlatma) durumu yeni örnek yönetir: geri yükleme onun
        // gizlemesiyle yarışıp AUTOHIDE'ı kayıtsız bırakabilirdi.
        if (WatchdogPolicy.Decide(result) == WatchdogAction.RestoreTaskbar && !KoyIsRunning())
        {
            try
            {
                // Koy'un kendi Shell_TrayWnd penceresi süreçle birlikte gider, yine de PID ile atlanır.
                TaskbarService.Restore((uint)koyPid);
            }
            catch
            {
                // Gözcü hiçbir koşulda hata penceresi göstermemeli.
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// `--restore-taskbar` (RunOnce, oturum açılışı): Koy çalışmıyorsa durum dosyasına göre görev çubuğunu geri getirir.
    /// Gezgin'in çubuğunu bekler (açılışta geç oluşur). Koy çalışıyorsa durumu o yönetir, dokunulmaz.
    /// </summary>
    private static int RestoreOnly()
    {
        // Watch kipindeki tek gözcü muteksini almaz: beklerken Koy açılırsa kendi gözcüsünü başlatabilmeli.
        using var singleton = new Mutex(true, WatchdogPolicy.RestoreOnlySingletonMutexName, out var createdNew);
        if (!createdNew)
            return 0;

        var deadline = Environment.TickCount64 + WatchdogPolicy.RestoreOnlyExplorerWaitMs;
        while (!TaskbarService.IsExplorerTaskbarReady)
        {
            if (Environment.TickCount64 >= deadline)
                return 0; // Durum dosyası kalır; Koy'un açılış Restore'u ya da sonraki oturum dener.

            Thread.Sleep(500);
        }

        if (KoyIsRunning())
            return 0;

        TaskbarService.Restore();
        return 0;
    }

    private static bool KoyIsRunning()
    {
        if (!Mutex.TryOpenExisting(WatchdogPolicy.KoySingleInstanceMutexName, out var koy))
            return false;

        koy.Dispose();
        return true;
    }

    private static unsafe WatchdogWaitResult Wait(int koyPid)
    {
        var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_SYNCHRONIZE, false, (uint)koyPid);
        if (process.IsNull)
            return WatchdogWaitResult.ProcessNotFound;

        EventWaitHandle? stopEvent = null;
        try
        {
            // Koy olayı gözcüyü başlatmadan önce oluşturur. Yoksa yalnız süreç beklenir.
            EventWaitHandle.TryOpenExisting(WatchdogPolicy.StopEventName(koyPid), out stopEvent);

            // Olay dizide önce: ikisi aynı anda hazırsa (Koy olayı kurup çıktı) temiz çıkış sayılır.
            var handles = stackalloc HANDLE[2];
            uint count;
            if (stopEvent is not null)
            {
                handles[0] = (HANDLE)stopEvent.SafeWaitHandle.DangerousGetHandle();
                handles[1] = process;
                count = 2;
            }
            else
            {
                handles[0] = process;
                count = 1;
            }

            var wait = PInvoke.WaitForMultipleObjects(new ReadOnlySpan<HANDLE>(handles, (int)count), false, uint.MaxValue);
            // WAIT_OBJECT_0 = 0; zaman aşımı/başarısızlık değerleri (WAIT_FAILED = 0xFFFFFFFF) dizin sınırının dışında kalır.
            var index = (uint)wait - (uint)WAIT_EVENT.WAIT_OBJECT_0;
            if (index >= count)
                return WatchdogWaitResult.Failed;

            if (stopEvent is not null && index == 0)
                return WatchdogWaitResult.StopRequested;

            return WatchdogWaitResult.ProcessExited;
        }
        finally
        {
            stopEvent?.Dispose();
            PInvoke.CloseHandle(process);
        }
    }
}
