namespace Koy.Core.JumpList;

/// <summary>
/// Exe yolundan çözülen AppUserModelID'ler için küçük, sınırlı önbellek. Bulunamadı sonucu (null) da tutulur; böylece
/// aynı uygulamaya her sağ tıkta Başlat menüsü yeniden taranmaz. Kapasite dolunca en eski girdi atılır. İş parçacığı güvenli.
/// </summary>
public sealed class ExeAppIdCache(int capacity = ExeAppIdCache.DefaultCapacity)
{
    public const int DefaultCapacity = 64;

    private readonly object _gate = new();
    private readonly Dictionary<string, string?> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _order = new();
    private readonly int _capacity = Math.Max(1, capacity);

    public int Count
    {
        get { lock (_gate) return _map.Count; }
    }

    /// <summary>Girdi varsa true; <paramref name="appId"/> negatif sonuçta null.</summary>
    public bool TryGet(string exePath, out string? appId)
    {
        lock (_gate)
            return _map.TryGetValue(exePath, out appId);
    }

    public void Set(string exePath, string? appId)
    {
        lock (_gate)
        {
            if (!_map.ContainsKey(exePath))
            {
                _order.Enqueue(exePath);
                while (_order.Count > _capacity)
                    _map.Remove(_order.Dequeue());
            }

            _map[exePath] = appId;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _order.Clear();
        }
    }
}
