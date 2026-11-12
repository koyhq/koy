using Koy.Core.Calendar;
using Xunit;

namespace Koy.Core.Tests;

public sealed class MonthGridTests
{
    [Fact]
    public void Build_AlwaysReturns42Days()
    {
        var grid = MonthGrid.Build(2026, 11, new DateOnly(2026, 11, 4));
        Assert.Equal(42, grid.Count);
    }

    [Fact]
    public void Build_FirstDayIsMonday()
    {
        var grid = MonthGrid.Build(2026, 11, new DateOnly(2026, 11, 4));
        Assert.Equal(DayOfWeek.Monday, grid[0].Date.DayOfWeek);
    }

    [Fact]
    public void Build_CorrectlyFlagsCurrentMonthAndToday()
    {
        var today = new DateOnly(2026, 11, 4);
        var grid = MonthGrid.Build(2026, 11, today);

        var todayItem = grid.FirstOrDefault(d => d.IsToday);
        Assert.NotNull(todayItem);
        Assert.Equal(today, todayItem.Date);
        Assert.True(todayItem.IsCurrentMonth);

        // November 2026 starts on Sunday (1st Nov 2026 is Sunday)
        // So offset is 6 days (26 Oct 2026 is Monday)
        Assert.False(grid[0].IsCurrentMonth);
        Assert.Equal(new DateOnly(2026, 10, 26), grid[0].Date);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday, 2026, 11, 1)]    // 1 Kasım 2026 Pazar: ızgara ayın ilk günüyle başlar
    [InlineData(DayOfWeek.Saturday, 2026, 10, 31)]
    [InlineData(DayOfWeek.Monday, 2026, 10, 26)]
    public void Build_starts_on_the_chosen_first_day(DayOfWeek firstDay, int y, int m, int d)
    {
        var grid = MonthGrid.Build(2026, 11, new DateOnly(2026, 11, 4), firstDay);

        Assert.Equal(firstDay, grid[0].Date.DayOfWeek);
        Assert.Equal(new DateOnly(y, m, d), grid[0].Date);
        Assert.Contains(grid, c => c.Date == new DateOnly(2026, 11, 30));
    }

    [Fact]
    public void Weekday_order_rotates_from_first_day()
    {
        Assert.Equal(
            [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
            MonthGrid.WeekdayOrder(DayOfWeek.Sunday));
        Assert.Equal(DayOfWeek.Friday, MonthGrid.WeekdayOrder(DayOfWeek.Saturday)[6]);
    }
}
