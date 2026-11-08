using System.Globalization;
using Koy.Core.Search;

namespace Koy.Core.Tests.Search;

public class ExpressionCalculatorTests
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    private static decimal Eval(string text, CultureInfo? culture = null)
    {
        Assert.True(ExpressionCalculator.TryEvaluate(text, culture ?? Tr, out var value), text);
        return value;
    }

    [Theory]
    [InlineData("12*8", 96)]
    [InlineData("2+3*4", 14)]
    [InlineData("(2+3)*4", 20)]
    [InlineData("10/4", 2.5)]
    [InlineData("10-2-3", 5)]
    [InlineData("2^10", 1024)]
    [InlineData("2^3^2", 512)]
    [InlineData("-2^2", -4)]
    [InlineData("2^-1", 0.5)]
    [InlineData("2*-3", -6)]
    [InlineData("=1+1", 2)]
    [InlineData(" 1 + 1 ", 2)]
    [InlineData("12x8", 96)]
    [InlineData("12×8", 96)]
    [InlineData("12÷8", 1.5)]
    public void Evaluates(string text, double expected) => Assert.Equal((decimal)expected, Eval(text));

    [Fact]
    public void Decimal_arithmetic_is_exact() => Assert.Equal(0.3m, Eval("0,1+0,2"));

    [Theory]
    [InlineData("3,5*2", 7)]
    [InlineData("3.5*2", 7)] // alışkanlık: nokta da ondalık sayılır
    [InlineData("1.000*2", 2000)] // Türkçede üç rakamlı grup binliktir
    [InlineData("1.000.000+1", 1000001)]
    public void Turkish_number_formats(string text, double expected) => Assert.Equal((decimal)expected, Eval(text, Tr));

    [Theory]
    [InlineData("1,000*2", 2000)]
    [InlineData("3.5*2", 7)]
    [InlineData("3,5*2", 7)]
    public void English_number_formats(string text, double expected) => Assert.Equal((decimal)expected, Eval(text, En));

    [Theory]
    [InlineData("123")] // işlem yok
    [InlineData("-5")] // yalnız işaret
    [InlineData("merhaba")]
    [InlineData("chrome-7")]
    [InlineData("1+")]
    [InlineData("(1+2")]
    [InlineData("1+2)")]
    [InlineData("1/0")]
    [InlineData("1,2,3+1")]
    [InlineData("")]
    public void Rejects(string text) => Assert.False(ExpressionCalculator.TryEvaluate(text, Tr, out _));

    [Fact]
    public void Too_long_input_is_rejected()
    {
        var text = string.Join("+", Enumerable.Repeat("1", ExpressionCalculator.MaxLength));

        Assert.False(ExpressionCalculator.TryEvaluate(text, Tr, out _));
    }

    [Fact]
    public void Deep_nesting_is_rejected_without_crashing()
    {
        var text = "1+" + new string('(', 60) + "1" + new string(')', 60);

        Assert.False(ExpressionCalculator.TryEvaluate(text, Tr, out _));
    }

    [Fact]
    public void Repeated_signs_are_limited_without_crashing() =>
        Assert.False(ExpressionCalculator.TryEvaluate("1+" + new string('-', 100) + "1", Tr, out _));

    [Fact]
    public void Overflow_gives_no_result() => Assert.False(ExpressionCalculator.TryEvaluate("9^1000", Tr, out _));

    [Fact]
    public void Huge_exponent_is_rejected_quickly() => Assert.False(ExpressionCalculator.TryEvaluate("1^99999999", Tr, out _));

    [Theory]
    [InlineData("sqrt(16)+1", 5)]
    [InlineData("abs(-3)", 3)]
    [InlineData("floor(2,9)", 2)]
    [InlineData("round(2,5)+1", 3)]
    [InlineData("log(1000)", 3)]
    [InlineData("0x10+1", 17)]
    [InlineData("1+1=", 2)]
    public void Functions_hex_and_trailing_equals(string text, double expected) => Assert.Equal((decimal)expected, Eval(text));

    [Fact]
    public void Constants_work() => Assert.Equal(6.28m, decimal.Round(Eval("2*pi"), 2));

    [Theory]
    [InlineData("foo(2)")]
    [InlineData("sqrt(-1)")]
    [InlineData("sqrt(")]
    public void Bad_functions_give_no_result(string text) => Assert.False(ExpressionCalculator.TryEvaluate(text, Tr, out _));

    [Theory]
    [InlineData("hex=255", "0xFF")]
    [InlineData("bin=5", "0b101")]
    [InlineData("oct=8", "010")]
    [InlineData("hex=250+5", "0xFF")]
    public void Base_prefix_formats_result(string text, string expected)
    {
        Assert.True(ExpressionCalculator.TryCalculate(text, Tr, out var display, out var copy));
        Assert.Equal((expected, expected), (display, copy));
    }

    [Fact]
    public void Base_prefix_needs_a_whole_number() => Assert.False(ExpressionCalculator.TryCalculate("hex=1/2", Tr, out _, out _));

    [Fact]
    public void Display_uses_grouping_and_copy_does_not()
    {
        Assert.Equal("1.234,5", ExpressionCalculator.FormatForDisplay(1234.5m, Tr));
        Assert.Equal("1234,5", ExpressionCalculator.FormatForCopy(1234.5m, Tr));
    }
}
