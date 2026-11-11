namespace Koy.Core.Appearance;

/// <summary>
/// Uygulama başına simge değişimi: anahtar kısayol adı (büyük/küçük harf duyarsız), değer simge dosyasının yolu ya da adı.
/// Değer yalnız okunur ve doğrulanır; hiçbir zaman çalıştırılmaz. Geçersiz değer haritada kalsa bile çözümlemede yok sayılır.
/// </summary>
public static class AppIconOverrides
{
    public const int MaxLength = 260;

    private static readonly string[] AllowedExtensions = [".png", ".ico", ".svg"];

    // Windows'ta yol ve dosya adında yasak karakterler. Ayırıcılar ayrıca denetlenir.
    private static readonly char[] ForbiddenChars = ['<', '>', '"', '|', '?', '*'];

    /// <summary>Anahtar: boş değil, uzunluk sınırında, denetim ve yol karakteri içermez.</summary>
    public static bool IsValidKey(string? appKey)
    {
        if (string.IsNullOrWhiteSpace(appKey)) return false;
        var key = appKey.Trim();
        if (key.Length > MaxLength) return false;
        return !key.Any(c => char.IsControl(c) || c is '\\' or '/' or ':' || ForbiddenChars.Contains(c));
    }

    /// <summary>
    /// Değer: mutlak yol ya da yalnız dosya adı; uzantı png, ico ya da svg olmalı. Yollarda ".." bileşeni yasak.
    /// Kırpılmış ve normalleştirilmiş değeri verir.
    /// </summary>
    public static bool TryValidateSource(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var candidate = value.Trim();
        if (candidate.Length > MaxLength) return false;
        if (candidate.Any(c => char.IsControl(c) || ForbiddenChars.Contains(c))) return false;

        var extension = Path.GetExtension(candidate);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;

        if (Path.IsPathRooted(candidate))
        {
            // Mutlak yol: sürücülü olmalı ve ".." bileşeni içermemeli.
            if (!Path.IsPathFullyQualified(candidate)) return false;
            if (candidate.Split('\\', '/').Any(part => part == "..")) return false;
        }
        else if (candidate.Contains('\\') || candidate.Contains('/') || candidate.Contains(':'))
        {
            // Göreli yol kabul edilmez: yalnız dosya adı (klasör çözümü Core dışında).
            return false;
        }

        normalized = candidate;
        return true;
    }

    /// <summary>Geçerli bir simge kaydeder. Geçersiz anahtar ya da değerde harita değişmez ve false döner.</summary>
    public static bool TrySet(IDictionary<string, string> overrides, string appKey, string value)
    {
        if (!IsValidKey(appKey) || !TryValidateSource(value, out var normalized)) return false;
        overrides[appKey.Trim()] = normalized;
        return true;
    }

    /// <summary>Uygulamanın simge değişimini siler ("Simgeyi sıfırla").</summary>
    public static void Reset(IDictionary<string, string> overrides, string appKey)
    {
        if (IsValidKey(appKey)) overrides.Remove(appKey.Trim());
    }

    /// <summary>Anahtara ait geçerli simge; yoksa ya da değer geçersizse null.</summary>
    public static string? Lookup(IReadOnlyDictionary<string, string>? overrides, string? appKey)
    {
        if (overrides is null || !IsValidKey(appKey)) return null;
        if (!overrides.TryGetValue(appKey!.Trim(), out var value)) return null;
        return TryValidateSource(value, out var normalized) ? normalized : null;
    }
}
