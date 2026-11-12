namespace Koy.Core.Notch;

/// <summary>
/// Çentik saati yalnız dakika değişince yenilenir: zamanlayıcı her tıkta bir sonraki dakika başına yeniden kurulur.
/// Saniyede ya da birkaç saniyede bir uyanmaktan ucuz, saat de dakika döner dönmez değişir. Saniye gösterilirse
/// bu hesap yetmez.
/// </summary>
public static class ClockTick
{
    /// <summary>Dakika döndükten sonra uyanma payı; zamanlayıcı birkaç ms erken düşse de yeni dakika okunur.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMilliseconds(30);

    /// <summary>En kısa bekleme; dakikanın son anında çok kısa bir zamanlayıcı kurulmasın.</summary>
    public static readonly TimeSpan MinimumDelay = TimeSpan.FromMilliseconds(100);

    /// <summary><paramref name="now"/> anından bir sonraki dakika başına (artı pay) kalan süre.</summary>
    public static TimeSpan DelayToNextMinute(DateTime now)
    {
        var intoMinute = TimeSpan.FromTicks(now.Ticks % TimeSpan.TicksPerMinute);
        var delay = TimeSpan.FromMinutes(1) - intoMinute + Margin;
        return delay < MinimumDelay ? MinimumDelay : delay;
    }
}
