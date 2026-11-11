namespace Koy.Core.Notifications;

/// <summary>Listedeki bir bildirimin kimliği ve oluşturulma zamanı. Aynı kimlik güncellenirse zaman ilerler.</summary>
public readonly record struct NotificationStamp(uint Id, DateTimeOffset Time);

/// <summary>Bir yoklamanın sonucu: şu an listede olan bütün bildirimlerin damgası ve (en az) yeni ya da güncellenmiş olanların ayrıntısı.</summary>
public sealed record NotificationSnapshot(IReadOnlyList<NotificationStamp> Stamps, IReadOnlyList<NotificationItem> Items);

/// <summary>
/// Yoklama sonuçlarından yeni gelen bildirimleri bulur. Yeni = bilinmeyen kimlik, ya da bilinen kimlik ama
/// <c>CreationTime</c> ilerlemiş (aynı etiket/grupla eski bildirimini değiştiren uygulamalar kimliği korur).
/// İlk okuma "temel çizgi" sayılır: açılışta aksiyon merkezinde duran eski bildirimler kart olarak yağmasın.
/// Saf C#; tek iş parçacığında kullanılır.
/// </summary>
public sealed class NotificationDiff
{
    private Dictionary<uint, DateTimeOffset> _known = [];

    /// <summary>İlk okuma yapıldı mı. Yapılmadıysa kaynak ayrıntıları okumak zorunda değildir.</summary>
    public bool HasBaseline { get; private set; }

    /// <summary>Daha önce görülen kimlikler ve zamanları; kaynak, zamanı ilerlememiş olanlar için ayrıntı üretmeyebilir.</summary>
    public IReadOnlyDictionary<uint, DateTimeOffset> Known => _known;

    /// <summary>
    /// Yeni gelenleri zaman sırasıyla döner. Bilinen küme her seferinde güncel listeyle değişir (kapanan bildirimlerin
    /// kimlikleri düşer, küme sınırsız büyümez).
    /// </summary>
    public IReadOnlyList<NotificationItem> Apply(NotificationSnapshot snapshot)
    {
        var current = new Dictionary<uint, DateTimeOffset>();
        foreach (var stamp in snapshot.Stamps)
            current[stamp.Id] = stamp.Time;

        if (!HasBaseline)
        {
            HasBaseline = true;
            _known = current;
            return [];
        }

        var arrived = snapshot.Items
            .Where(i => current.TryGetValue(i.Id, out var time)
                        && (!_known.TryGetValue(i.Id, out var knownTime) || time > knownTime))
            .DistinctBy(i => i.Id)
            .OrderBy(i => i.Time)
            .ThenBy(i => i.Id)
            .ToList();

        _known = current;
        return arrived;
    }
}
