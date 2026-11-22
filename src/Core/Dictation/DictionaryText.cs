using System.Text;

namespace Koy.Core.Dictation;

/// <summary>
/// Sözlüğün modellere gidecek iki yüzü: tanıma modeline <b>ipucu</b> (hedeflenen yazımlar), temizleme modeline <b>sözlük</b>
/// ("duyulan → doğru yazım"). Böylece "kuber netis" gibi yanlış duyulan adlar bağlamdan düzeltilebilir.
/// </summary>
public static class DictionaryText
{
    /// <summary>Tanıma ipucunun en çok uzunluğu; modeller uzun ipucunu keser ya da bozulur.</summary>
    public const int MaxHintLength = 600;

    /// <summary>Elle yazılanlar önce, sonra öğrenilenler; sığdığı kadar.</summary>
    public static string Hint(IEnumerable<DictationTerm> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var ordered = entries.Where(e => !string.IsNullOrWhiteSpace(e.Term)).OrderBy(e => e.Auto).ToList();
        var builder = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in ordered)
        {
            var term = entry.Term.Trim();
            if (!seen.Add(HallucinationFilter.Fold(term)))
                continue;

            var extra = (builder.Length == 0 ? 0 : 2) + term.Length;
            if (builder.Length + extra > MaxHintLength)
                break;

            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(term);
        }

        return builder.ToString();
    }

    /// <summary>Temizleme modeline satır satır sözlük; kayıt yoksa boş.</summary>
    public static string Glossary(IEnumerable<DictationTerm> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var term = entry.Term?.Trim() ?? "";
            if (term.Length == 0)
                continue;

            var heard = entry.Heard?.Trim() ?? "";
            lines.Add(heard.Length == 0 ? term : $"{heard} -> {term}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>Aynı terim iki kez eklenmez (büyük/küçük harf, aksan ve Türkçe i farkı yok sayılır).</summary>
    public static bool Contains(IEnumerable<DictationTerm> entries, string term)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var folded = HallucinationFilter.Fold(term);
        return folded.Length > 0 && entries.Any(e => HallucinationFilter.Fold(e.Term) == folded);
    }
}
