using Koy.Core.Search;

namespace Koy.Core.Tests.Search;

public class FileStampCacheTests
{
    [Fact]
    public void Same_stamp_reuses_value()
    {
        var cache = new FileStampCache<string>();
        var loads = 0;

        var first = cache.GetOrLoad("a", 10, 100, () => "v" + ++loads);
        var second = cache.GetOrLoad("A", 10, 100, () => "v" + ++loads);

        Assert.Equal("v1", first);
        Assert.Equal("v1", second);
        Assert.Equal(1, loads);
    }

    [Theory]
    [InlineData(11, 100)]
    [InlineData(10, 101)]
    public void Changed_write_time_or_size_reloads(long writeTicks, long length)
    {
        var cache = new FileStampCache<string>();
        var loads = 0;
        cache.GetOrLoad("a", 10, 100, () => "v" + ++loads);

        var value = cache.GetOrLoad("a", writeTicks, length, () => "v" + ++loads);

        Assert.Equal("v2", value);
    }

    [Fact]
    public void Removed_entry_is_loaded_again()
    {
        var cache = new FileStampCache<int>();
        var loads = 0;
        cache.GetOrLoad("a", 1, 1, () => ++loads);
        cache.Remove("a");
        cache.GetOrLoad("a", 1, 1, () => ++loads);

        Assert.Equal(2, loads);
    }
}
