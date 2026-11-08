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

    /// <summary>Girdiyi çalıştırır. <paramref name="appExePath"/> belge girdilerini uygulamanın kendisiyle açmak içindir.</summary>
    void Launch(JumpListEntry entry, string? appExePath);

    /// <summary>Önbelleği boşaltır (dock kapanırken).</summary>
    void Clear();
}
