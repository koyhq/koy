namespace Koy.Core.Media;

/// <summary>
/// Aynı kapağı yeniden çözmemek için: medya bilgisi oynat/duraklatla ya da aynı albümün sonraki parçasıyla
/// yenilendiğinde kapak baytları aynı kalır. Önce aynı dizi nesnesi, sonra hafif parmak izi
/// (<see cref="CoverFingerprint"/>) karşılaştırılır.
/// </summary>
public sealed class CoverChangeTracker
{
    private byte[]? _lastBytes;
    private CoverFingerprint? _lastKey;

    /// <summary>Kapak öncekinden farklıysa true (ve yeni kapağı hatırlar). Boş ya da null kapak "kapak yok" sayılır.</summary>
    public bool HasChanged(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
        {
            var hadCover = _lastKey is not null;
            Reset();
            return hadCover;
        }

        if (ReferenceEquals(bytes, _lastBytes))
            return false;

        var key = CoverFingerprint.Of(bytes);
        _lastBytes = bytes;
        if (key == _lastKey)
            return false;

        _lastKey = key;
        return true;
    }

    /// <summary>Hatırlananı unutur (çözme başarısız oldu ya da parça kalmadı): sonraki kapak değişmiş sayılır.</summary>
    public void Reset()
    {
        _lastBytes = null;
        _lastKey = null;
    }
}
