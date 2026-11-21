namespace Koy.Core.Media;

/// <summary>
/// Döngülü videonun kare zamanlaması: dosyadaki zaman damgalarını oynatma hızına göre duvar saatine çevirir, başa sarışta
/// (damga geriye düşünce) süreyi kesintisiz sürdürür ve gösterim hızını <c>maxFps</c> ile sınırlar. Saf ve testlidir.
/// </summary>
public sealed class VideoFramePacer
{
    private readonly double _speed;
    private readonly TimeSpan _minInterval;
    private TimeSpan _loopOffset;
    private TimeSpan? _lastStamp;
    private TimeSpan _frameStep = TimeSpan.FromMilliseconds(33);
    private TimeSpan? _lastShown;

    /// <param name="speed">Oynatma hızı (0,5 = yarı hız); 0 ya da negatifse 1.</param>
    /// <param name="maxFps">Saniyede en çok gösterilecek kare; 0 ya da negatifse sınır yok.</param>
    public VideoFramePacer(double speed, int maxFps)
    {
        _speed = speed > 0 ? speed : 1.0;
        _minInterval = maxFps > 0 ? TimeSpan.FromSeconds(1.0 / maxFps) : TimeSpan.Zero;
    }

    /// <summary>
    /// Kare, oynatma başlangıcından ne kadar sonra gösterilmeli. Damga bir öncekinden küçükse dosya başa sarmıştır:
    /// önceki döngünün süresi (son damga + bir kare) ofsete eklenir.
    /// </summary>
    public TimeSpan DueAt(TimeSpan stamp)
    {
        if (_lastStamp is { } last)
        {
            if (stamp < last)
                _loopOffset += last + _frameStep;
            else if (stamp > last)
                _frameStep = stamp - last;
        }
        _lastStamp = stamp;
        return TimeSpan.FromTicks((long)((_loopOffset + stamp).Ticks / _speed));
    }

    /// <summary>
    /// Kare gösterilsin mi: kare hızı sınırının altındaysa ya da zamanı çoktan geçtiyse (bir kare süresinden fazla
    /// geride kaldıysa) atlanır; gösterilecekse kayda geçer.
    /// </summary>
    /// <param name="due">Karenin gösterim zamanı (<see cref="DueAt"/>).</param>
    /// <param name="now">Oynatma başlangıcından bu yana geçen süre.</param>
    public bool ShouldShow(TimeSpan due, TimeSpan now)
    {
        var frameDuration = TimeSpan.FromTicks((long)(_frameStep.Ticks / _speed));
        if (now - due > frameDuration + _minInterval)
            return false;
        if (_lastShown is { } shown && due - shown < _minInterval - TimeSpan.FromMilliseconds(1))
            return false;
        _lastShown = due;
        return true;
    }
}
