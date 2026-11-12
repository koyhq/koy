namespace Koy.Core.Calendar;

public sealed record CalendarDay(
    DateOnly Date,
    bool IsCurrentMonth,
    bool IsToday,
    bool IsWeekend);

/// <summary>
/// Ay görünümü için 6×7 = 42 günlük ızgara üretir. Haftanın ilk günü varsayılan Pazartesi; ayardan
/// (<see cref="Clock.ClockFormatPolicy.ResolveFirstDayOfWeek"/>) Cumartesi ya da Pazar da olabilir.
/// Saf C#, kültür ve tarih bağımsız test edilebilir.
/// </summary>
public static class MonthGrid
{
    public static IReadOnlyList<CalendarDay> Build(int year, int month, DateOnly? today = null, DayOfWeek firstDay = DayOfWeek.Monday)
    {
        var currentToday = today ?? DateOnly.FromDateTime(DateTime.Today);
        var firstOfMonth = new DateOnly(year, month, 1);

        // Ayın ilk gününden geriye, seçilen haftanın ilk gününe kadar.
        var startOffset = ((int)firstOfMonth.DayOfWeek - (int)firstDay + 7) % 7;

        var startDate = firstOfMonth.AddDays(-startOffset);
        var days = new List<CalendarDay>(42);

        for (var i = 0; i < 42; i++)
        {
            var date = startDate.AddDays(i);
            var isCurrentMonth = date.Month == month && date.Year == year;
            var isToday = date == currentToday;
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            days.Add(new CalendarDay(date, isCurrentMonth, isToday, isWeekend));
        }

        return days;
    }

    /// <summary>Izgara sütunlarının gün sırası: ilk gün seçimden başlar, yedi gün.</summary>
    public static IReadOnlyList<DayOfWeek> WeekdayOrder(DayOfWeek firstDay) =>
        Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)firstDay + i) % 7)).ToArray();
}
