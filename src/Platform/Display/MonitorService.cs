using Koy.Core.Display;
using Koy.Core.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Koy.Platform.Display;

/// <summary>
/// Bağlı monitörleri EnumDisplayMonitors ve GetDpiForMonitor ile sorgulayan,
/// çözünürlük veya monitör değişikliklerinde (WM_DISPLAYCHANGE) pencerelerin yeniden konumlandırılmasını sağlayan servis.
/// </summary>
public sealed class MonitorService : IMonitorService
{
    private const uint MONITORINFOF_PRIMARY = 1;
    private bool _started;
    private bool _disposed;

    public event EventHandler? MonitorsChanged;

    // Son bildirimdeki ekran düzeni; pencerelerden ve sistem olaylarından (App tarafı bağlar) gelen tekrarlar aynı düzen için bir kez iletilir.
    private string _lastSignature = "";

    public void Start()
    {
        if (_started || _disposed) return;
        _started = true;
        _lastSignature = Signature(GetMonitors());
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
    }

    public void NotifyDisplayChanged()
    {
        if (!_started || _disposed) return;

        var signature = Signature(GetMonitors());
        if (signature == _lastSignature) return;
        _lastSignature = signature;
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Kimlik, sınırlar, DPI ve ana ekran imzası (çalışma alanı hariç: Core/MonitorLayoutSignature).</summary>
    private static string Signature(IReadOnlyList<DisplayMonitor> monitors) => MonitorLayoutSignature.Compute(monitors);

    public unsafe IReadOnlyList<DisplayMonitor> GetMonitors()
    {
        var list = new List<DisplayMonitor>();
        var configs = DisplayConfigReader.Read();

        PInvoke.EnumDisplayMonitors(
            default(HDC),
            (RECT*)null,
            (HMONITOR hMonitor, HDC _, RECT* _, LPARAM _) =>
            {
                var mi = new MONITORINFOEXW
                {
                    monitorInfo = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFOEXW) }
                };

                if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&mi))
                {
                    var isPrimary = (mi.monitorInfo.dwFlags & MONITORINFOF_PRIMARY) != 0;
                    var deviceName = mi.szDevice.ToString();

                    // DPI ölçeği oku (varsayılan 96 DPI = 1.0)
                    double dpiScaleX = 1.0;
                    double dpiScaleY = 1.0;

                    try
                    {
                        uint dpiX = 96, dpiY = 96;
                        if (PInvoke.GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &dpiX, &dpiY) == 0)
                        {
                            dpiScaleX = dpiX / 96.0;
                            dpiScaleY = dpiY / 96.0;
                        }
                    }
                    catch
                    {
                        // DPI API hatası durumunda 1.0 kullanılır
                    }

                    var bounds = new WindowRect(
                        mi.monitorInfo.rcMonitor.left,
                        mi.monitorInfo.rcMonitor.top,
                        mi.monitorInfo.rcMonitor.right,
                        mi.monitorInfo.rcMonitor.bottom);

                    var workArea = new WindowRect(
                        mi.monitorInfo.rcWork.left,
                        mi.monitorInfo.rcWork.top,
                        mi.monitorInfo.rcWork.right,
                        mi.monitorInfo.rcWork.bottom);

                    // Kararlı kimlik: DisplayConfig yolu (çözünürlük, sıra ve aygıt adı değişse de aynı kalır).
                    // Okunamazsa Windows aygıt adına düşülür.
                    var hasConfig = configs.TryGetValue(deviceName, out var config) && config.DevicePath.Length > 0;
                    var id = hasConfig ? config.DevicePath : deviceName;
                    var name = hasConfig && config.FriendlyName.Length > 0
                        ? config.FriendlyName
                        : FallbackName(deviceName);

                    list.Add(new DisplayMonitor(
                        Id: id,
                        Name: name,
                        Bounds: bounds,
                        WorkArea: workArea,
                        IsPrimary: isPrimary,
                        DpiScaleX: dpiScaleX,
                        DpiScaleY: dpiScaleY,
                        Handle: hMonitor.Value,
                        DeviceName: deviceName));
                }

                return true;
            }, 0);

        return list;
    }

    private static string FallbackName(string deviceName)
    {
        var digits = new string(deviceName.Where(char.IsAsciiDigit).ToArray());
        return digits.Length > 0 ? $"Display {digits}" : deviceName;
    }

    public DisplayMonitor? GetPrimaryMonitor()
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
    }

    public DisplayMonitor? GetMonitor(string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
            return GetPrimaryMonitor();

        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m =>
            string.Equals(m.Id, idOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Name, idOrName, StringComparison.OrdinalIgnoreCase)) ?? GetPrimaryMonitor();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
