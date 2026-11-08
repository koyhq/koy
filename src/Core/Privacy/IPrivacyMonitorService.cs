namespace Koy.Core.Privacy;

/// <summary>
/// Kamera ve mikrofon aktif kullanım durumunu izleyen servis sözleşmesi.
/// </summary>
public interface IPrivacyMonitorService : IDisposable
{
    PrivacyState CurrentState { get; }

    event Action<PrivacyState>? StateChanged;

    void Start();

    void Stop();
}
