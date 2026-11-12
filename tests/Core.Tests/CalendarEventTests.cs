using Koy.Core.Calendar;

namespace Koy.Core.Tests;

public class EventAgendaTests
{
    private static readonly DateOnly Friday = new(2026, 11, 6);

    private static CalendarEvent Ev(string title, DateTime start, int minutes = 60) =>
        new(Guid.NewGuid().ToString("N"), title, start, minutes);

    [Fact]
    public void ForDay_returns_only_that_day_ordered_by_time()
    {
        var events = new[]
        {
            Ev("Öğle", new DateTime(2026, 11, 6, 12, 0, 0)),
            Ev("Sabah", new DateTime(2026, 11, 6, 8, 30, 0)),
            Ev("Yarın", new DateTime(2026, 11, 7, 9, 0, 0))
        };

        var day = EventAgenda.ForDay(events, Friday);

        Assert.Equal(["Sabah", "Öğle"], day.Select(e => e.Title));
    }

    [Theory]
    [InlineData(2026, 11, 2, 2)]   // Pazartesi kendisi
    [InlineData(2026, 11, 6, 2)]   // Cuma
    [InlineData(2026, 11, 8, 2)]   // Pazar önceki Pazartesi'ye aittir
    [InlineData(2026, 11, 9, 9)]   // sonraki Pazartesi
    public void WeekStart_is_the_monday_of_the_week(int y, int m, int d, int expectedDay)
    {
        Assert.Equal(expectedDay, EventAgenda.WeekStart(new DateOnly(y, m, d)).Day);
        Assert.Equal(DayOfWeek.Monday, EventAgenda.WeekStart(new DateOnly(y, m, d)).DayOfWeek);
    }

    [Fact]
    public void ForWeek_has_seven_days_marks_today_and_groups_events()
    {
        var events = new[]
        {
            Ev("Pzt", new DateTime(2026, 11, 2, 9, 0, 0)),
            Ev("Cuma 1", new DateTime(2026, 11, 6, 9, 0, 0)),
            Ev("Cuma 2", new DateTime(2026, 11, 6, 15, 0, 0)),
            Ev("Gelecek hafta", new DateTime(2026, 11, 9, 9, 0, 0))
        };

        var week = EventAgenda.ForWeek(events, Friday, Friday);

        Assert.Equal(7, week.Count);
        Assert.Equal(new DateOnly(2026, 11, 2), week[0].Date);
        Assert.Equal(new DateOnly(2026, 11, 8), week[6].Date);
        Assert.Single(week, d => d.IsToday);
        Assert.True(week[4].IsToday);
        Assert.Single(week[0].Events);
        Assert.Equal(2, week[4].Events.Count);
        Assert.Equal(0, week.Sum(d => d.Events.Count(e => e.Title == "Gelecek hafta")));
    }

    [Fact]
    public void NextToday_skips_finished_events_and_other_days()
    {
        var events = new[]
        {
            Ev("Bitti", new DateTime(2026, 11, 6, 8, 0, 0), 60),
            Ev("Yarın", new DateTime(2026, 11, 7, 10, 0, 0)),
            Ev("Sıradaki", new DateTime(2026, 11, 6, 14, 0, 0)),
            Ev("Sonra", new DateTime(2026, 11, 6, 18, 0, 0))
        };

        var next = EventAgenda.NextToday(events, new DateTime(2026, 11, 6, 10, 0, 0));

        Assert.Equal("Sıradaki", next?.Title);
    }

    [Fact]
    public void NextToday_includes_an_event_in_progress()
    {
        var events = new[] { Ev("Toplantı", new DateTime(2026, 11, 6, 9, 0, 0), 120) };

        Assert.Equal("Toplantı", EventAgenda.NextToday(events, new DateTime(2026, 11, 6, 10, 0, 0))?.Title);
        Assert.Null(EventAgenda.NextToday(events, new DateTime(2026, 11, 6, 11, 0, 0)));
    }

    [Fact]
    public void NextToday_treats_zero_length_events_as_a_moment()
    {
        var events = new[] { Ev("Hatırlatma", new DateTime(2026, 11, 6, 9, 0, 0), 0) };

        Assert.NotNull(EventAgenda.NextToday(events, new DateTime(2026, 11, 6, 9, 0, 0)));
        Assert.Null(EventAgenda.NextToday(events, new DateTime(2026, 11, 6, 9, 1, 0)));
    }

    [Fact]
    public void NextToday_is_null_without_events()
    {
        Assert.Null(EventAgenda.NextToday([], new DateTime(2026, 11, 6, 9, 0, 0)));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Toplantı ", "Toplantı")]
    public void Titles_are_trimmed_and_blank_titles_rejected(string? input, string? expected)
    {
        Assert.Equal(expected, EventAgenda.NormalizeTitle(input));
    }

    [Fact]
    public void Long_titles_are_cut()
    {
        var title = EventAgenda.NormalizeTitle(new string('a', 200));

        Assert.Equal(EventAgenda.MaxTitleLength, title!.Length);
    }

    [Theory]
    [InlineData("14:30", 14, 30)]
    [InlineData("9:05", 9, 5)]
    [InlineData("09.05", 9, 5)]
    [InlineData("1430", 14, 30)]
    [InlineData("905", 9, 5)]
    [InlineData(" 00:00 ", 0, 0)]
    [InlineData("23:59", 23, 59)]
    public void Times_are_parsed_in_common_forms(string text, int hour, int minute)
    {
        Assert.True(EventAgenda.TryParseTime(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("12:5")]
    [InlineData("abc")]
    [InlineData("12:30:00")]
    [InlineData("-1:30")]
    public void Bad_times_are_rejected(string? text)
    {
        Assert.False(EventAgenda.TryParseTime(text, out _));
    }
}

public class EventBookTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-events-" + Guid.NewGuid());

    private static readonly DateOnly Day = new(2026, 11, 6);

    [Fact]
    public void Add_keeps_events_sorted_and_raises_changed()
    {
        var book = new EventBook();
        var raised = 0;
        book.Changed += (_, _) => raised++;

        book.Add("Akşam", Day, new TimeOnly(19, 0));
        book.Add("Sabah", Day, new TimeOnly(8, 0));

        Assert.Equal(["Sabah", "Akşam"], book.Events.Select(e => e.Title));
        Assert.Equal(2, raised);
    }

    [Fact]
    public void Blank_title_is_not_added()
    {
        var book = new EventBook();

        Assert.Null(book.Add("  ", Day, new TimeOnly(8, 0)));
        Assert.Empty(book.Events);
    }

    [Fact]
    public void Duration_is_clamped()
    {
        var book = new EventBook();

        Assert.Equal(0, book.Add("a", Day, new TimeOnly(8, 0), -5)!.DurationMinutes);
        Assert.Equal(EventAgenda.MaxDurationMinutes, book.Add("b", Day, new TimeOnly(9, 0), 99999)!.DurationMinutes);
    }

    [Fact]
    public void Book_is_capped_at_the_maximum()
    {
        var book = new EventBook();
        for (var i = 0; i < EventBook.MaxEvents; i++)
            Assert.NotNull(book.Add("e" + i, Day, new TimeOnly(8, 0)));

        Assert.Null(book.Add("fazla", Day, new TimeOnly(8, 0)));
        Assert.Equal(EventBook.MaxEvents, book.Events.Count);
    }

    [Fact]
    public void Remove_deletes_by_id_and_reports_result()
    {
        var book = new EventBook();
        var added = book.Add("Sil", Day, new TimeOnly(8, 0))!;

        Assert.True(book.Remove(added.Id));
        Assert.False(book.Remove(added.Id));
        Assert.Empty(book.Events);
    }

    [Fact]
    public void PruneBefore_removes_only_events_that_ended_before_the_cutoff()
    {
        var book = new EventBook();
        book.Add("Eski", new DateOnly(2026, 8, 1), new TimeOnly(8, 0));
        book.Add("Yeni", new DateOnly(2026, 11, 5), new TimeOnly(8, 0));

        var removed = book.PruneBefore(new DateOnly(2026, 10, 1));

        Assert.Equal(1, removed);
        Assert.Equal(["Yeni"], book.Events.Select(e => e.Title));
    }

    [Fact]
    public void Store_round_trips_events()
    {
        var store = new EventStore(Path.Combine(_dir, "events.json"));
        var book = new EventBook();
        book.Add("Doktor", Day, new TimeOnly(14, 30), 45);
        book.Add("Spor", Day.AddDays(1), new TimeOnly(7, 0), 0);

        store.Save(book.Events);
        var loaded = store.Load();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Doktor", loaded[0].Title);
        Assert.Equal(new DateTime(2026, 11, 6, 14, 30, 0), loaded[0].Start);
        Assert.Equal(45, loaded[0].DurationMinutes);
        Assert.False(File.Exists(store.Path + ".tmp"));
    }

    [Fact]
    public void Store_returns_an_empty_list_for_missing_or_corrupt_files()
    {
        Directory.CreateDirectory(_dir);
        var missing = new EventStore(Path.Combine(_dir, "yok.json"));
        var corrupt = Path.Combine(_dir, "bozuk.json");
        File.WriteAllText(corrupt, "{ bu json değil");

        Assert.Empty(missing.Load());
        Assert.Empty(new EventStore(corrupt).Load());
    }

    [Fact]
    public void Store_skips_entries_without_id_or_title()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "karisik.json");
        File.WriteAllText(path, """[{"Id":"","Title":"x","Start":"2026-11-06T09:00:00","DurationMinutes":30},{"Id":"a","Title":"İyi","Start":"2026-11-06T09:00:00","DurationMinutes":30},{"Id":"b","Title":" ","Start":"2026-11-06T09:00:00","DurationMinutes":30}]""");

        var loaded = new EventStore(path).Load();

        Assert.Equal(["İyi"], loaded.Select(e => e.Title));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }
}
