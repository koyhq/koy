using Koy.Core.Calendar;
using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

public class NotchHomePageTests
{
    [Theory]
    [InlineData(true, true, NotchHomePage.Music)]
    [InlineData(false, true, NotchHomePage.Home)]
    [InlineData(true, false, NotchHomePage.Home)]
    [InlineData(false, false, NotchHomePage.Home)]
    public void OnOpen_shows_music_only_with_a_track_and_the_music_card_enabled(bool hasTrack, bool musicEnabled, NotchHomePage expected) =>
        Assert.Equal(expected, NotchHomePages.OnOpen(hasTrack, musicEnabled));
}

public class EventAgendaUpcomingTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 23, 47, 0);

    private static CalendarEvent Ev(string title, DateTime start, int minutes = 60) =>
        new(Guid.NewGuid().ToString("N"), title, start, minutes);

    [Fact]
    public void Upcoming_skips_finished_keeps_ongoing_and_orders_across_days()
    {
        var events = new[]
        {
            Ev("Bitti", new DateTime(2026, 11, 13, 20, 0, 0)),
            Ev("Sürüyor", new DateTime(2026, 11, 13, 23, 30, 0)),
            Ev("Yarın öğle", new DateTime(2026, 11, 14, 14, 30, 0)),
            Ev("Yarın sabah", new DateTime(2026, 11, 14, 10, 0, 0)),
        };

        var list = EventAgenda.Upcoming(events, Now, 3);

        Assert.Equal(["Sürüyor", "Yarın sabah", "Yarın öğle"], list.Select(e => e.Title));
    }

    [Fact]
    public void Upcoming_drops_a_past_moment_and_respects_count_and_horizon()
    {
        var events = new[]
        {
            Ev("Geçmiş an", new DateTime(2026, 11, 13, 23, 0, 0), 0),
            Ev("Bir", new DateTime(2026, 11, 14, 9, 0, 0)),
            Ev("İki", new DateTime(2026, 11, 15, 9, 0, 0)),
            Ev("Çok ileride", new DateTime(2026, 12, 30, 9, 0, 0)),
        };

        Assert.Equal(["Bir"], EventAgenda.Upcoming(events, Now, 1).Select(e => e.Title));
        Assert.Equal(["Bir", "İki"], EventAgenda.Upcoming(events, Now, 5).Select(e => e.Title));
        Assert.Empty(EventAgenda.Upcoming(events, Now, 0));
    }

    [Fact]
    public void Until_is_zero_once_started()
    {
        var soon = Ev("Yakında", Now.AddMinutes(73));
        var started = Ev("Başladı", Now.AddMinutes(-5));

        Assert.Equal(TimeSpan.FromMinutes(73), EventAgenda.Until(soon, Now));
        Assert.Equal(TimeSpan.Zero, EventAgenda.Until(started, Now));
    }
}
