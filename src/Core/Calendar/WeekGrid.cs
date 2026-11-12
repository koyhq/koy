namespace Koy.Core.Calendar;

/// <summary>
/// Hafta görünümünün gün dizilimi. Saf: tarih ve haftanın ilk günü dışarıdan gelir
/// (<see cref="Clock.ClockFormatPolicy.ResolveFirstDayOfWeek"/>), böylece hafta görünümü ay ızgarasıyla aynı günle başlar.
/// </summary>
public static class WeekGrid
{
    public const int DaysInWeek = 7;

    /// <summary>Günün ait olduğu haftanın ilk günü (verilen günü içeren, <paramref name="firstDay"/> ile başlayan hafta).</summary>
    public static DateOnly Start(DateOnly day, DayOfWeek firstDay)
    {
        var offset = ((int)day.DayOfWeek - (int)firstDay + DaysInWeek) % DaysInWeek;
        return day.AddDays(-offset);
    }

    /// <summary>Günün haftasındaki 7 gün, <paramref name="firstDay"/> ile başlayarak.</summary>
    public static IReadOnlyList<DateOnly> Days(DateOnly day, DayOfWeek firstDay)
    {
        var start = Start(day, firstDay);
        var days = new DateOnly[DaysInWeek];
        for (var i = 0; i < DaysInWeek; i++)
            days[i] = start.AddDays(i);
        return days;
    }
}
