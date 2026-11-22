using System.Globalization;
using System.Text;

namespace Koy.Core.Voice;

/// <summary>
/// Konuşma tanıma modelleri kısa ve sessiz kayıtlara kalıp cümleler uydurur ("izlediğiniz için teşekkürler", "altyazı M.K.").
/// Sessizlik denetimini (<see cref="SpeechGate"/>) geçen kısa bir kayıttan böyle bir cümle dönerse yazıya dökülmez.
/// Yalnız <b>tam eşleşme</b> sayılır ve tek sözcüklük yanıtlar listede yoktur: insanlar "evet", "tamam", "bye" gibi
/// şeyleri gerçekten söyler.
/// </summary>
public static class HallucinationFilter
{
    /// <summary>Bundan uzun kayıttan dönen kalıp cümle büyük olasılıkla gerçekten söylenmiştir.</summary>
    public const double MaxClipSeconds = 10;

    // Katlanmış biçimde (küçük harf, aksansız, noktalamasız) yazılır; bkz. Fold.
    private static readonly HashSet<string> StockPhrases = new(StringComparer.Ordinal)
    {
        "thanks for watching",
        "thank you for watching",
        "please subscribe",
        "subscribe to my channel",
        "izlediginiz icin tesekkurler",
        "izlediginiz icin tesekkur ederim",
        "abone olmayi unutmayin",
        "kanalima abone olmayi unutmayin",
    };

    // Türkçe altyazı kredisi sözcükleri farklı sırayla dizilir ("Çeviri ve Altyazı M.K."); tek tek sıraya bakılmaz.
    private static readonly HashSet<string> CreditWords = new(StringComparer.Ordinal)
    {
        "altyazi", "altyazilar", "ceviri", "ve", "mk", "m", "k",
    };

    /// <summary>Metin, kısa bir kayıttan dönen uydurma bir kalıp cümle gibi görünüyorsa true.</summary>
    public static bool LooksInvented(string text, double clipSeconds)
    {
        if (string.IsNullOrWhiteSpace(text) || clipSeconds > MaxClipSeconds)
            return false;

        var folded = Fold(text);
        if (folded.Length == 0)
            return false;
        if (StockPhrases.Contains(folded))
            return true;

        var words = folded.Split(' ');
        return words.All(CreditWords.Contains)
            && (words.Contains("mk", StringComparer.Ordinal) || (words.Contains("m", StringComparer.Ordinal) && words.Contains("k", StringComparer.Ordinal)));
    }

    /// <summary>Küçük harf, aksansız, noktalama yerine tek boşluk; Türkçe i/ı/İ aynı harf sayılır.</summary>
    internal static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = true;
        foreach (var raw in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(raw))
            {
                var c = raw is 'ı' or 'İ' ? 'i' : char.ToLowerInvariant(raw);
                builder.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().TrimEnd();
    }
}
