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

    // Mağaza kartı, "Yerleşik canlı arka planlar" kartıyla aynı adı taşımasın (ikisi "Aurora" görünüyordu); mağaza adları da birbirinden farklı olmalı.
    [Fact]
    public void Store_names_are_unique_and_do_not_clash_with_builtin_background_names()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Koy.slnx")))
            root = root.Parent;
        Assert.NotNull(root);

        var builtin = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new[] { "Strings.resx", "Strings.tr.resx" })
        {
            var doc = System.Xml.Linq.XDocument.Load(Path.Combine(root!.FullName, "src", "App", "Resources", file));
            foreach (var data in doc.Root!.Elements("data"))
            {
                var key = (string)data.Attribute("name")!;
                if (key.StartsWith("LiveBgPreset", StringComparison.Ordinal) && key != "LiveBgPresetTitle")
                    builtin.Add(((string?)data.Element("value"))?.Trim() ?? "");
            }
        }

        var result = StoreIndexParser.Parse(File.ReadAllBytes(Path.Combine(root!.FullName, "store", "index.json")));
        var names = result.Index!.Items.Select(i => i.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, n => Assert.DoesNotContain(n, builtin));
    }
}
