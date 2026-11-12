using System.Globalization;
using System.Text;

namespace Koy.Core.Extensions.Store;

/// <summary>İndeks ve paket manifestinin ortak alan kuralları (saf, fırlatmaz).</summary>
internal static class StoreFieldRules
{
    public const int MaxLicenseLength = 64;
    public const int MaxSummaryLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxUrlLength = 512;

    /// <summary>
    /// SPDX ifadesi için yeterince geniş küme: harf, rakam ve ". - + ( ) :" ile boşluk ("MIT OR Apache-2.0").
    /// Baştaki/sondaki boşluk ve boş metin yok.
    /// </summary>
    public static bool IsValidLicense(string? value)
    {
        if (value is null || value.Length == 0 || value.Length > MaxLicenseLength)
        {
            return false;
        }

        if (value[0] == ' ' || value[^1] == ' ')
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '+' or '(' or ')' or ':' or ' '))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Yalnız https, mutlak, kullanıcı bilgisiz, boşluk/denetim karakterisiz adres.</summary>
    public static bool IsHttpsUrl(string? value)
    {
        if (value is null || value.Length == 0 || value.Length > MaxUrlLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c <= ' ' || c == '\u007f' || c == '\\')
            {
                return false;
            }
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Length > 0
            && string.IsNullOrEmpty(uri.UserInfo);
    }

    public static bool IsSha256(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Düz metin: uzunluk sınırında, eşleşmemiş vekil ve yön/biçim karakteri yok. Denetim karakterinden yalnız
    /// <paramref name="allowNewlines"/> açıkken satır sonu ve sekme serbest.
    /// </summary>
    public static bool IsPlainText(string? value, int maxLength, bool allowNewlines, bool allowEmpty)
    {
        if (value is null || value.Length > maxLength || (!allowEmpty && value.Trim().Length == 0))
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (allowNewlines && value[i] is '\n' or '\r' or '\t')
            {
                continue;
            }

            if (!Rune.TryGetRuneAt(value, i, out var rune))
            {
                return false;
            }

            switch (Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.Control:
                case UnicodeCategory.Format:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.OtherNotAssigned:
                    return false;
            }

            if (!rune.IsBmp)
            {
                i++;
            }
        }

        return true;
    }
}
