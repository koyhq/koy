using System.IO.Compression;
using System.Text;
using Koy.Core.Appearance;
using Koy.Core.Settings.Profiles;

namespace Koy.Core.Extensions.Store;

/// <summary>
/// Paket (zip) açma ve kurma mantığı; ağ yok, bu yüzden geçici klasörde test edilir. Sıra: giriş listesi denetimi
/// (<see cref="PackageEntryPolicy"/>) → manifest → tür denetimi → geçici klasöre sınırlı açma → içerik doğrulama →
/// eski sürümü yedekleyip tek <see cref="Directory.Move(string, string)"/> ile <c>extensions\&lt;id&gt;</c> →
/// kayıt. Hatada geçici klasör silinir; yarım kurulum kalmaz. Asla beklenen hatada fırlatmaz.
/// </summary>
public sealed class ExtensionPackageInstaller(
    string extensionsFolder, InstalledRegistryStore registry, Func<DateTimeOffset>? clock = null)
{
    public const string TempPrefix = ".tmp-";
    public const string OldPrefix = ".old-";

    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public string ExtensionsFolder { get; } = extensionsFolder;

    /// <summary>
    /// <paramref name="zip"/> aranabilir bir akış olmalı. <paramref name="expected"/> doluysa (mağaza kurulumu) paketin
    /// kimlik/sürüm/türü indeksle aynı olmalı; boşsa (dosyadan) manifest kendi başına doğrulanır.
    /// <paramref name="downloadSize"/> paketin bayt boyutu.
    /// </summary>
    public InstallResult Install(Stream zip, long downloadSize, StoreItem? expected, ExtensionSource source)
    {
        string? temp = null;
        try
        {
            if (downloadSize > PackageEntryPolicy.MaxDownloadBytes)
            {
                return InstallResult.Fail(ExtensionInstallError.DownloadTooLarge);
            }

            Directory.CreateDirectory(ExtensionsFolder);
            CleanupStale();

            using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
            var entries = archive.Entries.Select(e => new PackageEntry(e.FullName, e.Length)).ToList();

            var common = PackageEntryPolicy.CheckCommon(entries, downloadSize);
            if (!common.IsOk)
            {
                return InstallResult.Fail(common.Error);
            }

            var manifestBytes = ReadEntry(archive.GetEntry(PackageEntryPolicy.ManifestName)!, PackageEntryPolicy.MaxManifestBytes);
            if (manifestBytes is null)
            {
                return InstallResult.Fail(ExtensionInstallError.FileTooLarge);
            }

            var parsed = PackageManifestParser.Parse(manifestBytes);
            if (!parsed.IsValid)
            {
                return InstallResult.Fail(parsed.Error);
            }

            var package = parsed.Manifest!;
            if (expected is not null)
            {
                var match = PackageManifestParser.Match(package, expected);
                if (match != ExtensionInstallError.None)
                {
                    return InstallResult.Fail(match);
                }
            }

            var check = PackageEntryPolicy.Check(entries, package.Kind, downloadSize);
            if (!check.IsOk)
            {
                return InstallResult.Fail(check.Error);
            }

            var id = package.Manifest.Id;
            var previous = registry.Load().Find(id);
            if (previous is not null && previous.Kind != package.Kind)
            {
                return InstallResult.Fail(ExtensionInstallError.KindConflict);
            }

            // Sınır dolmuşsa açmaya başlamadan söyle (yeni kimlik için).
            if (previous is null && registry.Load().Entries.Count >= InstalledExtensionList.MaxInstalled)
            {
                return InstallResult.Fail(ExtensionInstallError.TooManyInstalled);
            }

            temp = Path.Combine(ExtensionsFolder, TempPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);

            var extract = Extract(archive, entries, temp);
            if (extract != ExtensionInstallError.None)
            {
                return InstallResult.Fail(extract);
            }

            var payload = ValidatePayload(package.Kind, entries, temp, out var payloadFile);
            if (payload != ExtensionInstallError.None)
            {
                return InstallResult.Fail(payload);
            }

            var entry = new InstalledEntry(
                id, package.Manifest.Version, package.Kind, package.Manifest.Name, package.Manifest.Author, package.License,
                _clock(), source, previous?.Applied ?? false, payloadFile);

            var moved = Replace(temp, id);
            if (moved != ExtensionInstallError.None)
            {
                return InstallResult.Fail(moved);
            }

            temp = null; // taşındı, temizlenecek bir şey yok

            var error = InstalledRegistryError.None;
            var saved = registry.Update(current =>
            {
                if (!current.TryUpsert(entry, out var next, out error))
                {
                    return null;
                }

                return entry.Applied ? next.WithApplied(id) : next;
            });

            if (!saved)
            {
                // Kayıt yazılamadı: kayıtsız klasör yetim kalmasın.
                TryDelete(Path.Combine(ExtensionsFolder, id));
                return InstallResult.Fail(error == InstalledRegistryError.TooManyExtensions
                    ? ExtensionInstallError.TooManyInstalled
                    : ExtensionInstallError.IoError);
            }

            return new InstallResult(ExtensionInstallError.None, entry, previous?.Applied ?? false);
        }
        catch (InvalidDataException)
        {
            return InstallResult.Fail(ExtensionInstallError.InvalidArchive);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return InstallResult.Fail(ExtensionInstallError.IoError);
        }
        finally
        {
            if (temp is not null)
            {
                TryDelete(temp);
            }
        }
    }

    /// <summary>
    /// Yalnız kayıttaki kimliğin klasörünü siler (yol <see cref="ExtensionsFolder"/>'ın kesin altında olmalı) ve
    /// kayıttan çıkarır. Kayıtta yoksa ya da silinemezse false; silinemezse kayıt korunur.
    /// </summary>
    public bool Uninstall(string id)
    {
        var folder = ExtensionPaths.FolderFor(id, ExtensionsFolder);
        if (folder is null || registry.Load().Find(id) is null || !ExtensionPaths.IsStrictlyUnder(ExtensionsFolder, folder))
        {
            return false;
        }

        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        return registry.Update(current => current.Remove(id));
    }

    /// <summary>Yarım kalmış (çökme, süreç öldürme) geçici klasör ve dosyaları, bir saatten eskiyse siler.</summary>
    public void CleanupStale()
    {
        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(ExtensionsFolder))
            {
                var name = Path.GetFileName(path);
                if (!name.StartsWith(TempPrefix, StringComparison.Ordinal) && !name.StartsWith(OldPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (_clock() - new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero) > StaleAfter)
                {
                    TryDelete(path);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // En iyi çaba: temizlik başarısızlığı kurulumu durdurmaz.
        }
    }

    // Sınırlı kopya: bildirilen boyuta ve toplam sınıra bakılmaz sadece, gerçekten okunan bayt sayılır.
    // Zip başlığı yalan söyleyebilir (zip bombası), bu yüzden sınır aşılınca akış kesilir.
    private static ExtensionInstallError Extract(ZipArchive archive, List<PackageEntry> entries, string temp)
    {
        var root = Path.GetFullPath(temp).TrimEnd('\\') + Path.DirectorySeparatorChar;
        long total = 0;
        var buffer = new byte[81920];

        foreach (var zipEntry in archive.Entries)
        {
            var name = zipEntry.FullName;
            var target = Path.GetFullPath(Path.Combine(temp, name.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar)));

            // Derinlemesine savunma: ilke zaten ".." ve mutlak yolu reddetti.
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return ExtensionInstallError.UnsafePath;
            }

            if (name.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = zipEntry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            long written = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                written += read;
                total += read;
                if (written > zipEntry.Length || total > PackageEntryPolicy.MaxExtractedBytes)
                {
                    return total > PackageEntryPolicy.MaxExtractedBytes
                        ? ExtensionInstallError.ExtractedTooLarge
                        : ExtensionInstallError.InvalidArchive;
                }

                output.Write(buffer, 0, read);
            }
        }

        return ExtensionInstallError.None;
    }

    private static byte[]? ReadEntry(ZipArchiveEntry entry, long limit)
    {
        using var input = entry.Open();
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (ms.Length + read > limit)
            {
                return null;
            }

            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Türe özgü içerik doğrulaması (açılmış dosya üzerinde). Tema: UserStyleParser (dosya düzeyi hata ya da hiç
    /// geçerli giriş yok → ret; tek tek bilinmeyen anahtar tanıları kabul, uygulamada yok sayılır). Profil:
    /// SettingsProfileCodec. Görseller: dosya imzası (magic bytes), uzantıya güvenilmez.
    /// </summary>
    private static ExtensionInstallError ValidatePayload(
        ExtensionKind kind, List<PackageEntry> entries, string folder, out string payloadFile)
    {
        payloadFile = "";
        switch (kind)
        {
            case ExtensionKind.Theme:
            {
                payloadFile = PackageEntryPolicy.ThemeName;
                var text = ReadStrictUtf8(Path.Combine(folder, payloadFile));
                if (text is null)
                {
                    return ExtensionInstallError.InvalidPayload;
                }

                var result = UserStyleParser.Parse(text);
                var fileLevel = result.Diagnostics.Any(d => d.Key == UserStyleParser.FileKey);
                return fileLevel || result.ValidEntries.Count == 0
                    ? ExtensionInstallError.InvalidPayload
                    : ExtensionInstallError.None;
            }

            case ExtensionKind.Profile:
            {
                payloadFile = PackageEntryPolicy.ProfileName;
                var text = ReadStrictUtf8(Path.Combine(folder, payloadFile));
                return text is not null && SettingsProfileCodec.Read(text).IsValid
                    ? ExtensionInstallError.None
                    : ExtensionInstallError.InvalidPayload;
            }

            case ExtensionKind.Background:
            {
                var name = entries.First(e => PackageEntryPolicy.IsBackgroundName(e.Name)).Name;
                payloadFile = name;
                return PackageContent.LooksLikeBackground(Path.Combine(folder, name), Path.GetExtension(name))
                    ? ExtensionInstallError.None
                    : ExtensionInstallError.InvalidPayload;
            }

            case ExtensionKind.IconPack:
            {
                payloadFile = PackageEntryPolicy.IconsFolder;
                foreach (var entry in entries.Where(e => PackageEntryPolicy.IsIconPath(e.Name)))
                {
                    var path = Path.Combine(folder, entry.Name.Replace('/', Path.DirectorySeparatorChar));
                    if (!PackageContent.LooksLikeIcon(path, Path.GetExtension(entry.Name)))
                    {
                        return ExtensionInstallError.InvalidPayload;
                    }
                }

                return ExtensionInstallError.None;
            }

            default:
                return ExtensionInstallError.InvalidPayload;
        }
    }

    private static string? ReadStrictUtf8(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            // Windows editörlerinin eklediği BOM ayrıştırıcıyı şaşırtmasın.
            var skip = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes, skip, bytes.Length - skip);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or IOException)
        {
            return null;
        }
    }

    // Eski sürüm silinmeden önce kenara alınır; yeni taşıma başarısızsa geri konur (eski sürüm kaybolmaz).
    private ExtensionInstallError Replace(string temp, string id)
    {
        var final = Path.Combine(ExtensionsFolder, id);
        if (!ExtensionPaths.IsStrictlyUnder(ExtensionsFolder, final))
        {
            return ExtensionInstallError.UnsafePath;
        }

        string? old = null;
        try
        {
            if (Directory.Exists(final))
            {
                old = Path.Combine(ExtensionsFolder, OldPrefix + Guid.NewGuid().ToString("N"));
                Directory.Move(final, old);
            }

            Directory.Move(temp, final);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (old is not null && !Directory.Exists(final))
            {
                try
                {
                    Directory.Move(old, final);
                }
                catch (Exception restore) when (restore is IOException or UnauthorizedAccessException)
                {
                    // Geri koyma da başarısız: eski klasör ".old-" adıyla kalır, bir sonraki kurulum temizler.
                }
            }

            return ExtensionInstallError.IoError;
        }

        if (old is not null)
        {
            TryDelete(old);
        }

        return ExtensionInstallError.None;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // En iyi çaba.
        }
    }
}

/// <summary>Görsel/video dosyalarının imza denetimi: uzantı yalan söyleyebilir (ör. .png adlı çalıştırılabilir).</summary>
internal static class PackageContent
{
    public static bool LooksLikeBackground(string path, string extension)
    {
        var head = Head(path, 16);
        if (head is null)
        {
            return false;
        }

        return extension switch
        {
            ".gif" => StartsWith(head, "GIF87a"u8) || StartsWith(head, "GIF89a"u8),
            ".webp" => head.Length >= 12 && StartsWith(head, "RIFF"u8) && head.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            ".mp4" => head.Length >= 12 && head.AsSpan(4, 4).SequenceEqual("ftyp"u8),
            ".webm" => head.Length >= 4 && head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3,
            _ => false,
        };
    }

    public static bool LooksLikeIcon(string path, string extension)
    {
        var lower = extension.ToLowerInvariant();
        if (lower == ".svg")
        {
            // Betik ya da dış kaynak taşıyan SVG kabul edilmez; WPF çizici güvenli olsa da dosya paylaşılabilir.
            var headText = Head(path, 4096);
            if (headText is null)
            {
                return false;
            }

            var text = Encoding.UTF8.GetString(headText);
            return text.Contains("<svg", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("<script", StringComparison.OrdinalIgnoreCase);
        }

        var head = Head(path, 8);
        if (head is null)
        {
            return false;
        }

        return lower switch
        {
            ".png" => StartsWith(head, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".ico" => head.Length >= 4 && head[0] == 0 && head[1] == 0 && head[2] == 1 && head[3] == 0,
            _ => false,
        };
    }

    private static bool StartsWith(byte[] data, ReadOnlySpan<byte> prefix) => data.AsSpan().StartsWith(prefix);

    private static byte[]? Head(string path, int count)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[count];
            var read = stream.Read(buffer, 0, count);
            return read == 0 ? null : buffer[..read];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
