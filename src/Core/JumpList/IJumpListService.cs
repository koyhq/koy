namespace Koy.Core.JumpList;

/// <summary>
/// Uygulama bazlı atlama listesi (Windows görev çubuğundaki sağ tık menüsü). Yalnız menü açılınca çağrılır; izleyici,
/// zamanlayıcı ya da yoklama yoktur. Sonuçlar dosya zamanına göre sınırlı sayıda önbelleğe alınır.
/// </summary>
public interface IJumpListService
{
    /// <summary>
    /// AppUserModelID için bölümleri okur (menü sırasında, sınırlı). Okuma ayrı bir iş parçacığında yapılır; çağıran
    /// iş parçacığı bloklanmaz. Hata, dosya yok ya da AppID boş: boş liste (istisna yok).
    /// </summary>
    Task<IReadOnlyList<JumpListSection>> GetAsync(string appUserModelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pencere ya da paket AUMID'i vermeyen paketsiz uygulamanın (Discord gibi süreç düzeyinde koyanlar) AppUserModelID'ini,
    /// exe'sini gösteren Başlat menüsü kısayolundan bulur. Yalnız sağ tıkta çağrılır; ayrı iş parçacığında, sınırlı tarama,
    /// sonuç (bulunamadı dahil) exe yoluna göre önbellekli. Bulunamazsa null (istisna yok).
    /// </summary>
    Task<string?> ResolveAppIdAsync(string exePath, CancellationToken cancellationToken = default);

    /// <summary>Girdiyi çalıştırır. <paramref name="appExePath"/> belge girdilerini uygulamanın kendisiyle açmak içindir.</summary>
    void Launch(JumpListEntry entry, string? appExePath);

    /// <summary>Önbelleği boşaltır (dock kapanırken).</summary>
    void Clear();
}
