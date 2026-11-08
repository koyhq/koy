using System.Text;

namespace Koy.Core.Search;

/// <summary>
/// Aramada karşılaştırılacak metni sadeleştirir. Standart küçük harfe çevirme Türkçe I/ı/İ/i için yetmez;
/// ABD klavyesiyle yazan da "sifre" yazınca "Şifre"yi bulsun diye Türkçe harfler sade karşılığına iner.
/// </summary>
public static class SearchText
{
    private const char CombiningDotAbove = '̇';

    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c != CombiningDotAbove)
                sb.Append(FoldChar(c));
        }

        return sb.ToString();
    }

    private static char FoldChar(char c) => c switch
    {
        'I' or 'İ' or 'ı' => 'i',
        'Ş' or 'ş' => 's',
        'Ğ' or 'ğ' => 'g',
        'Ü' or 'ü' => 'u',
        'Ö' or 'ö' => 'o',
        'Ç' or 'ç' => 'c',
        _ => char.ToLowerInvariant(c)
    };
}

/// <summary>Kullanıcının yazdığı metin ve karşılaştırma için hazırlanmış hâli.</summary>
public sealed class SearchQuery
{
    public SearchQuery(string? raw)
    {
        Raw = raw?.Trim() ?? "";
        Folded = SearchText.Fold(Raw);
        Words = Folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public string Raw { get; }

    public string Folded { get; }

    public IReadOnlyList<string> Words { get; }

    public bool IsEmpty => Words.Count == 0;
}
