using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Registry;
using Koy.Core.Privacy;

namespace Koy.Platform.Privacy;

/// <summary>
/// Windows ConsentStore kayıtlarını RegNotifyChangeKeyValue ile olay tabanlı izleyen servis.
/// Periyodik yoklama (polling) yapmaz, boştayken %0 CPU harcar.
/// </summary>
public sealed class ConsentStorePrivacyService : IPrivacyMonitorService
{
    private const string WebcamKeyPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";
    private const string MicKeyPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    private const string ConsentStorePath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    private readonly object _lock = new();
    private Run? _run;
    private PrivacyState _currentState = PrivacyState.Empty;
    private bool _disposed;

    public PrivacyState CurrentState
    {
        get
        {
            lock (_lock)
                return _currentState;
        }
    }

    public event Action<PrivacyState>? StateChanged;

    /// <summary>
    /// Bir Start–Stop döngüsünün tanıtıcıları. İş parçacığı kendi Run'ını sahiplenir ve çıkarken bırakır;
    /// böylece Stop iş parçacığı hâlâ beklerken tanıtıcıları altından çekmez.
    /// </summary>
    private sealed class Run
    {
        public readonly ManualResetEvent Cancel = new(false);
        public readonly AutoResetEvent CamEvent = new(false);
        public readonly AutoResetEvent MicEvent = new(false);
        public readonly AutoResetEvent ParentEvent = new(false);
        public RegistryKey? Webcam;
        public RegistryKey? Mic;
        public RegistryKey? Parent;
        public bool CamArmed;
        public bool MicArmed;
        public bool ParentArmed;
        public Thread? Thread;

        public void Release()
        {
            Webcam?.Dispose();
            Mic?.Dispose();
            Parent?.Dispose();
            Cancel.Dispose();
            CamEvent.Dispose();
            MicEvent.Dispose();
            ParentEvent.Dispose();
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_disposed || _run is not null)
                return;

            var run = new Run
            {
                Webcam = TryOpen(WebcamKeyPath),
                Mic = TryOpen(MicKeyPath),
            };

            // İlk durumu hemen oku (çentik açılışta doğru noktayla başlasın).
            _currentState = QueryCurrentState(run);

            run.Thread = new Thread(() => WorkerLoop(run))
            {
                Name = "Koy.PrivacyMonitor",
                IsBackground = true
            };
            _run = run;
            run.Thread.Start();
        }
    }

    public void Stop()
    {
        Run? run;
        lock (_lock)
        {
            run = _run;
            _run = null;
        }

        if (run is null)
            return;

        run.Cancel.Set();
        // Tanıtıcıları iş parçacığı kendisi bırakır (WorkerLoop finally); burada yalnız bekleriz.
        run.Thread?.Join(2000);
    }

    private static RegistryKey? TryOpen(string path)
    {
        try { return Registry.CurrentUser.OpenSubKey(path, RegistryKeyPermissionCheck.ReadSubTree); }
        catch { return null; }
    }

    private void WorkerLoop(Run run)
    {
        var waitHandles = new WaitHandle[] { run.Cancel, run.CamEvent, run.MicEvent, run.ParentEvent };

        try
        {
            while (true)
            {
                // Eksik anahtar (ör. kamerasız makinede webcam) yoklanmaz: üst ConsentStore anahtarında
                // alt anahtar oluşturma bildirimi beklenir, oluşunca açılır.
                run.Webcam ??= TryOpen(WebcamKeyPath);
                run.Mic ??= TryOpen(MicKeyPath);

                // RegNotifyChangeKeyValue tek seferliktir: yalnız sinyali tüketilmiş olanı yeniden kur.
                if (!run.CamArmed && run.Webcam is not null)
                    run.CamArmed = Arm(ref run.Webcam, run.CamEvent, watchSubtree: true, REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET);
                if (!run.MicArmed && run.Mic is not null)
                    run.MicArmed = Arm(ref run.Mic, run.MicEvent, watchSubtree: true, REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET);

                if (run.Webcam is null || run.Mic is null)
                {
                    run.Parent ??= TryOpen(ConsentStorePath);
                    if (!run.ParentArmed && run.Parent is not null)
                        run.ParentArmed = Arm(ref run.Parent, run.ParentEvent, watchSubtree: false, REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_NAME);
                }

                // Bildirimler kurulduktan SONRA oku: okuma ile kurulum arasındaki yazımlar kaçmaz.
                Publish(QueryCurrentState(run));

                var index = WaitHandle.WaitAny(waitHandles);
                if (index == 0)
                    break;
                if (index == 1) run.CamArmed = false;
                else if (index == 2) run.MicArmed = false;
                else run.ParentArmed = false;

                // Windows başlangıç ve bitiş damgalarını birkaç milisaniye arayla yazabilir.
                if (run.Cancel.WaitOne(50))
                    break;
            }
        }
        catch
        {
            // Arka plan iş parçacığında yakalanmayan hata süreci düşürür; izleme sessizce durur.
        }
        finally
        {
            run.Release();
        }
    }

    private void Publish(PrivacyState newState)
    {
        bool changed;
        lock (_lock)
        {
            changed = newState != _currentState;
            if (changed)
                _currentState = newState;
        }

        if (!changed)
            return;

        try
        {
            StateChanged?.Invoke(newState);
        }
        catch
        {
        }
    }

    /// <summary>Bildirimi kurar; kurulamazsa anahtarı kapatıp null yapar (bir sonraki turda yeniden açılır ya da üst anahtar izlenir).</summary>
    private static bool Arm(ref RegistryKey? key, AutoResetEvent evt, bool watchSubtree, REG_NOTIFY_FILTER filter)
    {
        if (key is null)
            return false;

        try
        {
            var hKey = new HKEY(key.Handle.DangerousGetHandle());
            var hEvt = new HANDLE(evt.SafeWaitHandle.DangerousGetHandle());
            var result = PInvoke.RegNotifyChangeKeyValue(hKey, watchSubtree, filter, hEvt, fAsynchronous: true);
            if (result == WIN32_ERROR.ERROR_SUCCESS)
                return true;
        }
        catch
        {
        }

        key.Dispose();
        key = null;
        return false;
    }

    private static PrivacyState QueryCurrentState(Run run)
    {
        var (camInUse, camApp) = CheckDeviceUsage(run.Webcam);
        var (micInUse, micApp) = CheckDeviceUsage(run.Mic);

        return new PrivacyState(camInUse, micInUse, camApp, micApp);
    }

    private static (bool InUse, string? AppName) CheckDeviceUsage(RegistryKey? rootKey)
    {
        if (rootKey is null)
            return (false, null);

        try
        {
            var subKeyNames = rootKey.GetSubKeyNames();

            // 1. Doğrudan paketli (UWP) alt anahtarlar
            foreach (var name in subKeyNames)
            {
                if (name.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var sub = rootKey.OpenSubKey(name);
                if (sub is null)
                    continue;

                var (inUse, app) = CheckSingleKey(sub, name);
                if (inUse)
                    return (true, app);
            }

            // 2. Paketsiz (Win32 exe) alt anahtarlar
            using var nonPackaged = rootKey.OpenSubKey("NonPackaged");
            if (nonPackaged is not null)
            {
                foreach (var name in nonPackaged.GetSubKeyNames())
                {
                    using var sub = nonPackaged.OpenSubKey(name);
                    if (sub is null)
                        continue;

                    var (inUse, app) = CheckSingleKey(sub, name);
                    if (inUse)
                        return (true, app);
                }
            }
        }
        catch
        {
        }

        return (false, null);
    }

    private static (bool InUse, string? AppName) CheckSingleKey(RegistryKey key, string keyName)
    {
        try
        {
            var startObj = key.GetValue("LastUsedTimeStart");
            var stopObj = key.GetValue("LastUsedTimeStop");

            if (startObj is long start)
            {
                var stop = stopObj is long s ? s : 0L;
                if (CaptureUsagePolicy.IsActivelyUsing(start, stop))
                {
                    var appName = CaptureUsagePolicy.ExtractAppNameFromKey(keyName);
                    return (true, appName);
                }
            }
        }
        catch
        {
        }

        return (false, null);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        // Önce _disposed, sonra Stop: Stop artık _disposed'a bakmaz, izleyici gerçekten durur (eskiden hiç durmuyordu).
        Stop();
    }
}
