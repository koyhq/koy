using System.Management;
using Koy.Core.Display;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Koy.Platform.Display;

/// <summary>
/// Parlaklık: çentiğin bulunduğu ekran dahiliyse WMI, harici ise DDC/CI (bkz. <see cref="BrightnessSourcePicker"/>).
/// Kaynak kurulumda bir kez değil, <see cref="Refresh"/> her çağrıldığında yeniden belirlenir (ekran düzeni değişir).
/// Monitör tanıtıcısı tutulmaz; her çağrıda güncel ekrandan çözülür, kapanmış monitörün ölü tanıtıcısına yazılmaz.
/// Ayar çağrıları arka plandadır ve birleşir (<see cref="BrightnessWriteQueue"/>).
/// </summary>
public sealed class BrightnessService : IBrightnessService
{
    private readonly Func<DisplayMonitor?>? _monitorProvider;
    private readonly BrightnessWriteQueue _queue = new();
    private readonly object _sync = new();
    private ManagementEventWatcher? _watcher;
    private volatile bool _disposed;

    private BrightnessSourceKind _source;
    private int _brightness = 50;
    private int _ddcMin;
    private int _ddcMax = 100;

    public bool IsSupported => Source != BrightnessSourceKind.None;

    public int Brightness
    {
        get { lock (_sync) return _brightness; }
    }

    public BrightnessSourceKind Source
    {
        get { lock (_sync) return _source; }
    }

    public event EventHandler<int>? BrightnessChanged;
    public event EventHandler? AvailabilityChanged;

    /// <param name="watchExternalChanges">
    /// true ise parlaklık Koy dışından (Fn tuşları, Windows ayarları) değişince de <see cref="BrightnessChanged"/> yükselir.
    /// Yalnız dahili ekran (WMI) için; harici monitörlerde (DDC/CI) olay yoktur. Sürekli sorgulama yapılmaz, WMI olayı beklenir.
    /// </param>
    /// <param name="monitorProvider">
    /// Parlaklığı ayarlanacak ekranı o anki haliyle verir (çentiğin bulunduğu ekran). Null ise ekran bilinmez:
    /// önce WMI, yoksa birincil ekranda DDC/CI.
    /// </param>
    public BrightnessService(bool watchExternalChanges = false, Func<DisplayMonitor?>? monitorProvider = null)
    {
        _monitorProvider = monitorProvider;
        Refresh();
        if (watchExternalChanges && Source == BrightnessSourceKind.Wmi)
            StartWatching();
    }

    public void Refresh()
    {
        if (_disposed)
            return;

        var monitor = _monitorProvider?.Invoke();
        var target = ResolveTarget(monitor);

        var wmiOk = TryReadWmi(out var wmiValue);
        // Dahili hedefte WMI yeterliyse DDC/CI sorgusu (yüzlerce ms) atlanır.
        var ddcOk = false;
        int ddcValue = 0, ddcMin = 0, ddcMax = 100;
        if (!(wmiOk && target != BrightnessTarget.External))
            ddcOk = TryReadDdc(monitor, out ddcValue, out ddcMin, out ddcMax);

        var kind = BrightnessSourcePicker.Pick(target, wmiOk, ddcOk);

        lock (_sync)
        {
            _source = kind;
            switch (kind)
            {
                case BrightnessSourceKind.Wmi:
                    _brightness = wmiValue;
                    break;
                case BrightnessSourceKind.Ddc:
                    _ddcMin = ddcMin;
                    _ddcMax = ddcMax;
                    _brightness = BrightnessSourcePicker.ToPercent(ddcValue, ddcMin, ddcMax);
                    break;
            }
        }

        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetBrightness(int brightness)
    {
        if (_disposed || !IsSupported)
            return;

        if (_queue.Post(brightness))
            Task.Run(ApplyLoop);
    }

    // Tek işçi: kuyrukta son değer kalana kadar döner. Çağrı sürerken gelen değerler birleşir.
    private void ApplyLoop()
    {
        var failed = false;
        while (!_disposed && _queue.TryTake(out var value))
        {
            if (!Apply(value))
                failed = true;
        }

        // Yazım başarısız olduysa (ekran gitmiş, sürücü reddetti) kaynak yeniden belirlenir;
        // artık yol yoksa kaydırıcı devre dışı kalır.
        if (failed && !_disposed)
            Refresh();
    }

    private bool Apply(int percent)
    {
        BrightnessSourceKind kind;
        int min, max;
        lock (_sync)
        {
            kind = _source;
            min = _ddcMin;
            max = _ddcMax;
        }

        var ok = kind switch
        {
            BrightnessSourceKind.Wmi => TrySetWmi(percent),
            BrightnessSourceKind.Ddc => TrySetDdc(_monitorProvider?.Invoke(), BrightnessSourcePicker.FromPercent(percent, min, max)),
            _ => false
        };

        if (ok)
        {
            lock (_sync)
                _brightness = percent;
        }

        return ok;
    }

    private static BrightnessTarget ResolveTarget(DisplayMonitor? monitor)
    {
        if (monitor is null || monitor.DeviceName.Length == 0)
            return BrightnessTarget.Unknown;

        var info = DisplayConfigReader.Read();
        if (!info.TryGetValue(monitor.DeviceName, out var entry))
            return BrightnessTarget.Unknown;

        return entry.IsInternal ? BrightnessTarget.Internal : BrightnessTarget.External;
    }

    private static bool TryReadWmi(out int value)
    {
        value = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                if (mo["CurrentBrightness"] is { } val)
                {
                    value = Convert.ToInt32(val, System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
            }
        }
        catch
        {
            // WMI desteklenmiyor ya da masaüstü PC
        }

        return false;
    }

    private static bool TrySetWmi(int percent)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                mo.InvokeMethod("WmiSetBrightness", [1u, (byte)Math.Clamp(percent, 0, 100)]);
                return true;
            }
        }
        catch
        {
            // Yazılamadı: çağıran kaynağı yeniden belirler.
        }

        return false;
    }

    // Verilen ekranın (null ise birincil ekranın) ilk fiziksel monitörü üzerinde işlem yapar.
    // Tanıtıcı her seferinde güncel ekrandan alınır ve hemen bırakılır.
    private static unsafe bool WithPhysicalMonitor(DisplayMonitor? monitor, Func<Microsoft.Win32.SafeHandles.SafeFileHandle, bool> action)
    {
        try
        {
            HMONITOR hMon;
            if (monitor is not null)
            {
                var b = monitor.Bounds;
                hMon = PInvoke.MonitorFromPoint(
                    new System.Drawing.Point((b.Left + b.Right) / 2, (b.Top + b.Bottom) / 2),
                    MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            }
            else
            {
                hMon = PInvoke.MonitorFromWindow(Windows.Win32.Foundation.HWND.Null, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
            }

            if (hMon == 0 || !PInvoke.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out var count) || count == 0)
                return false;

            var monitors = new Windows.Win32.Devices.Display.PHYSICAL_MONITOR[count];
            fixed (Windows.Win32.Devices.Display.PHYSICAL_MONITOR* pMonitors = monitors)
            {
                if (!PInvoke.GetPhysicalMonitorsFromHMONITOR(hMon, count, pMonitors))
                    return false;

                try
                {
                    using var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(monitors[0].hPhysicalMonitor.Value, ownsHandle: false);
                    return action(safeHandle);
                }
                finally
                {
                    PInvoke.DestroyPhysicalMonitors(count, pMonitors);
                }
            }
        }
        catch
        {
            // DDC/CI desteklenmiyor
            return false;
        }
    }

    private static bool TryReadDdc(DisplayMonitor? monitor, out int value, out int min, out int max)
    {
        int v = 0, lo = 0, hi = 100;
        var ok = WithPhysicalMonitor(monitor, handle =>
        {
            if (PInvoke.GetMonitorBrightness(handle, out var mn, out var cur, out var mx) == 0)
                return false;
            lo = (int)mn;
            v = (int)cur;
            hi = (int)mx;
            return true;
        });
        value = v;
        min = lo;
        max = hi;
        return ok;
    }

    private static bool TrySetDdc(DisplayMonitor? monitor, int raw) =>
        WithPhysicalMonitor(monitor, handle => PInvoke.SetMonitorBrightness(handle, (uint)raw) != 0);

    private void StartWatching()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
            _watcher = new ManagementEventWatcher(new ManagementScope(@"root\wmi"), query);
            _watcher.EventArrived += OnBrightnessEvent;
            _watcher.Start();
        }
        catch
        {
            // WMI olayı kurulamazsa yalnız Koy içi değişiklikler bildirilir.
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private void OnBrightnessEvent(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var value = Convert.ToInt32(e.NewEvent[nameof(Brightness)], System.Globalization.CultureInfo.InvariantCulture);
            lock (_sync)
            {
                if (value == _brightness)
                    return;
                _brightness = value;
            }

            BrightnessChanged?.Invoke(this, value);
        }
        catch
        {
            // Bozuk olay verisi göstergeyi düşürmesin.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.EventArrived -= OnBrightnessEvent;
            try
            {
                _watcher.Stop();
            }
            catch
            {
                // Zaten durmuş olabilir.
            }
            _watcher.Dispose();
            _watcher = null;
        }
    }
}
