using System.Globalization;

namespace Koy.Core.Clock;

/// <summary>
/// Saat ve tarih biçim kararları. Saf: kültür ve tarih dışarıdan gelir. Core yalnız .NET biçim desenini ve sayıyı
/// döndürür; metni App biçimlendirir (AGENTS: Core değer döndürür, App biçimlendirir).
/// </summary>
public static class ClockFormatPolicy
{
    public const int MaxCustomDateFormatLength = 64;

    // Özel biçim denetimi için sabit örnek tarih: kayıt öncesi biçim hatası burada yakalanır.
    private static readonly DateTime SampleDate = new(2026, 1, 5, 13, 7, 9);

    /// <summary>12 saatlik mi: Hour12/Hour24 zorlar, Auto kültüre bakar.</summary>
    public static bool UsesTwelveHourClock(ClockHourFormat setting, bool cultureUsesTwelveHour) => setting switch
    {
        ClockHourFormat.Hour12 => true,
        ClockHourFormat.Hour24 => false,
        _ => cultureUsesTwelveHour,
    };

    /// <summary>Saat deseni: "HH:mm", "HH:mm:ss", "h:mm tt", "h:mm:ss tt". "tt" öğlenden önce/sonra işaretini kültürden alır.</summary>
    public static string TimePattern(bool twelveHour, bool showSeconds)
    {
        var hour = twelveHour ? "h" : "HH";
        var pattern = showSeconds ? hour + ":mm:ss" : hour + ":mm";
        return twelveHour ? pattern + " tt" : pattern;
    }

    /// <summary>Haftanın ilk günü: seçim System değilse seçilen gün, değilse kültürün ilk günü.</summary>
    public static DayOfWeek ResolveFirstDayOfWeek(WeekStartSetting setting, DayOfWeek cultureFirstDay) => setting switch
    {
        WeekStartSetting.Saturday => DayOfWeek.Saturday,
        WeekStartSetting.Sunday => DayOfWeek.Sunday,
        WeekStartSetting.Monday => DayOfWeek.Monday,
        _ => cultureFirstDay,
    };

    /// <summary>
    /// Hafta numarası. Kural: yılın ilk haftası, yılın en az dört gününü içeren ilk hafta (ISO 8601 ile aynı mantık;
    /// haftanın ilk günü seçime göre). Yıl sonundaki günler sonraki yılın 1. haftasına düşebilir (Pazartesi
    /// başlangıçta 31.12.2007 → 1). Takvimde gösterilir.
    /// </summary>
    // Calendar.GetWeekOfYear(FirstFourDayWeek) yıl sonunu sonraki yıla taşımaz (31.12.2007 → 53), bu yüzden elle:
    // bir hafta, dördüncü gününün düştüğü yıla aittir; o yılın 1. haftası 4 Ocak'ı içeren haftadır.
    public static int WeekOfYear(DateTime date, DayOfWeek firstDay)
    {
        var weekStart = StartOfWeek(date.Date, firstDay);
        var year = weekStart.AddDays(3).Year;
        var firstWeekStart = StartOfWeek(new DateTime(year, 1, 4), firstDay);
        return (weekStart - firstWeekStart).Days / 7 + 1;
    }

    private static DateTime StartOfWeek(DateTime date, DayOfWeek firstDay) =>
        date.AddDays(-(((int)date.DayOfWeek - (int)firstDay + 7) % 7));

    /// <summary>
    /// Özel tarih biçimi geçerli mi: boş olmayan, en fazla <see cref="MaxCustomDateFormatLength"/> karakter, denetim
    /// karakteri içermeyen ve örnek tarihte biçimlenebilen metin.
    /// </summary>
    public static bool IsValidCustomDateFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format) || format.Length > MaxCustomDateFormatLength)
            return false;
        if (format.Any(char.IsControl))
            return false;

        try
        {
            return SampleDate.ToString(format, CultureInfo.InvariantCulture).Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>İkinci saat açık mı: saat dilimi kimliği dolu olmalı.</summary>
    public static bool HasSecondClock(string? timeZoneId) => !string.IsNullOrWhiteSpace(timeZoneId);
}
