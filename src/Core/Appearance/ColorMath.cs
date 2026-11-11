using System.Globalization;

namespace Koy.Core.Appearance;

/// <summary>sRGB renk (alfa yok; saydamlık ayrı tutulur). WCAG 2.x kontrast hesabı için.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>"#RGB", "#RRGGBB" ya da "#AARRGGBB" metnini çözer; alfa yok sayılır.</summary>
    public static bool TryParse(string? hex, out Rgb color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3)
            s = string.Concat(s.Select(c => new string(c, 2)));
        if (s.Length == 8)
            s = s[2..];
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return false;

        color = new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static Rgb Parse(string hex) =>
        TryParse(hex, out var c) ? c : throw new FormatException($"Geçersiz renk: {hex}");
}

/// <summary>HSL renk: H derece (0 ile 360 arası), S ve L 0 ile 1 arası.</summary>
public readonly record struct Hsl(double H, double S, double L);

public static class ColorMath
{
    /// <summary>WCAG göreli parlaklık (0 siyah, 1 beyaz).</summary>
    public static double Luminance(Rgb c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    /// <summary>WCAG kontrast oranı (1 ile 21 arası).</summary>
    public static double Contrast(Rgb a, Rgb b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    public static double Contrast(string hexA, string hexB) => Contrast(Rgb.Parse(hexA), Rgb.Parse(hexB));

    /// <summary>a'dan b'ye t oranında karışım (0 = a, 1 = b).</summary>
    public static Rgb Mix(Rgb a, Rgb b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        byte M(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
        return new Rgb(M(a.R, b.R), M(a.G, b.G), M(a.B, b.B));
    }

    /// <summary>sRGB'yi HSL'e çevirir. Gri tonlarda H 0 ve S 0 döner.</summary>
    public static Hsl ToHsl(Rgb c) => ToHsl(c.R, c.G, c.B);

    /// <summary>Bayt kanallarından doğrudan HSL (ayırma yapmaz; piksel döngüleri için).</summary>
    public static Hsl ToHsl(byte r8, byte g8, byte b8)
    {
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var d = max - min;
        if (d <= 0)
            return new Hsl(0, 0, l);

        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == r)
            h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g)
            h = (b - r) / d + 2;
        else
            h = (r - g) / d + 4;
        return new Hsl(h * 60, s, l);
    }

    /// <summary>HSL'i sRGB'ye çevirir; H 360'a sarılır, S ve L 0..1'e kıskaçlanır.</summary>
    public static Rgb FromHsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        static byte B(double v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
        return new Rgb(B(r + m), B(g + m), B(b + m));
    }

    public static Rgb FromHsl(Hsl hsl) => FromHsl(hsl.H, hsl.S, hsl.L);
}
