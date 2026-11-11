namespace Koy.Core.Extensions;

/// <summary>Manifest doğrulamasının ilk hatalı alanını söyler. Hata yoksa <see cref="None"/>.</summary>
public enum ExtensionManifestError
{
    None,
    InvalidId,
    InvalidName,
    InvalidVersion,
    InvalidAuthor,
    TooManyCapabilities,
    UnknownCapability,
    DuplicateCapability,
    UnknownEntryKind,
}

/// <summary>
/// Dışarıdan gelen ham değerleri (ör. JSON'dan okunmuş metinler) sıkı kurallarla doğrular.
/// Bilinmeyen yetki ya da tür, geçersiz kimlik ve sürüm, sınırları aşan metin reddedilir; sessizce düzeltilmez.
/// Ad ve yazarın baştaki ya da sondaki boşluğu da reddedilir, kırpılmaz.
/// </summary>
public static class ExtensionManifestValidator
{
    public const int MinIdLength = 2;
    public const int MaxIdLength = 64;
    public const int MaxNameLength = 80;
    public const int MaxAuthorLength = 80;

    // Sürüm parçası en çok 6 hane: "999999.999999.999999" gibi aşırı değerleri ve taşmayı engeller.
    public const int MaxVersionPartDigits = 6;

    // Yetki sayısı kapalı kümenin büyüklüğünden çok daha yüksekse, şişirilmiş bir manifesttir.
    public const int MaxCapabilities = 16;

    /// <summary>
    /// Ham değerleri doğrular. Başarılıysa <paramref name="manifest"/> dolar, değilse <paramref name="error"/>
    /// ilk hatalı alanı söyler.
    /// Yetki alanı hiç verilmezse (<c>null</c>) bu açık bir kuraldır: boş liste, yani hiç yetki istemeyen
    /// eklenti. Bu bir düzeltme değildir; eksik alan ile boş liste aynı anlama gelir.
    /// </summary>
    public static bool TryCreate(
        string? id,
        string? name,
        string? version,
        string? author,
        IEnumerable<string?>? capabilities,
        string? entryKind,
        out ExtensionManifest? manifest,
        out ExtensionManifestError error)
    {
        manifest = null;

        if (!IsValidId(id))
        {
            error = ExtensionManifestError.InvalidId;
            return false;
        }

        if (!IsValidText(name, MaxNameLength))
        {
            error = ExtensionManifestError.InvalidName;
            return false;
        }

        if (!IsValidVersion(version))
        {
            error = ExtensionManifestError.InvalidVersion;
            return false;
        }

        if (!IsValidText(author, MaxAuthorLength))
        {
            error = ExtensionManifestError.InvalidAuthor;
            return false;
        }

        // Sayı en fazla MaxCapabilities + 1 öğeye kadar okunur: sınır aşılınca numaralandırma durur ve
        // fazlası hiç kopyalanmaz. Sayı denetimi, yetki adlarının çözümlemesinden önce yapılır.
        var list = new List<string?>();
        if (capabilities is not null)
        {
            foreach (var value in capabilities)
            {
                if (list.Count == MaxCapabilities)
                {
                    error = ExtensionManifestError.TooManyCapabilities;
                    return false;
                }

                list.Add(value);
            }
        }

        var parsed = new List<ExtensionCapability>(list.Count);
        foreach (var value in list)
        {
            if (!TryParseCapability(value, out var capability))
            {
                error = ExtensionManifestError.UnknownCapability;
                return false;
            }

            // Aynı yetki iki kez yazılırsa kullanıcıya gösterilen liste yanıltıcı olur.
            if (parsed.Contains(capability))
            {
                error = ExtensionManifestError.DuplicateCapability;
                return false;
            }

            parsed.Add(capability);
        }

        if (!TryParseEntryKind(entryKind, out var kind))
        {
            error = ExtensionManifestError.UnknownEntryKind;
            return false;
        }

        manifest = new ExtensionManifest(id!, name!, version!, author!, parsed, kind);
        error = ExtensionManifestError.None;
        return true;
    }

    /// <summary>
    /// Kimlik: küçük harf ile başlar, yalnız küçük harf, rakam ve tire içerir, tireyle bitmez.
    /// Uzunluk 2 ile 64 arasında. Büyük harf ya da boşluk kabul edilmez, çünkü kimlik dosya adı ve
    /// karşılaştırma anahtarı olarak kullanılır.
    /// </summary>
    private static bool IsValidId(string? id)
    {
        if (id is null || id.Length < MinIdLength || id.Length > MaxIdLength)
        {
            return false;
        }

        if (!IsAsciiLower(id[0]))
        {
            return false;
        }

        foreach (var c in id)
        {
            if (!IsAsciiLower(c) && !char.IsAsciiDigit(c) && c != '-')
            {
                return false;
            }
        }

        return id[^1] != '-';
    }

    /// <summary>Sürüm tam olarak üç parçalı "major.minor.patch" biçiminde olmalı; her parça yalnız rakam içerir.</summary>
    private static bool IsValidVersion(string? version)
    {
        if (version is null)
        {
            return false;
        }

        var parts = version.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (part.Length < 1 || part.Length > MaxVersionPartDigits)
            {
                return false;
            }

            foreach (var c in part)
            {
                if (!char.IsAsciiDigit(c))
                {
                    return false;
                }
            }

            // "01" gibi baştaki sıfırlar iki farklı yazımın aynı sürüm sayılmasına yol açar.
            if (part.Length > 1 && part[0] == '0')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Ad ve yazar: boş olmayan, sınırı aşmayan, denetim karakteri içermeyen ve baştaki ya da sondaki
    /// boşluk taşımayan metin. Boşluk sessizce kırpılmaz; böyle bir değer reddedilir.
    /// </summary>
    private static bool IsValidText(string? value, int maxLength)
    {
        if (value is null || value.Length == 0 || value.Length > maxLength)
        {
            return false;
        }

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    // Eşleşme büyük/küçük harfe duyarlı: "Network" ya da "network " kabul edilmez.
    private static bool TryParseCapability(string? value, out ExtensionCapability capability)
    {
        switch (value)
        {
            case "network":
                capability = ExtensionCapability.Network;
                return true;
            case "files":
                capability = ExtensionCapability.Files;
                return true;
            case "clipboard":
                capability = ExtensionCapability.Clipboard;
                return true;
            default:
                capability = default;
                return false;
        }
    }

    private static bool TryParseEntryKind(string? value, out ExtensionEntryKind kind)
    {
        if (value == "data")
        {
            kind = ExtensionEntryKind.Data;
            return true;
        }

        kind = default;
        return false;
    }

    private static bool IsAsciiLower(char c) => c >= 'a' && c <= 'z';
}
