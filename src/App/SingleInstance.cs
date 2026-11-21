using System.Threading;

namespace Koy.App;

/// <summary>
/// Oturum başına tek Koy süreci. İki Koy aynı anda görev çubuğunu gizleyip geri getirmeye ve sistem tepsisini
/// (Shell_TrayWnd) barındırmaya çalışır; biri çökünce öteki görev çubuğunu gizli bırakabilir (2026-11-07: kurulu
/// eski sürüm ile geliştirme derlemesi birlikte açılmıştı). İkinci açılış ilk sürece Koy penceresini açtırır ve çıkar.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    // Gözcünün "yalnız geri getir" kipi Koy'un çalışıp çalışmadığını bu adla anlar (WatchdogPolicy ile ortak).
    private const string MutexName = Koy.Core.Shell.WatchdogPolicy.KoySingleInstanceMutexName;
    private const string ActivateEventName = @"Local\Koy.SingleInstance.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>İlk süreçse sahipliği döndürür; başka Koy çalışıyorsa ona haber verir ve null döner.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        if (createdNew)
            return new SingleInstance(mutex, activate);

        activate.Set();
        activate.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>Başka bir açılış denemesinde çağrılır (iş parçacığı havuzundan; ayrı iş parçacığı tutulmaz).</summary>
    public void ListenForActivation(Action onActivate)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Sahiplik başka iş parçacığındaysa sistem süreç bitince bırakır.
        }

        _mutex.Dispose();
    }
}
