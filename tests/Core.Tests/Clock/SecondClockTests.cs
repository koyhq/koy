using System.Globalization;
using Koy.Core.Clock;

namespace Koy.Core.Tests.Clock;

public class SecondClockTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/A_Real_Zone")]
    [InlineData("../../etc/passwd")]
    [InlineData("Bad\0Id")]
    public void Missing_or_invalid_id_gives_no_second_clock_and_never_throws(string? id)
    {
        Assert.Null(SecondClock.Resolve(id));
        Assert.Null(SecondClock.Read(id, Noon, "HH:mm", Invariant));
    }

    [Fact]
    public void Utc_id_resolves_on_every_os()
    {
        var reading = SecondClock.Read("UTC", Noon, "HH:mm", Invariant);

        Assert.NotNull(reading);
        Assert.Equal("12:00", reading.Time);
        Assert.Equal("UTC", reading.Label);
    }

    [Fact]
    public void Custom_zone_shifts_the_time_and_labels_the_offset()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test-plus-9", TimeSpan.FromHours(9), "Test", "Test");

        var reading = SecondClock.Read(zone, Noon, "HH:mm", Invariant);

        Assert.Equal("21:00", reading.Time);
        Assert.Equal("UTC+9", reading.Label);
        Assert.Equal("UTC+9 21:00", reading.Text);
    }

    [Fact]
    public void Custom_label_replaces_the_offset_label()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test-minus-5", TimeSpan.FromHours(-5), "Test", "Test");

        var reading = SecondClock.Read(zone, Noon, "h:mm tt", Invariant, "  NYC ");

        Assert.Equal("NYC", reading.Label);
        Assert.Equal("7:00 AM", reading.Time);
    }

    [Fact]
    public void Time_crosses_midnight_correctly()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test-plus-14", TimeSpan.FromHours(14), "Test", "Test");
        var late = new DateTimeOffset(2026, 11, 12, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal("13:30", SecondClock.Read(zone, late, "HH:mm", Invariant).Time);
    }

    [Theory]
    [InlineData(0, "UTC")]
    [InlineData(3, "UTC+3")]
    [InlineData(-5, "UTC-5")]
    [InlineData(5.5, "UTC+5:30")]
    [InlineData(-9.5, "UTC-9:30")]
    [InlineData(12.75, "UTC+12:45")]
    public void Offset_label_is_compact(double hours, string expected)
    {
        Assert.Equal(expected, SecondClock.OffsetLabel(TimeSpan.FromHours(hours)));
    }

    [Fact]
    public void Reading_without_label_has_only_the_time()
    {
        Assert.Equal("09:00", new SecondClockReading("", "09:00").Text);
    }
}
