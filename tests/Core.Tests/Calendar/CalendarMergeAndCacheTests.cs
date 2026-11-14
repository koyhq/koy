using Koy.Core.Calendar;

namespace Koy.Core.Tests.Calendar;

public sealed class CalendarMergeAndCacheTests
{
    private sealed class FakeProtector : ITokenProtector
    {
        public byte[] Protect(byte[] plainBytes) => plainBytes.Select(b => (byte)(b ^ 0x5A)).ToArray();
        public byte[] Unprotect(byte[] protectedBytes) => protectedBytes.Select(b => (byte)(b ^ 0x5A)).ToArray();
    }

    [Fact]
    public void EventBook_MergesLocalAndExternalEvents_AndProtectsReadOnlyEvents()
    {
        var book = new EventBook();
        var day = new DateOnly(2026, 11, 15);
        var local = book.Add("Yerel Toplantı", day, new TimeOnly(10, 0), 30);
        Assert.NotNull(local);

        book.ReplaceExternal(CalendarEventSource.Google,
        [
            new CalendarEvent("google:1", "Google Sync", day.ToDateTime(new TimeOnly(9, 0)), 45, CalendarEventSource.Google)
        ]);
        book.ReplaceExternal(CalendarEventSource.Outlook,
        [
            new CalendarEvent("outlook:1", "Outlook Sync", day.ToDateTime(new TimeOnly(11, 0)), 60, CalendarEventSource.Outlook)
        ]);

        Assert.Equal(3, book.Events.Count);
        Assert.Equal("google:1", book.Events[0].Id);
        Assert.Equal(local.Id, book.Events[1].Id);
        Assert.Equal("outlook:1", book.Events[2].Id);

        // Yerel listede yalnız yerel etkinlikler kalır
        Assert.Single(book.LocalEvents);

        // Harici etkinlik silinemez
        Assert.False(book.Remove("google:1"));
        Assert.Equal(3, book.Events.Count);

        // Yerel etkinlik silinebilir
        Assert.True(book.Remove(local.Id));
        Assert.Equal(2, book.Events.Count);

        // Sağlayıcı bağlantısı kesilince yalnız o sağlayıcı temizlenir
        Assert.True(book.ClearExternal(CalendarEventSource.Google));
        Assert.Single(book.Events);
        Assert.Equal(CalendarEventSource.Outlook, book.Events[0].Source);
    }

    [Fact]
    public void Parsers_ParseGoogleAndOutlookResponses_SkippingCancelled()
    {
        const string googleJson = """
            {
              "items": [
                {
                  "id": "g1",
                  "summary": "Standup",
                  "status": "confirmed",
                  "start": { "dateTime": "2026-11-15T09:00:00Z" },
                  "end": { "dateTime": "2026-11-15T09:30:00Z" }
                },
                {
                  "id": "g2",
                  "summary": "Cancelled Event",
                  "status": "cancelled",
                  "start": { "date": "2026-11-16" },
                  "end": { "date": "2026-11-17" }
                }
              ]
            }
            """;

        var googleEvents = CalendarEventParsers.ParseGoogleEvents(googleJson, TimeZoneInfo.Utc);
        Assert.Single(googleEvents);
        Assert.Equal("google:g1", googleEvents[0].Id);
        Assert.Equal("Standup", googleEvents[0].Title);
        Assert.Equal(30, googleEvents[0].DurationMinutes);
        Assert.Equal(CalendarEventSource.Google, googleEvents[0].Source);

        const string outlookJson = """
            {
              "value": [
                {
                  "id": "o1",
                  "subject": "Design Review",
                  "isCancelled": false,
                  "isAllDay": false,
                  "start": { "dateTime": "2026-11-15T14:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-11-15T15:00:00.0000000", "timeZone": "UTC" }
                },
                {
                  "id": "o2",
                  "subject": "Cancelled",
                  "isCancelled": true,
                  "isAllDay": false,
                  "start": { "dateTime": "2026-11-15T16:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-11-15T17:00:00.0000000", "timeZone": "UTC" }
                }
              ]
            }
            """;

        var outlookEvents = CalendarEventParsers.ParseOutlookEvents(outlookJson, TimeZoneInfo.Utc);
        Assert.Single(outlookEvents);
        Assert.Equal("outlook:o1", outlookEvents[0].Id);
        Assert.Equal("Design Review", outlookEvents[0].Title);
        Assert.Equal(60, outlookEvents[0].DurationMinutes);
        Assert.Equal(CalendarEventSource.Outlook, outlookEvents[0].Source);
    }

    [Fact]
    public void TokenStoreAndCacheStore_RoundTripWithProtection()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-cal-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var tokenStore = new CalendarTokenStore(new FakeProtector(), dir);
            var tokenSet = new OAuthTokenSet("acc-1", "ref-1", DateTimeOffset.UtcNow.AddHours(1), "user@example.com");
            tokenStore.Save(CalendarProvider.Google, tokenSet);

            Assert.True(tokenStore.Exists(CalendarProvider.Google));
            var rawBytes = File.ReadAllBytes(tokenStore.GetTokenPath(CalendarProvider.Google));
            Assert.DoesNotContain("acc-1", System.Text.Encoding.UTF8.GetString(rawBytes));

            var loadedToken = tokenStore.Load(CalendarProvider.Google);
            Assert.NotNull(loadedToken);
            Assert.Equal("acc-1", loadedToken.AccessToken);
            Assert.Equal("ref-1", loadedToken.RefreshToken);
            Assert.Equal("user@example.com", loadedToken.AccountLabel);

            tokenStore.Delete(CalendarProvider.Google);
            Assert.False(tokenStore.Exists(CalendarProvider.Google));

            var cachePath = Path.Combine(dir, "calendar-cache.json");
            var cacheStore = new CalendarCacheStore(cachePath);
            var snapshot = new CalendarCacheSnapshot(
                DateTimeOffset.UtcNow,
                [new CalendarEvent("google:1", "Cached Meeting", new DateTime(2026, 11, 15, 10, 0, 0), 30, CalendarEventSource.Google)]);
            cacheStore.Save(snapshot);

            var loadedCache = cacheStore.Load();
            Assert.Single(loadedCache.Events);
            Assert.Equal("Cached Meeting", loadedCache.Events[0].Title);

            cacheStore.RemoveProvider(CalendarEventSource.Google);
            Assert.Empty(cacheStore.Load().Events);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
