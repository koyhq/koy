using Koy.Core.Animation;

namespace Koy.Core.Media;

/// <summary>
/// Plak modunun hareketi (issue #182): plak çalarken sabit hızla döner, duraklatınca hız yayla sıfıra iner (birden
/// durmaz), yeniden çalınca yayla hızlanır. Ton kolu çalarken plağın üstüne iner, duraklatınca kalkar.
/// Kare hızından bağımsızdır; <see cref="IsAtRest"/> olunca çağıran kare dinlemeyi bırakır.
/// </summary>
public sealed class VinylMotion
{
    /// <summary>Bir tur süresi (sn): tasarımdaki yavaş, sakin dönüş.</summary>
    public const double SecondsPerTurn = 8;

    public const double ArmPlayingAngle = 24;
    public const double ArmRestAngle = 4;

    private const double SpinSpeed = 360 / SecondsPerTurn;

    // Hız yayı sekmesiz (kritik sönüm): hızlanma/yavaşlama ~1 sn, ters dönmez.
    private readonly Spring _speed = new(0) { Stiffness = 18, Damping = 2 * Math.Sqrt(18), RestThreshold = 0.05 };

    // Kol hafif sekerek iner ve kalkar.
    private readonly Spring _arm = new(ArmRestAngle) { Stiffness = 120, Damping = 14, RestThreshold = 0.05 };

    private bool _playing;

    /// <summary>Plağın açısı (derece, 0–360).</summary>
    public double Angle { get; private set; }

    /// <summary>Ton kolunun açısı (derece).</summary>
    public double ArmAngle => _arm.Value;

    /// <summary>Anlık dönüş hızı (derece/sn).</summary>
    public double Speed => _speed.Value;

    /// <summary>Plak durdu, kol yerinde ve çalmıyor: kare gerekmez.</summary>
    public bool IsAtRest => !_playing && _speed.IsAtRest && _arm.IsAtRest;

    public void SetPlaying(bool playing)
    {
        _playing = playing;
        _speed.Target = playing ? SpinSpeed : 0;
        _arm.Target = playing ? ArmPlayingAngle : ArmRestAngle;
    }

    /// <summary>Hareketi atlamadan son duruma getirir (hareket kapalı ya da görünüm yeni açıldı).</summary>
    public void Snap()
    {
        _speed.SnapTo(_speed.Target);
        _arm.SnapTo(_arm.Target);
    }

    /// <summary>Durumu <paramref name="dt"/> saniye ilerletir.</summary>
    public void Step(double dt)
    {
        if (dt <= 0)
            return;

        _speed.Step(dt);
        _arm.Step(dt);
        var speed = Math.Max(0, _speed.Value);
        Angle = (Angle + speed * dt) % 360;
    }
}
