using System.IO;
using System.Windows.Threading;
using Koy.Core.Appearance;

namespace Koy.App.Resources;

/// <summary>
/// user-theme.json değişince kaynakları yeniler. YALNIZ Koy ayarlar penceresi açıkken yaşar (pencere görününce kurulur,
/// gizlenince <see cref="Dispose"/> edilir; AGENTS #5). Yoklama zamanlayıcısı yok: olay tabanlı FileSystemWatcher ve
/// olay yağmurunu (editörler dosyayı birkaç olayla yazar) tek yeniden yüklemeye indiren, her olayda yenilenen tek seferlik gecikme.
/// Yalnızca bu dosya adı izlenir; ayar klasöründeki settings.json ve diğer dosyaların yazımları süzülür, dosya gerçekten
/// değişmediyse (boyut ve son yazım aynı) yeniden yükleme de yapılmaz (bkz. ThemeManager.ReloadUserStyleIfChanged).
/// </summary>
public sealed class UserStyleWatcher : IDisposable
{
    private const int DebounceMs = 300;

    private readonly Dispatcher _dispatcher;
    private readonly Action _reload;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _pending;
    private readonly object _gate = new();
    private volatile bool _disposed;

    /// <param name="reload">UI iş parçacığında çağrılır.</param>
    public UserStyleWatcher(Dispatcher dispatcher, Action reload)
    {
        _dispatcher = dispatcher;
        _reload = reload;
        Start();
    }

    private void Start()
    {
        try
        {
            // İzlenecek klasör yoksa oluşturulur (yalnız klasör; dosya kullanıcıya aittir ve Koy yazmaz).
            Directory.CreateDirectory(UserStyleFile.DefaultFolder);

            _watcher = new FileSystemWatcher(UserStyleFile.DefaultFolder, UserStyleFile.FileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = false
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException)
        {
            // İzleme başarısızsa "Yeniden yükle" düğmesi yine çalışır.
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            if (_disposed)
                return;
            // İptal edilen kaynak bırakılır, Dispose edilmez: bekleyen Task.Delay'in kaydı güvenle sonlanır.
            _pending?.Cancel();
            cts = _pending = new CancellationTokenSource();
        }

        _ = DebounceAsync(cts.Token);
    }

    private async Task DebounceAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(DebounceMs, token).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed)
                    _reload();
            });
        }
        catch (OperationCanceledException)
        {
            // Daha yeni bir olay geldi ya da izleyici bırakıldı.
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _pending?.Cancel();
            _pending = null;
        }

        if (_watcher is { } w)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
            _watcher = null;
        }
    }
}
