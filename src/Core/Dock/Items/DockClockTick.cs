using System.Globalization;
using Koy.Core.Notch;

namespace Koy.Core.Dock.Items;

/// <summary>Dock saat öğesinin zamanlama ve biçim kuralları. Saniye gösterilmediği için dakika başında bir kez uyanılır.</summary>
public static class DockClockTick
{
    /// <summary>Bir sonraki dakika başına kalan süre (Core.Notch.ClockTick ile ortak hesaplanır).</summary>
    public static TimeSpan DelayToNextMinute(DateTime now) => ClockTick.DelayToNextMinute(now);

    public static string FormatTime(DateTime now, CultureInfo culture) => now.ToString("t", culture);

    public static string FormatDate(DateTime now, CultureInfo culture) => now.ToString("ddd d MMM", culture);
}
