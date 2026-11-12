using System.Net.Http;
using System.Security.Cryptography;
using Koy.Core.Extensions;
using Koy.Core.Extensions.Store;
using Koy.Platform.Net;

namespace Koy.Platform.Extensions;

/// <summary>
/// <see cref="IExtensionStoreClient"/> uygulaması: indeksi ve paketleri <see cref="AppHttp"/> ile alır, boyut sınırlı
/// akışla geçici dosyaya indirir (25 MB bellekte tutulmaz), SHA256'yı doğrular ve açma/kurma işini Core'daki
/// <see cref="ExtensionPackageInstaller"/>'a verir. Hata fırlatmaz, hata kodu döner. Kurulumlar tek sıradadır.
/// </summary>
public sealed class ExtensionStoreClient : IExtensionStoreClient
{
    public const string IndexUrl = "https://raw.githubusercontent.com/koyhq/koy/main/store/index.json";

    /// <summary>İndeks için süre sınırı (saniye).</summary>
    public const int IndexTimeoutSeconds = 20;

    /// <summary>Paket indirme için süre sınırı (saniye), AppHttp'nin 10 sn'lik başlık süresinden ayrı.</summary>
    public const int PackageTimeoutSeconds = 60;

    private readonly HttpClient _http;
    private readonly string _indexUrl;
    private readonly ExtensionPackageInstaller _installer;
    private readonly SemaphoreSlim _installGate = new(1, 1);

    public ExtensionStoreClient(
        InstalledRegistryStore? registry = null, string? extensionsFolder = null, HttpClient? http = null, string? indexUrl = null)
    {
        Registry = registry ?? new InstalledRegistryStore();
        ExtensionsFolder = extensionsFolder ?? ExtensionPaths.ExtensionsFolder;
        _http = http ?? AppHttp.Client;
        _indexUrl = indexUrl ?? IndexUrl;
        _installer = new ExtensionPackageInstaller(ExtensionsFolder, Registry);
    }

    /// <summary>Kurulu eklenti kaydı; App "Uygula" işaretini de buradan <c>Update</c> ile yazar.</summary>
    public InstalledRegistryStore Registry { get; }

    public string ExtensionsFolder { get; }

    public async Task<StoreIndexResult> FetchIndexAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(IndexTimeoutSeconds));
        try
        {
            using var response = await _http.GetAsync(_indexUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new StoreIndexResult(StoreIndexError.Network, null);
            }

            if (response.Content.Headers.ContentLength is > StoreIndexParser.MaxBytes)
            {
                return new StoreIndexResult(StoreIndexError.TooLarge, null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var ms = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (ms.Length + read > StoreIndexParser.MaxBytes)
                {
                    return new StoreIndexResult(StoreIndexError.TooLarge, null);
                }

                ms.Write(buffer, 0, read);
            }

            return StoreIndexParser.Parse(ms.ToArray());
        }
        catch (OperationCanceledException)
        {
            return new StoreIndexResult(
                cancellationToken.IsCancellationRequested ? StoreIndexError.Cancelled : StoreIndexError.Network, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            return new StoreIndexResult(StoreIndexError.Network, null);
        }
    }

    public async Task<InstallResult> InstallFromStoreAsync(StoreItem item, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        await _installGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? part = null;
        try
        {
            Directory.CreateDirectory(ExtensionsFolder);
            part = Path.Combine(ExtensionsFolder, ExtensionPackageInstaller.TempPrefix + Guid.NewGuid().ToString("N") + ".part");

            // Süre sınırı bütün indirmeyi kapsar (AppHttp.Timeout yalnız başlıklara kadar).
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(PackageTimeoutSeconds));

            var download = await DownloadAsync(item, part, progress, timeout.Token).ConfigureAwait(false);
            if (download.Error != ExtensionInstallError.None)
            {
                return InstallResult.Fail(download.Error);
            }

            // Açma ve kurma eşzamanlı disk işi: arayüz iş parçacığını tutmasın.
            var size = download.Length;
            return await Task.Run(() =>
            {
                using var stream = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read);
                return _installer.Install(stream, size, item, ExtensionSource.Store);
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Kullanıcı iptali ile 60 sn dolması ayrılır.
            return InstallResult.Fail(cancellationToken.IsCancellationRequested ? ExtensionInstallError.Cancelled : ExtensionInstallError.Timeout);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            return InstallResult.Fail(ex is HttpRequestException ? ExtensionInstallError.Network : ExtensionInstallError.IoError);
        }
        finally
        {
            TryDelete(part);
            _installGate.Release();
        }
    }

    public async Task<InstallResult> InstallFromFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return InstallResult.Fail(ExtensionInstallError.FileNotFound);
            }

            if (info.Length > PackageEntryPolicy.MaxDownloadBytes)
            {
                return InstallResult.Fail(ExtensionInstallError.DownloadTooLarge);
            }

            await _installGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var size = info.Length;
                return await Task.Run(() =>
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return _installer.Install(stream, size, expected: null, ExtensionSource.File);
                }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _installGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return InstallResult.Fail(ExtensionInstallError.Cancelled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return InstallResult.Fail(ExtensionInstallError.IoError);
        }
    }

    public async Task<bool> UninstallAsync(string id)
    {
        await _installGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => _installer.Uninstall(id)).ConfigureAwait(false);
        }
        finally
        {
            _installGate.Release();
        }
    }

    private readonly record struct DownloadResult(ExtensionInstallError Error, long Length);

    // İndirme: Content-Length ve okunan bayt sayısı iki ayrı sınır; SHA256 akarken hesaplanır.
    private async Task<DownloadResult> DownloadAsync(StoreItem item, string destination, IProgress<double>? progress, CancellationToken token)
    {
        // Bildirilen boyut hem 25 MB'ı hem indeks öğesini aşamaz (indeks ayrıştırıcısı da sınırlar).
        var limit = Math.Min(PackageEntryPolicy.MaxDownloadBytes, item.Package.Size);

        using var response = await _http.GetAsync(item.Package.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        // AppHttp yönlendirmeleri kendiliğinden izler (hava durumu da paylaşır, genel davranışı değiştirmeyiz):
        // son adres güvenilir küme dışına çıktıysa içerik kabul edilmez.
        if (!response.IsSuccessStatusCode || !StoreUrlPolicy.IsTrustedAssetUri(response.RequestMessage?.RequestUri))
        {
            return new(ExtensionInstallError.Network, 0);
        }

        var declared = response.Content.Headers.ContentLength;
        if (declared is { } length && length > limit)
        {
            return new(ExtensionInstallError.DownloadTooLarge, 0);
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;
        await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > limit)
                {
                    return new(ExtensionInstallError.DownloadTooLarge, 0);
                }

                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                progress?.Report(Math.Min(1.0, (double)total / (declared ?? item.Package.Size)));
            }
        }

        var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
        return string.Equals(actual, item.Package.Sha256, StringComparison.OrdinalIgnoreCase)
            ? new(ExtensionInstallError.None, total)
            : new(ExtensionInstallError.HashMismatch, 0);
    }

    private static void TryDelete(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // En iyi çaba: yetim .part dosyasını sonraki kurulum CleanupStale ile siler.
        }
    }
}
