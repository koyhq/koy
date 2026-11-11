using System.Globalization;

namespace Koy.Core.Appearance;

/// <summary>
/// Geçerliliği denetlenmiş geçersiz kılmalar: token adı → kanonik metin değer. Değerler her zaman
/// <see cref="StyleTokens"/> kurallarından geçmiş olur (renk "#RRGGBB", sayı invariant, enum listedeki ad).
/// </summary>
public sealed record StyleOverrides(IReadOnlyDictionary<string, string> Values)
{
    public static StyleOverrides Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));
}

public enum StyleDropReason
{
    /// <summary>Ad whitelist'te yok.</summary>
    UnknownToken,

    /// <summary>Aynı ad girdide daha önce geldi; ilk değer kalır.</summary>
    Duplicate,

    /// <summary>Değer boş ya da null.</summary>
    MissingValue,

    /// <summary>Renk "#RGB" ya da "#RRGGBB" biçiminde değil.</summary>
    MalformedColor,

    /// <summary>Sayı çözülemedi ya da sonlu değil.</summary>
    MalformedNumber,

    /// <summary>Sayı izinli aralığın dışında.</summary>
    OutOfRange,

    /// <summary>Enum adı izinli listede yok.</summary>
    UnknownEnumValue
}

public sealed record DroppedStyleOverride(string Name, StyleDropReason Reason);

public sealed record StyleSanitizeResult(StyleOverrides Accepted, IReadOnlyList<DroppedStyleOverride> Dropped);

/// <summary>
/// Kullanıcı (ya da paylaşılan paket) girdisini whitelist'e göre süzer. Saf fonksiyon: dosya, WPF ya da
/// ağ yok. Reddedilen her giriş <see cref="StyleSanitizeResult.Dropped"/> içinde nedeniyle raporlanır,
/// böylece tanılama ekranı neyin neden atıldığını gösterebilir.
/// </summary>
public static class StyleOverrideSanitizer
{
    // Bundan uzun bir değer zaten geçerli bir renk, sayı ya da enum değil; ayrıştırmadan önce elenir.
    private const int MaxValueLength = 32;

    // Rapora yazılan anahtar uzunluğu sınırı: dosyadaki dev anahtar tanılama ekranını şişirmesin.
    private const int MaxEchoedNameLength = 64;

    public static StyleSanitizeResult Sanitize(IEnumerable<KeyValuePair<string, string?>> raw)
    {
        var accepted = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var dropped = new List<DroppedStyleOverride>();

        foreach (var (name, value) in raw)
        {
            // JSON anahtarı null gelemez ama sınır çağrıcıdan geçer; çökmek yerine reddedilir.
            if (name is null)
            {
                dropped.Add(new DroppedStyleOverride(string.Empty, StyleDropReason.UnknownToken));
                continue;
            }

            if (!seen.Add(name))
            {
                dropped.Add(new DroppedStyleOverride(EchoName(name), StyleDropReason.Duplicate));
                continue;
            }

            if (!StyleTokens.TryGet(name, out var token))
            {
                dropped.Add(new DroppedStyleOverride(EchoName(name), StyleDropReason.UnknownToken));
                continue;
            }

            var reason = Validate(token, value, out var canonical);
            if (reason is null)
                accepted[name] = canonical;
            else
                dropped.Add(new DroppedStyleOverride(name, reason.Value));
        }

        return new StyleSanitizeResult(new StyleOverrides(accepted), dropped);
    }

    // Yalnız rapora giden ad kırpılır; eşleşme ve tekrar kontrolü tam ad üzerinden yapılmıştır.
    private static string EchoName(string name) =>
        name.Length <= MaxEchoedNameLength ? name : name[..MaxEchoedNameLength];

    private static StyleDropReason? Validate(StyleToken token, string? value, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return StyleDropReason.MissingValue;

        var text = value.Trim();
        if (text.Length > MaxValueLength)
            return token.Kind switch
            {
                StyleValueKind.Color => StyleDropReason.MalformedColor,
                StyleValueKind.Number => StyleDropReason.MalformedNumber,
                _ => StyleDropReason.UnknownEnumValue
            };

        switch (token.Kind)
        {
            case StyleValueKind.Color:
                return ValidateColor(text, out canonical);

            case StyleValueKind.Number:
                return ValidateNumber(token, text, out canonical);

            default:
                return ValidateEnum(token, text, out canonical);
        }
    }

    private static StyleDropReason? ValidateColor(string text, out string canonical)
    {
        canonical = string.Empty;
        // Yalnız "#" ile başlayan 3 ya da 6 hanelik onaltılık; alfa ve ad (örn. "red") kabul edilmez.
        if (text.Length is not (4 or 7) || text[0] != '#' || !IsHexOnly(text.AsSpan(1)))
            return StyleDropReason.MalformedColor;

        if (!Rgb.TryParse(text, out var rgb))
            return StyleDropReason.MalformedColor;

        canonical = rgb.ToHex();
        return null;
    }

    private static StyleDropReason? ValidateNumber(StyleToken token, string text, out string canonical)
    {
        canonical = string.Empty;
        // Sayı kültürden bağımsız okunur: "1,5" Türkçe ortamda da aynı reddedilir/kabul edilir.
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            return StyleDropReason.MalformedNumber;

        if (number < token.Min || number > token.Max)
            return StyleDropReason.OutOfRange;

        // -0 eşitlikte 0'a eşit olduğu için aralığı geçer; kanonik metinde "-0" çıkmasın diye sıfır yazılır.
        if (number == 0)
            number = 0;

        // "R" küçük değerleri "1E-05" gibi üstel biçime çevirir. Sabit basamaklı biçim aralıkta hiç üstel üretmez.
        canonical = number.ToString("0.################", CultureInfo.InvariantCulture);
        return null;
    }

    private static StyleDropReason? ValidateEnum(StyleToken token, string text, out string canonical)
    {
        canonical = string.Empty;
        // Listesi olmayan token hiçbir enum değerini kabul etmez (default ImmutableArray üzerinde sayım çöker).
        if (token.EnumValues.IsDefault)
            return StyleDropReason.UnknownEnumValue;

        foreach (var option in token.EnumValues)
        {
            if (string.Equals(option, text, StringComparison.OrdinalIgnoreCase))
            {
                canonical = option;
                return null;
            }
        }

        return StyleDropReason.UnknownEnumValue;
    }

    private static bool IsHexOnly(ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return true;
    }
}
