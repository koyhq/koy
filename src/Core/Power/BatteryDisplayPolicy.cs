using System.Globalization;

namespace Koy.Core.Power;

/// <summary>Pilin kullanıcıya gösterilen durumu (Windows 11 pil simgesi mantığı).</summary>
public enum BatteryState
{
    /// <summary>Prizde ve şarj oluyor (%100'ün altında).</summary>
    Charging,
    /// <summary>Prizde ve %100: tam dolu.</summary>
    PluggedFull,
    /// <summary>Prizde ama şarj olmuyor (şarj sınırı, yetersiz adaptör).</summary>
    PluggedNotCharging,
    /// <summary>Pilde, düşük değil.</summary>
    OnBattery,
    /// <summary>Pilde ve %20 ve altı.</summary>
    Low,
    /// <summary>Pilde ve %10 ve altı.</summary>
    Critical
}

/// <summary>Pil gövdesinin içine çizilen küçük işaret.</summary>
public enum BatteryOverlay
{
    None,
    /// <summary>Yıldırım: şarj oluyor ya da prizde tam dolu.</summary>
    Bolt,
    /// <summary>Fiş: prizde, şarj olmuyor.</summary>
    Plug,
    /// <summary>Yaprak: pil tasarrufu (Energy Saver) açık.</summary>
    Leaf
}

/// <summary>Dolum rengi: Normal yeşil, Warning uyarı, Critical tehlike.</summary>
public enum BatteryTone
{
    Normal,
    Warning,
    Critical
}

/// <summary>İpucu metninin biçimi; çeviriyi App yapar (resx anahtarı eşlemesi orada).</summary>
public enum BatteryTooltipKind
{
    /// <summary>"Pil durumu: tam dolu 100%"</summary>
    Full,
    /// <summary>"Pil durumu: şarj oluyor 80%"</summary>
    Charging,
    /// <summary>Şarj oluyor + tam dolmaya kalan süre.</summary>
    ChargingWithTime,
    /// <summary>"Pil durumu: prizde, şarj olmuyor 80%"</summary>
    PluggedNotCharging,
    /// <summary>"Pil durumu: 54% kaldı"</summary>
    Remaining,
    /// <summary>"%x kaldı" + kalan süre.</summary>
    RemainingWithTime
}

/// <summary>Güç bilgisinin gösterim kararı. <see cref="BatteryDisplayPolicy.Decide"/> üretir.</summary>
public readonly record struct BatteryDisplay(
    bool HasBattery,
    int Percent,
    BatteryState State,
    BatteryOverlay Overlay,
    double FillRatio,
    BatteryTone Tone,
    BatteryTooltipKind Tooltip,
    bool EnergySaver,
    int TimeHours,
    int TimeMinutes)
{
    /// <summary>Hiç pil bilgisi olmayan başlangıç değeri.</summary>
    public static BatteryDisplay None { get; } = BatteryDisplayPolicy.Decide(false, 100, false, false, false, null);

    /// <summary>"100%" biçimi.</summary>
    public string PercentText => BatteryDisplayPolicy.FormatPercent(Percent);

    /// <summary>Şarj ya da prizde durumu (yıldırım / fiş gösterilen haller).</summary>
    public bool IsPluggedIn => State is BatteryState.Charging or BatteryState.PluggedFull or BatteryState.PluggedNotCharging;

    public bool HasTime => TimeHours > 0 || TimeMinutes > 0;
}

/// <summary>
/// Pilin ne gösterileceğine karar veren saf mantık: durum, simge işareti, dolum, renk ve ipucu türü.
/// Dock pil öğesi, çentik ve durum alanı aynı kararı kullanır.
/// </summary>
public static class BatteryDisplayPolicy
{
    /// <summary>Bu yüzdeye kadar (dahil) ve pildeyken düşük sayılır.</summary>
    public const int LowThreshold = 20;

    /// <summary>Bu yüzdeye kadar (dahil) ve pildeyken kritik sayılır.</summary>
    public const int CriticalThreshold = 10;

    /// <summary>Gösterilen kalan süre üst sınırı; üstü anlamsız (bilinmiyor) sayılır.</summary>
    private static readonly TimeSpan MaxTime = TimeSpan.FromHours(100);

    public static int Clamp(int percent) => Math.Clamp(percent, 0, 100);

    /// <summary>"82%" biçimi: kültürden bağımsız, Windows tepsisindeki gibi (Türkçede de "100%").</summary>
    public static string FormatPercent(int percent) =>
        Clamp(percent).ToString(CultureInfo.InvariantCulture) + "%";

    /// <param name="hasBattery">Makinede pil var mı.</param>
    /// <param name="percent">Kalan yüzde.</param>
    /// <param name="acConnected">Güç kaynağı takılı mı (şarj olmasa da).</param>
    /// <param name="charging">Pil şarj oluyor mu.</param>
    /// <param name="energySaver">Pil tasarrufu açık mı.</param>
    /// <param name="remaining">Kalan süre (pilde: bitene, şarjda: dolana); bilinmiyorsa null.</param>
    public static BatteryDisplay Decide(
        bool hasBattery, int percent, bool acConnected, bool charging, bool energySaver, TimeSpan? remaining)
    {
        var pct = Clamp(percent);
        var plugged = acConnected || charging;

        BatteryState state;
        if (plugged)
        {
            state = pct >= 100 ? BatteryState.PluggedFull
                : charging ? BatteryState.Charging
                : BatteryState.PluggedNotCharging;
        }
        else
        {
            state = pct <= CriticalThreshold ? BatteryState.Critical
                : pct <= LowThreshold ? BatteryState.Low
                : BatteryState.OnBattery;
        }

        var overlay = state switch
        {
            BatteryState.Charging or BatteryState.PluggedFull => BatteryOverlay.Bolt,
            BatteryState.PluggedNotCharging => BatteryOverlay.Plug,
            _ => energySaver ? BatteryOverlay.Leaf : BatteryOverlay.None
        };

        var tone = state switch
        {
            BatteryState.Critical => BatteryTone.Critical,
            BatteryState.Low => BatteryTone.Warning,
            _ => BatteryTone.Normal
        };

        var (hours, minutes) = SplitTime(remaining);
        var hasTime = hours > 0 || minutes > 0;
        var tooltip = state switch
        {
            BatteryState.PluggedFull => BatteryTooltipKind.Full,
            BatteryState.Charging => hasTime ? BatteryTooltipKind.ChargingWithTime : BatteryTooltipKind.Charging,
            BatteryState.PluggedNotCharging => BatteryTooltipKind.PluggedNotCharging,
            _ => hasTime ? BatteryTooltipKind.RemainingWithTime : BatteryTooltipKind.Remaining
        };

        return new BatteryDisplay(hasBattery, pct, state, overlay, pct / 100.0, tone, tooltip,
            energySaver, hasTime ? hours : 0, hasTime ? minutes : 0);
    }

    /// <summary>Süreyi saat ve dakikaya böler; bilinmiyor, sıfır ya da uçuk büyükse (0, 0).</summary>
    public static (int Hours, int Minutes) SplitTime(TimeSpan? time)
    {
        if (time is not { } t || t < TimeSpan.FromMinutes(1) || t > MaxTime)
            return (0, 0);

        return ((int)t.TotalHours, t.Minutes);
    }
}
