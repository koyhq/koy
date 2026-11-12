namespace Koy.Core.Search;

/// <summary>
/// Yazılan metnin komut ya da program olarak çalıştırılmadan önce onay isteyip istemediği.
/// Pano metni ve satır sonu / denetim karakteri içeren metin tek Enter ile çalışmaz.
/// </summary>
public static class SearchCommandSafety
{
    public const string NewlineMark = "⏎";

    /// <summary>Sekme dışında denetim karakteri (satır sonu dahil) var mı.</summary>
    public static bool HasControlCharacters(string text)
    {
        foreach (var c in text)
        {
            if (char.IsControl(c) && c != '\t')
                return true;
        }

        return false;
    }

    /// <summary>Onayda gösterilecek metin: satır sonları ⏎, diğer denetim karakterleri boşluk olur.</summary>
    public static string ForDisplay(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                result.Append(NewlineMark.AsSpan());
            }
            else
            {
                result.Append(char.IsControl(c) && c != '\t' ? ' ' : c);
            }
        }

        return result.ToString();
    }

    /// <summary>Kabuğa verilecek metin: satır sonu ve denetim karakterleri boşluğa çevrilir, kabuğa hiç satır sonu gitmez.</summary>
    public static string ToSingleLine(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
            result.Append(char.IsControl(c) && c != '\t' ? ' ' : c);
        return result.ToString().Trim();
    }
}
