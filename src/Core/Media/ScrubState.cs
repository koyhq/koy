namespace Koy.Core.Media;

/// <summary>
/// Müzik ilerleme çubuğunda sürükleyerek sarmanın karar durumu: başla / taşı / bitir / iptal. Sürükleme sırasında oynatıcıya
/// hiçbir şey gönderilmez; yalnız <see cref="End"/> tek bir hedef konum döndürür. Süresi bilinmeyen (canlı) oynatımda kapalıdır.
/// </summary>
public sealed class ScrubState
{
    /// <summary>Klavyeyle (sol / sağ ok) tek adım.</summary>
    public static readonly TimeSpan KeyStep = TimeSpan.FromSeconds(5);

    private TimeSpan _duration;

    public bool IsActive { get; private set; }

    /// <summary>Kıstırılmış (0–1) önizleme kesri; sürükleme yokken 0.</summary>
    public double Fraction { get; private set; }

    public TimeSpan PreviewPosition => IsActive ? Scale(_duration, Fraction) : TimeSpan.Zero;

    /// <summary>Balonda görünen hedef süre ("1:23").</summary>
    public string Text => IsActive ? FormatTime(PreviewPosition) : string.Empty;

    /// <summary>Sürükleme başlar. Süre bilinmiyorsa (≤ 0, canlı yayın) başlamaz ve false döner.</summary>
    public bool Begin(TimeSpan duration, double fraction)
    {
        if (duration <= TimeSpan.Zero)
            return false;

        _duration = duration;
        IsActive = true;
        Fraction = Clamp(fraction);
        return true;
    }

    public void Move(double fraction)
    {
        if (IsActive)
            Fraction = Clamp(fraction);
    }

    /// <summary>Bırakma: sürükleme biter, sarılacak konum döner (sürükleme yoksa null).</summary>
    public TimeSpan? End()
    {
        if (!IsActive)
            return null;

        var target = PreviewPosition;
        Reset();
        return target;
    }

    /// <summary>Esc ya da yakalama kaybı: hiçbir şey sarılmaz.</summary>
    public void Cancel() => Reset();

    /// <summary>Konumun süreye oranı, 0–1 kıstırılmış; süre bilinmiyorsa 0.</summary>
    public static double FractionOf(TimeSpan position, TimeSpan duration) =>
        duration <= TimeSpan.Zero ? 0.0 : Clamp(position.TotalSeconds / duration.TotalSeconds);

    /// <summary>Ok tuşu adımı: konuma <paramref name="delta"/> eklenir, [0, süre] aralığına kıstırılır; süre bilinmiyorsa null.</summary>
    public static TimeSpan? StepTarget(TimeSpan position, TimeSpan duration, TimeSpan delta)
    {
        if (duration <= TimeSpan.Zero)
            return null;

        var target = position + delta;
        if (target < TimeSpan.Zero)
            return TimeSpan.Zero;
        return target > duration ? duration : target;
    }

    public static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";

    private void Reset()
    {
        IsActive = false;
        Fraction = 0;
        _duration = TimeSpan.Zero;
    }

    private static double Clamp(double value) =>
        double.IsNaN(value) ? 0.0 : Math.Clamp(value, 0.0, 1.0);

    private static TimeSpan Scale(TimeSpan duration, double fraction) =>
        TimeSpan.FromTicks((long)(duration.Ticks * fraction));
}
