using System.Globalization;
using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

public class IdleDisplayFormatTests
{
    private static readonly CultureInfo Tr = new("tr-TR");
    private static readonly CultureInfo En = new("en-US");

    // 2026-11-17 bir salı.
    private static readonly DateTime Tuesday = new(2026, 11, 17, 19, 21, 5);

    [Fact]
    public void DayTime_uses_short_day_name_and_24_hour_clock()
    {
        Assert.Equal("Sal 19:21", IdleDisplayFormat.DayTime(Tuesday, Tr));
        Assert.Equal("Tue 19:21", IdleDisplayFormat.DayTime(Tuesday, En));
        Assert.Equal("Sal 07:05", IdleDisplayFormat.DayTime(new DateTime(2026, 11, 17, 7, 5, 0), Tr));
    }

    [Fact]
    public void DayTime_uses_the_given_time_pattern()
    {
        Assert.Equal("Tue 7:21 PM", IdleDisplayFormat.DayTime(Tuesday, En, "h:mm tt"));
        Assert.Equal("Sal 19:21:05", IdleDisplayFormat.DayTime(Tuesday, Tr, "HH:mm:ss"));
    }

    [Fact]
    public void ShortDate_follows_language_day_month_order()
    {
        Assert.Equal("17 Kas", IdleDisplayFormat.ShortDate(Tuesday, Tr));
        Assert.Equal("Nov 17", IdleDisplayFormat.ShortDate(Tuesday, En));
    }

    [Fact]
    public void ShortDate_uses_valid_custom_format_and_ignores_invalid_one()
    {
        Assert.Equal("17.11.2026", IdleDisplayFormat.ShortDate(Tuesday, Tr, "dd.MM.yyyy"));
        Assert.Equal("17 Kas", IdleDisplayFormat.ShortDate(Tuesday, Tr, ""));
        Assert.Equal("Nov 17", IdleDisplayFormat.ShortDate(Tuesday, En, "   "));
    }

    [Theory]
    [InlineData(43, "%43", "43%")]
    [InlineData(100, "%100", "100%")]
    [InlineData(150, "%100", "100%")] // taşan değer kıskaçlanır
    [InlineData(-5, "%0", "0%")]
    public void Percent_follows_language_sign_position(int value, string tr, string en)
    {
        Assert.Equal(tr, IdleDisplayFormat.Percent(value, Tr));
        Assert.Equal(en, IdleDisplayFormat.Percent(value, En));
    }

    [Theory]
    [InlineData(12, false, true)]
    [InlineData(20, false, true)]
    [InlineData(21, false, false)]
    [InlineData(12, true, false)] // şarjda düşük renk yok
    public void IsLow_only_when_discharging_at_or_below_threshold(int percent, bool charging, bool expected)
    {
        Assert.Equal(expected, IdleDisplayFormat.IsLow(percent, charging));
    }

    [Theory]
    [InlineData(0, 9, 1.75)]     // en üst
    [InlineData(25, 16.25, 9)]   // sağ
    [InlineData(50, 9, 16.25)]   // alt
    [InlineData(75, 1.75, 9)]    // sol
    public void ArcPoint_starts_at_top_and_goes_clockwise(double percent, double x, double y)
    {
        var (px, py) = IdleDisplayFormat.ArcPoint(percent, 7.25, 9);
        Assert.Equal(x, px, 3);
        Assert.Equal(y, py, 3);
    }

    [Fact]
    public void ArcPath_is_empty_at_zero()
    {
        Assert.Equal("", IdleDisplayFormat.ArcPath(0));
        Assert.Equal("", IdleDisplayFormat.ArcPath(-3));
    }

    [Fact]
    public void ArcPath_uses_small_arc_up_to_half_and_large_arc_after()
    {
        Assert.Equal("M 9,1.75 A 7.25,7.25 0 0 1 16.25,9", IdleDisplayFormat.ArcPath(25));
        Assert.Contains(" 0 0 1 ", IdleDisplayFormat.ArcPath(50));
        Assert.Contains(" 0 1 1 ", IdleDisplayFormat.ArcPath(51));
    }

    [Fact]
    public void ArcPath_draws_full_circle_as_two_half_arcs()
    {
        Assert.Equal("M 9,1.75 A 7.25,7.25 0 1 1 9,16.25 A 7.25,7.25 0 1 1 9,1.75", IdleDisplayFormat.ArcPath(100));
        Assert.Equal(IdleDisplayFormat.ArcPath(100), IdleDisplayFormat.ArcPath(140));
    }

    [Fact]
    public void ArcPath_is_culture_invariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = Tr; // ondalık ayırıcı virgül olan dil
            Assert.DoesNotContain(",7", IdleDisplayFormat.ArcPath(43).Split(' ')[1]);
            Assert.StartsWith("M 9,1.75 A 7.25,7.25", IdleDisplayFormat.ArcPath(43));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
