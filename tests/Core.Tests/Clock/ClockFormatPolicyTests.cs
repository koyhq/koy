using Koy.Core.Clock;

namespace Koy.Core.Tests.Clock;

public class ClockFormatPolicyTests
{
    [Theory]
    [InlineData(ClockHourFormat.Auto, true, true)]
    [InlineData(ClockHourFormat.Auto, false, false)]
    [InlineData(ClockHourFormat.Hour12, false, true)]
    [InlineData(ClockHourFormat.Hour24, true, false)]
    public void Hour_format_setting_overrides_culture_only_when_explicit(ClockHourFormat setting, bool culture12, bool expected)
        => Assert.Equal(expected, ClockFormatPolicy.UsesTwelveHourClock(setting, culture12));

    [Theory]
    [InlineData(false, false, "HH:mm")]
    [InlineData(false, true, "HH:mm:ss")]
    [InlineData(true, false, "h:mm tt")]
    [InlineData(true, true, "h:mm:ss tt")]
    public void Time_pattern_follows_hour_and_seconds(bool twelveHour, bool seconds, string expected)
        => Assert.Equal(expected, ClockFormatPolicy.TimePattern(twelveHour, seconds));

    [Theory]
    [InlineData("en-US", ClockHourFormat.Auto, false, "h:mm tt")]
    [InlineData("tr-TR", ClockHourFormat.Auto, false, "HH:mm")]
    [InlineData("tr-TR", ClockHourFormat.Hour12, true, "h:mm:ss tt")]
    [InlineData("en-US", ClockHourFormat.Hour24, false, "HH:mm")]
    public void Resolved_pattern_follows_culture_on_auto(string culture, ClockHourFormat setting, bool seconds, string expected)
        => Assert.Equal(expected, ClockFormatPolicy.ResolveTimePattern(setting, seconds, new System.Globalization.CultureInfo(culture)));

    [Fact]
    public void Time_pattern_formats_a_known_instant()
    {
        var at = new DateTime(2026, 1, 5, 13, 7, 9);

        Assert.Equal("13:07:09", at.ToString(ClockFormatPolicy.TimePattern(false, true), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("1:07 PM", at.ToString(ClockFormatPolicy.TimePattern(true, false), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(WeekStartSetting.System, DayOfWeek.Sunday, DayOfWeek.Sunday)]
    [InlineData(WeekStartSetting.System, DayOfWeek.Monday, DayOfWeek.Monday)]
    [InlineData(WeekStartSetting.Saturday, DayOfWeek.Monday, DayOfWeek.Saturday)]
    [InlineData(WeekStartSetting.Sunday, DayOfWeek.Monday, DayOfWeek.Sunday)]
    [InlineData(WeekStartSetting.Monday, DayOfWeek.Sunday, DayOfWeek.Monday)]
    public void First_day_of_week_uses_culture_only_for_system(WeekStartSetting setting, DayOfWeek culture, DayOfWeek expected)
        => Assert.Equal(expected, ClockFormatPolicy.ResolveFirstDayOfWeek(setting, culture));

    [Fact]
    public void Week_of_year_with_monday_start_matches_iso_week()
    {
        // ISO 8601: 2026-03-22 (Pazar) 12. haftada (16-22 Mart).
        Assert.Equal(12, ClockFormatPolicy.WeekOfYear(new DateTime(2026, 3, 22), DayOfWeek.Monday));
    }

    [Fact]
    public void Week_of_year_with_monday_start_matches_iso_week_for_every_day()
    {
        for (var day = new DateTime(2000, 1, 1); day.Year < 2041; day = day.AddDays(1))
            Assert.Equal(System.Globalization.ISOWeek.GetWeekOfYear(day), ClockFormatPolicy.WeekOfYear(day, DayOfWeek.Monday));
    }

    [Theory]
    // Calendar.GetWeekOfYear bunları 53 sayar; hafta, dördüncü gününün düştüğü yeni yıla aittir.
    [InlineData(2007, 12, 31, DayOfWeek.Monday, 1)]
    [InlineData(2024, 12, 29, DayOfWeek.Sunday, 1)]
    // Yıl başındaki günler önceki yılın son haftasında kalır.
    [InlineData(2021, 1, 1, DayOfWeek.Monday, 53)]
    public void Week_of_year_moves_days_across_the_year_boundary(int y, int m, int d, DayOfWeek firstDay, int expected)
        => Assert.Equal(expected, ClockFormatPolicy.WeekOfYear(new DateTime(y, m, d), firstDay));

    [Fact]
    public void Week_of_year_ignores_the_time_of_day()
        => Assert.Equal(1, ClockFormatPolicy.WeekOfYear(new DateTime(2007, 12, 31, 23, 59, 0), DayOfWeek.Monday));

    [Fact]
    public void Week_of_year_first_week_starts_on_the_first_day_that_has_four_days_in_the_year()
    {
        // 4 gün kuralı: 2026 Ocak'ın ilk haftası Cumartesi başlangıçlıysa 3 Ocak Cumartesi'dir (Ocak 3-9, yılın 7 günü).
        Assert.Equal(1, ClockFormatPolicy.WeekOfYear(new DateTime(2026, 1, 3), DayOfWeek.Saturday));
        Assert.Equal(1, ClockFormatPolicy.WeekOfYear(new DateTime(2026, 1, 9), DayOfWeek.Saturday));
        Assert.Equal(2, ClockFormatPolicy.WeekOfYear(new DateTime(2026, 1, 10), DayOfWeek.Saturday));
    }

    [Theory]
    [InlineData("dd.MM.yyyy")]
    [InlineData("ddd, d MMMM")]
    [InlineData("yyyy-MM-dd")]
    public void Valid_custom_date_formats_are_accepted(string format)
        => Assert.True(ClockFormatPolicy.IsValidCustomDateFormat(format));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\\")]
    [InlineData("dd\tMM")]
    public void Invalid_custom_date_formats_are_rejected(string? format)
        => Assert.False(ClockFormatPolicy.IsValidCustomDateFormat(format));

    [Fact]
    public void Custom_date_format_longer_than_the_limit_is_rejected()
    {
        var tooLong = new string('d', ClockFormatPolicy.MaxCustomDateFormatLength + 1);
        var atLimit = new string('d', ClockFormatPolicy.MaxCustomDateFormatLength);

        Assert.False(ClockFormatPolicy.IsValidCustomDateFormat(tooLong));
        Assert.True(ClockFormatPolicy.IsValidCustomDateFormat(atLimit));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Tokyo Standard Time", true)]
    public void Second_clock_is_on_only_with_a_time_zone_id(string? id, bool expected)
        => Assert.Equal(expected, ClockFormatPolicy.HasSecondClock(id));

    [Theory]
    [InlineData("12:00", null, null, "12:00")]
    [InlineData("12:00", "", "  ", "12:00")]
    [InlineData("12:00", "12 Nov", null, "12:00 · 12 Nov")]
    [InlineData("12:00", null, "UTC+9 21:00", "12:00 · UTC+9 21:00")]
    [InlineData("12:00", "12 Nov", "UTC+9 21:00", "12:00 · 12 Nov · UTC+9 21:00")]
    [InlineData("", null, "UTC+9 21:00", " · UTC+9 21:00")]
    public void Peek_line_adds_only_what_is_enabled(string time, string? date, string? second, string expected)
        => Assert.Equal(expected, ClockFormatPolicy.ComposePeekLine(time, date, second));

    [Fact]
    public void Notch_defaults_keep_current_behaviour()
    {
        var notch = new Koy.Core.Settings.NotchSettings();

        Assert.False(notch.ClockShowDate);
        Assert.Equal("", notch.ClockSecondTimeZoneId);
        Assert.False(ClockFormatPolicy.HasSecondClock(notch.ClockSecondTimeZoneId));
    }
}
