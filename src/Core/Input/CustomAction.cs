namespace Koy.Core.Input;

/// <summary>Özel eylemin türü. Koy yalnız bu üç türü bilir; Core hiçbirini çalıştırmaz.</summary>
public enum CustomActionKind
{
    RunCommand,
    OpenUrl,
    OpenPath
}

/// <summary>
/// Kullanıcının tanımladığı eylem: yalnız veri. Çalıştırma (Process.Start) App/Platform'da, Koy'un kendi sürecinde yapılır
/// (AGENTS #4); Core komut çalıştırmaz.
/// </summary>
public sealed record CustomAction(
    string Id,
    CustomActionKind Kind,
    string Target,
    string? Arguments = null,
    string? WorkingDirectory = null);

/// <summary>Ayardan okunan ham özel eylem.</summary>
public sealed record CustomActionData(
    string? Id,
    string? Kind,
    string? Target,
    string? Arguments = null,
    string? WorkingDirectory = null);

/// <summary>
/// Özel eylemleri doğrular. Kimlik zorunlu ve tekil (büyük-küçük harf duyarsız; ilk gelen kalır), tür bilinmeli,
/// hedef boş olmamalı. URL yalnız mutlak http/https olabilir. Geçersiz kayıt sessizce düşer ve sayılır.
/// En çok <see cref="MaxActions"/> eylem tutulur; fazlası düşer (elle bozulmuş dosya listeyi şişirmesin).
/// </summary>
public static class CustomActionParser
{
    public const int MaxActions = 32;

    public sealed record Result(IReadOnlyList<CustomAction> Accepted, int Dropped);

    public static Result Parse(IEnumerable<CustomActionData?> items)
    {
        var accepted = new List<CustomAction>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dropped = 0;

        foreach (var item in items)
        {
            // JSON'daki null öğe okunurken null olarak gelir; bütün listeyi çökertmesin.
            var id = item?.Id?.Trim();
            var target = item?.Target?.Trim();

            // Kimlik yalnız kayıt geçerliyse rezerve edilir; geçersiz kayıt sonraki geçerli kaydı düşürmesin.
            if (item is null || accepted.Count >= MaxActions ||
                string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target) ||
                !TryKind(item.Kind, out var kind) ||
                (kind == CustomActionKind.OpenUrl && !IsWebUrl(target)) ||
                !ids.Add(id))
            {
                dropped++;
                continue;
            }

            accepted.Add(new CustomAction(
                id,
                kind,
                target,
                string.IsNullOrWhiteSpace(item.Arguments) ? null : item.Arguments.Trim(),
                string.IsNullOrWhiteSpace(item.WorkingDirectory) ? null : item.WorkingDirectory.Trim()));
        }

        return new Result(accepted, dropped);
    }

    private static bool TryKind(string? text, out CustomActionKind kind)
    {
        kind = default;
        foreach (var name in Enum.GetNames<CustomActionKind>())
        {
            if (string.Equals(name, text?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                kind = Enum.Parse<CustomActionKind>(name);
                return true;
            }
        }

        return false;
    }

    private static bool IsWebUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
