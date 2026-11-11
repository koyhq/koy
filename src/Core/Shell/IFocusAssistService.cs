namespace Koy.Core.Shell;

/// <summary>
/// Windows Odak Yardımı / Rahatsız Etme sözleşmesi.
/// </summary>
public interface IFocusAssistService : IDisposable
{
    bool IsActive { get; }

    /// <summary>
    /// Rahatsız etmeyin / Odaklanma / Sessiz saatler / sunum modu açık mı. Tek seferlik okuma (zamanlayıcı yok);
    /// çağıran ihtiyaç anında sorar (bildirim geldiğinde, Kontrol sekmesi açılınca).
    /// </summary>
    bool IsDoNotDisturb { get; }

    /// <summary>
    /// Odaklanma oturumu açılıp kapanınca <see cref="IsDoNotDisturb"/> değeriyle tetiklenir (olay tabanlı, arka plan iş
    /// parçacığından olabilir). Rahatsız etmeyin düğmesinin kendisi için olay yoktur: çağıran açılışta yeniden okur.
    /// </summary>
    event EventHandler<bool>? StateChanged;

    void OpenSettings();
}
