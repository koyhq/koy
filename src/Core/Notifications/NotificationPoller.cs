namespace Koy.Core.Notifications;

/// <summary>
/// Windows bildirim listesini sabit aralıkla yoklar ve yeni gelenleri <see cref="Arrived"/> ile bildirir.
/// Paketsiz süreçte <c>NotificationChanged</c> olayı kurulamadığı için tek yol budur (bkz. AGENTS #5 istisnası).
/// Döngü tek görevdir: bir yoklama bitmeden sonraki başlamaz, iş thread pool'da yürür (UI iş parçacığı değil).
/// Zamanlayıcı yalnız <see cref="Start"/> ile kurulur, <see cref="Stop"/> ile sökülür.
/// </summary>
/// <param name="read">
/// Listeyi okur. <c>known</c> null ise temel çizgi okumasıdır: kaynak yalnız kimlikleri döner, ayrıntı üretmez.
/// Aksi halde en azından <c>known</c> içinde olmayan kimliklerin ayrıntısını verir.
/// </param>
public sealed class NotificationPoller(
    Func<IReadOnlySet<uint>?, CancellationToken, Task<NotificationSnapshot>> read,
    TimeProvider? clock = null) : IDisposable
{
    /// <summary>
    /// Yoklama aralığı. 2 sn: Windows'un köşe başlığı birkaç sn görünür, bildirim kartı en çok 2 sn gecikmeyle çıkar;
    /// tek bir WinRT liste çağrısı olduğundan CPU maliyeti çok küçük. Daha sık yoklamak kazanç getirmez, daha seyrek
    /// yoklamak bildirimi geç gösterir.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    /// <summary>Okuma hata verirse (izin kalktı, Windows servisi yanıtsız) bir sonraki deneme bu kadar sonra.</summary>
    public static readonly TimeSpan FailureInterval = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;

    /// <summary>Yeni bildirim geldi; thread pool iş parçacığından tetiklenir.</summary>
    public event Action<NotificationItem>? Arrived;

    public bool IsRunning
    {
        get { lock (_gate) return _cts is not null; }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_cts is not null)
                return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            // Her çalıştırmanın kendi farkı var: yeniden başlatmada ilk okuma yine temel çizgidir.
            var diff = new NotificationDiff();
            _ = Task.Run(() => RunAsync(diff, token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _cts;
            _cts = null;
        }

        if (cts is null)
            return;

        cts.Cancel();
        cts.Dispose();
    }

    private async Task RunAsync(NotificationDiff diff, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var delay = Interval;
            try
            {
                await PollOnceAsync(diff, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                delay = FailureInterval;
            }

            try
            {
                await Task.Delay(delay, _clock, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Tek yoklama; yeni gelenleri <see cref="Arrived"/> ile bildirir. Testlerde doğrudan çağrılır.</summary>
    public async Task PollOnceAsync(NotificationDiff diff, CancellationToken token)
    {
        var snapshot = await read(diff.HasBaseline ? diff.Known : null, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
            return;

        foreach (var item in diff.Apply(snapshot))
            Arrived?.Invoke(item);
    }

    public void Dispose() => Stop();
}
