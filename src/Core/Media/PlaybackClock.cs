namespace Koy.Core.Media;

/// <summary>Oynatıcının seyrek bildirdiği zaman çizelgesinden şu anki konumu hesaplar.</summary>
public static class PlaybackClock
{
    // Bu kadar eski bir damga güvenilmez: oynatıcı bildirimi kesmiştir, süreyi eklemek konumu sona fırlatırdı.
    private static readonly TimeSpan MaxExtrapolation = TimeSpan.FromHours(1);

    public static TimeSpan Duration(PlaybackTimeline timeline)
    {
        var duration = timeline.EndTime - timeline.StartTime;
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    public static TimeSpan PositionAt(PlaybackTimeline timeline, bool isPlaying, DateTimeOffset now)
    {
        var position = timeline.Position - timeline.StartTime;

        if (isPlaying && timeline.LastUpdated != DateTimeOffset.MinValue)
        {
            var elapsed = now - timeline.LastUpdated;
            if (elapsed > TimeSpan.Zero && elapsed < MaxExtrapolation)
                position += elapsed;
        }

        var duration = Duration(timeline);
        if (position < TimeSpan.Zero)
            return TimeSpan.Zero;

        return position > duration ? duration : position;
    }
}
