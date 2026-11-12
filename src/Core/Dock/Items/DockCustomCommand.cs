namespace Koy.Core.Dock.Items;

/// <summary>Özel düğmenin çalıştıracağı hedef ve (yalnız dosya hedeflerinde) bağımsız değişken metni.</summary>
public sealed record DockCustomLaunch(string Target, string? Arguments);

/// <summary>
/// Özel düğme komutunun güvenli ayrıştırması. Komut ayar dosyasından gelir ve güvenilmez sayılır:
/// kabuk komut satırı kurulmaz, hiçbir şey birleştirilmez; çıkan hedef ve bağımsız değişken ayrı ayrı çalıştırıcıya gider.
/// Kabul edilenler: tam yollu dosya/klasör (sürücü harfi ya da UNC) ve http, https, mailto adresleri.
/// Göreli yol, shell:, ::{CLSID}, ms-* ve benzeri özel şemalar, denetim karakterleri reddedilir.
/// </summary>
public static class DockCustomCommand
{
    private static readonly System.Buffers.SearchValues<char> InvalidPathChars = System.Buffers.SearchValues.Create("\"<>|*?");

    public static bool TryParse(string? command, out DockCustomLaunch launch)
    {
        launch = new DockCustomLaunch("", null);
        if (string.IsNullOrWhiteSpace(command))
            return false;

        var text = command.Trim();
        if (text.Length > DockItemRules.MaxCommandLength || text.Any(char.IsControl))
            return false;

        string target;
        string? arguments = null;

        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            if (close <= 1)
                return false;

            target = text[1..close];
            var rest = text[(close + 1)..].Trim();
            arguments = rest.Length > 0 ? rest : null;
        }
        else
        {
            target = text;
        }

        if (IsUrl(target))
        {
            // Adres yalnız adres olarak açılır; bağımsız değişken eklenemez.
            if (arguments is not null)
                return false;
            launch = new DockCustomLaunch(target, null);
            return true;
        }

        if (!IsFullPath(target) || target.AsSpan().ContainsAny(InvalidPathChars))
            return false;

        launch = new DockCustomLaunch(target, arguments);
        return true;
    }

    private static bool IsUrl(string target)
    {
        var web = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                  target.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var mail = target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
        if (!web && !mail)
            return false;

        return Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
               (uri.Scheme is "http" or "https" or "mailto") &&
               !target.Any(char.IsWhiteSpace);
    }

    private static bool IsFullPath(string target)
    {
        if (target.Length >= 3 && char.IsAsciiLetter(target[0]) && target[1] == ':' && (target[2] == '\\' || target[2] == '/'))
            return true;

        // UNC: \\sunucu\paylasim. \\?\ ve \\.\ aygit yollari kabul edilmez.
        return target.StartsWith(@"\\", StringComparison.Ordinal) &&
               target.Length > 2 && target[2] != '?' && target[2] != '.' && target[2] != '\\';
    }
}
