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

    private readonly object _lock = new();
    private Thread? _workerThread;
    private AutoResetEvent? _camEvent;
    private AutoResetEvent? _micEvent;
    private ManualResetEvent? _cancelEvent;
    private RegistryKey? _webcamKey;
    private RegistryKey? _micKey;
    private PrivacyState _currentState = PrivacyState.Empty;
    private bool _started;
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

    public void Start()
    {
        lock (_lock)
        {
            if (_disposed || _started)
                return;

            _started = true;
            _cancelEvent = new ManualResetEvent(false);
            _camEvent = new AutoResetEvent(false);
            _micEvent = new AutoResetEvent(false);

            try
            {
                _webcamKey = Registry.CurrentUser.OpenSubKey(WebcamKeyPath, RegistryKeyPermissionCheck.ReadSubTree);
            }
            catch
            {
                _webcamKey = null;
            }

            try
            {
                _micKey = Registry.CurrentUser.OpenSubKey(MicKeyPath, RegistryKeyPermissionCheck.ReadSubTree);
            }
            catch
            {
                _micKey = null;
            }

            // İlk durumu hemen oku
            _currentState = QueryCurrentState();

            _workerThread = new Thread(WorkerLoop)
            {
                Name = "Koy.PrivacyMonitor",
                IsBackground = true
            };
            _workerThread.Start();
        }
    }

    public void Stop()
    {
        Thread? threadToJoin = null;
        lock (_lock)
        {
            if (!_started || _disposed)
                return;

            _started = false;
            _cancelEvent?.Set();
            threadToJoin = _workerThread;
            _workerThread = null;
        }

        threadToJoin?.Join(1000);

        lock (_lock)
        {
            _webcamKey?.Dispose();
            _webcamKey = null;
            _micKey?.Dispose();
            _micKey = null;

            _cancelEvent?.Dispose();
            _cancelEvent = null;
            _camEvent?.Dispose();
            _camEvent = null;
            _micEvent?.Dispose();
            _micEvent = null;
        }
    }

    private void WorkerLoop()
    {
        var waitHandles = new WaitHandle[] { _cancelEvent!, _camEvent!, _micEvent! };

        while (true)
        {
            // Olayları kaydet (RegNotifyChangeKeyValue tek seferliktir; sinyalden sonra tekrar kurulmalı)
            RegisterKeyNotification(_webcamKey, _camEvent);
            RegisterKeyNotification(_micKey, _micEvent);

            var index = WaitHandle.WaitAny(waitHandles);
            if (index == 0) // cancelEvent
                break;

            // Kısa bir debounce/tolerans: Windows başlangıç ve bitiş damgalarını birkaç milisaniye arayla yazabilir
            Thread.Sleep(50);

            var newState = QueryCurrentState();
            bool changed;
            lock (_lock)
            {
                changed = newState != _currentState;
                if (changed)
                    _currentState = newState;
            }

            if (changed)
            {
                try
                {
                    StateChanged?.Invoke(newState);
                }
                catch
                {
                }
            }
        }
    }

    private static void RegisterKeyNotification(RegistryKey? key, AutoResetEvent? evt)
    {
        if (key is null || evt is null)
            return;

        try
        {
            var hKey = new HKEY(key.Handle.DangerousGetHandle());
            var hEvt = new HANDLE(evt.SafeWaitHandle.DangerousGetHandle());

            PInvoke.RegNotifyChangeKeyValue(
                hKey,
                bWatchSubtree: true,
                REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET,
                hEvt,
                fAsynchronous: true);
        }
        catch
        {
        }
    }

    private PrivacyState QueryCurrentState()
    {
        var (camInUse, camApp) = CheckDeviceUsage(_webcamKey);
        var (micInUse, micApp) = CheckDeviceUsage(_micKey);

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
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}
