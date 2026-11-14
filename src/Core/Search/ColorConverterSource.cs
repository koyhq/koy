using System.Globalization;
using System.Text.RegularExpressions;
using Koy.Core.Appearance;

namespace Koy.Core.Search;

/// <summary>
/// Çözülmüş renk. <see cref="Alpha"/> 0..255. <see cref="InputHsl"/>: HSL girdisinde kullanıcının yazdığı değer;
/// sekizli kanala yuvarlanınca kayan S/L geri hesaplanıp gösterilmesin diye saklanır.
/// </summary>
public sealed record ParsedColor(Rgb Color, byte Alpha, Hsl? InputHsl = null)
{
    public bool HasAlpha => Alpha != 255;

    public string ToHex() => HasAlpha ? $"#{Alpha:X2}{Color.R:X2}{Color.G:X2}{Color.B:X2}" : Color.ToHex();

    public string ToRgbText() => HasAlpha
        ? $"rgba({Color.R}, {Color.G}, {Color.B}, {AlphaText()})"
        : $"rgb({Color.R}, {Color.G}, {Color.B})";

    public string ToHslText()
    {
        var hsl = InputHsl ?? ColorMath.ToHsl(Color);
        var h = Whole(((hsl.H % 360) + 360) % 360);
        if (h == "360")
            h = "0";
        var s = Whole(hsl.S * 100);
        var l = Whole(hsl.L * 100);
        return HasAlpha ? $"hsla({h}, {s}%, {l}%, {AlphaText()})" : $"hsl({h}, {s}%, {l}%)";
    }

    private static string Whole(double value) => ((int)Math.Round(value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    private string AlphaText() => Math.Round(Alpha / 255.0, 2).ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>Renk girdisini çözer: #RGB, #RRGGBB, #AARRGGBB (Windows/WPF sırası, alfa önde), rgb(r,g,b), hsl(h,s%,l%).</summary>
public static partial class ColorParser
{
    private const int MaxInputLength = 48;

    [GeneratedRegex(@"^#(?<hex>[0-9a-f]{3}|[0-9a-f]{6}|[0-9a-f]{8})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HexPattern();

    [GeneratedRegex(@"^rgb\(\s*(?<r>\d{1,3})\s*[,\s]\s*(?<g>\d{1,3})\s*[,\s]\s*(?<b>\d{1,3})\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RgbPattern();

    [GeneratedRegex(@"^hsl\(\s*(?<h>[+-]?\d+(?:\.\d+)?)\s*(?:°|deg)?\s*[,\s]\s*(?<s>\d+(?:\.\d+)?)\s*%?\s*[,\s]\s*(?<l>\d+(?:\.\d+)?)\s*%?\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HslPattern();

    /// <summary>Girdi gerçekten renk gibi değilse (kanal 255'ten büyük, S/L yüzde 100'den büyük...) false.</summary>
    public static bool TryParse(string? text, out ParsedColor color)
    {
        color = null!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxInputLength)
            return false;

        var s = text.Trim();
        return TryParseHex(s, out color) || TryParseRgb(s, out color) || TryParseHsl(s, out color);
    }

    private static bool TryParseHex(string s, out ParsedColor color)
    {
        color = null!;
        var match = HexPattern().Match(s);
        if (!match.Success)
            return false;

        var hex = match.Groups["hex"].Value;
        byte alpha = 255;
        if (hex.Length == 8)
        {
            alpha = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            hex = hex[2..];
        }

        if (!Rgb.TryParse(hex, out var rgb))
            return false;

        color = new ParsedColor(rgb, alpha);
        return true;
    }

    private static bool TryParseRgb(string s, out ParsedColor color)
    {
        color = null!;
        var match = RgbPattern().Match(s);
        if (!match.Success)
            return false;

        var r = int.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture);
        var g = int.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture);
        var b = int.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture);
        if (r > 255 || g > 255 || b > 255)
            return false;

        color = new ParsedColor(new Rgb((byte)r, (byte)g, (byte)b), 255);
        return true;
    }

    private static bool TryParseHsl(string s, out ParsedColor color)
    {
        color = null!;
        var match = HslPattern().Match(s);
        if (!match.Success)
            return false;

        var h = double.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var sat = double.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
        var light = double.Parse(match.Groups["l"].Value, CultureInfo.InvariantCulture);
        if (!double.IsFinite(h) || sat > 100 || light > 100)
            return false;

        var hsl = new Hsl(((h % 360) + 360) % 360, sat / 100, light / 100);
        color = new ParsedColor(ColorMath.FromHsl(hsl), 255, hsl);
        return true;
    }
}

/// <summary>Renk çevirici: "#FF8800", "rgb(255, 136, 0)", "hsl(32, 100%, 50%)" yazınca HEX, RGB ve HSL karşılıkları; her satır Enter ile kopyalanır.</summary>
public sealed class ColorConverterSource(ISearchShell shell, Func<string, string> t) : SearchSource("color", t("SearchCatColor"))
{
    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (!ColorParser.TryParse(query.Raw, out var color))
            return [];

        var rows = new (string Format, string Text)[]
        {
            ("HEX", color.ToHex()),
            ("RGB", color.ToRgbText()),
            ("HSL", color.ToHslText())
        };

        return rows.Select((row, index) => new SearchResult($"color:{row.Format}:{query.Raw}", Name, row.Text, () => shell.CopyText(row.Text))
        {
            Subtitle = $"{row.Format} · {t("SearchHintCopy")}",
            Glyph = SearchGlyph.Clipboard,
            CategoryRelevance = SearchRelevance.Highest,
            Relevance = 1 - index * 0.1,
            RestoresFocus = true,
            Drag = new SearchDragData(SearchDragKind.Text, row.Text)
        }).ToList();
    }
}
