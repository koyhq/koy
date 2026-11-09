namespace Koy.Core.Search;

public enum BrowserFamily
{
    Unknown,
    Chromium,
    Firefox
}

/// <param name="Executable">Tarayıcının exe yolu (varsayılan "açma" komutundan).</param>
/// <param name="Name">Arama satırında görünen ad ("Chrome").</param>
public sealed record DefaultBrowser(string Executable, string Name, BrowserFamily Family);

public sealed record BrowserSearchCommand(string Executable, IReadOnlyList<string> Arguments);

/// <summary>
/// Hiç sonuç yokken web araması kullanıcının varsayılan tarayıcısında, o tarayıcının kendi arama motoruyla yapılır.
/// Arama motoru adresi tarayıcının profil dosyalarında saklı ve biçimi değişken; onun yerine tarayıcıya "ara" denir:
/// Chromium ailesi "? terim" adresini varsayılan motorla arar, Firefox ailesi <c>-search</c> seçeneğini tanır.
/// Tanınmayan tarayıcıda null döner, çağıran sabit bir motora düşer.
/// </summary>
public static class DefaultBrowserSearch
{
    private static readonly Dictionary<string, (string Name, BrowserFamily Family)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = ("Chrome", BrowserFamily.Chromium),
        ["msedge"] = ("Edge", BrowserFamily.Chromium),
        ["brave"] = ("Brave", BrowserFamily.Chromium),
        ["vivaldi"] = ("Vivaldi", BrowserFamily.Chromium),
        ["chromium"] = ("Chromium", BrowserFamily.Chromium),
        ["thorium"] = ("Thorium", BrowserFamily.Chromium),
        ["firefox"] = ("Firefox", BrowserFamily.Firefox),
        ["librewolf"] = ("LibreWolf", BrowserFamily.Firefox),
        ["waterfox"] = ("Waterfox", BrowserFamily.Firefox),
        ["floorp"] = ("Floorp", BrowserFamily.Firefox),
        ["zen"] = ("Zen", BrowserFamily.Firefox)
    };

    /// <summary>
    /// Kayıt defterindeki "açma" komutundan (ör. <c>"C:\…\chrome.exe" --single-argument %1</c>) tarayıcıyı çıkarır.
    /// </summary>
    public static DefaultBrowser? FromOpenCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var text = command.Trim();
        string exe;
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end <= 1)
                return null;
            exe = text[1..end];
        }
        else
        {
            var space = text.IndexOf(' ');
            exe = space < 0 ? text : text[..space];
        }

        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return null;

        return Known.TryGetValue(Path.GetFileNameWithoutExtension(exe), out var info)
            ? new DefaultBrowser(exe, info.Name, info.Family)
            : null;
    }

    public static BrowserSearchCommand? SearchCommand(DefaultBrowser browser, string term)
    {
        ArgumentNullException.ThrowIfNull(browser);
        term = term.Trim();
        if (term.Length == 0)
            return null;

        return browser.Family switch
        {
            // --single-argument: ardından gelen her şey tek adres; terim "--" ile başlasa da seçenek sayılmaz.
            BrowserFamily.Chromium => new BrowserSearchCommand(browser.Executable, ["--single-argument", "? " + term]),
            // Firefox'ta böyle bir koruma yok: tire ile başlayan terim seçenek sanılabilir, sabit motora bırakılır.
            BrowserFamily.Firefox when !term.StartsWith('-') => new BrowserSearchCommand(browser.Executable, ["-search", term]),
            _ => null
        };
    }
}
