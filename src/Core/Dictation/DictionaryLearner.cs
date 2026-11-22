using Koy.Core.Settings;
using Koy.Core.Voice;

namespace Koy.Core.Dictation;

/// <summary>
/// Sözlüğün kendiliğinden öğrenmesi: temizlenmiş metinde sık geçen, cümle başında olmayan ad benzeri sözcükler
/// (Büyük harfle başlayan, camelCase ya da rakam/nokta içeren) belirli sayıda tekrarlanınca sözlüğe girer.
/// Yalnız sayar; bir ad bir kez geçti diye eklenmez.
/// </summary>
public static class DictionaryLearner
{
    /// <summary>Bir adın sözlüğe girmesi için kaç ayrı yazıda geçmesi gerekir.</summary>
    public const int Threshold = 3;

    /// <summary>Sayılan aday sayısı bu sayıyı aşarsa en eskiler atılır (dosya şişmesin).</summary>
    public const int MaxPending = 200;

    private static readonly char[] Whitespace = [' ', '\n', '\r', '\t'];

    /// <summary>Metindeki ad benzeri sözcükler (tekrarsız, yazıldığı gibi).</summary>
    public static IReadOnlyList<string> Candidates(string text)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var atSentenceStart = true;
        foreach (var raw in text.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = raw.Trim('"', '\'', '(', ')', ',', ';', ':', '!', '?', '“', '”', '…').TrimEnd('.');
            var endsSentence = raw.EndsWith('.') || raw.EndsWith('!') || raw.EndsWith('?');
            if (word.Length >= 2 && !atSentenceStart && LooksLikeName(word) && seen.Add(HallucinationFilter.Fold(word)))
                found.Add(word);

            atSentenceStart = endsSentence;
        }

        return found;
    }

    /// <summary>Bir yazıdaki adayları sayar; eşiğe ulaşanları sözlüğe ekler ve sayaçtan düşer. Eklenen terimler döner.</summary>
    public static IReadOnlyList<string> Learn(DictionaryFile file, string cleanedText)
    {
        ArgumentNullException.ThrowIfNull(file);
        var added = new List<string>();
        foreach (var candidate in Candidates(cleanedText))
        {
            if (DictionaryText.Contains(file.Entries, candidate))
                continue;

            var key = HallucinationFilter.Fold(candidate);
            var count = file.Pending.TryGetValue(key, out var current) ? current + 1 : 1;
            if (count >= Threshold)
            {
                file.Entries.Add(new DictationTerm { Term = candidate, Auto = true });
                file.Pending.Remove(key);
                added.Add(candidate);
            }
            else
            {
                file.Pending[key] = count;
            }
        }

        // Dosya şişmesin: sayaç sınırı aşarsa en düşük sayılılar atılır.
        if (file.Pending.Count > MaxPending)
        {
            foreach (var key in file.Pending.OrderBy(p => p.Value).Take(file.Pending.Count - MaxPending).Select(p => p.Key).ToList())
                file.Pending.Remove(key);
        }

        return added;
    }

    private static bool LooksLikeName(string word)
    {
        if (!word.Any(char.IsLetter))
            return false;

        // Teknik terim: rakam ya da iç nokta (GPT-4o, Node.js).
        if (word.Any(char.IsDigit) || word.Trim('.').Contains('.', StringComparison.Ordinal))
            return word.Any(char.IsLetter);

        var hasLower = word.Any(char.IsLower);
        if (!hasLower)
            return false; // BÜYÜK HARFLİ kısaltma ya da bağırma: ad sayılmaz.

        var firstUpper = char.IsUpper(word[0]);
        var innerUpper = word.Skip(1).Any(char.IsUpper); // camelCase
        return firstUpper || innerUpper;
    }
}
