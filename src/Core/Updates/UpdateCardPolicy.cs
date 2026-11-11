namespace Koy.Core.Updates;

/// <summary>
/// Çentikteki "yeni sürüm var" kartının ne zaman çıkacağına karar verir. Saf C#; durum yalnız bu oturum için bellekte tutulur.
/// Kural: bir sürüm için oturumda en çok bir kez çıkar, × ile kapatılan sürüm bir daha çıkmaz, farklı bir sürüm yeniden çıkabilir,
/// çentik açıkken bekler (kapanınca gösterilir), gösterildikten 15 sn sonra kendiliğinden kapanır.
/// </summary>
public sealed class UpdateCardPolicy(TimeProvider? clock = null)
{
    /// <summary>Kartın kendiliğinden kapanma süresi.</summary>
    public static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(15);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private string? _shownVersion;
    private string? _dismissedVersion;
    private DateTimeOffset _shownAt;

    /// <summary>Kart şimdi gösterilmeli mi. Çentik açıkken false döner; çağıran çentik kapanınca yeniden sorar.</summary>
    public bool ShouldShow(bool hasUpdate, string? version, bool notchExpanded)
    {
        if (!hasUpdate || string.IsNullOrWhiteSpace(version) || notchExpanded)
            return false;

        return !string.Equals(version, _dismissedVersion, StringComparison.Ordinal)
               && !string.Equals(version, _shownVersion, StringComparison.Ordinal);
    }

    /// <summary>Kart gösterildi: bu sürüm için oturumda ikinci kez çıkmaz.</summary>
    public void MarkShown(string version)
    {
        _shownVersion = version;
        _shownAt = _clock.GetUtcNow();
    }

    /// <summary>Kullanıcı × ile kapattı: bu sürüm bu oturumda bir daha gösterilmez.</summary>
    public void Dismiss(string version) => _dismissedVersion = version;

    /// <summary>Gösterilen kartın süresi doldu mu.</summary>
    public bool IsExpired() =>
        _shownVersion is not null && _clock.GetUtcNow() - _shownAt >= AutoDismissAfter;
}
