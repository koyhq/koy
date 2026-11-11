using System.Globalization;

namespace Koy.Core.Notch;

/// <summary>
/// Halka modundaki boşta metinleri ve halka geometrisi. XAML yalnız bağlanır; biçim ve çizim burada, testli.
/// </summary>
public static class IdleDisplayFormat
{
    /// <summary>Halkanın kutusu (DIP).</summary>
    public const double RingSize = 18;

    /// <summary>Halkanın yarıçapı: kalınlık 2,5 olduğundan 18 px kutuya taşmadan sığar (9 - 2,5 / 2 - 0,5).</summary>
    public const double RingRadius = 7.25;

    /// <summary>Halka kalınlığı (DIP).</summary>
    public const double RingThickness = 2.5;

    /// <summary>Bu yüzde ve altında, şarj olmuyorsa halka düşük pil rengine geçer.</summary>
    public const int LowBatteryPercent = 20;

    /// <summary>Kısa gün adı ve 24 saatlik saat: "Sal 19:21" / "Tue 19:21".</summary>
    public static string DayTime(DateTime now, CultureInfo culture) =>
        culture.DateTimeFormat.GetAbbreviatedDayName(now.DayOfWeek) + " " + now.ToString("HH:mm", culture);

    /// <summary>Peek'te saatin yanındaki kısa tarih, dilin gün-ay sırasıyla: "18 Kas" / "Nov 18".</summary>
    public static string ShortDate(DateTime now, CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);
        return now.ToString(pattern, culture);
    }

    /// <summary>Yüzde, dilin yazımıyla: "%43" (Türkçe) / "43%" (İngilizce).</summary>
    public static string Percent(int percent, CultureInfo culture)
    {
        var n = Math.Clamp(percent, 0, 100).ToString(culture);
        var sign = culture.NumberFormat.PercentSymbol;
        return culture.NumberFormat.PercentPositivePattern switch
        {
            0 => n + " " + sign,
            2 => sign + n,
            3 => sign + " " + n,
            _ => n + sign
        };
    }

    /// <summary>Halka düşük pil renginde mi: şarj olmuyor ve yüzde eşikte ya da altında.</summary>
    public static bool IsLow(int percent, bool charging) => !charging && percent <= LowBatteryPercent;

    /// <summary>
    /// Halkanın dolu kısmının yay sonundaki nokta. Yay en üstten (saat 12) başlar ve saat yönünde ilerler.
    /// </summary>
    public static (double X, double Y) ArcPoint(double percent, double radius, double center)
    {
        var angle = Math.Clamp(percent, 0, 100) / 100.0 * 2 * Math.PI;
        return (center + radius * Math.Sin(angle), center - radius * Math.Cos(angle));
    }

    /// <summary>
    /// Halkanın dolu kısmı için yol verisi (WPF / SVG yol söz dizimi). %0'da boş metin (çizilecek yay yok); %100'de tek
    /// yay tam daire çizemediği için iki yarım yay.
    /// </summary>
    public static string ArcPath(int percent, double size = RingSize, double radius = RingRadius)
    {
        var pct = Math.Clamp(percent, 0, 100);
        if (pct == 0)
            return "";

        var c = size / 2;
        var top = c - radius;
        var r = F(radius);
        if (pct == 100)
            return $"M {F(c)},{F(top)} A {r},{r} 0 1 1 {F(c)},{F(c + radius)} A {r},{r} 0 1 1 {F(c)},{F(top)}";

        var (x, y) = ArcPoint(pct, radius, c);
        var large = pct > 50 ? 1 : 0;
        return $"M {F(c)},{F(top)} A {r},{r} 0 {large} 1 {F(x)},{F(y)}";
    }

    private static string F(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
}
