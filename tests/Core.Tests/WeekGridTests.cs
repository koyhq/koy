using Koy.Core.Calendar;

namespace Koy.Core.Tests;

public class WeekGridTests
{
    // 12 Kasım 2026 Perşembe.
    private static readonly DateOnly Thursday = new(2026, 11, 12);

    [Theory]
    [InlineData(DayOfWeek.Monday, 9)]
    [InlineData(DayOfWeek.Sunday, 8)]
    [InlineData(DayOfWeek.Saturday, 7)]
    [InlineData(DayOfWeek.Thursday, 12)]
    [InlineData(DayOfWeek.Friday, 6)]
    public void Week_starts_on_the_chosen_first_day(DayOfWeek first, int expectedDay)
    {
        var start = WeekGrid.Start(Thursday, first);

        Assert.Equal(first, start.DayOfWeek);
        Assert.Equal(expectedDay, start.Day);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Saturday)]
    public void Days_are_seven_consecutive_and_contain_the_anchor(DayOfWeek first)
    {
        var days = WeekGrid.Days(Thursday, first);

        Assert.Equal(7, days.Count);
        Assert.Equal(first, days[0].DayOfWeek);
        Assert.Contains(Thursday, days);
        for (var i = 1; i < 7; i++)
            Assert.Equal(days[i - 1].AddDays(1), days[i]);
    }

    [Fact]
    public void Every_day_of_a_week_maps_to_the_same_start()
    {
        var start = WeekGrid.Start(Thursday, DayOfWeek.Sunday);

        for (var i = 0; i < 7; i++)
            Assert.Equal(start, WeekGrid.Start(start.AddDays(i), DayOfWeek.Sunday));
    }

    [Fact]
    public void Week_can_cross_month_and_year_boundaries()
    {
        var days = WeekGrid.Days(new DateOnly(2026, 1, 1), DayOfWeek.Monday);

        Assert.Equal(new DateOnly(2025, 12, 29), days[0]);
        Assert.Equal(new DateOnly(2026, 1, 4), days[6]);
    }

    [Fact]
    public void Agenda_week_defaults_to_monday_and_follows_the_first_day()
    {
        var today = Thursday;
        var monday = EventAgenda.ForWeek([], Thursday, today);
        var saturday = EventAgenda.ForWeek([], Thursday, today, DayOfWeek.Saturday);

        Assert.Equal(DayOfWeek.Monday, monday[0].Date.DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, saturday[0].Date.DayOfWeek);
        Assert.Single(saturday, d => d.IsToday);
        Assert.Equal(new DateOnly(2026, 11, 7), EventAgenda.WeekStart(Thursday, DayOfWeek.Saturday));
    }

    [Fact]
    public void Agenda_week_keeps_events_on_their_days_for_any_first_day()
    {
        var ev = new CalendarEvent("1", "Toplantı", new DateTime(2026, 11, 8, 10, 0, 0), 30);

        var week = EventAgenda.ForWeek([ev], Thursday, Thursday, DayOfWeek.Sunday);

        Assert.Equal(new DateOnly(2026, 11, 8), week[0].Date);
        Assert.Single(week[0].Events);
        Assert.All(week.Skip(1), d => Assert.Empty(d.Events));
    }
}
