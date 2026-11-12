using System.Text.RegularExpressions;

namespace Koy.Core.Clipboard;

/// <summary>
/// Pano metnini türe ayırır: Bağlantı, Renk, Kod, yoksa Metin. Saf fonksiyon, WinAPI yok.
///
/// Kod sezgisi (kaçırmak, düz yazıya "Kod" etiketi basmaktan iyidir):
/// - Tek satır: yalnız güçlü işaret varsa kod. Satır ; { } ile bitiyor VE ( ) { } = karakterlerinden birini
///   içeriyor, ya da "=>" / "->" ile birlikte parantez var, ya da HTML etiketi içeriyor.
/// - Çok satır: işaretlerden en az 2 ayrı çeşit gerekir (satır sonu ; { }, süslü parantez çifti, parantezli satır sonu,
///   satır başı anahtar sözcük, girinti, ok/eşitlik işleçleri, HTML etiketi). Tek işaret düz yazıda da çıkabilir.
/// </summary>
public static partial class ClipboardClassifier
{
    public static ClipboardKind Classify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ClipboardKind.Text;

        var trimmed = text.Trim();

        if (IsLink(trimmed))
            return ClipboardKind.Link;
        if (IsColor(trimmed))
            return ClipboardKind.Color;
        if (IsCode(trimmed))
            return ClipboardKind.Code;
        return ClipboardKind.Text;
    }

    public static bool IsLink(string trimmed)
    {
        if (trimmed.Length == 0 || trimmed.AsSpan().IndexOfAny(" \t\r\n") >= 0)
            return false;
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host);
    }

    public static bool IsColor(string trimmed) => HexColor().IsMatch(trimmed) || RgbColor().IsMatch(trimmed);

    public static bool IsCode(string trimmed)
    {
        var lines = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
            return false;

        if (lines.Length == 1)
        {
            var line = lines[0];
            var endsLikeCode = line[^1] is ';' or '{' or '}';
            if (endsLikeCode && line.AsSpan().IndexOfAny("(){}=") >= 0)
                return true;
            if ((line.Contains("=>") || line.Contains("->")) && line.Contains('(') && line.Contains(')'))
                return true;
            return HtmlTag().IsMatch(line);
        }

        var raw = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var score = 0;
        if (lines.Count(l => l[^1] is ';' or '{' or '}') >= 2) score++;
        if (trimmed.Contains('{') && trimmed.Contains('}')) score++;
        if (lines.Any(l => l.EndsWith(')') || l.EndsWith(");", StringComparison.Ordinal) || l.EndsWith('{')) && trimmed.Contains('(')) score++;
        if (lines.Any(l => KeywordLine().IsMatch(l))) score++;
        if (raw.Count(l => l.StartsWith("    ", StringComparison.Ordinal) || l.StartsWith('\t')) >= 2) score++;
        if (trimmed.Contains("=>") || trimmed.Contains("->") || trimmed.Contains("==") || trimmed.Contains("!=")) score++;
        // Birden çok etiket (açılış+kapanış) tek başına yeterli kanıt.
        if (HtmlTag().Count(trimmed) >= 2) score += 2;
        else if (HtmlTag().IsMatch(trimmed)) score++;
        return score >= 2;
    }

    [GeneratedRegex(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex HexColor();

    [GeneratedRegex(@"^rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(,\s*(\d{1,3}%?|\d*\.\d+)\s*)?\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbColor();

    [GeneratedRegex(@"^((public|private|protected|internal|static|class|interface|struct|enum|namespace|using|import|from|def|function|const|let|var|return|if|else|for|foreach|while|switch|case|try|catch|SELECT|INSERT|UPDATE|DELETE|CREATE)\b.*[(){}:;=]|#include\b|#!/|@\w+)")]
    private static partial Regex KeywordLine();

    [GeneratedRegex(@"</?[a-zA-Z][a-zA-Z0-9]*(\s[^<>]*)?/?>")]
    private static partial Regex HtmlTag();
}
