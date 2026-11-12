using Koy.Core.Extensions.Store;
using static Koy.Core.Tests.Extensions.Store.StoreTestKit;

namespace Koy.Core.Tests.Extensions.Store;

public class LocalizedTextTests
{
    private static LocalizedText Both => new(new Dictionary<string, string> { ["en"] = "Hello", ["tr"] = "Merhaba" });

    [Fact]
    public void Resolve_picks_the_requested_language_then_english_then_anything()
    {
        Assert.Equal("Merhaba", Both.Resolve("tr"));
        Assert.Equal("Merhaba", Both.Resolve("TR"));
        Assert.Equal("Hello", Both.Resolve("de"));
        Assert.Equal("Hello", Both.Resolve(null));
        Assert.Equal("Hello", Both.Resolve(""));
        Assert.Equal("Hallo", new LocalizedText(new Dictionary<string, string> { ["de"] = "Hallo" }).Resolve("fr"));
        Assert.Equal("", LocalizedText.Empty.Resolve("tr"));
    }

    [Fact]
    public void Plain_text_is_the_same_in_every_language()
    {
        LocalizedText plain = "Ozet";

        Assert.Equal("Ozet", plain.Resolve("tr"));
        Assert.Equal("Ozet", plain.Resolve("en"));
    }

    [Fact]
    public void Index_accepts_plain_string_and_language_object()
    {
        var plain = StoreIndexParser.Parse(IndexJson(ItemJson()));
        var localized = StoreIndexParser.Parse(IndexJson(ItemJson().Replace(
            "\"summary\": \"Ozet\"", "\"summary\": { \"en\": \"Summary\", \"tr\": \"Özet\" }")));

        Assert.Equal("Ozet", Assert.Single(plain.Index!.Items).Summary.Resolve("tr"));
        var item = Assert.Single(localized.Index!.Items);
        Assert.Equal("Özet", item.Summary.Resolve("tr"));
        Assert.Equal("Summary", item.Summary.Resolve("fr"));
    }

    [Fact]
    public void Description_may_be_a_language_object_too()
    {
        var json = IndexJson(ItemJson(extra: ", \"description\": { \"en\": \"a\\nb\", \"tr\": \"c\\nd\" }"));

        var item = Assert.Single(StoreIndexParser.Parse(json).Index!.Items);

        Assert.Equal("c\nd", item.Description.Resolve("tr"));
        Assert.Equal("a\nb", item.Description.Resolve("en"));
    }

    [Theory]
    [InlineData("{ \"tr\": \"Özet\" }")]                          // "en" yok
    [InlineData("{}")]
    [InlineData("{ \"en\": \"\" }")]                              // boş özet
    [InlineData("{ \"en\": \"x\", \"tr\": \"\" }")]
    [InlineData("{ \"en\": \"x\", \"turkish\": \"y\" }")]          // dil kodu iki harf değil
    [InlineData("{ \"en\": \"x\", \"TR\": \"y\" }")]               // büyük harf
    [InlineData("{ \"en\": \"x\", \"tr\": 5 }")]
    [InlineData("{ \"en\": \"x\", \"en\": \"y\" }")]               // yinelenen dil
    [InlineData("{ \"en\": \"x\", \"tr\": \"a\\nb\" }")]            // özette satır sonu yok
    [InlineData("{ \"en\": \"x\", \"tr\": \"a\\u202Eb\" }")]        // yön karakteri
    [InlineData("[\"x\"]")]
    [InlineData("5")]
    [InlineData("null")]
    public void Bad_localized_summary_skips_the_item(string summary)
    {
        var item = ItemJson().Replace("\"summary\": \"Ozet\"", "\"summary\": " + summary);

        var result = StoreIndexParser.Parse(IndexJson(item));

        Assert.Equal(1, result.SkippedItems);
        Assert.Empty(result.Index!.Items);
    }

    [Fact]
    public void Every_language_value_obeys_the_length_limit()
    {
        var tooLong = new string('a', 201);
        var item = ItemJson().Replace("\"summary\": \"Ozet\"", "\"summary\": { \"en\": \"x\", \"tr\": \"" + tooLong + "\" }");
        var longDescription = new string('a', 2001);

        Assert.Equal(1, StoreIndexParser.Parse(IndexJson(item)).SkippedItems);
        Assert.Equal(1, StoreIndexParser.Parse(IndexJson(ItemJson(
            extra: ", \"description\": { \"en\": \"x\", \"tr\": \"" + longDescription + "\" }"))).SkippedItems);
    }

    [Fact]
    public void Catalog_shows_the_ui_language_and_searches_it()
    {
        var item = Item("aa", name: "Alpha") with
        {
            Summary = new LocalizedText(new Dictionary<string, string> { ["en"] = "Dark blue", ["tr"] = "Koyu mavi" }),
            Description = new LocalizedText(new Dictionary<string, string> { ["en"] = "Long", ["tr"] = "Uzun" }),
        };
        var state = StoreListState.Initial with { Index = new StoreIndex(null, [item]) };

        var en = Assert.Single((state with { Language = "en" }).Visible());
        var tr = Assert.Single((state with { Language = "tr" }).Visible());
        var searchTr = (state with { Language = "tr", Query = new StoreQuery(Search: "koyu") }).Visible();

        Assert.Equal(("Dark blue", "Long"), (en.Summary, en.Description));
        Assert.Equal(("Koyu mavi", "Uzun"), (tr.Summary, tr.Description));
        Assert.Single(searchTr);
        Assert.Empty((state with { Language = "en", Query = new StoreQuery(Search: "koyu") }).Visible());
    }
}
