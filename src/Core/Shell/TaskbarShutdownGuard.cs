namespace Koy.Core.Shell;

/// <summary>
/// Oturum kapanırken görev çubuğu geri getirildikten sonra dock'un pencere olaylarıyla yeniden gizlemesini bastırır.
/// Kapatma başka uygulamaca iptal edilirse bastırma süre dolunca kendiliğinden biter (dock yeniden gizler).
/// </summary>
public sealed class TaskbarShutdownGuard(long suppressMs = TaskbarShutdownGuard.DefaultSuppressMs)
{
    public const long DefaultSuppressMs = 20_000;

    private long _until = long.MinValue;

    /// <summary>Kapanış başladı: şu andan <c>suppressMs</c> sonrasına kadar gizleme bastırılır.</summary>
    public void Begin(long nowMs) => _until = nowMs + suppressMs;

    public bool IsSuppressed(long nowMs) => nowMs < _until;
}
