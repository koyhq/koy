namespace Koy.Core.Extensions.Store;

/// <summary>
/// Paket denetimi ve kurulumunun hata nedeni. Metin değil kod: çeviri App'te yapılır.
/// Hata yoksa <see cref="None"/>.
/// </summary>
public enum ExtensionInstallError
{
    None,

    // Ağ ve indirme
    Network,
    Timeout,
    Cancelled,
    DownloadTooLarge,
    HashMismatch,
    FileNotFound,

    // Arşiv ve giriş denetimi
    InvalidArchive,
    UnsafePath,
    DuplicateEntry,
    TooManyFiles,
    ExtractedTooLarge,
    FileTooLarge,
    DisallowedFile,
    MissingManifest,
    MissingPayload,
    MultiplePayloads,

    // Manifest
    InvalidManifest,
    ManifestMismatch,
    CapabilitiesNotAllowed,

    // İçerik
    InvalidPayload,

    // Kayıt ve disk
    TooManyInstalled,
    KindConflict,
    IoError,
}

/// <summary>Kurulum sonucu. Başarıda <see cref="Entry"/> kayda yazılan öğedir.</summary>
/// <param name="PreviouslyApplied">
/// Güncelleme ise ve eski sürüm uygulanmıştı: yeni dosyalar yerinde ama ayara/kullanıcı dosyasına yansımadı,
/// App "Uygula"yı yeniden çalıştırmalı (arka plan uzantısı değişmiş olabilir).
/// </param>
public sealed record InstallResult(ExtensionInstallError Error, InstalledEntry? Entry, bool PreviouslyApplied = false)
{
    public bool IsSuccess => Error == ExtensionInstallError.None && Entry is not null;

    public static InstallResult Fail(ExtensionInstallError error) => new(error, null);
}
