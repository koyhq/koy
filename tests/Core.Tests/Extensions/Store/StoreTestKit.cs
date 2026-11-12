using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Koy.Core.Extensions.Store;
using Koy.Core.Settings;
using Koy.Core.Settings.Profiles;

namespace Koy.Core.Tests.Extensions.Store;

/// <summary>Mağaza testlerinin ortak yardımcıları: geçici klasör, bellekte zip, örnek paket ve indeks.</summary>
internal static class StoreTestKit
{
    public static readonly byte[] Gif = [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0];
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    public const string ThemeJson = "{ \"KoyBgWindow\": \"#112233\" }";

    public static string ProfileJson() =>
        SettingsProfileCodec.Export(new AppSettings(), "Ornek", [ProfileSection.Appearance]);

    public static string Manifest(string id, string kind, string version = "1.0.0", string? extra = null) =>
        "{ \"id\": \"" + id + "\", \"name\": \"Ad " + id + "\", \"version\": \"" + version + "\", \"author\": \"Yazar\"," +
        " \"kind\": \"" + kind + "\", \"license\": \"MIT\", \"capabilities\": []" + (extra ?? "") + " }";

    public static MemoryStream Zip(params (string Name, byte[] Data)[] files)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in files)
            {
                var entry = zip.CreateEntry(name);
                using var s = entry.Open();
                s.Write(data);
            }
        }

        ms.Position = 0;
        return ms;
    }

    public static (string, byte[]) F(string name, string text) => (name, Encoding.UTF8.GetBytes(text));

    public static (string, byte[]) F(string name, byte[] data) => (name, data);

    public static string Sha256(Stream stream)
    {
        stream.Position = 0;
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        stream.Position = 0;
        return hash;
    }

    public static StoreItem Item(
        string id = "koyhq.ornek",
        ExtensionKind kind = ExtensionKind.Theme,
        string version = "1.0.0",
        string name = "Ornek",
        string author = "Koy",
        string summary = "Ozet",
        long downloads = 0,
        string updated = "2026-11-12") => new(
        id, name, kind, version, author, "MIT", summary, "", null, DateOnly.ParseExact(updated, "yyyy-MM-dd"), downloads,
        new StorePackage("https://example.com/p.koyext", new string('a', 64), 1000), null);

    public static InstalledEntry Installed(
        string id = "koyhq.ornek",
        ExtensionKind kind = ExtensionKind.Theme,
        string version = "1.0.0",
        bool applied = false,
        string payload = "") => new(
        id, version, kind, "Ad " + id, "Yazar", "MIT", new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
        ExtensionSource.Store, applied, payload);

    public static InstalledRegistry Registry(params InstalledEntry[] entries)
    {
        Assert.True(InstalledRegistry.TryBuild(entries, out var registry, out _));
        return registry;
    }

    /// <summary>Tek öğeli geçerli indeks JSON'u; <paramref name="itemOverrides"/> öğenin ham metnini değiştirir.</summary>
    public static string ItemJson(string id = "koyhq.ornek", string kind = "theme", string extra = "") =>
        "{ \"id\": \"" + id + "\", \"name\": \"Ornek\", \"kind\": \"" + kind + "\", \"version\": \"1.0.0\"," +
        " \"author\": \"Koy\", \"license\": \"GPL-3.0-or-later\", \"summary\": \"Ozet\", \"updated\": \"2026-11-12\"," +
        " \"package\": { \"url\": \"https://example.com/p.koyext\", \"sha256\": \"" + new string('a', 64) + "\", \"size\": 1234 }" +
        extra + " }";

    public static string IndexJson(params string[] items) =>
        "{ \"format\": \"koy-store-index\", \"formatVersion\": 1, \"generated\": \"2026-11-12T00:00:00Z\", \"items\": [" +
        string.Join(",", items) + "] }";

    public sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "koy-store-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public string Combine(string part) => System.IO.Path.Combine(Path, part);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>Depo kökü (Koy.slnx'in klasörü); bulunamazsa null.</summary>
    public static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "Koy.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
