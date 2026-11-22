using Koy.Core.Calendar;
using Koy.Core.Tasks;

namespace Koy.Core.Notch;

/// <summary>Çentikteki hızlı ekleme formlarının (etkinlik, görev) hata durumu; metni App seçer.</summary>
public enum QuickAddError
{
    None = 0,
    TitleEmpty = 1,
    TimeInvalid = 2,
    /// <summary>Yerel etkinlik defteri dolu (<see cref="EventBook.MaxEvents"/>); yeni etkinlik eklenemez.</summary>
    EventBookFull = 3
}

/// <summary>
/// "+" ya da Enter'a basınca sessizce hiçbir şey olmasın: eksik başlık ve geçersiz saat görünür bir hata metnine
/// dönüşür. Başlık önce denetlenir (boşken saat hatası göstermek yanıltır).
/// </summary>
public static class QuickAddValidation
{
    /// <summary>Etkinlikte saat zorunludur ("14:30", "1430" gibi).</summary>
    public static QuickAddError ForEvent(string? title, string? time)
    {
        if (EventAgenda.NormalizeTitle(title) is null)
            return QuickAddError.TitleEmpty;

        return EventAgenda.TryParseTime(time, out _) ? QuickAddError.None : QuickAddError.TimeInvalid;
    }

    /// <summary>
    /// Yukarıdaki denetime ek olarak defter doluysa <see cref="QuickAddError.EventBookFull"/> döner (ekleme sessizce
    /// başarısız olmasın). Başlık ve saat hatası önce gelir: kullanıcı önce onu düzeltir.
    /// </summary>
    public static QuickAddError ForEvent(string? title, string? time, int localEventCount)
    {
        var error = ForEvent(title, time);
        if (error != QuickAddError.None)
            return error;

        return localEventCount >= EventBook.MaxEvents ? QuickAddError.EventBookFull : QuickAddError.None;
    }

    /// <summary>Görevde saat isteğe bağlıdır: boş geçerlidir, dolu ama bozuksa hatadır.</summary>
    public static QuickAddError ForTask(string? title, string? time)
    {
        if (string.IsNullOrWhiteSpace(title))
            return QuickAddError.TitleEmpty;

        return TaskViews.TryParseTime(time, out _) ? QuickAddError.None : QuickAddError.TimeInvalid;
    }
}
