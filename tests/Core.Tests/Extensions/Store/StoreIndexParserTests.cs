using System.Text;
using Koy.Core.Extensions.Store;
using static Koy.Core.Tests.Extensions.Store.StoreTestKit;

namespace Koy.Core.Tests.Extensions.Store;

public class StoreIndexParserTests
{
    [Fact]
    public void Valid_index_is_parsed()
    {
        var result = StoreIndexParser.Parse(IndexJson(ItemJson(extra: ", \"downloads\": 7, \"homepage\": \"https://github.com/koyhq/koy\", \"description\": \"a\\nb\"")));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.SkippedItems);
        var item = Assert.Single(result.Index!.Items);
        Assert.Equal("koyhq.ornek", item.Id);
        Assert.Equal(ExtensionKind.Theme, item.Kind);
        Assert.Equal(7, item.Downloads);
        Assert.Equal(new DateOnly(2026, 11, 12), item.Updated);
        Assert.Equal("a\nb", item.Description);
        Assert.Equal(1234, item.Package.Size);
        Assert.Equal(new string('a', 64), item.Package.Sha256);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero), result.Index.Generated);
    }

    [Theory]
    [InlineData("theme", ExtensionKind.Theme)]
    [InlineData("iconpack", ExtensionKind.IconPack)]
    [InlineData("background", ExtensionKind.Background)]
    [InlineData("profile", ExtensionKind.Profile)]
    public void Closed_set_of_kinds_is_parsed(string json, ExtensionKind kind)
    {
        var result = StoreIndexParser.Parse(IndexJson(ItemJson(kind: json)));

        Assert.Equal(kind, Assert.Single(result.Index!.Items).Kind);
    }

    [Theory]
    [InlineData("Theme")]
    [InlineData("widget")]
    [InlineData("")]
    public void Unknown_kind_skips_only_that_item(string kind)
    {
        var result = StoreIndexParser.Parse(IndexJson(ItemJson(kind: kind), ItemJson(id: "koyhq.baska")));

        Assert.Equal(1, result.SkippedItems);
        Assert.Equal("koyhq.baska", Assert.Single(result.Index!.Items).Id);
    }

    [Fact]
    public void Duplicate_id_skips_the_second()
    {
        var first = ItemJson(extra: ", \"downloads\": 1");
        var second = ItemJson(extra: ", \"downloads\": 2");
        var result = StoreIndexParser.Parse(IndexJson(first, second));

        Assert.Equal(1, result.SkippedItems);
        Assert.Equal(1, Assert.Single(result.Index!.Items).Downloads);
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        var result = StoreIndexParser.Parse(IndexJson(ItemJson(extra: ", \"rating\": 5, \"tags\": [\"a\"]")));

        Assert.Single(result.Index!.Items);
        Assert.Equal(0, result.SkippedItems);
    }

    [Fact]
    public void Http_package_url_skips_item()
    {
        var item = ItemJson().Replace(PackageUrl, "http://example.com/p.koyext");

        var result = StoreIndexParser.Parse(IndexJson(item));

        Assert.Equal(1, result.SkippedItems);
        Assert.Empty(result.Index!.Items);
    }

    [Theory]
    [InlineData("ftp://example.com/p")]
    [InlineData("file:///c:/p.koyext")]
    [InlineData("https://user:pw@example.com/p")]
    [InlineData("https://exa mple.com/p")]
    [InlineData("//example.com/p")]
    [InlineData("example.com/p")]
    public void Bad_urls_skip_item(string url)
    {
        var item = ItemJson().Replace(PackageUrl, url);

        Assert.Equal(1, StoreIndexParser.Parse(IndexJson(item)).SkippedItems);
    }

    [Fact]
    public void Http_homepage_skips_item()
    {
        var result = StoreIndexParser.Parse(IndexJson(ItemJson(extra: ", \"homepage\": \"http://example.com\"")));

        Assert.Equal(1, result.SkippedItems);
    }

    [Fact]
    public void Bad_preview_is_dropped_but_item_is_kept()
    {
        var result = StoreIndexParser.Parse(IndexJson(ItemJson(extra: ", \"preview\": { \"url\": \"http://x.com/p.png\", \"sha256\": \"zz\" }")));

        Assert.Equal(0, result.SkippedItems);
        Assert.Null(Assert.Single(result.Index!.Items).Preview);
    }

    [Fact]
    public void Good_preview_is_kept_and_hash_is_lowercased()
    {
        var hash = new string('A', 64);
        var result = StoreIndexParser.Parse(IndexJson(ItemJson(extra: ", \"preview\": { \"url\": \"" + PreviewUrl + "\", \"sha256\": \"" + hash + "\" }")));

        Assert.Equal(new string('a', 64), Assert.Single(result.Index!.Items).Preview!.Sha256);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void Bad_sha256_skips_item(string hash)
    {
        var item = ItemJson().Replace(new string('a', 64), hash);

        Assert.Equal(1, StoreIndexParser.Parse(IndexJson(item)).SkippedItems);
    }

    [Fact]
    public void Non_hex_sha256_skips_item()
    {
        var item = ItemJson().Replace(new string('a', 64), new string('g', 64));

        Assert.Equal(1, StoreIndexParser.Parse(IndexJson(item)).SkippedItems);
    }

    [Theory]
    [InlineData("Bad Id")]
    [InlineData("X")]
    [InlineData("con")]
    [InlineData("a..b")]
    public void Invalid_id_skips_item(string id)
    {
        Assert.Equal(1, StoreIndexParser.Parse(IndexJson(ItemJson(id: id))).SkippedItems);
    }

    [Fact]
    public void Summary_over_limit_skips_item_and_limit_itself_is_accepted()
    {
        var atLimit = ItemJson().Replace("\"Ozet\"", "\"" + new string('s', 200) + "\"");
        var over = ItemJson(id: "koyhq.uzun").Replace("\"Ozet\"", "\"" + new string('s', 201) + "\"");

        var result = StoreIndexParser.Parse(IndexJson(atLimit, over));

        Assert.Equal(1, result.SkippedItems);
        Assert.Single(result.Index!.Items);
    }

    [Fact]
    public void Description_over_limit_skips_item_and_limit_itself_is_accepted()
    {
        var atLimit = ItemJson(extra: ", \"description\": \"" + new string('d', 2000) + "\"");
        var over = ItemJson(id: "koyhq.uzun", extra: ", \"description\": \"" + new string('d', 2001) + "\"");

        var result = StoreIndexParser.Parse(IndexJson(atLimit, over));

        Assert.Equal(1, result.SkippedItems);
        Assert.Equal(2000, Assert.Single(result.Index!.Items).Description.Length);
    }

    [Fact]
    public void Summary_with_newline_is_rejected_but_description_allows_it()
    {
        var bad = ItemJson().Replace("\"Ozet\"", "\"a\\nb\"");

        Assert.Equal(1, StoreIndexParser.Parse(IndexJson(bad)).SkippedItems);
    }

    [Fact]
    public void Missing_required_fields_skip_item()
    {
        var noLicense = ItemJson().Replace("\"license\": \"GPL-3.0-or-later\",", "");
        var noUpdated = ItemJson(id: "koyhq.b").Replace("\"updated\": \"2026-11-12\",", "");
        var badDate = ItemJson(id: "koyhq.c").Replace("2026-11-12\",", "12.11.2026\",");

        var result = StoreIndexParser.Parse(IndexJson(noLicense, noUpdated, badDate));

        Assert.Equal(3, result.SkippedItems);
        Assert.Empty(result.Index!.Items);
    }

    [Fact]
    public void Negative_downloads_or_size_skip_item()
    {
        var negativeDownloads = ItemJson(extra: ", \"downloads\": -1");
        var zeroSize = ItemJson(id: "koyhq.b").Replace("\"size\": 1234", "\"size\": 0");
        var hugeSize = ItemJson(id: "koyhq.c").Replace("\"size\": 1234", "\"size\": " + (PackageEntryPolicy.MaxDownloadBytes + 1));

        Assert.Equal(3, StoreIndexParser.Parse(IndexJson(negativeDownloads, zeroSize, hugeSize)).SkippedItems);
    }

    [Fact]
    public void Non_object_item_is_skipped()
    {
        var result = StoreIndexParser.Parse("{ \"format\": \"koy-store-index\", \"formatVersion\": 1, \"items\": [1, \"x\", null, " + ItemJson() + "] }");

        Assert.Equal(3, result.SkippedItems);
        Assert.Single(result.Index!.Items);
    }

    [Fact]
    public void Wrong_format_name_is_rejected()
    {
        var result = StoreIndexParser.Parse("{ \"format\": \"other\", \"formatVersion\": 1, \"items\": [] }");

        Assert.Equal(StoreIndexError.NotAStoreIndex, result.Error);
        Assert.Null(result.Index);
    }

    [Fact]
    public void Newer_format_version_is_rejected()
    {
        var result = StoreIndexParser.Parse("{ \"format\": \"koy-store-index\", \"formatVersion\": 2, \"items\": [] }");

        Assert.Equal(StoreIndexError.UnsupportedFormatVersion, result.Error);
    }

    [Theory]
    [InlineData("{ \"format\": \"koy-store-index\", \"items\": [] }")]
    [InlineData("{ \"format\": \"koy-store-index\", \"formatVersion\": 0, \"items\": [] }")]
    [InlineData("{ \"format\": \"koy-store-index\", \"formatVersion\": \"1\", \"items\": [] }")]
    [InlineData("{ \"format\": \"koy-store-index\", \"formatVersion\": 1 }")]
    [InlineData("{ \"format\": \"koy-store-index\", \"formatVersion\": 1, \"items\": {} }")]
    [InlineData("[]")]
    public void Structurally_wrong_index_is_rejected(string json)
    {
        Assert.Equal(StoreIndexError.NotAStoreIndex, StoreIndexParser.Parse(json).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("{ \"format\": ")]
    public void Malformed_json_is_rejected(string json)
    {
        Assert.Equal(StoreIndexError.MalformedJson, StoreIndexParser.Parse(json).Error);
    }

    [Fact]
    public void Null_text_is_rejected_without_throwing()
    {
        Assert.False(StoreIndexParser.Parse((string?)null).IsSuccess);
    }

    [Fact]
    public void More_than_500_items_reject_whole_index_and_exactly_500_is_accepted()
    {
        string[] Make(int n) => Enumerable.Range(0, n).Select(i => ItemJson(id: "koyhq.i" + i)).ToArray();

        Assert.Equal(StoreIndexError.TooManyItems, StoreIndexParser.Parse(IndexJson(Make(501))).Error);
        Assert.Equal(500, StoreIndexParser.Parse(IndexJson(Make(500))).Index!.Items.Count);
    }

    [Fact]
    public void Index_over_one_megabyte_is_rejected_before_parsing()
    {
        var big = "{ \"format\": \"koy-store-index\", \"formatVersion\": 1, \"items\": [], \"pad\": \"" + new string('x', StoreIndexParser.MaxBytes) + "\" }";

        Assert.Equal(StoreIndexError.TooLarge, StoreIndexParser.Parse(big).Error);
        Assert.Equal(StoreIndexError.TooLarge, StoreIndexParser.Parse(Encoding.UTF8.GetBytes(big)).Error);
    }

    [Fact]
    public void Multibyte_text_is_measured_in_bytes()
    {
        // 600k karakter ama 1.2 MB UTF-8: karakter sayısına bakan bir denetim bunu kaçırırdı.
        var big = "{ \"pad\": \"" + new string('ş', 600_000) + "\" }";

        Assert.Equal(StoreIndexError.TooLarge, StoreIndexParser.Parse(big).Error);
    }

    [Fact]
    public void Utf8_bom_is_tolerated()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(IndexJson(ItemJson()))).ToArray();

        Assert.True(StoreIndexParser.Parse(bytes).IsSuccess);
    }

    [Fact]
    public void Deeply_nested_json_is_rejected_not_thrown()
    {
        var json = "{ \"format\": \"koy-store-index\", \"formatVersion\": 1, \"items\": [], \"x\": " + new string('[', 200) + new string(']', 200) + " }";

        Assert.Equal(StoreIndexError.MalformedJson, StoreIndexParser.Parse(json).Error);
    }

    [Fact]
    public void Real_store_index_in_repository_parses_without_skips()
    {
        var root = RepoRoot();
        if (root is null || !File.Exists(Path.Combine(root, "store", "index.json")))
        {
            return; // depo kökü yok (kaynak dışı çalıştırma): atla
        }

        var result = StoreIndexParser.Parse(File.ReadAllBytes(Path.Combine(root, "store", "index.json")));

        Assert.True(result.IsSuccess, result.Error.ToString());
        Assert.Equal(0, result.SkippedItems);
        var ids = result.Index!.Items.Select(i => i.Id).ToHashSet();
        foreach (var id in new[]
                 {
                     "koyhq.nord-theme", "koyhq.solarized-theme", "koyhq.high-contrast-theme",
                     "koyhq.mono-icons", "koyhq.aurora-background", "koyhq.minimal-profile",
                 })
        {
            Assert.Contains(id, ids);
        }
    }
}
