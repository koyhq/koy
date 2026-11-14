namespace Koy.Core.Calendar;

/// <summary>Takvim görünümü: ay ızgarası, hafta listesi ya da tek gün.</summary>
public enum CalendarViewMode
{
    Month,
    Week,
    Day
}

/// <summary>Etkinliğin kaynağı: yerel defter ya da salt okunur bağlı hesaplar.</summary>
public enum CalendarEventSource
{
    Local = 0,
    Google = 1,
    Outlook = 2
}

/// <summary>
/// Takvim etkinliği. <paramref name="Start"/> yerel saattir (saat dilimi taşımaz);
/// <paramref name="DurationMinutes"/> 0 ise tek bir an (hatırlatma gibi). Harici kaynaklı etkinlikler salt okunurdur.
/// </summary>
public sealed record CalendarEvent(
    string Id,
    string Title,
    DateTime Start,
    int DurationMinutes,
    CalendarEventSource Source = CalendarEventSource.Local)
{
    public DateTime End => Start.AddMinutes(DurationMinutes);

    public DateOnly Date => DateOnly.FromDateTime(Start);

    public bool IsReadOnly => Source != CalendarEventSource.Local;
}

/// <summary>Bir günün etkinlikleri (hafta görünümünün satırı).</summary>
public sealed record DayAgenda(DateOnly Date, bool IsToday, IReadOnlyList<CalendarEvent> Events);
