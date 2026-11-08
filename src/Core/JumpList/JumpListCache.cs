namespace Koy.Core.JumpList;

/// <summary>Bir dosyanın boyutu ve son yazılma zamanı; dosya yoksa <see cref="Missing"/>.</summary>
public readonly record struct FileStamp(long Length, long LastWriteUtcTicks)
{
    public static FileStamp Missing => new(-1, 0);
}

/// <summary>
/// Uygulama başına son okunan bölümleri tutan sınırlı (en az yakın zamanda kullanılan atılır) önbellek. Girdi, iki
/// kaynak dosyanın (özel hedefler, otomatik hedefler) damgası değiştiyse geçersizdir. Zamanlayıcısı yoktur; eski girdiler
/// yalnız yeni bir ekleme sırasında atılır. İş parçacığı güvenlidir.
/// </summary>
public sealed class JumpListCache(int capacity = JumpListCache.DefaultCapacity)
{
    public const int DefaultCapacity = 16;

    private readonly object _gate = new();
    private readonly LinkedList<Entry> _lru = new();
    private readonly int _capacity = Math.Max(1, capacity);

    private sealed record Entry(string AppId, FileStamp Custom, FileStamp Automatic, IReadOnlyList<JumpListSection> Sections);

    public int Count
    {
        get { lock (_gate) return _lru.Count; }
    }

    public bool TryGet(string appId, FileStamp custom, FileStamp automatic, out IReadOnlyList<JumpListSection> sections)
    {
        lock (_gate)
        {
            for (var node = _lru.First; node is not null; node = node.Next)
            {
                if (!string.Equals(node.Value.AppId, appId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (node.Value.Custom == custom && node.Value.Automatic == automatic)
                {
                    _lru.Remove(node);
                    _lru.AddFirst(node);
                    sections = node.Value.Sections;
                    return true;
                }

                _lru.Remove(node); // bayat
                break;
            }
        }

        sections = [];
        return false;
    }

    public void Set(string appId, FileStamp custom, FileStamp automatic, IReadOnlyList<JumpListSection> sections)
    {
        lock (_gate)
        {
            for (var node = _lru.First; node is not null; node = node.Next)
            {
                if (string.Equals(node.Value.AppId, appId, StringComparison.OrdinalIgnoreCase))
                {
                    _lru.Remove(node);
                    break;
                }
            }

            _lru.AddFirst(new Entry(appId, custom, automatic, sections));
            while (_lru.Count > _capacity)
                _lru.RemoveLast();
        }
    }

    public void Clear()
    {
        lock (_gate)
            _lru.Clear();
    }
}
