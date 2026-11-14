using System.Text;

namespace Koy.Core.Dock;

/// <summary>
/// Windows Taskband <c>Favorites</c> ikili değerinden görev çubuğuna sabitlenmiş Mağaza (UWP / MSIX) uygulamalarının
/// AppUserModelID (AUMID) kimliklerini çıkarır. Windows 11'de bu uygulamalar <c>TaskBar</c> klasöründe <c>.lnk</c>
/// dosyası oluşturmaz; yalnız ikili <c>Favorites</c> kaydında yaşarlar.
/// </summary>
public static class TaskbandPackagedPins
{
    public const string AppsFolderPrefix = @"shell:AppsFolder\";

    /// <summary>AUMID için kabuk yolu üretir: <c>shell:AppsFolder\&lt;AUMID&gt;</c>.</summary>
    public static string AppsFolderPath(string aumid) => AppsFolderPrefix + aumid;

    /// <summary>Yolun <c>shell:AppsFolder\</c> önekiyle başlayıp başlamadığını döndürür.</summary>
    public static bool IsAppsFolderPath(string? path) =>
        !string.IsNullOrEmpty(path) && path.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Taskband <c>Favorites</c> ikili verisindeki paketli uygulama AUMID'lerini (<c>PaketAdı_Yayıncı!Uygulama</c>)
    /// ilk görülme sırasıyla ve tekrarsız döndürür. Boş, bozuk ya da eşleşmesiz veride boş liste döner.
    /// </summary>
    public static IReadOnlyList<string> ExtractAumids(byte[]? favorites)
    {
        if (favorites is not { Length: >= 8 })
            return [];

        try
        {
            var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var alignment = 0; alignment < 2; alignment++)
            {
                var length = (favorites.Length - alignment) & ~1;
                if (length < 8)
                    continue;

                var text = Encoding.Unicode.GetString(favorites, alignment, length);

                var runStart = -1;
                for (var i = 0; i <= text.Length; i++)
                {
                    var inRun = i < text.Length && !char.IsControl(text[i]) && !char.IsSurrogate(text[i]);
                    if (inRun)
                    {
                        if (runStart < 0)
                            runStart = i;
                        continue;
                    }

                    if (runStart >= 0)
                    {
                        var run = text.AsSpan(runStart, i - runStart);
                        if (IsPackagedAumid(run))
                        {
                            var aumid = run.ToString();
                            var bytePosition = runStart * 2 + alignment;
                            if (!positions.TryGetValue(aumid, out var existing) || bytePosition < existing)
                                positions[aumid] = bytePosition;
                        }

                        runStart = -1;
                    }
                }
            }

            if (positions.Count == 0)
                return [];

            return positions
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Metnin paketli Windows uygulaması AUMID biçiminde (<c>PaketAdı_13KarakterBase32!UygulamaId</c>) olup olmadığını denetler.
    /// </summary>
    public static bool IsPackagedAumid(ReadOnlySpan<char> value)
    {
        var bang = value.IndexOf('!');
        if (bang <= 0 || bang >= value.Length - 1)
            return false;

        var family = value[..bang];
        var appId = value[(bang + 1)..];

        if (appId.Length > 64)
            return false;

        foreach (var c in appId)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-'))
                return false;
        }

        var underscore = family.LastIndexOf('_');
        if (underscore <= 0)
            return false;

        var packageName = family[..underscore];
        var publisherId = family[(underscore + 1)..];

        // Windows paket aile adında yayıncı kimliği her zaman 13 karakterlik Crockford Base32 karmasıdır.
        if (publisherId.Length != 13)
            return false;

        foreach (var c in publisherId)
        {
            if (!IsCrockfordBase32(c))
                return false;
        }

        if (packageName.Length is < 1 or > 50)
            return false;

        if (packageName[0] is '.' or '-' || packageName[^1] is '.' or '-')
            return false;

        var hasAlnum = false;
        foreach (var c in packageName)
        {
            if (char.IsAsciiLetterOrDigit(c))
                hasAlnum = true;
            else if (c is not ('.' or '-'))
                return false;
        }

        return hasAlnum;
    }

    private static bool IsCrockfordBase32(char c)
    {
        if (c is >= '0' and <= '9')
            return true;

        var lower = (char)(c | 0x20);
        return lower is (>= 'a' and <= 'z') and not ('i' or 'l' or 'o' or 'u');
    }
}
