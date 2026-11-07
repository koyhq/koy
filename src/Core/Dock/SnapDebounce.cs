namespace Koy.Core.Dock;

/// <summary>
/// Kapsülün "yüzen (60 px) ↔ alta yapışık (46 px)" geçişini gürültüden korur. Alt+Tab sırasında ön plan kısa süre
/// ara pencerelere uğrar (büyütülmüş / değil, YouTube tam ekran denemesi); her uğrakta dock'u 14 px oynatmak "zıplama"
/// gibi görünüyordu. İstenen durum <see cref="Delay"/> boyunca değişmeden kalırsa geçerli olur; daha kısa süren değişim
/// yok sayılır. Yoklama yok: durum bekliyorsa çağıran kalan süre sonra tek atımlık bir zamanlayıcıyla yeniden sorar.
/// </summary>
public sealed class SnapDebounce(TimeSpan delay)
{
    public TimeSpan Delay { get; } = delay;

    private bool _hasEffective;
    private bool _pendingDesired;
    private TimeSpan _pendingSince;
    private bool _hasPending;

    /// <summary>Şu an uygulanan durum (true = alta yapışık).</summary>
    public bool Effective { get; private set; }

    /// <summary>İstenen durum uygulananla farklı ve bekliyor.</summary>
    public bool IsPending => _hasPending;

    /// <summary>Bekleyen değişimin geçerli olmasına kalan süre; bekleme yoksa sıfır.</summary>
    public TimeSpan Remaining(TimeSpan now) =>
        _hasPending ? TimeSpan.FromTicks(Math.Max(0, (Delay - (now - _pendingSince)).Ticks)) : TimeSpan.Zero;

    /// <summary>İstenen durumu bildirir, uygulanacak durumu döndürür. İlk çağrı beklemeden benimsenir.</summary>
    public bool Update(bool desired, TimeSpan now)
    {
        if (!_hasEffective)
        {
            _hasEffective = true;
            Effective = desired;
            _hasPending = false;
            return Effective;
        }

        if (desired == Effective)
        {
            _hasPending = false;
            return Effective;
        }

        if (!_hasPending || _pendingDesired != desired)
        {
            _hasPending = true;
            _pendingDesired = desired;
            _pendingSince = now;
        }

        if (now - _pendingSince >= Delay)
        {
            Effective = desired;
            _hasPending = false;
        }

        return Effective;
    }

    /// <summary>Beklemeyi bırakıp doğrudan geçer (gizlenme, tam genişlik gibi geometrinin zaten değiştiği durumlar).</summary>
    public void Reset(bool state)
    {
        _hasEffective = true;
        Effective = state;
        _hasPending = false;
    }
}
