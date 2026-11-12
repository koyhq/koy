using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Koy.Core.Settings;

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

    /// <summary>Yayın akışının paket notunda İngilizce ve Türkçe bölümleri ayıran satır.</summary>
    public const string TurkishNotesMarker = "<!-- koy-notes:tr -->";

    /// <summary>
    /// Derlemeye gömülü değişiklik günlüğünü okur: Türkçe için CHANGELOG.tr.md, diğer dillerde CHANGELOG.md.
    /// </summary>
    public static string? ReadEmbeddedChangelog(string language = AppLanguage.English, Assembly? assembly = null)
    {
        var asm = assembly ?? typeof(ChangelogParser).Assembly;
        var fileName = language == AppLanguage.Turkish ? "CHANGELOG.tr.md" : "CHANGELOG.md";
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

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
    public static ReleaseNotes? ExtractVersion(string? markdown, string targetVersion, string language = AppLanguage.English)
    {
        if (string.IsNullOrWhiteSpace(markdown) || string.IsNullOrWhiteSpace(targetVersion))
            return null;

        var cleanTarget = NormalizeVersion(targetVersion);
        var entries = SplitVersionEntries(markdown, language);

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
    public static ReleaseNotes? ParseLatest(string? markdown, string language = AppLanguage.English)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return null;

        var entries = SplitVersionEntries(markdown, language);
        return entries.FirstOrDefault();
    }

    /// <summary>
    /// Sürümün notunu seçili dilde arar, o dilde yoksa öbür dile düşer; ekran hiç boş kalmaz.
    /// </summary>
    public static ReleaseNotes? ResolveVersion(string version, string language, Func<string, string?> readChangelog)
    {
        var preferred = language == AppLanguage.Turkish ? AppLanguage.Turkish : AppLanguage.English;
        var other = preferred == AppLanguage.Turkish ? AppLanguage.English : AppLanguage.Turkish;
        return ExtractVersion(readChangelog(preferred), version, preferred)
               ?? ExtractVersion(readChangelog(other), version, other);
    }

    /// <summary>Gömülü günlüklerden <see cref="ResolveVersion"/>.</summary>
    public static ReleaseNotes? ResolveEmbedded(string version, string language) =>
        ResolveVersion(version, language, l => ReadEmbeddedChangelog(l));

    /// <summary>
    /// Güncelleme beslemesindeki paket notundan seçili dilin bölümünü ayırır. İşaret yoksa not İngilizcedir;
    /// Türkçe bölüm boşsa İngilizce kullanılır. Dönen dil, metnin gerçek dilidir.
    /// </summary>
    public static (string? Markdown, string Language) SelectFeedNotes(string? notes, string language)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return (notes, AppLanguage.English);

        var index = notes.IndexOf(TurkishNotesMarker, StringComparison.Ordinal);
        if (index < 0)
            return (notes, AppLanguage.English);

        var english = notes[..index].Trim();
        var turkish = notes[(index + TurkishNotesMarker.Length)..].Trim();
        return language == AppLanguage.Turkish && turkish.Length > 0
            ? (turkish, AppLanguage.Turkish)
            : (english, AppLanguage.English);
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

    private static List<ReleaseNotes> SplitVersionEntries(string markdown, string language)
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

            var parsed = ParseEntryBody(version, compareUrl, date, body, sectionRaw, language);
            result.Add(parsed);
        }

        return result;
    }

    private static ReleaseNotes ParseEntryBody(string version, string? compareUrl, string? date, string body, string raw, string language)
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

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                var bullet = CleanItem(line[2..].Trim(), language);
                if (bullet is null)
                    continue;
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

    // release-please'in madde sonuna eklediği bağlantılar: " ([#32](url))", " ([9aa22ea](url))", " ([#24](url), [#27](url))".
    private static readonly Regex RefGroupRegex = new(
        @"\s*\(\[[^\]]+\]\([^)]*\)(?:\s*,\s*\[[^\]]+\]\([^)]*\))*\)",
        RegexOptions.Compiled);

    private static readonly Regex LinkRegex = new(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);

    // Conventional commit kapsamı: "**dock:** ..." (küçük harfli tek sözcük). Öne çıkanlardaki "**Atlama listesi:**" başlığı kapsam değildir.
    private static readonly Regex ScopeRegex = new(@"^\*\*([a-z0-9-]+):\*\*\s*", RegexOptions.Compiled);

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Kapsamı uygulamaya ait olmayan maddeler "Neler Geldi"de gösterilmez (tanıtım sitesi, CI).</summary>
    private static readonly HashSet<string> HiddenScopes = new(StringComparer.OrdinalIgnoreCase) { "site", "ci", "deps" };

    /// <summary>
    /// Sürüm notu maddesini düz metne çevirir: bağlantılar, commit ve issue referansları, kalın/kod işaretleri atılır;
    /// conventional commit kapsamı ("**dock:**") düşer ve ilk harf büyür. Uygulamayla ilgisiz kapsamda null döner.
    /// </summary>
    public static string? CleanItem(string item, string language = AppLanguage.English)
    {
        if (string.IsNullOrWhiteSpace(item))
            return null;

        var s = RefGroupRegex.Replace(item, string.Empty);
        s = LinkRegex.Replace(s, "$1");

        var scope = ScopeRegex.Match(s);
        var droppedScope = false;
        if (scope.Success)
        {
            if (HiddenScopes.Contains(scope.Groups[1].Value))
                return null;
            s = s[scope.Length..];
            droppedScope = true;
        }

        s = s.Replace("**", string.Empty).Replace("`", string.Empty).Trim();
        if (s.Length == 0)
            return null;

        if (droppedScope || char.IsLower(s[0]))
            s = char.ToUpper(s[0], language == AppLanguage.Turkish ? Turkish : CultureInfo.InvariantCulture) + s[1..];

        return s;
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
