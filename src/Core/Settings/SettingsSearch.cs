using System.Text;

namespace Koy.Core.Settings;

/// <summary>
/// Ayar arama satırı: anahtar, sayfa ve App'in yerelleştirdiği metinler. Core metni çevirmez, yalnız eşleştirir
/// (Core resx'e erişemez, bkz. docs/MAP.md).
/// </summary>
public sealed record SettingSearchEntry(
    string Key,
    string Page,
    string Title,
    string Description,
    IReadOnlyList<string> Keywords);

/// <summary>Arama sonucu: eşleşen satır ve puanı (yüksek olan önce).</summary>
public sealed record SettingSearchHit(SettingSearchEntry Entry, int Score);

/// <summary>
/// Ayarlar penceresindeki arama. Sorgu boşluklara göre parçalanır; her parça satırın en az bir yerinde
/// (başlık, anahtar sözcük ya da açıklama) geçmelidir. Büyük/küçük harf ve Türkçe harfler (ı/i, ş/s, ğ/g, ç/c, ö/o, ü/u)
/// farksızdır: "karanlik" sorgusu "Karanlık tema" satırını bulur.
/// Puan, parça başına en iyi eşleşmeye göre: başlık 3, anahtar sözcük 2, açıklama 1. Başlığın tamamı sorguyla
/// başlıyorsa +2. Eşit puanda verilen sıra korunur.
/// </summary>
public static class SettingsSearch
{
    private const int TitleWeight = 3;
    private const int KeywordWeight = 2;
    private const int DescriptionWeight = 1;
    private const int PrefixBonus = 2;

    private static readonly char[] Separators = [' ', '\t', '\r', '\n'];

    public static IReadOnlyList<SettingSearchHit> Search(IEnumerable<SettingSearchEntry> entries, string? query, int limit = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        var tokens = Tokenize(query);
        if (tokens.Count == 0 || limit == 0)
        {
            return [];
        }

        var fullQuery = string.Join(' ', tokens);
        var hits = new List<SettingSearchHit>();
        foreach (var entry in entries)
        {
            var score = ScoreEntry(entry, tokens, fullQuery);
            if (score > 0)
            {
                hits.Add(new SettingSearchHit(entry, score));
            }
        }

        // OrderByDescending kararlıdır: eşit puanlı satırlar verildiği sırayla kalır.
        return hits.OrderByDescending(h => h.Score).Take(limit).ToList();
    }

    private static List<string> Tokenize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return Fold(query).Split(Separators, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();
    }

    private static int ScoreEntry(SettingSearchEntry entry, IReadOnlyList<string> tokens, string fullQuery)
    {
        var title = Fold(entry.Title);
        var description = Fold(entry.Description);
        var keywords = entry.Keywords.Select(Fold).ToList();

        var score = 0;
        foreach (var token in tokens)
        {
            var best = 0;
            if (title.Contains(token, StringComparison.Ordinal))
            {
                best = Math.Max(best, TitleWeight);
            }

            if (keywords.Any(k => k.Contains(token, StringComparison.Ordinal)))
            {
                best = Math.Max(best, KeywordWeight);
            }

            if (description.Contains(token, StringComparison.Ordinal))
            {
                best = Math.Max(best, DescriptionWeight);
            }

            // Bir parça hiçbir yerde yoksa satır elenir (tüm parçalar gerekir).
            if (best == 0)
            {
                return 0;
            }

            score += best;
        }

        if (title.StartsWith(fullQuery, StringComparison.Ordinal))
        {
            score += PrefixBonus;
        }

        return score;
    }

    /// <summary>Küçük harfe çevirir ve Türkçe harfleri ASCII karşılığına indirir. Arama için yalnız eşleştirmede kullanılır.</summary>
    internal static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var lower = text.ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            // İ küçük harfe çevrilince oluşan birleşik nokta: yok say, yoksa "i" parçalanır.
            if (c == '̇')
            {
                continue;
            }

            sb.Append(c switch
            {
                'ı' or 'İ' or 'î' => 'i',
                'ş' => 's',
                'ç' => 'c',
                'ğ' => 'g',
                'ö' => 'o',
                'ü' => 'u',
                'â' => 'a',
                'û' => 'u',
                _ => c,
            });
        }

        return sb.ToString();
    }
}
