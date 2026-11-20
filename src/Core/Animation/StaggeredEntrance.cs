namespace Koy.Core.Animation;

/// <summary>
/// Kademeli giriş (issue #182): çentik açılınca sayfanın öğeleri sırayla, aralarında <see cref="Step"/> gecikmeyle,
/// aşağıdan ve 0,94 ölçekten belirir. Tek bir geçen süre değeri bütün öğelerin ilerlemesini verir; çağıran kare
/// başına süreyi artırır, <see cref="IsDone"/> olunca kare dinlemeyi bırakır.
/// Kapanış ayrı ve kısadır (<see cref="ExitSeconds"/>): içerik solar, şekil kapanırken yazı kalabalığı kalmaz.
/// </summary>
public sealed class StaggeredEntrance
{
    public const double StartScale = 0.94;
    public const double Rise = 8;
    public const double ExitSeconds = 0.12;

    public StaggeredEntrance(int count, double step = 0.05, double duration = 0.32)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Count = count;
        Step = step;
        Duration = duration;
    }

    public int Count { get; }

    /// <summary>Ardışık iki öğenin başlangıçları arasındaki süre (sn).</summary>
    public double Step { get; }

    /// <summary>Bir öğenin girişi (sn).</summary>
    public double Duration { get; }

    /// <summary>Son öğenin de bittiği an.</summary>
    public double TotalSeconds => Count == 0 ? 0 : (Count - 1) * Step + Duration;

    public bool IsDone(double elapsed) => elapsed >= TotalSeconds;

    /// <summary><paramref name="index"/>. öğenin yumuşatılmış ilerlemesi; hafif aşar (1'in biraz üstüne çıkıp oturur).</summary>
    public double ProgressAt(int index, double elapsed)
    {
        var t = (elapsed - index * Step) / Duration;
        if (t <= 0)
            return 0;
        if (t >= 1)
            return 1;
        return EaseOutBack(t);
    }

    /// <summary><paramref name="index"/>. öğenin karesi.</summary>
    public EntranceFrame FrameAt(int index, double elapsed)
    {
        var p = ProgressAt(index, elapsed);
        return new EntranceFrame(
            Opacity: Math.Clamp(p, 0, 1),
            Scale: StartScale + (1 - StartScale) * p,
            OffsetY: (1 - p) * Rise);
    }

    /// <summary>Kapanışta içeriğin saydamlığı (doğrusal, <see cref="ExitSeconds"/> içinde 1 → 0).</summary>
    public static double ExitOpacityAt(double elapsed) =>
        elapsed <= 0 ? 1 : elapsed >= ExitSeconds ? 0 : 1 - elapsed / ExitSeconds;

    // Hafif aşım: tasarımdaki cubic-bezier(.2,.9,.3,1.1) eğrisine yakın, en çok ~%2 aşar.
    private static double EaseOutBack(double t)
    {
        const double c1 = 0.8;
        const double c3 = c1 + 1;
        var u = t - 1;
        return 1 + c3 * u * u * u + c1 * u * u;
    }
}

/// <summary>Kademeli girişte bir öğenin saydamlık, ölçek ve dikey kayması.</summary>
public readonly record struct EntranceFrame(double Opacity, double Scale, double OffsetY);
