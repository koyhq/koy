namespace Koy.Core.Input;

/// <summary>
/// Ekran köşesi takibi için fare kancası. Yoklama yok: imleç hareket ettikçe Windows haber verir,
/// abone yalnız imleç bir köşeye girince ya da köşeden çıkınca çağrılır.
/// </summary>
public interface ICornerHookService : IDisposable
{
    /// <summary>İmlecin bulunduğu köşe değişti; köşeden çıkınca <c>null</c>. Arayüz iş parçacığında çağrılır.</summary>
    event EventHandler<CornerHit?>? CornerChanged;

    /// <summary>
    /// İzlenecek ekranlar (fiziksel piksel, <see cref="CornerHit.ScreenIndex"/> bu sıraya göre). Liste boşsa kanca kaldırılır.
    /// Kanca kurulamazsa false döner.
    /// </summary>
    bool SetScreens(IReadOnlyList<ScreenRect> screens, int cornerSizePx);
}
