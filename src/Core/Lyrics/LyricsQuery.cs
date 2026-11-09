using System.Text.RegularExpressions;

namespace Koy.Core.Lyrics;

/// <summary>Söz aranacak parça. Süre bilinmiyorsa null.</summary>
public sealed partial record LyricsQuery(string Title, string Artist, TimeSpan? Duration)
{
    private const int MaxFieldLength = 200;

    /// <summary>Parça kimliği. Süre dışarıda: sonradan gelen süre aynı parçayı yeniden yüklemesin.</summary>
    public string Key => $"{Artist.ToLowerInvariant()}\n{Title.ToLowerInvariant()}";

    /// <summary>"A, B" gibi birleşik sanatçı alanında ilk ad; söz veritabanları çoğunlukla onu tutar.</summary>
    public string PrimaryArtist => Artist.Split(',', 2)[0].Trim();

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*\b(?:official|lyrics?|audio|video|visuali[sz]er|hd|4k|remaster(?:ed)?|feat|ft|resmi|klip|sözleri?)\b[^\)\]]*[\)\]]",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TitleNoise();

    [GeneratedRegex(@"\s+-\s+(?:\d{4}\s+)?(?:digital\s+)?remaster(?:ed)?(?:\s+\d{4})?\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TrailingRemaster();

    [GeneratedRegex(@"(?:\s+-\s+Topic|VEVO)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ChannelSuffix();

    /// <summary>
    /// Medya oturumundaki ham başlık ve sanatçıdan sorgu üretir. Tarayıcıdaki videolarda başlık "Sanatçı - Parça (Official
    /// Video)", sanatçı kanal adı olur; bu gürültü temizlenmezse hiçbir kaynak eşleşmez. Başlık yoksa null.
    /// </summary>
    public static LyricsQuery? Create(string? rawTitle, string? rawArtist, TimeSpan duration)
    {
        var title = TrailingRemaster().Replace(TitleNoise().Replace(rawTitle ?? string.Empty, string.Empty), string.Empty).Trim();
        var artist = ChannelSuffix().Replace(rawArtist?.Trim() ?? string.Empty, string.Empty).Trim();

        var dash = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
        {
            var left = title[..dash].Trim();
            var right = title[(dash + 3)..].Trim();
            if (right.Length > 0 && (artist.Length == 0 || NamesOverlap(left, artist)))
            {
                artist = left;
                title = right;
            }
        }

        if (title.Length is 0 or > MaxFieldLength || artist.Length > MaxFieldLength)
            return null;

        return new LyricsQuery(title, artist, duration > TimeSpan.Zero ? duration : null);
    }

    // Kanal adı çoğu zaman sanatçı adına ek taşır ("Queen Official"). Harf değil kelime karşılaştırılır:
    // "A" sanatçısı "Anything - Else" başlığını ayırmasın.
    private static bool NamesOverlap(string first, string second)
    {
        var a = Words(first);
        var b = Words(second);
        if (a.Count == 0 || b.Count == 0)
            return false;

        return a.Count <= b.Count ? a.IsSubsetOf(b) : b.IsSubsetOf(a);
    }

    private static HashSet<string> Words(string text) =>
        text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
}
