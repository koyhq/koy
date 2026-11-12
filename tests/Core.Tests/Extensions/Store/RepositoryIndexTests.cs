using Koy.Core.Extensions.Store;

namespace Koy.Core.Tests.Extensions.Store;

public class RepositoryIndexTests
{
    // Depodaki gerçek store/index.json uygulamanın ayrıştırıcısından atlanmadan geçmeli (betik ve C# kuralları ayrışmasın).
    [Fact]
    public void Committed_store_index_parses_without_skipped_items_and_has_turkish_texts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "store", "index.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var result = StoreIndexParser.Parse(File.ReadAllBytes(Path.Combine(dir!.FullName, "store", "index.json")));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.SkippedItems);
        Assert.NotEmpty(result.Index!.Items);
        foreach (var item in result.Index.Items)
        {
            Assert.NotEqual(item.Summary.Resolve("en"), item.Summary.Resolve("tr"));
            Assert.NotEqual(item.Description.Resolve("en"), item.Description.Resolve("tr"));
        }
    }
}
