namespace Koy.Core.Appearance;

/// <summary>Değer kipi: renk (#RRGGBB), sınırlı sayı ya da sabit listeden bir ad.</summary>
public enum StyleValueKind
{
    Color,
    Number,
    Enum
}

/// <summary>
/// Kullanıcının geçersiz kılabileceği tek bir tema token'ı. Ad, değer kipi ve izinli aralık ya da liste burada sabittir;
/// dosyadan gelen hiçbir şey bu tanımın dışına çıkamaz.
/// </summary>
public sealed record StyleToken(string Name, StyleValueKind Kind, double Min = 0, double Max = 0, IReadOnlyList<string>? EnumValues = null);

/// <summary>
/// Güvenlik sınırı: yalnız bu listedeki tokenlar geçersiz kılınabilir. XAML, yol, tip adı ya da kod burada yoktur,
/// dolayısıyla kullanıcı dosyası ne XamlReader'a ne de bir türe ulaşır.
/// </summary>
public static class StyleTokens
{
    public static readonly StyleToken Accent = new("Accent", StyleValueKind.Color);
    public static readonly StyleToken Surface = new("Surface", StyleValueKind.Color);
    public static readonly StyleToken Text = new("Text", StyleValueKind.Color);
    public static readonly StyleToken CornerRadius = new("CornerRadius", StyleValueKind.Number, 0, 24);
    public static readonly StyleToken BorderThickness = new("BorderThickness", StyleValueKind.Number, 0, 4);
    public static readonly StyleToken FontSize = new("FontSize", StyleValueKind.Number, 10, 20);
    public static readonly StyleToken FontWeight = new("FontWeight", StyleValueKind.Enum,
        EnumValues: ["Normal", "Medium", "SemiBold", "Bold"]);

    public static IReadOnlyList<StyleToken> All { get; } =
        [Accent, Surface, Text, CornerRadius, BorderThickness, FontSize, FontWeight];

    private static readonly Dictionary<string, StyleToken> ByName =
        All.ToDictionary(t => t.Name, StringComparer.Ordinal);

    /// <summary>Ad tam eşleşmeli (büyük-küçük harf duyarlı) aranır; listede yoksa false.</summary>
    public static bool TryGet(string name, out StyleToken token)
    {
        if (name is not null && ByName.TryGetValue(name, out var found))
        {
            token = found;
            return true;
        }

        token = null!;
        return false;
    }
}
