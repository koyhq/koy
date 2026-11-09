using System.Globalization;
using System.Text.RegularExpressions;

namespace Koy.Core.Lyrics;

/// <summary>
/// LRC metnini zamana göre sıralı satırlara çevirir. Hem <c>[mm:ss.xx]</c> hem <c>[mm:ss:xx]</c> yazımını kabul eder,
/// kelime bazlı <c>&lt;mm:ss.xx&gt;</c> etiketlerini metinden siler. Damgasız satırlar (meta etiketleri) elenir.
/// </summary>
public static partial class LrcParser
{
    // Bozuk ya da kötü niyetli bir dosya belleği şişirmesin; gerçek şarkılar birkaç yüz satırdır.
    private const int MaxLines = 5000;

    [GeneratedRegex(@"\G\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimeTag();

    [GeneratedRegex(@"<\d{1,3}:\d{1,2}(?:[.:]\d{1,3})?>", RegexOptions.CultureInvariant)]
    private static partial Regex WordTag();

    [GeneratedRegex(@"^\[offset:\s*([+-]?\d{1,7})\s*\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex OffsetTag();

    public static IReadOnlyList<LyricLine> Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
            return [];

        var offset = TimeSpan.Zero;
        var lines = new List<LyricLine>();

        foreach (var rawLine in lrc.TrimStart('﻿').Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (OffsetTag().Match(line) is { Success: true } offsetMatch)
            {
                // LRC standardında pozitif offset sözü erkene alır.
                offset = TimeSpan.FromMilliseconds(int.Parse(offsetMatch.Groups[1].ValueSpan, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
                continue;
            }

            var position = 0;
            var stamps = new List<TimeSpan>(1);
            while (TimeTag().Match(line, position) is { Success: true } match)
            {
                stamps.Add(ReadStamp(match));
                position += match.Length;
            }

            if (stamps.Count == 0)
                continue;

            var text = WordTag().Replace(line[position..], string.Empty).Trim();
            foreach (var stamp in stamps)
            {
                if (lines.Count >= MaxLines)
                    break;
                lines.Add(new LyricLine(stamp, text));
            }
        }

        if (lines.TrueForAll(l => l.Text.Length == 0))
            return [];

        return Normalize(lines, offset);
    }

    private static TimeSpan ReadStamp(Match match)
    {
        var minutes = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
        var fraction = match.Groups[3];

        var milliseconds = 0;
        if (fraction.Success)
        {
            // ".5" 500 ms, ".50" 500 ms, ".500" 500 ms: basamak sayısı ölçeği belirler.
            milliseconds = int.Parse(fraction.ValueSpan, CultureInfo.InvariantCulture) * fraction.Length switch
            {
                1 => 100,
                2 => 10,
                _ => 1
            };
        }

        return new TimeSpan(0, 0, minutes, seconds, milliseconds);
    }

    private static List<LyricLine> Normalize(List<LyricLine> lines, TimeSpan offset)
    {
        var result = new List<LyricLine>(lines.Count);
        // OrderBy kararlıdır: aynı damgalı satırlar dosyadaki sırayı korur.
        foreach (var line in lines.OrderBy(l => l.Time))
        {
            // Art arda boş satırlar ekranda anlamsız "♪" dizisi olurdu.
            if (line.Text.Length == 0 && result.Count > 0 && result[^1].Text.Length == 0)
                continue;

            var time = line.Time - offset;
            result.Add(line with { Time = time < TimeSpan.Zero ? TimeSpan.Zero : time });
        }

        return result;
    }
}
