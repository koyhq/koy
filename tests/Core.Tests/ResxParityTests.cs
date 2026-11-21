using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Koy.Core.Tests;

/// <summary>
/// App'teki iki dilli kaynak dosyalarının (Strings.resx İngilizce, Strings.tr.resx Türkçe) eşitliğini denetler.
/// Core testleri App'e bağlı olmadığından dosyalar yol üzerinden XML olarak okunur.
/// </summary>
public class ResxParityTests
{
    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^}]*)?\}", RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Koy.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Depo kökü (Koy.slnx) bulunamadı.");
    }

    private static Dictionary<string, string> Load(string fileName)
    {
        var path = Path.Combine(RepoRoot(), "src", "App", "Resources", fileName);
        return XDocument.Load(path).Root!
            .Elements("data")
            .ToDictionary(
                d => (string)d.Attribute("name")!,
                d => (string?)d.Element("value") ?? "",
                StringComparer.Ordinal);
    }

    private static readonly Lazy<Dictionary<string, string>> En = new(() => Load("Strings.resx"));
    private static readonly Lazy<Dictionary<string, string>> Tr = new(() => Load("Strings.tr.resx"));

    private static string[] Placeholders(string value) =>
        Placeholder.Matches(value).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

    [Fact]
    public void BothLanguages_HaveTheSameKeys()
    {
        var onlyEn = En.Value.Keys.Except(Tr.Value.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var onlyTr = Tr.Value.Keys.Except(En.Value.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(onlyEn.Count == 0, "Strings.tr.resx'te eksik anahtarlar: " + string.Join(", ", onlyEn));
        Assert.True(onlyTr.Count == 0, "Strings.resx'te eksik anahtarlar: " + string.Join(", ", onlyTr));
    }

    [Fact]
    public void NoValue_IsEmpty()
    {
        var empty = En.Value.Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => "en:" + p.Key)
            .Concat(Tr.Value.Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => "tr:" + p.Key))
            .ToList();

        Assert.True(empty.Count == 0, "Boş değerler: " + string.Join(", ", empty));
    }

    [Fact]
    public void FormatPlaceholders_MatchBetweenLanguages()
    {
        var mismatched = En.Value
            .Where(p => Tr.Value.TryGetValue(p.Key, out var tr) && !Placeholders(p.Value).SequenceEqual(Placeholders(tr)))
            .Select(p => $"{p.Key} (en: {string.Join(",", Placeholders(p.Value))}; tr: {string.Join(",", Placeholders(Tr.Value[p.Key]))})")
            .ToList();

        Assert.True(mismatched.Count == 0, "Yer tutucuları uyuşmayan anahtarlar: " + string.Join("; ", mismatched));
    }
}
