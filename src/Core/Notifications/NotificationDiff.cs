namespace Koy.Core.Notifications;

/// <summary>Bir yoklamanın sonucu: şu an listede olan bütün kimlikler ve (en az) daha önce görülmemiş kimliklerin ayrıntısı.</summary>
public sealed record NotificationSnapshot(IReadOnlyList<uint> Ids, IReadOnlyList<NotificationItem> Items);

/// <summary>
/// Yoklama sonuçlarından yeni gelen bildirimleri bulur (kimlik kümesi farkı). İlk okuma "temel çizgi" sayılır:
/// açılışta aksiyon merkezinde duran eski bildirimler kart olarak yağmasın. Saf C#; tek iş parçacığında kullanılır.
/// </summary>
public sealed class NotificationDiff
{
    private HashSet<uint> _known = [];

    /// <summary>İlk okuma yapıldı mı. Yapılmadıysa kaynak ayrıntıları okumak zorunda değildir.</summary>
    public bool HasBaseline { get; private set; }

    /// <summary>Daha önce görülen kimlikler; kaynak ayrıntı üretirken bunları atlayabilir.</summary>
    public IReadOnlySet<uint> Known => _known;

    /// <summary>
    /// Yeni gelenleri zaman sırasıyla döner. Bilinen küme her seferinde güncel listeyle değişir (kapanan bildirimlerin
    /// kimlikleri düşer, küme sınırsız büyümez).
    /// </summary>
    public IReadOnlyList<NotificationItem> Apply(NotificationSnapshot snapshot)
    {
        var current = new HashSet<uint>(snapshot.Ids);

        if (!HasBaseline)
        {
            HasBaseline = true;
            _known = current;
            return [];
        }

        var arrived = snapshot.Items
            .Where(i => current.Contains(i.Id) && !_known.Contains(i.Id))
            .DistinctBy(i => i.Id)
            .OrderBy(i => i.Time)
            .ThenBy(i => i.Id)
            .ToList();

        _known = current;
        return arrived;
    }
}
