namespace Koy.Core.Extensions.Store;

/// <summary>
/// Mağaza istemcisinin sözleşmesi (uygulaması Platform'da). Hiçbir yöntem beklenen hatada fırlatmaz:
/// sonuçta hata kodu döner. İptal de hata kodudur (<see cref="ExtensionInstallError.Cancelled"/>).
/// </summary>
public interface IExtensionStoreClient
{
    /// <summary>İndeksi çeker ve ayrıştırır. Yalnız mağaza sayfası açıkken çağrılır (AGENTS #5).</summary>
    Task<StoreIndexResult> FetchIndexAsync(CancellationToken cancellationToken);

    /// <summary>Paketi indirir, SHA256'yı doğrular, denetler, kurar ve kaydı günceller. İlerleme 0..1.</summary>
    Task<InstallResult> InstallFromStoreAsync(StoreItem item, IProgress<double>? progress, CancellationToken cancellationToken);

    /// <summary>Yerel bir .koyext (zip) dosyasından kurar; aynı denetimler, SHA256 yok.</summary>
    Task<InstallResult> InstallFromFileAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Yalnız kayıttaki eklentinin klasörünü siler ve kayıttan çıkarır. Ayarların geri alınması çağıranındır
    /// (önce <see cref="ExtensionApplyPlanner.RevertOnUninstall"/>). Kayıtta yoksa ya da silinemezse false.
    /// </summary>
    Task<bool> UninstallAsync(string id);
}
