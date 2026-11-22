namespace Koy.Core.Clipboard;

/// <summary>
/// Pano geçmişinin saf bellek mantığı (Win32 yok). İş parçacığı güvenli: Win32 dinleyicisi arka planda ekler,
/// arayüz okur. En yeni başta. Sabitlenenler sınıra sayılmaz ve otomatik silinmez.
/// </summary>
public sealed class ClipboardHistory(Func<DateTimeOffset>? now = null)
{
    public const int MaxUnpinned = 50;
    public const int MaxTextLength = 10_000;

    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.Now);
    private readonly List<ClipboardItem> _items = [];

    /// <summary>
    /// Metni ekler. Boş/yalnız boşluk yok sayılır (null döner). Aynı metin varsa o kayıt başa taşınır (sabit durumu
    /// korunur). Uzun metin <see cref="MaxTextLength"/> karaktere kesilir. Sınır aşılırsa en eski sabitlenmemiş silinir.
    /// </summary>
    public ClipboardItem? Add(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (text.Length > MaxTextLength)
            text = text[..MaxTextLength];

        lock (_gate)
        {
            var index = _items.FindIndex(i => string.Equals(i.Text, text, StringComparison.Ordinal));
            var item = index >= 0
                ? _items[index] with { CopiedAt = _now() }
                : new ClipboardItem(Guid.NewGuid(), text, ClipboardClassifier.Classify(text), false, _now());

            if (index >= 0)
                _items.RemoveAt(index);
            _items.Insert(0, item);
            TrimLocked();
            return item;
        }
    }

    /// <summary>Süzgeç + arama (büyük/küçük harf duyarsız, kültürden bağımsız). En yeni başta.</summary>
    public IReadOnlyList<ClipboardItem> Query(ClipboardFilter filter = ClipboardFilter.All, string? search = null)
    {
        var needle = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        lock (_gate)
        {
            return _items
                .Where(i => Matches(i, filter))
                .Where(i => needle is null || i.Text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public ClipboardItem? Get(Guid id)
    {
        lock (_gate)
            return _items.FirstOrDefault(i => i.Id == id);
    }

    /// <summary>Sabitle / çöz. Değişiklik olduysa true. Çözünce sınır aşılmışsa en eskiler budanır.</summary>
    public bool SetPinned(Guid id, bool pinned)
    {
        lock (_gate)
        {
            var index = _items.FindIndex(i => i.Id == id);
            if (index < 0 || _items[index].Pinned == pinned)
                return false;
            _items[index] = _items[index] with { Pinned = pinned };
            TrimLocked();
            return true;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_gate)
            return _items.RemoveAll(i => i.Id == id) > 0;
    }

    /// <summary>Sabitlenmeyenlerin hepsini siler; sabitler kalır. Silinen sayısını döner.</summary>
    public int Clear()
    {
        lock (_gate)
            return _items.RemoveAll(i => !i.Pinned);
    }

    /// <summary>
    /// Silinen öğeleri kimlikleriyle geri koyar ("Geri al"); kopyalanma zamanına göre en yeni başta kalacak biçimde yerleşir.
    /// Aynı kimlik ya da aynı metin zaten varsa o öğe atlanır. Eklenen sayısını döner.
    /// </summary>
    public int Restore(IEnumerable<ClipboardItem> removed)
    {
        lock (_gate)
        {
            var added = 0;
            foreach (var item in removed)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Text)
                    || _items.Any(i => i.Id == item.Id || string.Equals(i.Text, item.Text, StringComparison.Ordinal)))
                    continue;
                _items.Add(item);
                added++;
            }

            if (added > 0)
            {
                // OrderBy kararlıdır: aynı zamanlılar eski sırasını korur.
                var sorted = _items.OrderByDescending(i => i.CopiedAt).ToList();
                _items.Clear();
                _items.AddRange(sorted);
                TrimLocked();
            }

            return added;
        }
    }

    /// <summary>Sabit öğeler (diske yazılacak olan), en yeni başta.</summary>
    public List<ClipboardItem> PinnedSnapshot()
    {
        lock (_gate)
            return _items.Where(i => i.Pinned).ToList();
    }

    /// <summary>Diskten gelen sabit öğeleri sona ekler (zaten varsa atlar). Metin sınırı yeniden uygulanır.</summary>
    public void LoadPinned(IEnumerable<ClipboardItem> pinned)
    {
        lock (_gate)
        {
            foreach (var p in pinned)
            {
                if (p is null || string.IsNullOrWhiteSpace(p.Text))
                    continue;
                var text = p.Text.Length > MaxTextLength ? p.Text[..MaxTextLength] : p.Text;
                if (_items.Any(i => i.Id == p.Id || string.Equals(i.Text, text, StringComparison.Ordinal)))
                    continue;
                var id = p.Id == Guid.Empty ? Guid.NewGuid() : p.Id;
                _items.Add(new ClipboardItem(id, text, ClipboardClassifier.Classify(text), true, p.CopiedAt));
            }
        }
    }

    private static bool Matches(ClipboardItem item, ClipboardFilter filter) => filter switch
    {
        ClipboardFilter.All => true,
        ClipboardFilter.Pinned => item.Pinned,
        ClipboardFilter.Link => item.Kind == ClipboardKind.Link,
        ClipboardFilter.Color => item.Kind == ClipboardKind.Color,
        ClipboardFilter.Code => item.Kind == ClipboardKind.Code,
        _ => true
    };

    private void TrimLocked()
    {
        var unpinned = 0;
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].Pinned)
                continue;
            if (++unpinned > MaxUnpinned)
            {
                _items.RemoveAt(i);
                i--;
            }
        }
    }
}
