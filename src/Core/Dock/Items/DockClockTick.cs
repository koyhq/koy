using System.Globalization;

namespace Koy.Core.Dock.Items;

/// <summary>Dock saat öğesinin zamanlama ve biçim kuralları. Saniye gösterilmediği için dakika başında bir kez uyanılır.</summary>
public static class DockClockTick
{
    private static readonly TimeSpan Slack = TimeSpan.FromMilliseconds(50);

    /// <summary>Bir sonraki dakika başına kalan süre (+50 ms pay, böylece uyanışta dakika kesin değişmiş olur). Hep 0'dan büyüktür, en çok 60,05 sn.</summary>
    public static TimeSpan DelayToNextMinute(DateTime now)
    {
        var intoMinute = TimeSpan.FromSeconds(now.Second) + TimeSpan.FromMilliseconds(now.Millisecond);
        return TimeSpan.FromMinutes(1) - intoMinute + Slack;
    }

    public static string FormatTime(DateTime now, CultureInfo culture) => now.ToString("t", culture);

    public static string FormatDate(DateTime now, CultureInfo culture) => now.ToString("ddd d MMM", culture);
}
