using Koy.Core.Appearance;

namespace Koy.Core.Tests;

/// <summary>Stil geçersiz kılma: whitelist ve süzgeç. Güvenlik sınırı, bu yüzden her ret yolu ayrı ayrı sınanır.</summary>
public class StyleOverrideSanitizerTests
{
    private static StyleSanitizeResult Run(params (string Name, string? Value)[] pairs) =>
        StyleOverrideSanitizer.Sanitize(pairs.Select(p => new KeyValuePair<string, string?>(p.Name, p.Value)));

    [Fact]
    public void Valid_color_is_normalized_to_upper_hex_rrggbb()
    {
        var result = Run(("Accent", "#abc"));

        Assert.Equal("#AABBCC", result.Accepted.Values["Accent"]);
        Assert.Empty(result.Dropped);
    }

    [Fact]
    public void Six_digit_color_is_kept()
    {
        var result = Run(("Surface", "#1A2B3C"));

        Assert.Equal("#1A2B3C", result.Accepted.Values["Surface"]);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("1A2B3C")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("#AARRGGBB")]
    [InlineData("#1234567")]
    [InlineData("rgb(1,2,3)")]
    public void Malformed_colors_are_dropped(string color)
    {
        var result = Run(("Text", color));

        Assert.False(result.Accepted.Values.ContainsKey("Text"));
        Assert.Equal([new DroppedStyleOverride("Text", StyleDropReason.MalformedColor)], result.Dropped);
    }

    [Fact]
    public void Number_inside_range_is_kept_with_invariant_formatting()
    {
        var result = Run(("CornerRadius", "12.5"));

        Assert.Equal("12.5", result.Accepted.Values["CornerRadius"]);
    }

    [Theory]
    [InlineData("CornerRadius", "-1")]
    [InlineData("CornerRadius", "25")]
    [InlineData("BorderThickness", "4.01")]
    [InlineData("FontSize", "9")]
    public void Number_outside_range_is_dropped(string name, string value)
    {
        var result = Run((name, value));

        Assert.False(result.Accepted.Values.ContainsKey(name));
        Assert.Equal([new DroppedStyleOverride(name, StyleDropReason.OutOfRange)], result.Dropped);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("abc")]
    [InlineData("12px")]
    public void Non_finite_or_unparsable_numbers_are_dropped(string value)
    {
        var result = Run(("FontSize", value));

        Assert.Equal([new DroppedStyleOverride("FontSize", StyleDropReason.MalformedNumber)], result.Dropped);
    }

    [Fact]
    public void Enum_value_is_matched_case_insensitively_and_stored_canonically()
    {
        var result = Run(("FontWeight", "semibold"));

        Assert.Equal("SemiBold", result.Accepted.Values["FontWeight"]);
    }

    [Theory]
    [InlineData("Italic")]
    [InlineData("<Style/>")]
    [InlineData("System.Object")]
    public void Enum_value_outside_list_is_dropped(string value)
    {
        var result = Run(("FontWeight", value));

        Assert.Equal([new DroppedStyleOverride("FontWeight", StyleDropReason.UnknownEnumValue)], result.Dropped);
    }

    [Theory]
    [InlineData("ResourceDictionary")]
    [InlineData("accent")]
    [InlineData("Accent.Brush")]
    [InlineData("")]
    public void Names_outside_whitelist_are_dropped_and_comparison_is_case_sensitive(string name)
    {
        var result = Run((name, "#FFFFFF"));

        Assert.Empty(result.Accepted.Values);
        Assert.Equal([new DroppedStyleOverride(name, StyleDropReason.UnknownToken)], result.Dropped);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_values_are_dropped(string? value)
    {
        var result = Run(("Accent", value));

        Assert.Equal([new DroppedStyleOverride("Accent", StyleDropReason.MissingValue)], result.Dropped);
    }

    [Fact]
    public void Overlong_value_is_rejected_before_parsing()
    {
        var result = Run(("Accent", "#" + new string('A', 200)));

        Assert.Equal([new DroppedStyleOverride("Accent", StyleDropReason.MalformedColor)], result.Dropped);
    }

    [Fact]
    public void Duplicate_name_keeps_first_value_and_reports_the_second()
    {
        var result = Run(("Accent", "#111111"), ("Accent", "#222222"));

        Assert.Equal("#111111", result.Accepted.Values["Accent"]);
        Assert.Equal([new DroppedStyleOverride("Accent", StyleDropReason.Duplicate)], result.Dropped);
    }

    [Fact]
    public void Mixed_input_keeps_valid_entries_and_reports_each_drop_in_order()
    {
        var result = Run(
            ("Accent", "#ABCDEF"),
            ("Evil", "x"),
            ("CornerRadius", "100"),
            ("FontWeight", "Bold"),
            ("Text", null));

        Assert.Equal(2, result.Accepted.Values.Count);
        Assert.Equal("#ABCDEF", result.Accepted.Values["Accent"]);
        Assert.Equal("Bold", result.Accepted.Values["FontWeight"]);
        Assert.Equal(
        [
            new DroppedStyleOverride("Evil", StyleDropReason.UnknownToken),
            new DroppedStyleOverride("CornerRadius", StyleDropReason.OutOfRange),
            new DroppedStyleOverride("Text", StyleDropReason.MissingValue),
        ], result.Dropped);
    }

    [Fact]
    public void Null_key_is_rejected_without_throwing()
    {
        var result = StyleOverrideSanitizer.Sanitize([new KeyValuePair<string, string?>(null!, "#FFFFFF")]);

        Assert.Empty(result.Accepted.Values);
        Assert.Single(result.Dropped);
    }

    [Fact]
    public void Empty_input_gives_empty_overrides_and_no_drops()
    {
        var result = StyleOverrideSanitizer.Sanitize([]);

        Assert.Empty(result.Accepted.Values);
        Assert.Empty(result.Dropped);
    }

    [Fact]
    public void Whitelist_names_are_unique_and_resolve_by_exact_name()
    {
        var names = StyleTokens.All.Select(t => t.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.True(StyleTokens.TryGet("Accent", out var accent));
        Assert.Equal(StyleValueKind.Color, accent.Kind);
        Assert.False(StyleTokens.TryGet("accent", out _));
    }

    [Fact]
    public void Every_numeric_token_has_a_sane_range()
    {
        foreach (var token in StyleTokens.All.Where(t => t.Kind == StyleValueKind.Number))
            Assert.True(token.Min < token.Max, token.Name);
    }
}
