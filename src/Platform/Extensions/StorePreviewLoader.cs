using System.Net.Http;
using System.Security.Cryptography;
using Koy.Core.Extensions.Store;
using Koy.Platform.Net;

namespace Koy.Platform.Extensions;

/// <summary>
/// Mağaza kartı önizlemesini indirir: https, en çok 1 MB, SHA256 indeksteki ile eşleşmezse atılır. Doğrulanmış bayt
/// dizisi <c>%LocalAppData%\Koy\store-cache\&lt;sha256&gt;.img</c> olarak saklanır; dosya adı özet olduğu için içerik
/// değişirse başka ada düşer. Önbellekteki dosya da her okunuşta yeniden doğrulanır (elle bozulmuş dosya kullanılmaz).
/// Çağıran yalnız kart görünürken ister (AGENTS #5). Asla fırlatmaz: olmadı ise null.
/// </summary>
public sealed class StorePreviewLoader(HttpClient? http = null, string? cacheFolder = null)
{
    /// <summary>İndirilebilecek en büyük önizleme (bayt).</summary>
    public const long MaxBytes = 1024 * 1024;

    /// <summary>Önbellekte tutulan en çok dosya; aşılınca en eskiler silinir.</summary>
    public const int MaxCachedFiles = 200;

    public const int TimeoutSeconds = 15;

    private readonly HttpClient _http = http ?? AppHttp.Client;

    public string CacheFolder { get; } = cacheFolder ?? DefaultCacheFolder;

    public static string DefaultCacheFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Koy", "store-cache");

    public async Task<byte[]?> LoadAsync(StorePreview preview, CancellationToken cancellationToken)
    {
        var path = CachePathOf(preview.Sha256);
        if (path is null)
        {
            return null;
        }

        var cached = await ReadCachedAsync(path, preview.Sha256, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var bytes = await DownloadAsync(preview, cancellationToken).ConfigureAwait(false);
        if (bytes is not null)
        {
            await WriteCacheAsync(path, bytes).ConfigureAwait(false);
        }

        return bytes;
    }

    // Özet yalnız 64 onaltılık karakter olabilir: yol ayırıcı sızdıramaz.
    private string? CachePathOf(string sha256) =>
        sha256.Length == 64 && sha256.All(Uri.IsHexDigit)
            ? Path.Combine(CacheFolder, sha256.ToLowerInvariant() + ".img")
            : null;

    private static async Task<byte[]?> ReadCachedAsync(string path, string sha256, CancellationToken token)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            if (info.Length > MaxBytes)
            {
                TryDelete(path);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            if (Matches(bytes, sha256))
            {
                return bytes;
            }

            TryDelete(path);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<byte[]?> DownloadAsync(StorePreview preview, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        try
        {
            using var response = await _http.GetAsync(preview.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MaxBytes)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var ms = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (ms.Length + read > MaxBytes)
                {
                    return null;
                }

                ms.Write(buffer, 0, read);
            }

            var bytes = ms.ToArray();
            return Matches(bytes, preview.Sha256) ? bytes : null;
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task WriteCacheAsync(string path, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(CacheFolder);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            Prune();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Önbellek en iyi çaba: yazılamazsa görsel yine gösterilir, yalnız bir sonraki seferde yeniden iner.
        }
    }

    private void Prune()
    {
        try
        {
            var files = new DirectoryInfo(CacheFolder).GetFiles("*.img");
            if (files.Length <= MaxCachedFiles)
            {
                return;
            }

            foreach (var file in files.OrderBy(f => f.LastWriteTimeUtc).Take(files.Length - MaxCachedFiles))
            {
                TryDelete(file.FullName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Temizlik de en iyi çaba.
        }
    }

    private static bool Matches(byte[] bytes, string sha256) =>
        string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), sha256, StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
