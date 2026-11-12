using System.Globalization;
using Koy.Core.Appearance;
using Koy.Core.Search;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Search;

public class UnitAndColorConverterTests
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private static string T(string key) => key;

    private sealed class FakeShell : ISearchShell
    {
        public List<string> Copied { get; } = [];

        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => false;
        public string? GetEnvironmentVariable(string name) => null;
        public void Open(string target) { }
        public void Run(string executable, string? arguments) { }
        public void RunWithArguments(string executable, IReadOnlyList<string> arguments) { }
        public void RunInTerminal(string commandLine) { }
        public void OpenContainingFolder(string path) { }
        public void CopyText(string text) => Copied.Add(text);
    }

    private static double Convert(string text, CultureInfo? culture = null)
    {
        Assert.True(UnitConverter.TryConvert(text, culture ?? En, out var c), $"'{text}' çevrilemedi");
        return c.Value;
    }

    // ---- Uzunluk, kütle, hız ----

    [Theory]
    [InlineData("10 km to mi", 6.21371192)]
    [InlineData("1 mi to km", 1.609344)]
    [InlineData("1 m to cm", 100)]
    [InlineData("250 cm to m", 2.5)]
    [InlineData("1 ft to in", 12)]
    [InlineData("1 in to cm", 2.54)]
    [InlineData("1 mi to ft", 5280)]
    [InlineData("3 ft to m", 0.9144)]
    [InlineData("1 km to m", 1000)]
    public void Length_conversions(string text, double expected) => Assert.Equal(expected, Convert(text), 6);

    [Theory]
    [InlineData("1 kg to g", 1000)]
    [InlineData("1 kg to lb", 2.20462262)]
    [InlineData("1 lb to oz", 16)]
    [InlineData("16 oz to lb", 1)]
    [InlineData("500 g to kg", 0.5)]
    [InlineData("5 kg lb", 11.0231131)]
    public void Mass_conversions(string text, double expected) => Assert.Equal(expected, Convert(text), 6);

    [Theory]
    [InlineData("60 mph to km/h", 96.56064)]
    [InlineData("100 km/h to mph", 62.1371192)]
    [InlineData("10 m/s to km/h", 36)]
    [InlineData("36 km/h to m/s", 10)]
    [InlineData("1 mph to m/s", 0.44704)]
    public void Speed_conversions(string text, double expected) => Assert.Equal(expected, Convert(text), 5);

    // ---- Sıcaklık: ofsetli ----

    [Theory]
    [InlineData("100 c in f", 212)]
    [InlineData("0 c in f", 32)]
    [InlineData("-40 c to f", -40)]
    [InlineData("32 f to c", 0)]
    [InlineData("212 f to c", 100)]
    [InlineData("-40 f to c", -40)]
    [InlineData("0 c to k", 273.15)]
    [InlineData("273.15 k to c", 0)]
    [InlineData("0 k to c", -273.15)]
    [InlineData("300 k to f", 80.33)]
    [InlineData("98.6 f to k", 310.15)]
    public void Temperature_uses_offsets_not_only_factors(string text, double expected) => Assert.Equal(expected, Convert(text), 6);

    [Theory]
    [InlineData("-300 c to f")]
    [InlineData("-1 k to c")]
    [InlineData("-500 f to c")]
    public void Temperature_below_absolute_zero_is_rejected(string text) =>
        Assert.False(UnitConverter.TryConvert(text, En, out _));

    [Fact]
    public void Temperature_degree_sign_and_long_names_work()
    {
        Assert.Equal(212, Convert("100 °C to °F"), 6);
        Assert.Equal(212, Convert("100 celsius to fahrenheit"), 6);
        Assert.Equal(212, Convert("100°c in °f"), 6);
        Assert.Equal(373.15, Convert("100 Celsius in Kelvin"), 6);
    }

    // ---- Veri boyutu ----

    [Theory]
    [InlineData("1 gb in mb", 1000)]
    [InlineData("1 kb to b", 1000)]
    [InlineData("1 tb to gb", 1000)]
    [InlineData("2500 mb to gb", 2.5)]
    [InlineData("1 mb to kb", 1000)]
    [InlineData("1 gib to mib", 1024)]
    [InlineData("1 kib to b", 1024)]
    [InlineData("1 tib to gib", 1024)]
    public void Data_is_si_by_default_and_binary_with_ib_units(string text, double expected) => Assert.Equal(expected, Convert(text), 9);

    [Fact]
    public void Data_si_and_binary_differ()
    {
        Assert.Equal(1000, Convert("1 gb to mb"), 9);
        Assert.Equal(1024, Convert("1 gib to mib"), 9);
        Assert.Equal(1073.741824, Convert("1 gib to mb"), 6);
    }

    // ---- Söz dizimi, takma adlar, büyük/küçük harf ----

    [Theory]
    [InlineData("10 km to mi")]
    [InlineData("10 km in mi")]
    [InlineData("10 km into mi")]
    [InlineData("10 km as mi")]
    [InlineData("10 km mi")]
    [InlineData("10km mi")]
    [InlineData("10km->mi")]
    [InlineData("10 km -> mi")]
    [InlineData("10 km => mi")]
    [InlineData("10 km = mi")]
    [InlineData("10 km > mi")]
    [InlineData("10 km → mi")]
    [InlineData("  10   km    to   mi  ")]
    [InlineData("10 KM TO MI")]
    [InlineData("10 Km To Mi")]
    [InlineData("10 kilometers to miles")]
    [InlineData("10 kilometres in mile")]
    public void Syntax_variants_all_give_the_same_answer(string text) => Assert.Equal(6.21371192, Convert(text), 6);

    [Theory]
    [InlineData("1 in in cm")]
    [InlineData("1 inch to cm")]
    [InlineData("1 inches in cm")]
    [InlineData("1 in cm")]
    [InlineData("1IN TO CM")]
    public void The_word_in_is_both_a_unit_and_a_separator(string text) => Assert.Equal(2.54, Convert(text), 9);

    [Theory]
    [InlineData("6 feet to cm", 182.88)]
    [InlineData("1 foot in inches", 12)]
    [InlineData("2 pounds to kg", 0.90718474)]
    [InlineData("2 lbs to oz", 32)]
    [InlineData("1 ounce to g", 28.349523125)]
    [InlineData("3 meters to ft", 9.84251969)]
    [InlineData("1 kmh to m/s", 0.27777778)]
    [InlineData("1 kph to mph", 0.62137119)]
    [InlineData("1 byte to b", 1)]
    [InlineData("1 megabyte to kilobytes", 1000)]
    public void Aliases(string text, double expected) => Assert.Equal(expected, Convert(text), 6);

    [Fact]
    public void Turkish_dotless_i_folds_in_unit_names()
    {
        // "İNÇ" / "inç" -> "inc"; "KİLO" -> "kilo"
        Assert.Equal(2.54, Convert("1 İNÇ to cm", Tr), 9);
        Assert.Equal(1000, Convert("1 KİLO to g", Tr), 9);
    }

    [Fact]
    public void Same_unit_returns_the_input()
    {
        Assert.Equal(7, Convert("7 km to km"), 12);
        Assert.Equal(25, Convert("25 c to c"), 12);
    }

    // ---- Sayı ayrıştırma ve kültür ----

    [Theory]
    [InlineData("10,5 km to m", 10500)]
    [InlineData("10.5 km to m", 10500)]
    public void Decimal_comma_and_point_both_work_in_any_culture(string text, double expected)
    {
        Assert.Equal(expected, Convert(text, Tr), 6);
        Assert.Equal(expected, Convert(text, En), 6);
        Assert.Equal(expected, Convert(text, De), 6);
        Assert.Equal(expected, Convert(text, CultureInfo.InvariantCulture), 6);
    }

    [Theory]
    [InlineData("1.500 kg to g", "tr-TR", 1_500_000)]
    [InlineData("1,500 kg to g", "en-US", 1_500_000)]
    [InlineData("1,500 kg to g", "tr-TR", 1_500)]
    [InlineData("1.500 kg to g", "en-US", 1_500)]
    public void A_single_separator_followed_by_three_digits_is_thousands_only_in_its_own_culture(string text, string culture, double expected) =>
        Assert.Equal(expected, Convert(text, CultureInfo.GetCultureInfo(culture)), 6);

    [Theory]
    [InlineData("1.000.000 m to km", 1000)]
    [InlineData("1,000,000 m to km", 1000)]
    [InlineData("1.234,5 m to cm", 123450)]
    [InlineData("1,234.5 m to cm", 123450)]
    public void Grouped_numbers(string text, double expected)
    {
        Assert.Equal(expected, Convert(text, Tr), 6);
        Assert.Equal(expected, Convert(text, En), 6);
    }

    [Theory]
    [InlineData("1.2.3 km to m")]
    [InlineData("1,23,456 km to m")]
    [InlineData("1.234,5,6 km to m")]
    [InlineData("1,5.2,3 km to m")]
    [InlineData("1..5 km to m")]
    public void Malformed_numbers_are_rejected(string text) => Assert.False(UnitConverter.TryConvert(text, En, out _));

    [Theory]
    [InlineData(".5 km to m", 500)]
    [InlineData(",5 km to m", 500)]
    [InlineData("0,5 km to m", 500)]
    [InlineData("0.500 km to m", 500)]
    [InlineData("+5 km to m", 5000)]
    [InlineData("-5 km to m", -5000)]
    [InlineData("−5 km to m", -5000)]
    [InlineData("5. km to m", 5000)]
    public void Number_edge_forms(string text, double expected)
    {
        Assert.Equal(expected, Convert(text, Tr), 6);
        Assert.Equal(expected, Convert(text, En), 6);
    }

    [Theory]
    [InlineData("10", "en-US", 10)]
    [InlineData("1234567", "en-US", 1234567)]
    [InlineData("-0.25", "tr-TR", -0.25)]
    [InlineData("-0,25", "en-US", -0.25)]
    public void TryParseNumber_basics(string text, string culture, double expected)
    {
        Assert.True(UnitConverter.TryParseNumber(text, CultureInfo.GetCultureInfo(culture), out var v));
        Assert.Equal(expected, v, 12);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData(",")]
    [InlineData("1e5")]
    [InlineData("1 000")]
    public void TryParseNumber_rejects_garbage(string? text) => Assert.False(UnitConverter.TryParseNumber(text, En, out _));

    // ---- Reddedilenler ----

    [Theory]
    [InlineData("10 km to kg")]
    [InlineData("10 kg to km")]
    [InlineData("10 c to km")]
    [InlineData("10 gb to mph")]
    [InlineData("10 mph to f")]
    [InlineData("10 m to b")]
    [InlineData("10 oz to mb")]
    public void Cross_category_conversions_are_rejected(string text) => Assert.False(UnitConverter.TryConvert(text, En, out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("chrome")]
    [InlineData("km to mi")]
    [InlineData("10")]
    [InlineData("10 km")]
    [InlineData("10 km to")]
    [InlineData("10 to mi")]
    [InlineData("10 km to mi extra")]
    [InlineData("10 km foo mi")]
    [InlineData("10 km to foo")]
    [InlineData("10 foo to mi")]
    [InlineData("10 xx yy")]
    [InlineData("12*8")]
    [InlineData("10 km to mi to ft")]
    [InlineData("to to to")]
    [InlineData("->")]
    [InlineData("1e999 km to m")]
    [InlineData("#fff")]
    [InlineData("rgb(1,2,3)")]
    [InlineData("10 km/ to mi")]
    [InlineData("10 k m to mi")]
    public void Garbage_is_rejected_without_throwing(string? text) => Assert.False(UnitConverter.TryConvert(text, En, out _));

    [Fact]
    public void Very_long_input_is_rejected()
    {
        var text = new string('9', 200) + " km to m";
        Assert.False(UnitConverter.TryConvert(text, En, out _));
    }

    [Fact]
    public void Huge_but_parseable_numbers_never_produce_non_finite_results()
    {
        var big = new string('9', 30);
        if (UnitConverter.TryConvert(big + " tb to b", En, out var c))
            Assert.True(double.IsFinite(c.Value));
    }

    [Fact]
    public void Never_throws_on_random_noise()
    {
        var random = new Random(1234);
        const string alphabet = "0123456789.,+-−eE kmgbcfinhsto/°→=> ()#%\t";
        for (var i = 0; i < 5000; i++)
        {
            var chars = new char[random.Next(0, 24)];
            for (var j = 0; j < chars.Length; j++)
                chars[j] = alphabet[random.Next(alphabet.Length)];

            var text = new string(chars);
            _ = UnitConverter.TryConvert(text, En, out _);
            _ = UnitConverter.TryConvert(text, Tr, out _);
            _ = ColorParser.TryParse(text, out _);
        }
    }

    // ---- Gidiş-dönüş ----

    [Theory]
    [InlineData("km", "mi")]
    [InlineData("m", "ft")]
    [InlineData("cm", "in")]
    [InlineData("kg", "lb")]
    [InlineData("g", "oz")]
    [InlineData("c", "f")]
    [InlineData("c", "k")]
    [InlineData("f", "k")]
    [InlineData("kb", "mb")]
    [InlineData("gib", "gb")]
    [InlineData("km/h", "mph")]
    [InlineData("m/s", "mph")]
    public void Round_trip_returns_the_original(string a, string b)
    {
        Assert.True(UnitConverter.TryFindUnit(a, out var ua));
        Assert.True(UnitConverter.TryFindUnit(b, out var ub));
        foreach (var value in new[] { 0.0, 1, 37.25, 1234.5678, 100 })
        {
            Assert.True(UnitConverter.TryConvert(value, ua, ub, out var there));
            Assert.True(UnitConverter.TryConvert(there, ub, ua, out var back));
            Assert.Equal(value, back, 6);
        }
    }

    [Fact]
    public void TryFindUnit_is_case_insensitive_and_rejects_unknowns()
    {
        Assert.True(UnitConverter.TryFindUnit("KM/H", out var kmh));
        Assert.Equal("km/h", kmh.Symbol);
        Assert.True(UnitConverter.TryFindUnit(" Miles ", out var mi));
        Assert.Equal("mi", mi.Symbol);
        Assert.False(UnitConverter.TryFindUnit("parsec", out _));
        Assert.False(UnitConverter.TryFindUnit(null, out _));
        Assert.False(UnitConverter.TryFindUnit("", out _));
    }

    [Fact]
    public void Categories_are_assigned_correctly()
    {
        UnitConverter.TryFindUnit("km", out var km);
        UnitConverter.TryFindUnit("lb", out var lb);
        UnitConverter.TryFindUnit("k", out var k);
        UnitConverter.TryFindUnit("gb", out var gb);
        UnitConverter.TryFindUnit("mph", out var mph);
        Assert.Equal(UnitCategory.Length, km.Category);
        Assert.Equal(UnitCategory.Mass, lb.Category);
        Assert.Equal(UnitCategory.Temperature, k.Category);
        Assert.Equal(UnitCategory.Data, gb.Category);
        Assert.Equal(UnitCategory.Speed, mph.Category);
    }

    // ---- Biçimleme ----

    [Theory]
    [InlineData(6.21371192, "en-US", false, "6.21371")]
    [InlineData(6.21371192, "tr-TR", false, "6,21371")]
    [InlineData(1234.5678, "en-US", false, "1234.57")]
    [InlineData(1234.5678, "en-US", true, "1,234.57")]
    [InlineData(1234.5678, "tr-TR", true, "1.234,57")]
    [InlineData(1234567.891, "en-US", false, "1234568")]
    [InlineData(1500000, "en-US", true, "1,500,000")]
    [InlineData(100, "en-US", false, "100")]
    [InlineData(2.5, "en-US", false, "2.5")]
    [InlineData(0.000123456789, "en-US", false, "0.000123457")]
    [InlineData(-40, "en-US", false, "-40")]
    [InlineData(-12.34567, "tr-TR", false, "-12,3457")]
    [InlineData(0, "en-US", false, "0")]
    [InlineData(0.1 + 0.2, "en-US", false, "0.3")]
    public void FormatNumber_rounds_sensibly(double value, string culture, bool grouping, string expected) =>
        Assert.Equal(expected, UnitConverter.FormatNumber(value, CultureInfo.GetCultureInfo(culture), grouping));

    [Fact]
    public void FormatNumber_never_uses_exponents_in_normal_range_and_survives_extremes()
    {
        Assert.DoesNotContain("E", UnitConverter.FormatNumber(1e-12, En, false));
        Assert.Equal("", UnitConverter.FormatNumber(double.NaN, En, false));
        Assert.Equal("", UnitConverter.FormatNumber(double.PositiveInfinity, En, false));
        Assert.NotEqual("", UnitConverter.FormatNumber(1e300, En, false));
        Assert.NotEqual("", UnitConverter.FormatNumber(1e-300, En, false));
        Assert.NotEqual("0", UnitConverter.FormatNumber(1e-300, En, false));
    }

    // ---- UnitConverterSource ----

    [Fact]
    public void Unit_source_returns_one_copyable_row()
    {
        var shell = new FakeShell();
        var source = new UnitConverterSource(shell, T, En);
        var result = source.Search(new SearchQuery("10 km to mi")).Single();

        Assert.Equal("6.21371 mi", result.Title);
        Assert.StartsWith("10 km", result.Subtitle);
        Assert.True(result.RestoresFocus);
        Assert.Equal(SearchRelevance.Highest, result.CategoryRelevance);
        Assert.Equal("units", source.Id);

        result.Execute();
        Assert.Equal(["6.21371"], shell.Copied);
    }

    [Fact]
    public void Unit_source_copies_the_bare_number_in_the_users_culture()
    {
        var shell = new FakeShell();
        var result = new UnitConverterSource(shell, T, Tr).Search(new SearchQuery("10,5 km to mi")).Single();

        Assert.Equal("6,5244 mi", result.Title);
        result.Execute();
        Assert.Equal(["6,5244"], shell.Copied);
    }

    [Fact]
    public void Unit_source_shows_grouping_but_copies_without_it()
    {
        var shell = new FakeShell();
        var result = new UnitConverterSource(shell, T, En).Search(new SearchQuery("1 tb in mb")).Single();

        Assert.Equal("1,000,000 MB", result.Title);
        result.Execute();
        Assert.Equal(["1000000"], shell.Copied);
    }

    [Fact]
    public void Unit_source_temperature_title()
    {
        var result = new UnitConverterSource(new FakeShell(), T, En).Search(new SearchQuery("100 c in f")).Single();
        Assert.Equal("212 °F", result.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("chrome")]
    [InlineData("10 km to kg")]
    [InlineData("12*8")]
    [InlineData("#ff8800")]
    public void Unit_source_ignores_non_conversions(string text) =>
        Assert.Empty(new UnitConverterSource(new FakeShell(), T, En).Search(new SearchQuery(text)));

    // ---- Renk ----

    [Theory]
    [InlineData("#FF8800", 255, 136, 0)]
    [InlineData("#ff8800", 255, 136, 0)]
    [InlineData("#f80", 255, 136, 0)]
    [InlineData("#F80", 255, 136, 0)]
    [InlineData("#000", 0, 0, 0)]
    [InlineData("#FFFFFF", 255, 255, 255)]
    [InlineData("  #3B82F6  ", 59, 130, 246)]
    [InlineData("rgb(255, 136, 0)", 255, 136, 0)]
    [InlineData("rgb(255,136,0)", 255, 136, 0)]
    [InlineData("RGB( 255 , 136 , 0 )", 255, 136, 0)]
    [InlineData("rgb(255 136 0)", 255, 136, 0)]
    [InlineData("rgb(0,0,0)", 0, 0, 0)]
    [InlineData("hsl(0, 100%, 50%)", 255, 0, 0)]
    [InlineData("hsl(120,100%,50%)", 0, 255, 0)]
    [InlineData("HSL(240, 100%, 50%)", 0, 0, 255)]
    [InlineData("hsl(0, 0%, 100%)", 255, 255, 255)]
    [InlineData("hsl(0, 0%, 0%)", 0, 0, 0)]
    [InlineData("hsl(360, 100%, 50%)", 255, 0, 0)]
    [InlineData("hsl(-120, 100%, 50%)", 0, 0, 255)]
    [InlineData("hsl(480, 100%, 50%)", 0, 255, 0)]
    [InlineData("hsl(0 100 50)", 255, 0, 0)]
    [InlineData("hsl(0deg, 100%, 50%)", 255, 0, 0)]
    [InlineData("hsl(0°, 100%, 50%)", 255, 0, 0)]
    public void Color_input_forms(string text, int r, int g, int b)
    {
        Assert.True(ColorParser.TryParse(text, out var color), text);
        Assert.Equal(new Rgb((byte)r, (byte)g, (byte)b), color.Color);
        Assert.False(color.HasAlpha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("#ff")]
    [InlineData("#ffff")]
    [InlineData("#fffff")]
    [InlineData("#fffffff")]
    [InlineData("#fffffffff")]
    [InlineData("#gggggg")]
    [InlineData("#ff88zz")]
    [InlineData("ff8800")]
    [InlineData("red")]
    [InlineData("rgb(256,0,0)")]
    [InlineData("rgb(0,300,0)")]
    [InlineData("rgb(-1,0,0)")]
    [InlineData("rgb(1,2)")]
    [InlineData("rgb(1,2,3,4)")]
    [InlineData("rgb(1,2,3")]
    [InlineData("rgb(a,b,c)")]
    [InlineData("rgb(1.5,2,3)")]
    [InlineData("hsl(0, 101%, 50%)")]
    [InlineData("hsl(0, 100%, 150%)")]
    [InlineData("hsl(0, -5%, 50%)")]
    [InlineData("hsl(a, b, c)")]
    [InlineData("hsl(0, 100%)")]
    [InlineData("hsl(0, 100%, 50%")]
    [InlineData("12*8")]
    [InlineData("10 km to mi")]
    public void Color_garbage_is_rejected(string? text) => Assert.False(ColorParser.TryParse(text, out _));

    [Fact]
    public void Color_long_input_is_rejected() => Assert.False(ColorParser.TryParse("#" + new string('f', 100), out _));

    [Fact]
    public void Eight_digit_hex_is_alpha_first_like_wpf()
    {
        Assert.True(ColorParser.TryParse("#80FF8800", out var color));
        Assert.Equal(new Rgb(255, 136, 0), color.Color);
        Assert.Equal(0x80, color.Alpha);
        Assert.True(color.HasAlpha);
        Assert.Equal("#80FF8800", color.ToHex());
        Assert.Equal("rgba(255, 136, 0, 0.5)", color.ToRgbText());
        Assert.Equal("hsla(32, 100%, 50%, 0.5)", color.ToHslText());
    }

    [Fact]
    public void Opaque_eight_digit_hex_drops_the_alpha_in_output()
    {
        Assert.True(ColorParser.TryParse("#FFFF8800", out var color));
        Assert.False(color.HasAlpha);
        Assert.Equal("#FF8800", color.ToHex());
        Assert.Equal("rgb(255, 136, 0)", color.ToRgbText());
    }

    [Fact]
    public void Fully_transparent_alpha()
    {
        Assert.True(ColorParser.TryParse("#00112233", out var color));
        Assert.Equal("rgba(17, 34, 51, 0)", color.ToRgbText());
    }

    [Theory]
    [InlineData("#FF8800", "#FF8800", "rgb(255, 136, 0)", "hsl(32, 100%, 50%)")]
    [InlineData("#f80", "#FF8800", "rgb(255, 136, 0)", "hsl(32, 100%, 50%)")]
    [InlineData("#000", "#000000", "rgb(0, 0, 0)", "hsl(0, 0%, 0%)")]
    [InlineData("#FFF", "#FFFFFF", "rgb(255, 255, 255)", "hsl(0, 0%, 100%)")]
    [InlineData("rgb(255, 0, 0)", "#FF0000", "rgb(255, 0, 0)", "hsl(0, 100%, 50%)")]
    [InlineData("rgb(59, 130, 246)", "#3B82F6", "rgb(59, 130, 246)", "hsl(217, 91%, 60%)")]
    [InlineData("hsl(120, 100%, 50%)", "#00FF00", "rgb(0, 255, 0)", "hsl(120, 100%, 50%)")]
    [InlineData("hsl(240,100%,25%)", "#000080", "rgb(0, 0, 128)", "hsl(240, 100%, 25%)")]
    [InlineData("hsl(360, 100%, 50%)", "#FF0000", "rgb(255, 0, 0)", "hsl(0, 100%, 50%)")]
    [InlineData("hsl(-120, 100%, 50%)", "#0000FF", "rgb(0, 0, 255)", "hsl(240, 100%, 50%)")]
    [InlineData("#808080", "#808080", "rgb(128, 128, 128)", "hsl(0, 0%, 50%)")]
    public void Color_outputs(string input, string hex, string rgb, string hsl)
    {
        Assert.True(ColorParser.TryParse(input, out var color));
        Assert.Equal(hex, color.ToHex());
        Assert.Equal(rgb, color.ToRgbText());
        Assert.Equal(hsl, color.ToHslText());
    }

    [Fact]
    public void Hsl_input_is_echoed_not_recomputed_from_quantized_rgb()
    {
        // hsl(120, 10%, 5%) 8 bitlik kanala inince S çok kayar; kullanıcının yazdığı değer görünmeli.
        Assert.True(ColorParser.TryParse("hsl(120, 10%, 5%)", out var color));
        Assert.Equal("hsl(120, 10%, 5%)", color.ToHslText());
    }

    [Fact]
    public void Hsl_fractions_round_for_display()
    {
        Assert.True(ColorParser.TryParse("hsl(32.4, 99.6%, 50.4%)", out var color));
        Assert.Equal("hsl(32, 100%, 50%)", color.ToHslText());
    }

    [Fact]
    public void Hsl_hue_that_rounds_to_360_shows_zero()
    {
        Assert.True(ColorParser.TryParse("hsl(359.6, 100%, 50%)", out var color));
        Assert.StartsWith("hsl(0,", color.ToHslText());
    }

    [Theory]
    [InlineData("#FF8800")]
    [InlineData("#3B82F6")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#123456")]
    [InlineData("#ABCDEF")]
    [InlineData("#7F7F7F")]
    public void Hex_rgb_hsl_round_trips(string hex)
    {
        Assert.True(ColorParser.TryParse(hex, out var original));

        Assert.True(ColorParser.TryParse(original.ToRgbText(), out var viaRgb));
        Assert.Equal(original.Color, viaRgb.Color);

        // HSL tam sayıya yuvarlanır; bir kanalda en çok birkaç birim sapma olabilir.
        Assert.True(ColorParser.TryParse(original.ToHslText(), out var viaHsl));
        Assert.InRange(Math.Abs(viaHsl.Color.R - original.Color.R), 0, 3);
        Assert.InRange(Math.Abs(viaHsl.Color.G - original.Color.G), 0, 3);
        Assert.InRange(Math.Abs(viaHsl.Color.B - original.Color.B), 0, 3);

        Assert.True(ColorParser.TryParse(original.ToHex(), out var viaHex));
        Assert.Equal(original.Color, viaHex.Color);
    }

    [Fact]
    public void Round_trip_over_the_whole_primary_grid()
    {
        for (var r = 0; r < 256; r += 51)
        for (var g = 0; g < 256; g += 51)
        for (var b = 0; b < 256; b += 51)
        {
            var rgb = new Rgb((byte)r, (byte)g, (byte)b);
            Assert.True(ColorParser.TryParse(rgb.ToHex(), out var parsed));
            Assert.Equal(rgb, parsed.Color);
            Assert.True(ColorParser.TryParse(parsed.ToRgbText(), out var again));
            Assert.Equal(rgb, again.Color);
        }
    }

    [Fact]
    public void Color_source_returns_hex_rgb_hsl_rows_each_copyable()
    {
        var shell = new FakeShell();
        var source = new ColorConverterSource(shell, T);
        var results = source.Search(new SearchQuery("#FF8800")).ToList();

        Assert.Equal(["#FF8800", "rgb(255, 136, 0)", "hsl(32, 100%, 50%)"], results.Select(r => r.Title));
        Assert.Equal("color", source.Id);
        Assert.All(results, r =>
        {
            Assert.True(r.RestoresFocus);
            Assert.Equal(SearchRelevance.Highest, r.CategoryRelevance);
            Assert.EndsWith("SearchHintCopy", r.Subtitle);
        });
        Assert.Equal(3, results.Select(r => r.Id).Distinct().Count());
        Assert.True(results[0].Relevance > results[1].Relevance && results[1].Relevance > results[2].Relevance);

        foreach (var row in results)
            row.Execute();

        Assert.Equal(["#FF8800", "rgb(255, 136, 0)", "hsl(32, 100%, 50%)"], shell.Copied);
    }

    [Theory]
    [InlineData("rgb(255,136,0)")]
    [InlineData("hsl(32, 100%, 50%)")]
    [InlineData("#f80")]
    public void Color_source_gives_the_same_three_rows_for_any_input_form(string input)
    {
        var results = new ColorConverterSource(new FakeShell(), T).Search(new SearchQuery(input)).ToList();
        Assert.Equal(["#FF8800", "rgb(255, 136, 0)", "hsl(32, 100%, 50%)"], results.Select(r => r.Title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("chrome")]
    [InlineData("#zzz")]
    [InlineData("rgb(999,0,0)")]
    [InlineData("10 km to mi")]
    public void Color_source_ignores_non_colors(string text) =>
        Assert.Empty(new ColorConverterSource(new FakeShell(), T).Search(new SearchQuery(text)));

    // ---- Kaynaklar birbirine ve hesap makinesine karışmaz ----

    [Theory]
    [InlineData("10 km to mi")]
    [InlineData("100 c in f")]
    [InlineData("5 kg lb")]
    [InlineData("1 gb in mb")]
    [InlineData("60 mph to km/h")]
    [InlineData("#FF8800")]
    [InlineData("rgb(255, 136, 0)")]
    [InlineData("hsl(32, 100%, 50%)")]
    public void The_calculator_does_not_claim_converter_queries(string text) =>
        Assert.Empty(new CalculatorSearchSource(new FakeShell(), T, En).Search(new SearchQuery(text)));

    [Theory]
    [InlineData("#FF8800")]
    [InlineData("rgb(255, 136, 0)")]
    [InlineData("hsl(32, 100%, 50%)")]
    public void Colors_are_not_units(string text) =>
        Assert.Empty(new UnitConverterSource(new FakeShell(), T, En).Search(new SearchQuery(text)));

    [Theory]
    [InlineData("10 km to mi")]
    [InlineData("100 c in f")]
    [InlineData("1 gb in mb")]
    public void Units_are_not_colors(string text) =>
        Assert.Empty(new ColorConverterSource(new FakeShell(), T).Search(new SearchQuery(text)));

    [Fact]
    public void Help_lists_documented_converter_sources()
    {
        var units = new UnitConverterSource(new FakeShell(), T, En) { Syntaxes = [new SearchSyntax("10 km to mi", "x")] };
        var color = new ColorConverterSource(new FakeShell(), T) { Syntaxes = [new SearchSyntax("#FF8800", "y")] };
        ISearchSource[] all = [units, color];
        var help = new HelpSearchSource(() => all, T);

        var rows = help.Search(new SearchQuery("?")).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Title == "10 km to mi");
        Assert.Contains(rows, r => r.Title == "#FF8800");
    }
}
