using Koy.Core.Settings;

namespace Koy.Core.Dictation;

/// <summary>Geçmiş satırı. Başarısız işte <see cref="RecordingPath"/> saklanan sestir (yeniden deneme için).</summary>
public sealed record DictationHistoryEntry(
    string Id,
    DateTimeOffset Time,
    string Raw,
    string Text,
    string Language,
    string Model,
    string Profile,
    double DurationSeconds,
    string Status,
    string Message,
    string? RecordingPath);

/// <summary>Her yazının kaydı (<c>dictation-history.json</c>). Eskiler sınırı aşınca düşer; 0 = geçmiş tutma.</summary>
public sealed class DictationHistory(string? path = null)
{
    public const string StatusOk = "ok";
    public const string StatusWarning = "warning";
    public const string StatusFailed = "failed";

    private readonly Lock _gate = new();
    private readonly JsonListFile<DictationHistoryEntry> _file =
        new(path ?? JsonListFile.InSettingsFolder("dictation-history.json"), DataJsonContext.Default.ListDictationHistoryEntry);

    public string Path => _file.Path;

    /// <summary>Eskiden yeniye.</summary>
    public IReadOnlyList<DictationHistoryEntry> Load()
    {
        lock (_gate)
            return _file.Load();
    }

    /// <summary>Satırı ekler ya da aynı kimlikli satırın yerine koyar (yeniden deneme sonucu), sınırı uygular. Silinen satırlar döner.</summary>
    public IReadOnlyList<DictationHistoryEntry> Add(DictationHistoryEntry entry, int limit)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            var items = _file.Load();
            var index = items.FindIndex(e => e.Id == entry.Id);
            if (index >= 0)
                items[index] = entry;
            else
                items.Add(entry);

            List<DictationHistoryEntry> dropped;
            if (limit <= 0)
            {
                dropped = [.. items];
                items.Clear();
            }
            else
            {
                dropped = Trim(items, limit);
            }

            _file.Save(items);
            return dropped;
        }
    }

    /// <summary>Kimliklere göre siler; silinen satırlar (ses dosyalarını temizlemek için) döner.</summary>
    public IReadOnlyList<DictationHistoryEntry> Remove(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var wanted = ids.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            var items = _file.Load();
            var removed = items.Where(e => wanted.Contains(e.Id)).ToList();
            if (removed.Count == 0)
                return [];

            items.RemoveAll(e => wanted.Contains(e.Id));
            _file.Save(items);
            return removed;
        }
    }

    public IReadOnlyList<DictationHistoryEntry> Clear()
    {
        lock (_gate)
        {
            var all = _file.Load();
            _file.Save([]);
            return all;
        }
    }

    public DictationHistoryEntry? Find(string id)
    {
        lock (_gate)
            return _file.Load().FirstOrDefault(e => e.Id == id);
    }

    /// <summary>Sınırı aşan en eski satırları düşürür ve düşenleri verir.</summary>
    public static List<DictationHistoryEntry> Trim(List<DictationHistoryEntry> items, int limit)
    {
        ArgumentNullException.ThrowIfNull(items);
        var excess = items.Count - limit;
        if (limit <= 0 || excess <= 0)
            return [];

        var dropped = items.Take(excess).ToList();
        items.RemoveRange(0, excess);
        return dropped;
    }
}
