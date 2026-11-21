namespace Koy.Core.Clipboard;

/// <summary>
/// Pano geçmişi servisi sözleşmesi (yalnız metin). Ayar kapalıysa App servisi hiç kurmaz/Start etmez; kapalıyken
/// Win32 dinleyicisi yoktur, boşta CPU %0.
///
/// İPLİK: <see cref="Changed"/> ARKA PLAN iş parçacığından gelebilir (pano mesajı kendi iş parçacığında işlenir).
/// App tarafı Dispatcher'a geçirmeli. Diğer üyeler her iş parçacığından çağrılabilir.
/// Gizlilik: parola yöneticilerinin "geçmişe alma" işaretini taşıyan panolar kaydedilmez. Sabitlenen öğeler düz
/// metin olarak diske yazılır (clipboard-pinned.json); gerisi yalnız bellekte kalır.
/// </summary>
public interface IClipboardHistoryService : IDisposable
{
    /// <summary>Dinleyici kurulu mu.</summary>
    bool IsRunning { get; }

    /// <summary>Dinlemeyi başlatır (idempotent). Sabit öğeler diskten ilk Start'ta yüklenir.</summary>
    void Start();

    /// <summary>Dinleyiciyi kaldırır (idempotent). Bellekteki geçmiş durur; silmek için <see cref="Clear"/>.</summary>
    void Stop();

    /// <summary>Liste değişti (yeni öğe, taşıma, sabitleme, silme). Arka plan iş parçacığından gelebilir.</summary>
    event EventHandler? Changed;

    /// <summary>En yeni başta; süzgeç ve arama büyük/küçük harf duyarsız.</summary>
    IReadOnlyList<ClipboardItem> Query(ClipboardFilter filter = ClipboardFilter.All, string? search = null);

    bool SetPinned(Guid id, bool pinned);

    bool Remove(Guid id);

    /// <summary>Sabitlenmeyenleri siler.</summary>
    void Clear();

    /// <summary>Silinen öğeleri geri koyar ("Geri al"; sabitler dahil). Bir şey eklendiyse <see cref="Changed"/> gelir.</summary>
    void Restore(IReadOnlyList<ClipboardItem> items);

    /// <summary>Öğeyi panoya yazar; bu yazma geçmişe yeniden eklenmez (öğe başa taşınmaz). Başarılıysa true.</summary>
    bool CopyBack(Guid id);
}
