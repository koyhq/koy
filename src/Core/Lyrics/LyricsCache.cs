using System.Security.Cryptography;
using System.Text;

namespace Koy.Core.Lyrics;

public enum LyricsCacheStatus
{
    Miss,
    Hit,

    /// <summary>Yakın zamanda arandı, söz yoktu: hizmete boşuna tekrar gidilmez.</summary>
    KnownMissing
}

/// <summary>
/// Çevrim içi bulunan sözlerin disk önbelleği. Parça başına bir dosya; boş dosya "söz yok" kaydıdır ve süresi dolar.
/// Dosya sayısı sınırlıdır, en eskiler silinir. Önbellek hatası özelliği durdurmaz: en kötü ihtimalle yeniden indirilir.
/// </summary>
public sealed class LyricsCache(string directory, TimeProvider? timeProvider = null, int maxFiles = 1000)
{
    /// <summary>"Söz yok" kaydının ömrü: veritabanına sonradan söz eklenebilir.</summary>
    public static readonly TimeSpan MissingLifetime = TimeSpan.FromDays(7);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Koy", "lyrics");

    public (LyricsCacheStatus Status, string? Lrc) Read(LyricsQuery query)
    {
        try
        {
            var file = new FileInfo(PathFor(query));
            if (!file.Exists)
                return (LyricsCacheStatus.Miss, null);

            if (file.Length > 0)
                return (LyricsCacheStatus.Hit, File.ReadAllText(file.FullName));

            var age = _time.GetUtcNow() - new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            return age < MissingLifetime ? (LyricsCacheStatus.KnownMissing, null) : (LyricsCacheStatus.Miss, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (LyricsCacheStatus.Miss, null);
        }
    }

    public void Write(LyricsQuery query, string lrc) => Store(query, lrc);

    public void WriteMissing(LyricsQuery query) => Store(query, string.Empty);

    private void Store(LyricsQuery query, string content)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = PathFor(query);
            var temp = path + ".tmp";
            File.WriteAllText(temp, content);
            File.Move(temp, path, overwrite: true);
            Prune();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Önbellek isteğe bağlı: yazılamazsa bir sonraki açılışta yeniden indirilir.
        }
    }

    private void Prune()
    {
        var files = new DirectoryInfo(directory).GetFiles("*.lrc");
        if (files.Length <= maxFiles)
            return;

        // Her yazmada tek tek silmemek için sınırın altına belirgin bir pay bırakılır.
        foreach (var file in files.OrderBy(f => f.LastWriteTimeUtc).Take(files.Length - maxFiles * 4 / 5))
            file.Delete();
    }

    // Parça adları dosya adına uygun olmayabilir ve diskte okunur halde durmasın.
    private string PathFor(LyricsQuery query)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(query.Key));
        return Path.Combine(directory, Convert.ToHexStringLower(hash)[..32] + ".lrc");
    }
}
