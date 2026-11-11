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

    public static int ClampDelay(int delayMs) => Math.Clamp(delayMs, 0, MaxDelayMs);

    /// <summary>
    /// Üzerine gelince ve sürükleyince açılmada gecikme uygulanır (yanlış tetiklemeyi önler). Tıklamada gecikme yoktur:
    /// kullanıcı zaten bilerek tıkladı. Tıklanmış açık çentik kapanır.
    /// </summary>
    public static NotchOpenDecision Decide(NotchOpenBehavior behavior, NotchPointerEvent pointerEvent, bool isOpen, int openDelayMs, int closeDelayMs)
    {
        var open = ClampDelay(openDelayMs);
        var close = ClampDelay(closeDelayMs);

        switch (behavior)
        {
            case NotchOpenBehavior.Hover:
                if (pointerEvent == NotchPointerEvent.Enter && !isOpen) return new(NotchOpenAction.Open, open);
                if (pointerEvent == NotchPointerEvent.Leave && isOpen) return new(NotchOpenAction.Close, close);
                break;

            case NotchOpenBehavior.Click:
                if (pointerEvent == NotchPointerEvent.Click) return new(isOpen ? NotchOpenAction.Close : NotchOpenAction.Open, 0);
                break;

            case NotchOpenBehavior.DragOver:
                if (pointerEvent == NotchPointerEvent.DragEnter && !isOpen) return new(NotchOpenAction.Open, open);
                if (pointerEvent == NotchPointerEvent.DragLeave && isOpen) return new(NotchOpenAction.Close, close);
                break;
        }

        return new(NotchOpenAction.None, 0);
    }
}
