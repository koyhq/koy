using Koy.Core.Extensions.Store;

namespace Koy.Core.Tests.Extensions.Store;

public class ExtensionVersionTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("1.9.0", "1.10.0", -1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("0.0.9", "0.0.10", -1)]
    [InlineData("10.0.0", "9.0.0", 1)]
    [InlineData("999999.999999.999999", "999999.999999.999998", 1)]
    public void Numeric_comparison(string left, string right, int sign)
    {
        Assert.Equal(sign, Math.Sign(ExtensionVersion.Compare(left, right)));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.x")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0-beta")]
    [InlineData("-1.0.0")]
    [InlineData("1234567.0.0")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_versions_do_not_parse(string? text)
    {
        Assert.False(ExtensionVersion.TryParse(text, out _));
    }

    [Fact]
    public void Invalid_sorts_below_valid_and_two_invalids_are_equal()
    {
        Assert.True(ExtensionVersion.Compare("bad", "0.0.1") < 0);
        Assert.True(ExtensionVersion.Compare("0.0.1", "bad") > 0);
        Assert.Equal(0, ExtensionVersion.Compare("bad", null));
    }

    [Fact]
    public void IsNewer_is_strict_and_false_for_invalid()
    {
        Assert.True(ExtensionVersion.IsNewer("1.1.0", "1.0.9"));
        Assert.False(ExtensionVersion.IsNewer("1.0.0", "1.0.0"));
        Assert.False(ExtensionVersion.IsNewer("0.9.0", "1.0.0"));
        Assert.False(ExtensionVersion.IsNewer("bad", "1.0.0"));
        Assert.False(ExtensionVersion.IsNewer("2.0.0", "bad"));
    }

    [Fact]
    public void Parsed_parts_and_to_string_round_trip()
    {
        Assert.True(ExtensionVersion.TryParse("3.14.159", out var v));
        Assert.Equal(new ExtensionVersion(3, 14, 159), v);
        Assert.Equal("3.14.159", v.ToString());
    }
}
