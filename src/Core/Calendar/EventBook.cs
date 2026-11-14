namespace Koy.Core.Calendar;

/// <summary>
/// Etkinlik defteri: yerel ekleme/silme ve harici (salt okunur) takvim etkinliklerini zamana göre birleştirir.
/// Saf bellek içi mantık; diske yazmayı <see cref="EventStore"/> yapar.
/// </summary>
public sealed class EventBook
{
    /// <summary>Yerel defterdeki en çok etkinlik; sınırsız büyüyüp ayar dosyasını şişirmesin.</summary>
    public const int MaxEvents = 500;

    /// <summary>Her harici sağlayıcı için bellekte tutulacak en çok etkinlik.</summary>
    public const int MaxExternalPerProvider = 250;

    private readonly List<CalendarEvent> _localEvents;
    private readonly List<CalendarEvent> _externalEvents = [];
    private List<CalendarEvent> _merged;

    public EventBook(IEnumerable<CalendarEvent>? initial = null)
    {
        _localEvents = initial is null
            ? []
            : initial
                .Where(e => e.Source == CalendarEventSource.Local)
                .Take(MaxEvents)
                .OrderBy(e => e.Start)
                .ToList();
        _merged = [.. _localEvents];
    }

    /// <summary>Yerel ya da harici etkinlik listesi değişti.</summary>
    public event EventHandler? Changed;

    /// <summary>Yerel etkinlikler değişti (yalnız bunlar <see cref="EventStore"/> ile diske yazılır).</summary>
    public event EventHandler? LocalChanged;

    /// <summary>Birleşik (yerel + harici) etkinlikler, başlangıç saatine göre sıralı.</summary>
    public IReadOnlyList<CalendarEvent> Events => _merged;

    /// <summary>Yalnız yerel etkinlikler.</summary>
    public IReadOnlyList<CalendarEvent> LocalEvents => _localEvents;

    /// <summary>
    /// Yerel etkinlik ekler. Başlık boşsa ya da yerel defter doluysa eklenmez ve null döner. Süre 0 - 24 saate sıkıştırılır.
    /// </summary>
    public CalendarEvent? Add(string? title, DateOnly day, TimeOnly time, int durationMinutes = 60)
    {
        var normalized = EventAgenda.NormalizeTitle(title);
        if (normalized is null || _localEvents.Count >= MaxEvents)
            return null;

        var created = new CalendarEvent(
            Guid.NewGuid().ToString("N"),
            normalized,
            day.ToDateTime(time),
            EventAgenda.ClampDuration(durationMinutes),
            CalendarEventSource.Local);

        var index = _localEvents.FindIndex(e => e.Start > created.Start);
        _localEvents.Insert(index < 0 ? _localEvents.Count : index, created);
        RebuildMerged();
        LocalChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
        return created;
    }

    /// <summary>Yalnız yerel etkinlikleri siler; harici (salt okunur) etkinlikler silinmez.</summary>
    public bool Remove(string id)
    {
        var removed = _localEvents.RemoveAll(e => e.Id == id) > 0;
        if (removed)
        {
            RebuildMerged();
            LocalChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>Belirtilen harici kaynağın etkinliklerini yenileriyle değiştirir (salt okunur birleştirme).</summary>
    public void ReplaceExternal(CalendarEventSource source, IEnumerable<CalendarEvent> events)
    {
        if (source == CalendarEventSource.Local)
            throw new ArgumentException("Yerel kaynak ReplaceExternal ile değiştirilemez.", nameof(source));

        _externalEvents.RemoveAll(e => e.Source == source);
        foreach (var e in events.Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Title))
                     .OrderBy(e => e.Start)
                     .Take(MaxExternalPerProvider))
        {
            _externalEvents.Add(e.Source == source ? e : e with { Source = source });
        }

        RebuildMerged();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Harici etkinlikleri bellekten temizler (hesap bağlantısı kesilince).</summary>
    public bool ClearExternal(CalendarEventSource? source = null)
    {
        var removed = source is null
            ? _externalEvents.RemoveAll(_ => true) > 0
            : _externalEvents.RemoveAll(e => e.Source == source.Value) > 0;

        if (removed)
        {
            RebuildMerged();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>Bitişi <paramref name="cutoff"/> gününden önce olan eski yerel etkinlikleri siler; silinen sayısını döner.</summary>
    public int PruneBefore(DateOnly cutoff)
    {
        var removed = _localEvents.RemoveAll(e => DateOnly.FromDateTime(e.End) < cutoff);
        if (removed > 0)
        {
            RebuildMerged();
            LocalChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    private void RebuildMerged()
    {
        if (_externalEvents.Count == 0)
        {
            _merged = [.. _localEvents];
            return;
        }

        var combined = new List<CalendarEvent>(_localEvents.Count + _externalEvents.Count);
        combined.AddRange(_localEvents);
        combined.AddRange(_externalEvents);
        combined.Sort((a, b) => a.Start.CompareTo(b.Start));
        _merged = combined;
    }
}
