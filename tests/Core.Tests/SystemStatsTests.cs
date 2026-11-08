using System.Globalization;
using Koy.Core.SystemStats;

namespace Koy.Core.Tests;

public class SystemStatsMathTests
{
    [Fact]
    public void Cpu_is_busy_share_where_kernel_includes_idle()
    {
        // delta: kernel 800 (idle 600 dahil), user 200 -> toplam 1000, boşta 600, meşgul 400
        Assert.Equal(40, SystemStatsMath.CpuPercent(0, 0, 0, 600, 800, 200), 6);
    }

    [Fact]
    public void Cpu_is_zero_without_elapsed_time_or_when_counters_go_back()
    {
        Assert.Equal(0, SystemStatsMath.CpuPercent(10, 10, 10, 10, 10, 10));
        Assert.Equal(0, SystemStatsMath.CpuPercent(100, 100, 100, 50, 50, 50));
    }

    [Fact]
    public void Cpu_is_clamped_to_0_100()
    {
        Assert.Equal(100, SystemStatsMath.CpuPercent(0, 0, 0, 0, 100, 0));
        Assert.Equal(0, SystemStatsMath.CpuPercent(0, 0, 0, 100, 100, 0));
    }

    [Fact]
    public void Bytes_per_second_divides_by_elapsed()
    {
        Assert.Equal(1000, SystemStatsMath.BytesPerSecond(1000, 3000, 2.0));
    }

    [Fact]
    public void Bytes_per_second_ignores_counter_reset_and_bad_time()
    {
        Assert.Equal(0, SystemStatsMath.BytesPerSecond(5000, 100, 1.0));
        Assert.Equal(0, SystemStatsMath.BytesPerSecond(0, 100, 0));
    }

    [Fact]
    public void Percent_handles_zero_total()
    {
        Assert.Equal(0, SystemStatsMath.Percent(5, 0));
        Assert.Equal(50, SystemStatsMath.Percent(8, 16));
    }

    [Fact]
    public void Process_cpu_divides_by_cores()
    {
        // 1 sn'de 1 çekirdek tam dolu, 4 çekirdek -> %25
        Assert.Equal(25, SystemStatsMath.ProcessCpuPercent(1.0, 1.0, 4), 6);
        Assert.Equal(0, SystemStatsMath.ProcessCpuPercent(-1, 1, 4));
    }
}

public class TopProcessPickerTests
{
    [Fact]
    public void Picks_largest_delta_and_ignores_idle_and_new_processes()
    {
        var previous = new Dictionary<int, double> { [10] = 5, [20] = 5, [0] = 0 };
        ProcessCpuSample[] now =
        [
            new(0, "Idle", 999),
            new(10, "a", 5.5),
            new(20, "b", 7.0),
            new(30, "yeni", 500)
        ];

        var top = TopProcessPicker.Pick(previous, now, 3.0, 2);

        Assert.NotNull(top);
        Assert.Equal("b", top!.Value.Name);
        Assert.Equal(2.0 * 100 / (3.0 * 2), top.Value.Percent, 6);
    }

    [Fact]
    public void Returns_null_when_nothing_used_cpu()
    {
        var previous = new Dictionary<int, double> { [10] = 5 };
        Assert.Null(TopProcessPicker.Pick(previous, [new ProcessCpuSample(10, "a", 5)], 3, 4));
    }
}

public class SystemFormatTests
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(0, "0 B/sn")]
    [InlineData(512, "512 B/sn")]
    [InlineData(360 * 1024, "360 KB/sn")]
    [InlineData(2621440, "2,5 MB/sn")]
    [InlineData(2 * 1024 * 1024, "2 MB/sn")]
    [InlineData(1536, "1,5 KB/sn")]
    [InlineData(1024L * 1024 * 1024 * 3, "3 GB/sn")]
    [InlineData(-5, "0 B/sn")]
    public void Speed(long bytes, string expected) => Assert.Equal(expected, SystemFormat.Speed(bytes, Tr));

    [Fact]
    public void Speed_rounding_rolls_over_to_next_unit() =>
        Assert.Equal("1 MB/sn", SystemFormat.Speed(1024 * 1024 - 1, Tr));

    [Fact]
    public void Memory_uses_one_decimal_for_used_and_trims_whole_total()
    {
        const long gb = 1024L * 1024 * 1024;
        Assert.Equal("9,2 / 16 GB", SystemFormat.Memory((long)(9.2 * gb), 16 * gb, Tr));
        Assert.Equal("9,0 / 15,8 GB", SystemFormat.Memory(9 * gb, (long)(15.8 * gb), Tr));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(150L * 1024 * 1024, "150 MB")]
    [InlineData(1503238554L, "1,4 GB")]
    public void Size(long bytes, string expected) => Assert.Equal(expected, SystemFormat.Size(bytes, Tr));

    [Theory]
    [InlineData(42.4, "%42")]
    [InlineData(42.5, "%43")]
    [InlineData(-3, "%0")]
    [InlineData(140, "%100")]
    public void Percent(double value, string expected) => Assert.Equal(expected, SystemFormat.Percent(value, Tr));

    [Fact]
    public void English_uses_decimal_point_seconds_and_trailing_percent()
    {
        const long gb = 1024L * 1024 * 1024;
        Assert.Equal("2.5 MB/s", SystemFormat.Speed(2621440, En));
        Assert.Equal("2 MB/s", SystemFormat.Speed(2 * 1024 * 1024, En));
        Assert.Equal("1.4 GB", SystemFormat.Size(1503238554L, En));
        Assert.Equal("9.2 / 16 GB", SystemFormat.Memory((long)(9.2 * gb), 16 * gb, En));
        Assert.Equal("42%", SystemFormat.Percent(42.4, En));
    }
}
