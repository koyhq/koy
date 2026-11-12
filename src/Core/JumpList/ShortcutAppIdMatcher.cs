using System.IO;

namespace Koy.Core.JumpList;

/// <summary>
/// Süreç düzeyinde AppUserModelID koyan paketsiz uygulamaların (Discord gibi) AUMID'i Başlat menüsü kısayolunda durur.
/// Bu sınıf bir kısayolun belirli bir exe'ye ait olup olmadığına karar verir; kısayol okuma Platform'da yapılır.
/// <list type="bullet">
/// <item>Tam eşleşme: kısayol hedefi exe yoluyla (büyük/küçük harf duyarsız) aynı.</item>
/// <item>Squirrel düzeni: exe <c>&lt;kök&gt;\app-x.y.z\X.exe</c> (yolu sürümle değişir), kısayol ise
/// kökteki bir başlatıcıyı (<c>Update.exe</c> ya da saplama <c>X.exe</c>) <c>--processStart X.exe</c> ile çalıştırır.</item>
/// </list>
/// </summary>
public static class ShortcutAppIdMatcher
{
    public const string SquirrelFolderPrefix = "app-";

    /// <summary>Kısayol hedefi exe'ye tam yolla işaret ediyorsa true.</summary>
    public static bool MatchesExact(string exePath, string? linkTarget)
    {
        var exe = Normalize(exePath);
        var target = Normalize(linkTarget);
        return exe is not null && target is not null && string.Equals(exe, target, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kısayol hedefi <c>&lt;kök&gt;</c> klasöründeki bir exe ise (<c>Update.exe</c> ya da kökteki saplama <c>X.exe</c>) ve bağımsız değişkenlerinde <c>--processStart</c> ile exe dosya adı
    /// geçiyorsa, exe de <c>&lt;kök&gt;\app-*\&lt;ad&gt;.exe</c> ise true.
    /// </summary>
    public static bool MatchesSquirrel(string exePath, string? linkTarget, string? linkArguments)
    {
        var exe = Normalize(exePath);
        var target = Normalize(linkTarget);
        if (exe is null || target is null || string.IsNullOrWhiteSpace(linkArguments))
            return false;

        var appFolder = Path.GetDirectoryName(exe);
        if (appFolder is null ||
            !Path.GetFileName(appFolder).StartsWith(SquirrelFolderPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var root = Path.GetDirectoryName(appFolder);
        var updaterRoot = Path.GetDirectoryName(target);
        if (root is null || updaterRoot is null ||
            !string.Equals(root, updaterRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        return HasProcessStart(linkArguments, Path.GetFileName(exe));
    }

    /// <summary>Bağımsız değişkenlerde <c>--processStart</c> (ya da <c>--processStartAndWait</c>) var ve exe adı bir sözcük olarak geçiyor.</summary>
    public static bool HasProcessStart(string arguments, string exeName)
    {
        var hasFlag = false;
        var hasName = false;
        foreach (var token in arguments.Split([' ', '\t', '"', '='], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("--processStart", StringComparison.OrdinalIgnoreCase))
                hasFlag = true;
            else if (string.Equals(token, exeName, StringComparison.OrdinalIgnoreCase))
                hasName = true;
        }

        return hasFlag && hasName;
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch
        {
            return null;
        }
    }
}
