namespace Koy.Core.Notifications;

/// <summary>
/// Çentikte gösterilecek bildirimlerin kuyruğu. Aynı anda tek kart; art arda gelenler sırada bekler (en çok
/// <see cref="MaxPending"/>, dolunca en eskisi düşer); çentik açıkken kart alınmaz; Koy'un kendi bildirimleri ve
/// Rahatsız Etme açıkken gelenler hiç kuyruğa girmez. Saf C#; yalnız UI iş parçacığında kullanılır.
/// </summary>
public sealed class NotificationQueue(TimeProvider? clock = null, string ownAppName = NotificationQueue.DefaultOwnAppName)
{
    public const string DefaultOwnAppName = "Koy";

    /// <summary>Bir kartın ekranda kalma süresi; kapanışı kartın sahibi (OSD zamanlayıcısı) yürütür.</summary>
    public static readonly TimeSpan CardDuration = TimeSpan.FromSeconds(6);

    /// <summary>Bekleyen kart üst sınırı; kart gösterilirken sayılmaz.</summary>
    public const int MaxPending = 5;

    /// <summary>Bu süreden eski bildirim kartı artık anlamsızdır (çentik uzun süre açık kaldıysa): gösterilmeden düşer.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(2);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly List<NotificationItem> _pending = [];

    /// <summary>Şu an kartı gösterilen bildirim; yoksa null.</summary>
    public NotificationItem? Current { get; private set; }

    public int PendingCount => _pending.Count;

    /// <summary>
    /// Süzgeçten geçerse kuyruğa ekler. Süzülen: Koy'un kendi bildirimi, Rahatsız Etme açıkken gelen, boş içerikli,
    /// aynı kimlikle zaten kuyrukta ya da gösterilen. Dönüş: eklendi mi.
    /// </summary>
    public bool Enqueue(NotificationItem item, bool doNotDisturb)
    {
        if (doNotDisturb || IsOwn(item) || IsEmpty(item))
            return false;

        // Aynı kimlik ama ilerlemiş zaman = yerine konan yeni bildirim (kuyruğa girer); aynı damga = yinelenen okuma.
        if (IsSame(Current, item) || _pending.Any(p => IsSame(p, item)))
            return false;

        _pending.Add(item);
        if (_pending.Count > MaxPending)
            _pending.RemoveAt(0);
        return true;
    }

    /// <summary>
    /// Sıradaki kartı gösterilmek üzere alır; yoksa null. Zaten bir kart gösteriliyorsa ya da çentik açıksa
    /// (çağıran kapanınca yeniden sorar) null döner. Çok eskimiş öğeler atlanır.
    /// </summary>
    public NotificationItem? TryTakeNext(bool notchExpanded)
    {
        if (Current is not null || notchExpanded)
            return null;

        var now = _clock.GetUtcNow();
        _pending.RemoveAll(p => now - p.Time > MaxAge);
        if (_pending.Count == 0)
            return null;

        Current = _pending[0];
        _pending.RemoveAt(0);
        return Current;
    }

    /// <summary>Gösterilen kart bitti (süre doldu, × ile kapandı ya da başka gösterge yerini aldı).</summary>
    public void Complete() => Current = null;

    /// <summary>Bekleyenleri ve gösterileni bırakır (dinleyici durduğunda).</summary>
    public void Clear()
    {
        _pending.Clear();
        Current = null;
    }

    private static bool IsSame(NotificationItem? a, NotificationItem b) => a is not null && a.Id == b.Id && a.Time == b.Time;

    private bool IsOwn(NotificationItem item) =>
        string.Equals(item.AppName.Trim(), ownAppName, StringComparison.OrdinalIgnoreCase);

    private static bool IsEmpty(NotificationItem item) =>
        string.IsNullOrWhiteSpace(item.Title) && string.IsNullOrWhiteSpace(item.Body);
}
