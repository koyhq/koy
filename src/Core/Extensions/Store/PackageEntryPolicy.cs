using Koy.Core.Appearance;
using Koy.Core.Notch;
using Koy.Core.Settings.Profiles;

namespace Koy.Core.Extensions.Store;

/// <summary>Zip içindeki bir girdi: adı (zip'te yazıldığı gibi) ve bildirilen açılmış boyutu.</summary>
public readonly record struct PackageEntry(string Name, long Length);

/// <summary>Denetimin sonucu: hata yoksa <see cref="ExtensionInstallError.None"/>, hatalıysa ilk sorunlu girdi.</summary>
public readonly record struct PackageCheck(ExtensionInstallError Error, string? Entry = null)
{
    public bool IsOk => Error == ExtensionInstallError.None;

    public static PackageCheck Ok { get; } = new(ExtensionInstallError.None);
}

/// <summary>
/// Paket (.koyext, zip) girdilerinin saf kuralı: hiçbir dosyaya dokunmaz, yalnız ad ve boyut listesine bakar.
/// Zip slip (mutlak yol, "..", sürücü harfi, ters bölü, ":" yani alternatif veri akışı, boş ad), sayı ve boyut
/// sınırları, türe göre izinli dosya kümesi ve zorunlu dosyalar burada denetlenir. Büyük/küçük harfi farklı
/// aynı ad (Windows aynı dosyadır) tekrar sayılır.
/// </summary>
public static class PackageEntryPolicy
{
    public const long MaxDownloadBytes = 25L * 1024 * 1024;
    public const long MaxExtractedBytes = 50L * 1024 * 1024;
    public const int MaxFiles = 512;
    public const int MaxEntryNameLength = 200;

    public const long MaxManifestBytes = 16 * 1024;
    public const long MaxPreviewBytes = 2L * 1024 * 1024;
    public const long MaxIconBytes = 2L * 1024 * 1024;

    public const string ManifestName = "manifest.json";
    public const string PreviewName = "preview.png";
    public const string ThemeName = "theme.json";
    public const string ProfileName = "profile.json";
    public const string IconsFolder = "icons";
    public const string BackgroundStem = "background";

    private static readonly string[] IconExtensions = [".png", ".ico", ".svg"];

    /// <summary>
    /// Tür bilinmeden yapılabilen denetim: indirme boyutu, girdi sayısı, toplam açılmış boyut, ad güvenliği,
    /// tekrar ve manifest varlığı. Dosya yüklemede önce bu, sonra manifest okunup <see cref="Check"/> çağrılır.
    /// </summary>
    public static PackageCheck CheckCommon(IReadOnlyList<PackageEntry> entries, long downloadSize)
    {
        if (downloadSize > MaxDownloadBytes)
        {
            return new(ExtensionInstallError.DownloadTooLarge);
        }

        if (entries.Count > MaxFiles)
        {
            return new(ExtensionInstallError.TooManyFiles);
        }

        long total = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasManifest = false;
        foreach (var entry in entries)
        {
            var name = entry.Name;
            var isDirectory = name.EndsWith('/');
            if (!IsSafeName(isDirectory ? name[..^1] : name))
            {
                return new(ExtensionInstallError.UnsafePath, name);
            }

            if (entry.Length < 0)
            {
                return new(ExtensionInstallError.InvalidArchive, name);
            }

            if (!seen.Add(name))
            {
                return new(ExtensionInstallError.DuplicateEntry, name);
            }

            total += entry.Length;
            if (total > MaxExtractedBytes)
            {
                return new(ExtensionInstallError.ExtractedTooLarge, name);
            }

            if (name == ManifestName)
            {
                hasManifest = true;
            }
        }

        return hasManifest ? PackageCheck.Ok : new(ExtensionInstallError.MissingManifest);
    }

    /// <summary>
    /// Tam denetim: <see cref="CheckCommon"/> artı türe göre izinli dosyalar, tek tek boyut sınırları ve zorunlu dosya.
    /// <c>manifest.json</c> ve <c>preview.png</c> her türde serbest.
    /// </summary>
    public static PackageCheck Check(IReadOnlyList<PackageEntry> entries, ExtensionKind kind, long downloadSize)
    {
        var common = CheckCommon(entries, downloadSize);
        if (!common.IsOk)
        {
            return common;
        }

        var payloads = 0;
        foreach (var entry in entries)
        {
            var name = entry.Name;
            if (name == ManifestName)
            {
                if (entry.Length > MaxManifestBytes)
                {
                    return new(ExtensionInstallError.FileTooLarge, name);
                }

                continue;
            }

            if (name == PreviewName)
            {
                if (entry.Length > MaxPreviewBytes)
                {
                    return new(ExtensionInstallError.FileTooLarge, name);
                }

                continue;
            }

            // Klasör girdisi (Gezgin ekler) yalnız simge paketinin "icons/" klasörü için serbest.
            if (name.EndsWith('/'))
            {
                if (kind == ExtensionKind.IconPack && name == IconsFolder + "/")
                {
                    continue;
                }

                return new(ExtensionInstallError.DisallowedFile, name);
            }

            switch (kind)
            {
                case ExtensionKind.Theme when name == ThemeName:
                    if (entry.Length > UserStyleParser.MaxBytes)
                    {
                        return new(ExtensionInstallError.FileTooLarge, name);
                    }

                    payloads++;
                    break;

                case ExtensionKind.Profile when name == ProfileName:
                    if (entry.Length > ProfileLibrary.MaxFileBytes)
                    {
                        return new(ExtensionInstallError.FileTooLarge, name);
                    }

                    payloads++;
                    break;

                case ExtensionKind.Background when IsBackgroundName(name):
                    payloads++;
                    break;

                case ExtensionKind.IconPack when IsIconPath(name):
                    if (entry.Length > MaxIconBytes)
                    {
                        return new(ExtensionInstallError.FileTooLarge, name);
                    }

                    payloads++;
                    break;

                default:
                    return new(ExtensionInstallError.DisallowedFile, name);
            }
        }

        if (kind == ExtensionKind.Background && payloads > 1)
        {
            return new(ExtensionInstallError.MultiplePayloads);
        }

        // Tema ve profil tek dosya: ad sabit ve tekrar zaten reddedildi, yani payloads 0 ya da 1.
        return payloads == 0 ? new(ExtensionInstallError.MissingPayload) : PackageCheck.Ok;
    }

    /// <summary>
    /// Zip girdi adı güvenli mi (klasör girdisinde sondaki "/" atılmış hâli verilir): boş değil, kısa, denetim
    /// karakteri yok; mutlak yol, sürücü harfi/":" (ADS), ters bölü, "." ve ".." bileşeni, boş bileşen,
    /// nokta/boşlukla biten bileşen yok. Zip yolu ayırıcısı yalnız "/".
    /// </summary>
    public static bool IsSafeName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxEntryNameLength)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (char.IsControl(c) || c is '\\' or ':' or '<' or '>' or '"' or '|' or '?' or '*')
            {
                return false;
            }
        }

        foreach (var part in name.Split('/'))
        {
            if (part.Length == 0 || part is "." or ".." || part[^1] is '.' or ' ')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>"background.gif|webp|mp4|webm" (küçük harf; kök klasörde).</summary>
    public static bool IsBackgroundName(string name)
    {
        var dot = name.LastIndexOf('.');
        if (dot <= 0 || name.Contains('/'))
        {
            return false;
        }

        return name[..dot] == BackgroundStem
            && LiveBackgroundFileValidator.AllowedExtensions.Contains(name[dot..], StringComparer.Ordinal);
    }

    /// <summary>"icons/&lt;dosya&gt;" ve uzantı .png/.ico/.svg; alt klasör ve aygıt adı yok (IconPackFiles kuralı).</summary>
    public static bool IsIconPath(string name)
    {
        if (!name.StartsWith(IconsFolder + "/", StringComparison.Ordinal))
        {
            return false;
        }

        var file = name[(IconsFolder.Length + 1)..];
        if (!AppIconOverrides.TryValidatePackFile(file, out _))
        {
            return false;
        }

        return IconExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
    }
}
