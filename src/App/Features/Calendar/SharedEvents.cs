using Koy.Core.Calendar;
using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Koy.Platform.Calendar;

namespace Koy.App.Features.Calendar;

/// <summary>
/// Etkinlik defterinin tek örneği: bütün monitörlerdeki çentikler aynı listeyi görür. Dosya ilk ihtiyaçta
/// okunur (takvim ya da ana görünüm ilk açılınca), açılışta disk işi yapılmaz. Yerel değişiklik hemen diske yazılır;
/// harici Google/Outlook etkinlikleri çevrimdışı önbellekten yüklenir ve istendiğinde eşitlenir.
/// Yalnız arayüz iş parçacığından kullanılır.
/// </summary>
public sealed class SharedEvents
{
    /// <summary>Bitişi bu kadar günden eskiyse yerel etkinlik silinir (liste şişmesin).</summary>
    private const int KeepDays = 60;

    private readonly EventStore _store;
    private readonly CalendarCacheStore _cacheStore;
    private readonly ICalendarSyncService _syncService;
    private EventBook? _book;
    private bool _syncing;

    public SharedEvents(
        EventStore? store = null,
        CalendarCacheStore? cacheStore = null,
        ICalendarSyncService? syncService = null)
    {
        _store = store ?? new EventStore(EventStore.DefaultPath);
        _cacheStore = cacheStore ?? new CalendarCacheStore();
        _syncService = syncService ?? new CalendarSyncService(cacheStore: _cacheStore);
    }

    /// <summary>Etkinlik eklendi, silindi ya da harici takvim eşitlendi.</summary>
    public event EventHandler? Changed;

    public EventBook Book
    {
        get
        {
            if (_book is null)
            {
                _book = new EventBook(_store.Load());
                _book.PruneBefore(DateOnly.FromDateTime(DateTime.Today).AddDays(-KeepDays));
                LoadExternalCacheInto(_book);
                _book.LocalChanged += OnLocalBookChanged;
                _book.Changed += OnBookChanged;
            }

            return _book;
        }
    }

    /// <summary>Önbellekteki son bilinen harici etkinlikleri deftere yeniden yükler.</summary>
    public void ReloadExternalFromCache()
    {
        if (_book is null)
            return;

        _book.ClearExternal();
        LoadExternalCacheInto(_book);
    }

    /// <summary>Bağlı Google / Outlook hesaplarını salt okunur eşitler; çevrimdışıysa önbellekle devam eder.</summary>
    public async Task SyncExternalAsync(CalendarSettings settings, CancellationToken ct = default)
    {
        if (_syncing)
            return;

        if (!settings.GoogleEnabled && !settings.OutlookEnabled)
            return;

        _syncing = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var from = now.AddDays(-14);
            var to = now.AddDays(60);

            if (settings.GoogleEnabled && _syncService.HasToken(CalendarProvider.Google))
            {
                var clientId = CalendarOAuthConfig.ResolveClientId(CalendarProvider.Google, settings.GoogleClientId);
                var result = await _syncService.SyncAsync(CalendarProvider.Google, clientId, from, to, ct);
                if (result.Events.Count > 0 || result.Success)
                    Book.ReplaceExternal(CalendarEventSource.Google, result.Events);
            }

            if (settings.OutlookEnabled && _syncService.HasToken(CalendarProvider.Outlook))
            {
                var clientId = CalendarOAuthConfig.ResolveClientId(CalendarProvider.Outlook, settings.OutlookClientId);
                var result = await _syncService.SyncAsync(CalendarProvider.Outlook, clientId, from, to, ct);
                if (result.Events.Count > 0 || result.Success)
                    Book.ReplaceExternal(CalendarEventSource.Outlook, result.Events);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("Harici takvim eşitlemesi", ex);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void LoadExternalCacheInto(EventBook book)
    {
        var snap = _cacheStore.Load();
        if (snap.Events.Count == 0)
            return;

        var google = snap.Events.Where(e => e.Source == CalendarEventSource.Google).ToList();
        if (google.Count > 0)
            book.ReplaceExternal(CalendarEventSource.Google, google);

        var outlook = snap.Events.Where(e => e.Source == CalendarEventSource.Outlook).ToList();
        if (outlook.Count > 0)
            book.ReplaceExternal(CalendarEventSource.Outlook, outlook);
    }

    private void OnLocalBookChanged(object? sender, EventArgs e)
    {
        try
        {
            _store.Save(Book.LocalEvents);
        }
        catch (Exception ex)
        {
            // Disk dolu ya da dosya kilitli: etkinlik bu oturumda bellekte kalır, uygulama çökmez.
            CrashLog.Write("Takvim etkinlikleri kaydedilemedi", ex);
        }
    }

    private void OnBookChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
