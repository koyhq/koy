namespace Koy.Core.Animation;

/// <summary>
/// Kütle-yay-sönüm modeli. Sabit alt adımlarla ilerlediği için kare hızından bağımsızdır.
/// Varsayılan değerler hafif bir sekme verir (kritik sönümün biraz altı).
/// </summary>
public sealed class Spring
{
    private const double MaxStep = 1.0 / 240;

    public Spring(double initial)
    {
        Value = initial;
        Target = initial;
    }

    public double Stiffness { get; init; } = 300;
    /// <summary>Sönüm. Ayarlanabilir: aynı yay yön değiştirirken farklı sönümle sürebilir (çentik kapanışı gibi).</summary>
    public double Damping { get; set; } = 26;
    public double Mass { get; init; } = 1;
    public double RestThreshold { get; init; } = 0.01;

    public double Value { get; private set; }
    public double Velocity { get; private set; }
    public double Target { get; set; }

    /// <summary>Sekmesiz (hedefi aşmadan) en hızlı oturan sönüm: 2·√(sertlik·kütle).</summary>
    public double CriticalDamping => 2 * Math.Sqrt(Stiffness * Mass);

    public bool IsAtRest =>
        Math.Abs(Target - Value) < RestThreshold && Math.Abs(Velocity) < RestThreshold;

    /// <summary>Durumu <paramref name="dt"/> saniye ilerletir. Hareket sürüyorsa true döner.</summary>
    public bool Step(double dt)
    {
        while (dt > 0)
        {
            var h = Math.Min(dt, MaxStep);
            var force = -Stiffness * (Value - Target) - Damping * Velocity;
            Velocity += force / Mass * h;
            Value += Velocity * h;
            dt -= h;
        }

        if (!IsAtRest)
            return true;

        Value = Target;
        Velocity = 0;
        return false;
    }

    /// <summary>Anlık bir darbe: hıza <paramref name="velocity"/> ekler (birim/sn). Yay sonra hedefine geri döner.</summary>
    public void Kick(double velocity) => Velocity += velocity;

    /// <summary>Animasyonsuz olarak değere atlar.</summary>
    public void SnapTo(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
    }
}
