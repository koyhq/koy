namespace Koy.Core.Tasks;

/// <summary>
/// Odak görev listesi. Her değişiklik anında diske yazılır (tasks.json, ayar klasörü). Servis saf Core'dadır,
/// WinAPI yok. <see cref="Changed"/> değişikliği yapan iş parçacığında tetiklenir: arayüzden çağrılırsa UI
/// iş parçacığında gelir (başka iş parçacığından çağrılırsa App Dispatcher'a geçirmeli).
/// </summary>
public interface ITaskService
{
    /// <summary>En çok bu kadar kayıt tutulur; fazlası budanır (önce eski tamamlananlar).</summary>
    const int MaxTasks = 200;

    event EventHandler? Changed;

    /// <summary>
    /// Bugün görünümü: bugünün görevleri ve önceki günlerden kalan tamamlanmamışlar. Sıra: tamamlanmamışlar
    /// (gün, sonra saatli olanlar saate göre, saatsizler sonda), en altta tamamlananlar. Dünün (ve eski)
    /// tamamlananları gizlenir ama silinmez.
    /// </summary>
    IReadOnlyList<TaskItem> GetToday();

    /// <summary>Tek gün sorgusu (gizleme kuralı yok): o güne ait bütün görevler, saate göre.</summary>
    IReadOnlyList<TaskItem> GetDay(DateOnly day);

    /// <summary>Hafta/ay görünümü için aralık sorgusu (uçlar dahil), gün sonra saate göre.</summary>
    IReadOnlyList<TaskItem> GetRange(DateOnly from, DateOnly to);

    /// <summary>Başlık boşsa null. Gün verilmezse bugün.</summary>
    TaskItem? Add(string title, TimeOnly? time = null, DateOnly? day = null);

    /// <summary>Başlık, saat ve günü günceller. Boş başlık ya da bilinmeyen id için false.</summary>
    bool Update(Guid id, string title, TimeOnly? time, DateOnly? day = null);

    bool SetDone(Guid id, bool done);

    bool Remove(Guid id);

    /// <summary>Silinen görevi aynı kimlik, gün, saat ve tamamlanma durumuyla geri koyar ("Geri al"). Zaten varsa ya da başlık boşsa false.</summary>
    bool Restore(TaskItem item);
}
