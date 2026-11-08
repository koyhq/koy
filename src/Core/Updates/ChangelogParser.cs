using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Koy.Core.Updates;

/// <summary>
/// Ayrıştırılmış sürüm notları bilgisi.
/// </summary>
public sealed record ReleaseNotes(
    string Version,
    string? CompareUrl,
    string? Date,
    string? Summary,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<ReleaseSection> Sections,
    string RawMarkdown);

/// <summary>
/// Sürüm notları alt bölümü (Örn: Eklenenler, Düzeltmeler).
/// </summary>
public sealed record ReleaseSection(
    string Title,
    IReadOnlyList<string> Items);

/// <summary>
/// CHANGELOG.md dosyasından sürüme özel notları, öne çıkanları ve kategorileri ayrıştıran saf mantık.
/// </summary>
public static class ChangelogParser
{
    private static readonly Regex VersionHeaderRegex = new(
        @"^##\s+\[?v?([0-9]+\.[0-9]+(?:\.[0-9]+)?)\]?(?:\(([^\)]+)\))?\s*(?:\(([^)]+)\))?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex SectionHeaderRegex = new(
        @"^###\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Derlemeye gömülü CHANGELOG.md dosyasını okur.
    /// </summary>
    public static string? ReadEmbeddedChangelog(Assembly? assembly = null)
    {
        var asm = assembly ?? typeof(ChangelogParser).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("CHANGELOG.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return null;

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Belirtilen sürüme ait notları ayıklar.
    /// </summary>
    public static ReleaseNotes? ExtractVersion(string? markdown, string targetVersion)
    {
        if (string.IsNullOrWhiteSpace(markdown) || string.IsNullOrWhiteSpace(targetVersion))
            return null;

        var cleanTarget = NormalizeVersion(targetVersion);
        var entries = SplitVersionEntries(markdown);

        foreach (var entry in entries)
        {
            if (NormalizeVersion(entry.Version).Equals(cleanTarget, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Belgedeki en son (en üstteki) sürüm notunu ayrıştırır.
    /// </summary>
    public static ReleaseNotes? ParseLatest(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return null;

        var entries = SplitVersionEntries(markdown);
        return entries.FirstOrDefault();
    }

    /// <summary>
    /// Belgedeki tüm sürüm numaralarını listeler.
    /// </summary>
    public static IReadOnlyList<string> GetAllVersions(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var list = new List<string>();
        var matches = VersionHeaderRegex.Matches(markdown);
        foreach (Match m in matches)
        {
            if (m.Groups[1].Success)
                list.Add(m.Groups[1].Value);
        }
        return list;
    }

    private static List<ReleaseNotes> SplitVersionEntries(string markdown)
    {
        var result = new List<ReleaseNotes>();
        var matches = VersionHeaderRegex.Matches(markdown);

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var version = match.Groups[1].Value;
            var compareUrl = match.Groups[2].Success && match.Groups[2].Value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? match.Groups[2].Value
                : null;

            string? date = null;
            if (match.Groups[3].Success)
                date = match.Groups[3].Value;
            else if (match.Groups[2].Success && !match.Groups[2].Value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                date = match.Groups[2].Value;

            var startIndex = match.Index;
            var endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : markdown.Length;
            var sectionRaw = markdown[startIndex..endIndex].Trim();

            // Başlık satırını atlayıp gövdeyi al
            var bodyIndex = sectionRaw.IndexOf('\n');
            var body = bodyIndex >= 0 ? sectionRaw[(bodyIndex + 1)..].Trim() : string.Empty;

            var parsed = ParseEntryBody(version, compareUrl, date, body, sectionRaw);
            result.Add(parsed);
        }

        return result;
    }

    private static ReleaseNotes ParseEntryBody(string version, string? compareUrl, string? date, string body, string raw)
    {
        var highlights = new List<string>();
        var sections = new List<ReleaseSection>();
        var lines = body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        string? currentSectionTitle = null;
        var currentSectionItems = new List<string>();
        var inHighlights = false;
        var summaryBuilder = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                if (currentSectionTitle is not null)
                {
                    sections.Add(new ReleaseSection(currentSectionTitle, [.. currentSectionItems]));
                    currentSectionItems.Clear();
                }
                currentSectionTitle = line[4..].Trim();
                inHighlights = false;
                continue;
            }

            if (line.StartsWith("Öne çıkanlar:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Highlights:", StringComparison.OrdinalIgnoreCase))
            {
                inHighlights = true;
                continue;
            }

            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                var bullet = line[2..].Trim();
                if (inHighlights)
                {
                    highlights.Add(bullet);
                }
                else if (currentSectionTitle is not null)
                {
                    currentSectionItems.Add(bullet);
                }
                else
                {
                    // Başlık yokken gelen maddeler de öne çıkan sayılır
                    highlights.Add(bullet);
                }
            }
            else if (!inHighlights && currentSectionTitle is null && !line.StartsWith('>'))
            {
                if (summaryBuilder.Length > 0)
                    summaryBuilder.Append(' ');
                summaryBuilder.Append(line);
            }
        }

        if (currentSectionTitle is not null && currentSectionItems.Count > 0)
        {
            sections.Add(new ReleaseSection(currentSectionTitle, [.. currentSectionItems]));
        }

        var summary = summaryBuilder.Length > 0 ? summaryBuilder.ToString() : null;

        return new ReleaseNotes(version, compareUrl, date, summary, highlights, sections, raw);
    }

    private static string NormalizeVersion(string v)
    {
        var s = v.Trim().TrimStart('v', 'V');
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        var dash = s.IndexOf('-');
        if (dash >= 0) s = s[..dash];
        return s;
    }
}
