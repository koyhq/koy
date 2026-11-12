using System.Globalization;
using System.Text.RegularExpressions;

namespace Koy.Core.Search;

public enum UnitCategory
{
    Length,
    Mass,
    Temperature,
    Data,
    Speed
}

/// <summary>Bir birim. Sıcaklık dışında <see cref="Factor"/>: kategorinin taban birimine çarpan.</summary>
public sealed record MeasureUnit(string Symbol, UnitCategory Category, double Factor);

/// <summary>Başarılı çeviri: girdi, iki birim ve sonuç (yuvarlanmamış).</summary>
public sealed record UnitConversion(double Input, MeasureUnit From, MeasureUnit To, double Value);

/// <summary>
/// Birim çevirici: uzunluk, kütle, sıcaklık, veri boyutu, hız. Saf C#, ağ yok, hiçbir yol istisna fırlatmaz.
/// Taban birimler: metre, kilogram, bayt, m/s; sıcaklık Celsius üzerinden (ofsetli) çevrilir.
/// Veri boyutu: kb, mb, gb, tb SI'dır (1 kb = 1000 bayt, 1 gb = 1000 mb). İkili katlar için kib, mib, gib, tib (1024) yazılır.
/// </summary>
public static partial class UnitConverter
{
    private const int MaxInputLength = 64;
    private const double AbsoluteZeroCelsius = -273.15;
    private const int SignificantDigits = 6;
    private const int MaxDecimalPlaces = 12;

    private static readonly MeasureUnit Km = new("km", UnitCategory.Length, 1000);
    private static readonly MeasureUnit M = new("m", UnitCategory.Length, 1);
    private static readonly MeasureUnit Cm = new("cm", UnitCategory.Length, 0.01);
    private static readonly MeasureUnit Mi = new("mi", UnitCategory.Length, 1609.344);
    private static readonly MeasureUnit Ft = new("ft", UnitCategory.Length, 0.3048);
    private static readonly MeasureUnit In = new("in", UnitCategory.Length, 0.0254);

    private static readonly MeasureUnit Kg = new("kg", UnitCategory.Mass, 1);
    private static readonly MeasureUnit G = new("g", UnitCategory.Mass, 0.001);
    private static readonly MeasureUnit Lb = new("lb", UnitCategory.Mass, 0.45359237);
    private static readonly MeasureUnit Oz = new("oz", UnitCategory.Mass, 0.028349523125);

    private static readonly MeasureUnit Celsius = new("°C", UnitCategory.Temperature, 1);
    private static readonly MeasureUnit Fahrenheit = new("°F", UnitCategory.Temperature, 1);
    private static readonly MeasureUnit Kelvin = new("K", UnitCategory.Temperature, 1);

    private static readonly MeasureUnit Byte = new("B", UnitCategory.Data, 1);
    private static readonly MeasureUnit Kb = new("kB", UnitCategory.Data, 1e3);
    private static readonly MeasureUnit Mb = new("MB", UnitCategory.Data, 1e6);
    private static readonly MeasureUnit Gb = new("GB", UnitCategory.Data, 1e9);
    private static readonly MeasureUnit Tb = new("TB", UnitCategory.Data, 1e12);
    private static readonly MeasureUnit Kib = new("KiB", UnitCategory.Data, 1024);
    private static readonly MeasureUnit Mib = new("MiB", UnitCategory.Data, 1024.0 * 1024);
    private static readonly MeasureUnit Gib = new("GiB", UnitCategory.Data, 1024.0 * 1024 * 1024);
    private static readonly MeasureUnit Tib = new("TiB", UnitCategory.Data, 1024.0 * 1024 * 1024 * 1024);

    private static readonly MeasureUnit Kmh = new("km/h", UnitCategory.Speed, 1 / 3.6);
    private static readonly MeasureUnit Mph = new("mph", UnitCategory.Speed, 0.44704);
    private static readonly MeasureUnit Ms = new("m/s", UnitCategory.Speed, 1);

    /// <summary>Anahtarlar SearchText.Fold ile sadeleşmiş (küçük harf, Türkçe harfler sade).</summary>
    private static readonly Dictionary<string, MeasureUnit> Aliases = BuildAliases();

    private static readonly HashSet<string> Separators = ["to", "in", "into", "as"];

    [GeneratedRegex(@"^(?<num>[+-]?(?:\d[\d.,]*|[.,]\d[\d.,]*))\s*(?<rest>\S.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadPattern();

    [GeneratedRegex(@"\s*(->|=>|→|=|>)\s*", RegexOptions.CultureInvariant)]
    private static partial Regex ArrowPattern();

    private static Dictionary<string, MeasureUnit> BuildAliases()
    {
        var map = new Dictionary<string, MeasureUnit>(StringComparer.Ordinal);

        void Add(MeasureUnit unit, params string[] names)
        {
            foreach (var name in names)
                map[name] = unit;
        }

        Add(Km, "km", "kilometer", "kilometers", "kilometre", "kilometres");
        Add(M, "m", "meter", "meters", "metre", "metres");
        Add(Cm, "cm", "centimeter", "centimeters", "centimetre", "centimetres", "santimetre");
        Add(Mi, "mi", "mile", "miles", "mil");
        Add(Ft, "ft", "foot", "feet", "ayak");
        Add(In, "in", "inch", "inches", "inc");

        Add(Kg, "kg", "kilogram", "kilograms", "kilo");
        Add(G, "g", "gr", "gram", "grams");
        Add(Lb, "lb", "lbs", "pound", "pounds");
        Add(Oz, "oz", "ounce", "ounces", "ons");

        Add(Celsius, "c", "°c", "ºc", "celsius", "santigrat");
        Add(Fahrenheit, "f", "°f", "ºf", "fahrenheit");
        Add(Kelvin, "k", "kelvin");

        Add(Byte, "b", "byte", "bytes");
        Add(Kb, "kb", "kilobyte", "kilobytes");
        Add(Mb, "mb", "megabyte", "megabytes");
        Add(Gb, "gb", "gigabyte", "gigabytes");
        Add(Tb, "tb", "terabyte", "terabytes");
        Add(Kib, "kib", "kibibyte", "kibibytes");
        Add(Mib, "mib", "mebibyte", "mebibytes");
        Add(Gib, "gib", "gibibyte", "gibibytes");
        Add(Tib, "tib", "tebibyte", "tebibytes");

        Add(Kmh, "km/h", "kmh", "kph", "kmph");
        Add(Mph, "mph");
        Add(Ms, "m/s", "mps");
        return map;
    }

    /// <summary>"10 km to mi", "100 c in f", "5 kg lb", "10,5km->mi" gibi metni çözer ve çevirir.</summary>
    public static bool TryConvert(string? text, CultureInfo culture, out UnitConversion conversion)
    {
        conversion = null!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxInputLength)
            return false;

        var head = HeadPattern().Match(text.Trim().Replace('−', '-'));
        if (!head.Success || !head.Groups["rest"].Success)
            return false;

        if (!TryParseNumber(head.Groups["num"].Value, culture, out var input))
            return false;

        var rest = ArrowPattern().Replace(SearchText.Fold(head.Groups["rest"].Value), " to ");
        var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string fromName, toName;
        switch (tokens.Length)
        {
            case 2:
                (fromName, toName) = (tokens[0], tokens[1]);
                break;
            case 3 when Separators.Contains(tokens[1]):
                (fromName, toName) = (tokens[0], tokens[2]);
                break;
            default:
                return false;
        }

        if (!Aliases.TryGetValue(fromName, out var from) || !Aliases.TryGetValue(toName, out var to))
            return false;

        if (!TryConvert(input, from, to, out var value))
            return false;

        conversion = new UnitConversion(input, from, to, value);
        return true;
    }

    /// <summary>Aynı kategorideki iki birim arasında çevirir; kategori farklıysa, sıcaklık mutlak sıfırın altındaysa ya da sonuç sonsuzsa false.</summary>
    public static bool TryConvert(double value, MeasureUnit from, MeasureUnit to, out double result)
    {
        result = 0;
        if (from.Category != to.Category || !double.IsFinite(value))
            return false;

        if (from.Category == UnitCategory.Temperature)
        {
            var celsius = ToCelsius(value, from);
            if (celsius < AbsoluteZeroCelsius - 1e-9)
                return false;

            result = FromCelsius(celsius, to);
        }
        else
        {
            result = value * from.Factor / to.Factor;
        }

        return double.IsFinite(result);
    }

    /// <summary>Takma ad ("KM/H", "°C", "miles") ile birim bulur.</summary>
    public static bool TryFindUnit(string? name, out MeasureUnit unit)
    {
        unit = null!;
        return !string.IsNullOrWhiteSpace(name) && Aliases.TryGetValue(SearchText.Fold(name.Trim()), out unit!);
    }

    private static double ToCelsius(double value, MeasureUnit unit) =>
        ReferenceEquals(unit, Fahrenheit) ? (value - 32) * 5 / 9
        : ReferenceEquals(unit, Kelvin) ? value - 273.15
        : value;

    private static double FromCelsius(double celsius, MeasureUnit unit) =>
        ReferenceEquals(unit, Fahrenheit) ? celsius * 9 / 5 + 32
        : ReferenceEquals(unit, Kelvin) ? celsius + 273.15
        : celsius;

    /// <summary>
    /// "10,5" ve "10.5" ondalıktır. Tek ayraç kültürün binlik ayracıysa ve ardından tam 3 hane geliyorsa binliktir
    /// (tr: "1.500" = 1500, en: "1,500" = 1500). Birden çok aynı ayraç binliktir; ikisi birlikte ise sonuncusu ondalıktır.
    /// </summary>
    public static bool TryParseNumber(string? text, CultureInfo culture, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var s = text.Trim();
        var sign = "";
        if (s[0] is '+' or '-')
        {
            sign = s[0] == '-' ? "-" : "";
            s = s[1..];
        }

        if (s.Length == 0 || !s.Any(char.IsAsciiDigit) || s.Any(c => !char.IsAsciiDigit(c) && c is not ('.' or ',')))
            return false;

        var dots = s.Count(c => c == '.');
        var commas = s.Count(c => c == ',');
        string clean;
        if (dots > 0 && commas > 0)
        {
            var decimalChar = s.LastIndexOf('.') > s.LastIndexOf(',') ? '.' : ',';
            var groupChar = decimalChar == '.' ? ',' : '.';
            if (s.Count(c => c == decimalChar) != 1 || s.LastIndexOf(groupChar) > s.IndexOf(decimalChar))
                return false;

            clean = s.Replace(groupChar.ToString(), "").Replace(decimalChar, '.');
        }
        else if (dots + commas > 1)
        {
            // Aynı ayraç tekrar ediyor: binlik. "1.2.3" gibi bozuk gruplar geçersiz.
            var separator = dots > 0 ? '.' : ',';
            var groups = s.Split(separator);
            if (groups[0].Length is 0 or > 3 || groups.Skip(1).Any(g => g.Length != 3))
                return false;

            clean = string.Concat(groups);
        }
        else if (dots + commas == 1)
        {
            var separator = dots > 0 ? '.' : ',';
            var index = s.IndexOf(separator);
            var before = s[..index];
            var after = s[(index + 1)..];
            var cultureGroup = culture.NumberFormat.NumberGroupSeparator;
            var isGroup = cultureGroup.Length == 1 && cultureGroup[0] == separator
                && after.Length == 3 && before.Length is >= 1 and <= 3 && before != "0";
            clean = isGroup ? before + after : before + "." + after;
        }
        else
        {
            clean = s;
        }

        return double.TryParse(sign + clean, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    /// <summary>6 anlamlı haneye yuvarlar (büyük sayılarda tam sayıya), gereksiz sıfırları atar; üstel yazım yok.</summary>
    public static string FormatNumber(double value, CultureInfo culture, bool grouping)
    {
        if (!double.IsFinite(value))
            return "";

        if (value == 0)
            return "0";

        var magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var places = Math.Clamp(SignificantDigits - 1 - magnitude, 0, MaxDecimalPlaces);
        var pattern = grouping ? "#,##0.############" : "0.############";
        if (Math.Abs(value) < 7.9e27)
        {
            var rounded = Math.Round((decimal)value, places, MidpointRounding.AwayFromZero);
            if (rounded != 0)
                return rounded.ToString(pattern, culture);
        }

        // Çok büyük ya da çok küçük: ondalık türe sığmaz, bilimsel yazım.
        return value.ToString("G" + SignificantDigits, culture);
    }
}
