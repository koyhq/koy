using Koy.Core.Extensions.Store;
using static Koy.Core.Tests.Extensions.Store.StoreTestKit;

namespace Koy.Core.Tests.Extensions.Store;

public class StoreUrlPolicyTests
{
    [Theory]
    [InlineData("https://raw.githubusercontent.com/koyhq/koy/main/store/packages/a.koyext", true)]
    [InlineData("https://RAW.GITHUBUSERCONTENT.COM/koyhq/koy/abc123/store/previews/a.png", true)]
    [InlineData("http://raw.githubusercontent.com/koyhq/koy/main/store/packages/a.koyext", false)]
    [InlineData("https://example.com/koyhq/koy/main/a.koyext", false)]
    [InlineData("https://raw.githubusercontent.com.evil.com/koyhq/koy/main/a.koyext", false)]
    [InlineData("https://raw.githubusercontent.com/other/repo/main/a.koyext", false)]
    [InlineData("https://raw.githubusercontent.com/koyhq/koy-fork/main/a.koyext", false)]
    [InlineData("https://raw.githubusercontent.com/koyhq/koy/../evil/a.koyext", false)]
    [InlineData("https://raw.githubusercontent.com:8443/koyhq/koy/main/a.koyext", false)]
    [InlineData("https://user@raw.githubusercontent.com/koyhq/koy/main/a.koyext", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_store_repository_on_raw_githubusercontent_is_trusted(string? url, bool trusted)
    {
        Assert.Equal(trusted, StoreUrlPolicy.IsTrustedAssetUrl(url));
        if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Assert.Equal(trusted, StoreUrlPolicy.IsTrustedAssetUri(uri));
        }
    }

    [Fact]
    public void Final_response_uri_after_a_redirect_is_checked_the_same_way()
    {
        Assert.False(StoreUrlPolicy.IsTrustedAssetUri(new Uri("https://cdn.example.com/koyhq/koy/a.koyext")));
        Assert.False(StoreUrlPolicy.IsTrustedAssetUri(null));
    }

    [Fact]
    public void Index_item_with_untrusted_package_host_is_skipped()
    {
        var item = ItemJson().Replace(PackageUrl, "https://example.com/koyhq/koy/p.koyext");

        var result = StoreIndexParser.Parse(IndexJson(item));

        Assert.Equal(1, result.SkippedItems);
        Assert.Empty(result.Index!.Items);
    }

    [Fact]
    public void Index_item_with_untrusted_preview_keeps_the_item_but_drops_the_preview()
    {
        var hash = new string('a', 64);
        var json = IndexJson(ItemJson(extra: ", \"preview\": { \"url\": \"https://example.com/koyhq/koy/p.png\", \"sha256\": \"" + hash + "\" }"));

        var result = StoreIndexParser.Parse(json);

        Assert.Equal(0, result.SkippedItems);
        Assert.Null(Assert.Single(result.Index!.Items).Preview);
    }
}
