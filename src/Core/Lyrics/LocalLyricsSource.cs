namespace Koy.Core.Lyrics;

/// <summary>
/// Kullanıcının klasöründeki <c>.lrc</c> dosyaları. Önce "Sanatçı - Başlık.lrc" adı denenir, bulunamazsa alt klasörler
/// taranır. Her zaman arka plan iş parçacığında çağrılır (<see cref="LyricsSession"/>).
/// </summary>
public sealed class LocalLyricsSource(string directory) : ILyricsSource
{
    private const long MaxFileBytes = 1024 * 1024;
    private const int MaxScannedFiles = 5000;

    private static readonly EnumerationOptions ScanOptions = new()
    {
        RecurseSubdirectories = true,
        MaxRecursionDepth = 5,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
        // Bağlantı klasörleri atlanır: döngüye girebilir ve klasörün dışına çıkar.
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
    };

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Lyrics");

    public async Task<LyricsLookup> FindAsync(LyricsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(directory) || FindFile(query) is not { } path || new FileInfo(path).Length > MaxFileBytes)
                return LyricsLookup.NotFound;

            return LyricsLookup.Found(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return LyricsLookup.NotFound;
        }
    }

    private string? FindFile(LyricsQuery query)
    {
        foreach (var name in CandidateNames(query))
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
                return path;
        }

        // Sanatçısız gevşek arama alakasız dosya getirir ("Love" adı onlarca dosyada geçer).
        if (query.Artist.Length == 0)
            return null;

        return Directory.EnumerateFiles(directory, "*.lrc", ScanOptions)
            .Take(MaxScannedFiles)
            .FirstOrDefault(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return name.Contains(query.Title, StringComparison.OrdinalIgnoreCase)
                    && name.Contains(query.PrimaryArtist, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static IEnumerable<string> CandidateNames(LyricsQuery query)
    {
        if (query.Artist.Length == 0)
        {
            yield return Sanitize(query.Title) + ".lrc";
            yield break;
        }

        yield return $"{Sanitize(query.Artist)} - {Sanitize(query.Title)}.lrc";
        if (query.PrimaryArtist != query.Artist)
            yield return $"{Sanitize(query.PrimaryArtist)} - {Sanitize(query.Title)}.lrc";
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
