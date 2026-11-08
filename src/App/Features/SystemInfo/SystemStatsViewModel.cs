using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Koy.App.Resources;
using Koy.Core.SystemStats;

namespace Koy.App.Features.SystemInfo;

/// <summary>
/// CPU, bellek, ağ ve Koy belleği; hem Sistem sayfası hem Ana'daki küçük kartlar bunu paylaşır.
/// Boşta %0 CPU: servis YALNIZ <see cref="SetActive"/>(true) ile alınır (çentik açık ve Ana ya da Sistem görünürken),
/// false olunca olaya abonelik kalkar ve servis bırakılır (son bırakan zamanlayıcıyı söker).
/// Olaylar arka plan iş parçacığından gelir; Dispatcher'a geçirilir.
/// </summary>
public sealed partial class SystemStatsViewModel : ObservableObject, IDisposable
{
    private const string NoValue = "–";

    private readonly Func<ISystemStatsService> _acquire;
    private readonly Action _release;
    private readonly Dispatcher _dispatcher;
    private ISystemStatsService? _service;
    private bool _disposed;

    // Servis her görünüşte yeniden kurulur ve ilk örneğe kadar boştur; son tam örnek burada kalır ki
    // sekmeye dönünce "–" yerine hemen son değerler görünsün (yalnız bir sayı, bellek maliyeti yok).
    private SystemSnapshot? _lastKnown;

    /// <param name="acquire">Servisi alır (SharedServices.AcquireSystemStats); çağrı sayılır.</param>
    /// <param name="release">Aldığı servisi bırakır (SharedServices.ReleaseSystemStats).</param>
    public SystemStatsViewModel(Func<ISystemStatsService> acquire, Action release, Dispatcher? dispatcher = null)
    {
        _acquire = acquire;
        _release = release;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        Apply(SystemSnapshot.Empty);
    }

    /// <summary>Servis şu an alınmış mı (testler ve yaşam döngüsü kontrolü için).</summary>
    public bool IsActive => _service is not null;

    [ObservableProperty]
    private double _cpuProgress;

    [ObservableProperty]
    private string _cpuText = NoValue;

    /// <summary>"En çok: Chrome"; süreç henüz bilinmiyorsa boş.</summary>
    [ObservableProperty]
    private string _topProcessText = "";

    [ObservableProperty]
    private double _ramProgress;

    [ObservableProperty]
    private string _ramText = NoValue;

    /// <summary>"9,2 / 16 GB".</summary>
    [ObservableProperty]
    private string _memoryDetailText = "";

    [ObservableProperty]
    private string _networkName = "";

    [ObservableProperty]
    private string _downloadText = "";

    [ObservableProperty]
    private string _uploadText = "";

    /// <summary>Koy'un özel çalışma kümesi ("64 MB").</summary>
    [ObservableProperty]
    private string _koyMemoryText = NoValue;

    /// <summary>
    /// Görünürlüğe göre servisi alır ya da bırakır (idempotent). Alırken anlık değerler (RAM...) hemen okunur,
    /// CPU / ağ hızı gibi iki örnek isteyenler ilk yeni örneğe kadar son bilinen değerle gösterilir.
    /// </summary>
    public void SetActive(bool active)
    {
        if (_disposed || active == (_service is not null))
            return;

        if (active)
        {
            var service = _acquire();
            _service = service;
            service.Updated += OnUpdated;
            Apply(service.Current);
        }
        else
        {
            Deactivate();
        }
    }

    private void Deactivate()
    {
        if (_service is null)
            return;

        _service.Updated -= OnUpdated;
        _service = null;
        _release();
    }

    private void OnUpdated(object? sender, SystemSnapshot snapshot)
    {
        _dispatcher.InvokeAsync(() =>
        {
            // Bırakıldıktan sonra kuyruktaki olay gelirse yok sayılır.
            if (!_disposed && _service is not null)
                Apply(snapshot);
        });
    }

    private void Apply(SystemSnapshot raw)
    {
        var s = SystemSnapshotMerge.Merge(raw, _lastKnown);
        if (raw.RatesReady)
            _lastKnown = raw;

        // "–" yalnız hiç değer yokken: anlık veri (RAM) ya da oran (CPU, ağ hızı) henüz bir kez bile gelmediyse.
        var hasData = SystemSnapshotMerge.HasInstantData(s);
        var hasRates = hasData && s.RatesReady;

        CpuProgress = Math.Clamp(s.CpuPercent, 0, 100) / 100;
        CpuText = hasRates ? SystemFormat.Percent(s.CpuPercent, CultureInfo.CurrentCulture) : NoValue;
        TopProcessText = string.IsNullOrWhiteSpace(s.TopProcessName)
            ? ""
            : string.Format(Strings.Get("SystemTopProcess"), s.TopProcessName);

        RamProgress = Math.Clamp(s.RamPercent, 0, 100) / 100;
        RamText = hasData ? SystemFormat.Percent(s.RamPercent, CultureInfo.CurrentCulture) : NoValue;
        MemoryDetailText = hasData ? SystemFormat.Memory(s.RamUsedBytes, s.RamTotalBytes, CultureInfo.CurrentCulture) : "";

        NetworkName = string.IsNullOrWhiteSpace(s.NetworkName) ? Strings.Get("SystemNoNetwork") : s.NetworkName;
        DownloadText = "↓ " + (hasRates ? SystemFormat.Speed(s.NetDownBytesPerSec, CultureInfo.CurrentCulture) : NoValue);
        UploadText = "↑ " + (hasRates ? SystemFormat.Speed(s.NetUpBytesPerSec, CultureInfo.CurrentCulture) : NoValue);

        KoyMemoryText = s.KoyMemoryBytes > 0 ? SystemFormat.Size(s.KoyMemoryBytes, CultureInfo.CurrentCulture) : NoValue;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Deactivate();
        _disposed = true;
    }
}
