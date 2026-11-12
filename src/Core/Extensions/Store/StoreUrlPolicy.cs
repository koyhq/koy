namespace Koy.Core.Extensions.Store;

/// <summary>
/// İndirilecek varlıkların (paket, önizleme) güvenilir adres kümesi: yalnız https, <see cref="AllowedHost"/> üzerinde ve
/// <see cref="AllowedPathPrefix"/> yolunun altında. Mağaza deposu dışına, ele geçirilmiş bir indeksle bile indirme yapılmaz.
/// Aynı kural scripts/build-store.py içinde de uygulanır (<c>--check</c>). Ana sayfa bağlantısı bu kuralın dışındadır
/// (tarayıcıda açılır, indirilmez).
/// </summary>
public static class StoreUrlPolicy
{
    public const string AllowedHost = "raw.githubusercontent.com";

    public const string AllowedPathPrefix = "/koyhq/koy/";

    /// <summary>İndeksteki ham adres güvenilir mi (biçim kuralları <see cref="StoreFieldRules.IsHttpsUrl"/> ile aynı).</summary>
    public static bool IsTrustedAssetUrl(string? value) =>
        StoreFieldRules.IsHttpsUrl(value)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && IsTrustedAssetUri(uri);

    /// <summary>
    /// Adres güvenilir mi. İndirme sonrası son yanıtın adresi de buradan geçer: yönlendirme başka bir sunucuya
    /// götürdüyse içerik kabul edilmez.
    /// </summary>
    public static bool IsTrustedAssetUri(Uri? uri) =>
        uri is not null
        && uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.Equals(uri.Host, AllowedHost, StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith(AllowedPathPrefix, StringComparison.Ordinal)
        && !uri.AbsolutePath.Contains("..", StringComparison.Ordinal);
}
