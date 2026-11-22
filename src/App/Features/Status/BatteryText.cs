using System.Globalization;
using Koy.App.Resources;
using Koy.Core.Power;

namespace Koy.App.Features.Status;

/// <summary>Pil gösterim kararını (Core) kullanıcı diline çevirir: ipucu ve süre metni.</summary>
public static class BatteryText
{
    /// <summary>Windows tepsisindeki gibi ipucu: "Pil durumu: tam dolu 100%".</summary>
    public static string Tooltip(BatteryDisplay d)
    {
        var culture = CultureInfo.CurrentCulture;
        var percent = d.PercentText;
        var time = d.HasTime ? Duration(d.TimeHours, d.TimeMinutes, culture) : string.Empty;

        var text = d.Tooltip switch
        {
            BatteryTooltipKind.Full => Format("BatteryTipFull", culture, percent),
            BatteryTooltipKind.Charging => Format("BatteryTipCharging", culture, percent),
            BatteryTooltipKind.ChargingWithTime => Format("BatteryTipChargingTime", culture, percent, time),
            BatteryTooltipKind.PluggedNotCharging => Format("BatteryTipPlugged", culture, percent),
            BatteryTooltipKind.RemainingWithTime => Format("BatteryTipRemainingTime", culture, percent, time),
            _ => Format("BatteryTipRemaining", culture, percent)
        };

        return d.EnergySaver ? Format("BatteryTipSaver", culture, text) : text;
    }

    private static string Duration(int hours, int minutes, CultureInfo culture) =>
        hours > 0
            ? Format("BatteryTimeHoursMinutes", culture, hours, minutes)
            : Format("BatteryTimeMinutes", culture, minutes);

    private static string Format(string key, CultureInfo culture, params object[] args) =>
        string.Format(culture, Strings.Get(key), args);
}
