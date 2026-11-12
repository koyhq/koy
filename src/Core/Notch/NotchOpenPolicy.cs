namespace Koy.Core.Notch;

/// <summary>Çentik açılıp kapanmasını tetikleyen imleç ve sürükleme olayları.</summary>
public enum NotchPointerEvent
{
    Enter,
    Leave,
    Click,
    DragEnter,
    DragLeave
}

/// <summary>Olaydan sonra yapılacak iş: hiçbiri, açmak ya da kapatmak.</summary>
public enum NotchOpenAction
{
    None,
    Open,
    Close
}

/// <summary>Karar: ne yapılacağı ve kaç ms sonra (0 = hemen).</summary>
public readonly record struct NotchOpenDecision(NotchOpenAction Action, int DelayMs);

/// <summary>
/// Çentik açılma kipine göre olay → karar eşlemesi. Saf mantık; zamanlayıcıyı çağıran kod kurar.
/// Kural: imleç yeniden girerse (Enter) bekleyen kapanma iptal edilmelidir; bu karar onu yeniden söylemez.
/// </summary>
public static class NotchOpenPolicy
{
    public const int MaxDelayMs = 2000;

    /// <summary>İmleç çıkınca kapanma gecikmesinin bugünkü değeri (<see cref="NotchController"/> zamanlayıcısı).</summary>
    public const int DefaultCloseDelayMs = 400;

    public static int ClampDelay(int delayMs) => Math.Clamp(delayMs, 0, MaxDelayMs);

    /// <summary>
    /// Sürüklenen öğe çentiğin üstünde <paramref name="elapsedMs"/> süredir duruyor: çentik açılsın mı. Yalnız
    /// "sürükleyince aç" kipinde açılma gecikmesi beklenir; diğer kiplerde hemen açılır (bugünkü davranış, Raf'a bırakmak için).
    /// Sürükleme olayları zaten sürekli geldiği için zamanlayıcı gerekmez.
    /// </summary>
    public static bool DragShouldOpen(NotchOpenBehavior behavior, double elapsedMs, int openDelayMs) =>
        behavior != NotchOpenBehavior.DragOver || elapsedMs >= ClampDelay(openDelayMs);

    /// <summary>
    /// Kipe özgü olan yalnız çentiği neyin açtığıdır: üzerine gelmek, tıklamak ya da sürüklemek. Açılma gecikmesi
    /// kipin kendi tetikleyicisine uygulanır (yanlış tetiklemeyi önler); tıklamada gecikme yoktur, kullanıcı bilerek tıkladı.
    /// Her kipte ortak olanlar bugünkü davranıştır: imleç (ya da sürüklenen öğe) çıkınca açık çentik kapanma gecikmesiyle
    /// kapanır; dosya ya da metin sürüklenince çentik, Raf'a bırakılabilsin diye açılır.
    /// </summary>
    public static NotchOpenDecision Decide(NotchOpenBehavior behavior, NotchPointerEvent pointerEvent, bool isOpen, int openDelayMs, int closeDelayMs)
    {
        var open = ClampDelay(openDelayMs);
        var close = ClampDelay(closeDelayMs);

        return pointerEvent switch
        {
            NotchPointerEvent.Leave or NotchPointerEvent.DragLeave when isOpen => new(NotchOpenAction.Close, close),
            NotchPointerEvent.Enter when !isOpen && behavior == NotchOpenBehavior.Hover => new(NotchOpenAction.Open, open),
            NotchPointerEvent.Click when behavior == NotchOpenBehavior.Click =>
                new(isOpen ? NotchOpenAction.Close : NotchOpenAction.Open, 0),
            NotchPointerEvent.DragEnter when !isOpen =>
                new(NotchOpenAction.Open, behavior == NotchOpenBehavior.DragOver ? open : 0),
            _ => new(NotchOpenAction.None, 0),
        };
    }
}
