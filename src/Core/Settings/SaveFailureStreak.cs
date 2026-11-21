namespace Koy.Core.Settings;

/// <summary>
/// Art arda kayıt hatalarını tek bildirime indirir: seri başlayınca bir kez, bitince bir kez haber verilir. Kaydırıcı
/// sürüklemesi gibi her değişimde yazılan yerlerde aynı hata kullanıcıya ya da log'a yığılmasın.
/// </summary>
public sealed class SaveFailureStreak
{
    public enum Change { None, Started, Ended }

    private readonly object _gate = new();
    private bool _failing;

    /// <summary>Son kayıt başarısız mı (pencere sonradan açılırsa uyarıyı göstermek için).</summary>
    public bool IsFailing
    {
        get { lock (_gate) return _failing; }
    }

    public Change Report(bool success)
    {
        lock (_gate)
        {
            if (success)
            {
                if (!_failing)
                    return Change.None;
                _failing = false;
                return Change.Ended;
            }

            if (_failing)
                return Change.None;
            _failing = true;
            return Change.Started;
        }
    }
}
