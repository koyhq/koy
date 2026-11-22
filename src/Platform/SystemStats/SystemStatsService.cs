using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using Koy.Core.Diagnostics;
using Koy.Core.SystemStats;
using Koy.Platform.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.System.SystemInformation;

namespace Koy.Platform.SystemStats;

/// <summary>
/// CPU, RAM, ağ hızı ve Koy belleği örnekleyicisi. Zamanlayıcı yalnız <see cref="Start"/> ile kurulur,
/// <see cref="Stop"/> onu tamamen kaldırır; durmuşken sıfır iş. Her saniye yalnız ucuz çağrılar:
/// GetSystemTimes, GlobalMemoryStatusEx, GetIfTable2, K32GetProcessMemoryInfo (mikrosaniyeler).
///
/// "En çok CPU kullanan süreç" PAHALI: Process.GetProcesses her taramada onlarca-yüzlerce süreç nesnesi ve
/// TotalProcessorTime için süreç başına tutamaç açar (tek tarama on ms mertebesi, çöp üretir). Taban taraması
/// <see cref="Start"/>'ı çağıran (arayüz) iş parçacığında değil, ilk örnekte thread pool'da yapılır. Bu yüzden 3 saniyede
/// bir yapılır (<see cref="ProcessScanEveryTicks"/>); iki tarama arası değer sabit kalır. 1 sn'de bir yapmak
/// çentik açıkken bile gereksiz CPU yakardı; 3 sn gözle fark edilmez.
/// <see cref="Updated"/> zamanlayıcının thread pool iş parçacığından gelir (UI'ya Dispatcher ile geçirin).
/// </summary>
public sealed class SystemStatsService : ISystemStatsService
{
    private const int IntervalMs = 1000;
    private const int FirstTickMs = 400;
    private const int ProcessScanEveryTicks = 3;
    private const uint IfTypeLoopback = 24;
    private const uint IfTypeTunnel = 131;

    private readonly Func<string?> _networkName;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _disposed;
    private int _ticking;

    private ulong _prevIdle, _prevKernel, _prevUser;
    private ulong _prevIn, _prevOut;
    private long _prevTicks;
    private int _tick;
    private bool _needProcessBaseline;
    private Dictionary<int, double> _prevProcessCpu = [];
    private long _prevScanTicks;
    private string? _topName;
    private double _topPercent;
    private SystemSnapshot _current = SystemSnapshot.Empty;

    /// <param name="networkName">Bağlı ağ adını verir (INetworkService.Current.Name); null verilirse ad boş kalır.</param>
    public SystemStatsService(Func<string?>? networkName = null) => _networkName = networkName ?? (() => null);

    public bool IsRunning
    {
        get { lock (_gate) return _timer is not null; }
    }

    public SystemSnapshot Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<SystemSnapshot>? Updated;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _timer is not null)
                return;

            // Taban ölçüm: ilk saniye sonunda delta anlamlı olsun.
            ReadCpuTimes(out _prevIdle, out _prevKernel, out _prevUser);
            ReadNetworkCounters(out _prevIn, out _prevOut);
            _prevTicks = Stopwatch.GetTimestamp();
            _tick = 0;
            // Süreç taban taraması (yüzlerce tutamaç) çentik açılış yayında arayüzü tutmasın: ilk örnekte, thread pool'da yapılır.
            _needProcessBaseline = true;
            _prevProcessCpu = [];
            _prevScanTicks = _prevTicks;
            _topName = null;
            _topPercent = 0;
            // Anlık değerler (RAM, Koy belleği, ağ adı) hemen okunur; CPU ve hızlar iki örnek ister ve RatesReady=false kalır.
            _current = InstantSnapshot();
            // İlk gerçek örnek bir saniye beklemesin: CPU/ağ farkı gerçek süre üzerinden hesaplanır, 400 ms yeter.
            _timer = new Timer(_ => OnTick(), null, FirstTickMs, IntervalMs);
        }
    }

    public void Stop()
    {
        Timer? timer;
        lock (_gate)
        {
            timer = _timer;
            _timer = null;
            _prevProcessCpu = []; // tarama sözlüğünü bırak
            _current = SystemSnapshot.Empty;
        }

        timer?.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
            _disposed = true;
        Stop();
    }

    private void OnTick()
    {
        // Önceki tur bitmediyse atla (yavaş makinede üst üste binmesin).
        if (Interlocked.Exchange(ref _ticking, 1) == 1)
            return;

        try
        {
            lock (_gate)
            {
                if (_timer is null)
                    return;
            }

            // Kilit dışında: süreç taraması onlarca ms sürebilir, Stop() arayüzü bekletmesin.
            // Durum alanlarına yalnız bu tur (ticking koruması) dokunur.
            var snapshot = Sample();
            lock (_gate)
            {
                if (_timer is null)
                    return;
                _current = snapshot;
            }

            Updated?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            CrashLog.Write("SystemStatsService.OnTick", ex);
        }
        finally
        {
            Volatile.Write(ref _ticking, 0);
        }
    }

    private SystemSnapshot InstantSnapshot()
    {
        var (used, total, ramPercent) = ReadMemory();
        return new SystemSnapshot(
            0, null, 0, used, total, ramPercent, 0, 0,
            SafeNetworkName(), ProcessMemory.GetPrivateWorkingSetBytes(), RatesReady: false);
    }

    private static (long Used, long Total, double Percent) ReadMemory()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!PInvoke.GlobalMemoryStatusEx(ref mem))
            return (0, 0, 0);

        var total = (long)mem.ullTotalPhys;
        var used = total - (long)mem.ullAvailPhys;
        return (used, total, SystemStatsMath.Percent(used, total));
    }

    private SystemSnapshot Sample()
    {
        var nowTicks = Stopwatch.GetTimestamp();
        var elapsed = (nowTicks - _prevTicks) / (double)Stopwatch.Frequency;
        _prevTicks = nowTicks;

        ReadCpuTimes(out var idle, out var kernel, out var user);
        var cpu = SystemStatsMath.CpuPercent(_prevIdle, _prevKernel, _prevUser, idle, kernel, user);
        (_prevIdle, _prevKernel, _prevUser) = (idle, kernel, user);

        ReadNetworkCounters(out var inBytes, out var outBytes);
        var down = SystemStatsMath.BytesPerSecond(_prevIn, inBytes, elapsed);
        var up = SystemStatsMath.BytesPerSecond(_prevOut, outBytes, elapsed);
        (_prevIn, _prevOut) = (inBytes, outBytes);

        if (_needProcessBaseline)
        {
            // İlk tur: yalnız taban (en çok CPU henüz bilinmez); tarama sayacı buradan başlar.
            _prevProcessCpu = ScanProcesses(out _);
            _prevScanTicks = Stopwatch.GetTimestamp();
            _tick = 0;
            _needProcessBaseline = false;
        }
        else if (++_tick % ProcessScanEveryTicks == 0)
        {
            var current = ScanProcesses(out var samples);
            var scanElapsed = (nowTicks - _prevScanTicks) / (double)Stopwatch.Frequency;
            var top = TopProcessPicker.Pick(_prevProcessCpu, samples, scanElapsed, Environment.ProcessorCount);
            _topName = top?.Name;
            _topPercent = top?.Percent ?? 0;
            _prevProcessCpu = current;
            _prevScanTicks = nowTicks;
        }

        var (used, total, ramPercent) = ReadMemory();

        return new SystemSnapshot(
            cpu, _topName, _topPercent, used, total, ramPercent, down, up,
            SafeNetworkName(), ProcessMemory.GetPrivateWorkingSetBytes());
    }

    private string? SafeNetworkName()
    {
        try
        {
            return _networkName();
        }
        catch (Exception ex)
        {
            CrashLog.Write("SystemStatsService.NetworkName", ex);
            return null;
        }
    }

    private static unsafe void ReadCpuTimes(out ulong idle, out ulong kernel, out ulong user)
    {
        FILETIME i, k, u;
        if (PInvoke.GetSystemTimes(&i, &k, &u))
        {
            idle = ToUlong(i);
            kernel = ToUlong(k);
            user = ToUlong(u);
        }
        else
        {
            idle = kernel = user = 0;
        }
    }

    private static ulong ToUlong(FILETIME t) => ((ulong)(uint)t.dwHighDateTime << 32) | (uint)t.dwLowDateTime;

    /// <summary>
    /// Fiziksel ve çalışan arayüzlerin birikimli bayt toplamı. Sanal/tünel arayüzler (Hyper-V vEthernet, VPN)
    /// sayılmaz: aynı trafik fiziksel arayüzden de geçer, çift sayım olur. Loopback hariç.
    /// </summary>
    private static unsafe void ReadNetworkCounters(out ulong inBytes, out ulong outBytes)
    {
        inBytes = outBytes = 0;
        MIB_IF_TABLE2* table = null;
        try
        {
            if (PInvoke.GetIfTable2(&table) != 0 || table is null)
                return;

            for (uint i = 0; i < table->NumEntries; i++)
            {
                ref var row = ref table->Table[(int)i];
                var hardware = (row.InterfaceAndOperStatusFlags._bitfield & 0x1) != 0;
                if (!hardware || row.Type == IfTypeLoopback || row.Type == IfTypeTunnel)
                    continue;
                if (row.OperStatus != Windows.Win32.NetworkManagement.Ndis.IF_OPER_STATUS.IfOperStatusUp)
                    continue;

                inBytes += row.InOctets;
                outBytes += row.OutOctets;
            }
        }
        finally
        {
            if (table is not null)
                PInvoke.FreeMibTable(table);
        }
    }

    private static Dictionary<int, double> ScanProcesses(out List<ProcessCpuSample> samples)
    {
        samples = [];
        var map = new Dictionary<int, double>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var seconds = p.TotalProcessorTime.TotalSeconds;
                map[p.Id] = seconds;
                samples.Add(new ProcessCpuSample(p.Id, p.ProcessName, seconds));
            }
            catch
            {
                // Korumalı/kapanmış süreç: CPU süresi okunamaz, atla.
            }
            finally
            {
                p.Dispose();
            }
        }

        return map;
    }
}
