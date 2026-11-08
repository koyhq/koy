namespace Koy.Core.Search;

/// <summary>
/// Sorgunun bir metne ne kadar uyduğunu 0 (uymuyor) ile 1 arasında puanlar. Yazılan her sözcük eşleşmelidir.
/// Metin <see cref="SearchText.Fold"/> ile sadeleştirilmiş gelir.
/// </summary>
public static class SearchMatcher
{
    /// <summary>Yazım hatası toleransı kısa sözcüklerde her şeyi eşleştirmesin diye en az bu uzunlukta uygulanır.</summary>
    private const int MinTypoLength = 4;

    private static readonly char[] WordSeparators = [' ', '-', '_', '.', ',', '(', ')', '[', ']', '\\', '/', ':', '+'];

    public static double Score(SearchQuery query, string folded)
    {
        if (query.IsEmpty || folded.Length == 0)
            return 0;

        if (folded == query.Folded)
            return 1;

        var words = folded.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Concat(words.Select(w => w[0]));

        var total = 0.0;
        foreach (var term in query.Words)
        {
            var score = ScoreTerm(term, folded, words, initials);
            if (score == 0)
                return 0;

            total += score;
        }

        // Aynı tür eşleşmede daha kısa ad öne geçer: sorgu metnin büyük kısmını kaplıyordur.
        var coverage = Math.Min(1.0, (double)query.Folded.Length / folded.Length);
        return total / query.Words.Count * 0.9 + coverage * 0.09;
    }

    private static double ScoreTerm(string term, string folded, string[] words, string initials)
    {
        if (folded.StartsWith(term, StringComparison.Ordinal))
            return 0.95;

        if (words.Contains(term))
            return 0.9;

        if (words.Any(w => w.StartsWith(term, StringComparison.Ordinal)))
            return 0.8;

        if (term.Length >= 2 && initials.StartsWith(term, StringComparison.Ordinal))
            return 0.7;

        if (folded.Contains(term, StringComparison.Ordinal))
            return 0.5;

        if (term.Length >= MinTypoLength && words.Any(w => IsOneEditAway(term, w)))
            return 0.3;

        return 0;
    }

    /// <summary>Yazım hatası: bir harf farklı, bir harf eksik ya da fazla, ya da yan yana iki harf yer değiştirmiş.</summary>
    private static bool IsOneEditAway(string term, string word)
    {
        // Uzun sözcüğün başını da yoklar: "chorm" da "chrome" bulsun.
        if (word.Length > term.Length + 1)
            word = word[..(term.Length + 1)];

        return Math.Abs(term.Length - word.Length) switch
        {
            0 => IsOneSubstitutionOrSwap(term, word),
            1 => term.Length < word.Length ? IsOneInsertion(term, word) : IsOneInsertion(word, term),
            _ => false
        };
    }

    private static bool IsOneSubstitutionOrSwap(string a, string b)
    {
        var first = -1;
        var differences = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i])
                continue;

            if (first < 0)
                first = i;
            differences++;
        }

        if (differences <= 1)
            return true;

        return differences == 2 && first + 1 < a.Length && a[first] == b[first + 1] && a[first + 1] == b[first];
    }

    private static bool IsOneInsertion(string shorter, string longer)
    {
        var i = 0;
        while (i < shorter.Length && shorter[i] == longer[i])
            i++;

        return string.CompareOrdinal(shorter, i, longer, i + 1, shorter.Length - i) == 0;
    }
}
