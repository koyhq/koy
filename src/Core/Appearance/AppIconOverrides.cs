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
    /// Değer: mutlak yol ya da yalnız dosya adı; uzantı png, ico ya da svg olmalı. Yollarda nokta bileşeni, boş ya da nokta/boşlukla biten bileşen ve aygıt adı (CON, NUL, COM1…) yasak.
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
            // Mutlak yol yalnız <harf>:\ biçiminde. UNC (ağ, SMB/NTLM tetikler), ADS (iki nokta: gizli akış)
            // ve nokta bileşenleri (".", "..") kaçış yolu olduğu için reddedilir.
            if (IsUncPath(candidate)) return false;
            if (!IsDriveRootedPath(candidate)) return false;
            if (candidate.IndexOf(':', 2) >= 0) return false;
            if (!candidate[3..].Split('\\', '/').All(IsSafeComponent)) return false;
        }
        else if (candidate.Contains('\\') || candidate.Contains('/') || candidate.Contains(':'))
        {
            // Göreli yol kabul edilmez: yalnız dosya adı (klasör çözümü Core dışında).
            return false;
        }
        else if (!IsSafeComponent(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    /// <summary>
    /// Simge paketi girdisi: yalnız dosya adı (klasör, sürücü ya da UNC yok). Uzantı ve yasak karakter kuralları
    /// <see cref="TryValidateSource"/> ile aynıdır.
    /// </summary>
    public static bool TryValidatePackFile(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (!TryValidateSource(value, out var candidate)) return false;
        if (candidate.IndexOfAny(['\\', '/', ':']) >= 0) return false;

        normalized = candidate;
        return true;
    }

    // Win32'nin ayrılmış aygıt adları. Uzantı yok sayılır: "NUL.png" da NUL aygıtıdır, "CON.png" okumak sonsuza dek bekler.
    // COM¹/COM²/COM³ ve LPT¹/LPT²/LPT³ (üst simge rakam) Windows'ta aynı şekilde ayrılmıştır.
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
    ];

    /// <summary>
    /// Yol bileşeni: boş değil, sonu nokta ya da boşluk değil (Win32 bunları sessizce kırpar, "..." ve "x." gibi biçimler
    /// bu yüzden başka bir şeye çözülür) ve ayrılmış aygıt adı değil.
    /// </summary>
    private static bool IsSafeComponent(string part)
    {
        if (part.Length == 0 || part[^1] is '.' or ' ') return false;

        var dot = part.IndexOf('.');
        var stem = (dot >= 0 ? part[..dot] : part).TrimEnd(' ');
        return !ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }

    // Ağ ve aygıt yolu önekleri. "\\" ve "//" zaten "\\?\" ile "\\.\" önekini de kapsar; yine de açık listelenir.
    private static readonly string[] UncPrefixes = ["\\\\", "//", "\\\\?\\", "\\\\.\\"];

    private static bool IsUncPath(string candidate) =>
        UncPrefixes.Any(prefix => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsDriveRootedPath(string candidate) =>
        candidate.Length >= 3 && char.IsAsciiLetter(candidate[0]) && candidate[1] == ':' && candidate[2] == '\\';

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
