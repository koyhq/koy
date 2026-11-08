namespace Koy.Core.Search;

/// <summary>
/// Dosyadan okunan sonucu dosyanın son yazma zamanı ve boyutu değişmedikçe saklar;
/// büyük dosyalar panel her açıldığında yeniden okunup ayrıştırılmaz.
/// </summary>
public sealed class FileStampCache<T>
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (long WriteTicks, long Length, T Value)> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="load">Yalnız damga değiştiyse ya da kayıt yoksa çağrılır.</param>
    public T GetOrLoad(string key, long writeTicks, long length, Func<T> load)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry) && entry.WriteTicks == writeTicks && entry.Length == length)
                return entry.Value;
        }

        // Ayrıştırma kilit dışında: iki panel aynı anda okursa en kötü ihtimal çift iş.
        var value = load();
        lock (_gate)
            _entries[key] = (writeTicks, length, value);
        return value;
    }

    /// <summary>Dosya artık yok: kaydı bırakır.</summary>
    public void Remove(string key)
    {
        lock (_gate)
            _entries.Remove(key);
    }
}
