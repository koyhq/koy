using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Koy.Core.Appearance;

namespace Koy.App.Resources;

/// <summary>
/// Doğrulanmış kullanıcı stil değerlerini (Core, WPF'siz) WPF kaynaklarına çevirir. Yalnız ilkel, önceden bilinen
/// türler üretilir (fırça, renk, double, CornerRadius, Thickness, FontFamily); tip adı çözülmez, XAML okunmaz,
/// yansıma yoktur. Fırçalar donmuş üretilir (iş parçacığı sınırı yok, WPF değişiklik izlemez).
/// </summary>
public static class UserStyleResources
{
    /// <summary>Geçerli girdileri ayrıştırılmış kaynaklara çevirir (anahtar → WPF nesnesi).</summary>
    public static void Fill(ResourceDictionary target, IReadOnlyDictionary<string, UserStyleValue> entries)
    {
        foreach (var (key, value) in entries)
        {
            var converted = Convert(value);
            if (converted is not null)
                target[key] = converted;
        }
    }

    public static object? Convert(UserStyleValue value) => value.Kind switch
    {
        UserStyleKind.Brush => FrozenBrush(value.Color),
        UserStyleKind.Color => ToColor(value.Color),
        UserStyleKind.Number => value.Number,
        UserStyleKind.CornerRadius => new CornerRadius(value.Quad.First, value.Quad.Second, value.Quad.Third, value.Quad.Fourth),
        UserStyleKind.Thickness => new Thickness(value.Quad.First, value.Quad.Second, value.Quad.Third, value.Quad.Fourth),
        UserStyleKind.FontFamily => new FontFamily(value.FontFamily),
        _ => null
    };

    private static Color ToColor(UserStyleColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    private static SolidColorBrush FrozenBrush(UserStyleColor c)
    {
        var brush = new SolidColorBrush(ToColor(c));
        brush.Freeze();
        return brush;
    }

    /// <summary>Tanı satırı: "Anahtar: yerelleştirilmiş neden" (anahtar dosya düzeyi tanıda yazılmaz).</summary>
    public static string Describe(UserStyleDiagnostic diagnostic)
    {
        var reason = Strings.Get("UserStyleDiag" + diagnostic.Code);
        if (reason == "UserStyleDiag" + diagnostic.Code)
            reason = diagnostic.Reason; // çeviri yoksa Core'un İngilizce metni
        return diagnostic.Key == UserStyleParser.FileKey ? reason : $"{diagnostic.Key}: {reason}";
    }

    /// <summary>Tanı listesinin satırları; rapor sınırını aşan tanılar için tek "ve N tane daha" satırı eklenir.</summary>
    public static IEnumerable<string> DescribeAll(UserStyleParseResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
            yield return Describe(diagnostic);

        if (result.OmittedDiagnostics > 0)
            yield return string.Format(CultureInfo.CurrentCulture, Strings.Get("UserStyleDiagMore"), result.OmittedDiagnostics);
    }

    /// <summary>Durum satırı: kapalı / dosya yok / "N uygulandı, M yok sayıldı".</summary>
    public static string StatusText(bool enabled, bool fileExists, UserStyleParseResult result)
    {
        if (!enabled)
            return Strings.Get("UserStyleStatusOff");
        if (!fileExists)
            return Strings.Get("UserStyleStatusNoFile");
        return string.Format(CultureInfo.CurrentCulture, Strings.Get("UserStyleStatusLoaded"),
            result.ValidEntries.Count, result.Diagnostics.Count + result.OmittedDiagnostics);
    }
}
