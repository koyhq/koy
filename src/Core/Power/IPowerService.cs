namespace Koy.Core.Power;

public enum BatteryStatus
{
    NotPresent,
    Discharging,
    Charging,
    Idle
}

/// <summary>
/// Pil ve güç yönetimi sözleşmesi. Güç kaynağı, şarj ve pil tasarrufu ayrı bilgilerdir:
/// prizde ama şarj olmayan (ya da tam dolu) pil <see cref="IsAcConnected"/> true, <see cref="IsCharging"/> false verir.
/// </summary>
public interface IPowerService : IDisposable
{
    bool HasBattery { get; }
    int RemainingPercent { get; }

    /// <summary>Pil şarj oluyor (güç kaynağı takılı olması yetmez).</summary>
    bool IsCharging { get; }

    /// <summary>Güç kaynağı (priz) takılı; şarj olmayabilir.</summary>
    bool IsAcConnected { get; }

    /// <summary>Windows pil tasarrufu (Energy Saver) açık.</summary>
    bool IsEnergySaverOn { get; }

    /// <summary>Pilde tahmini kalan süre; prizdeyken ya da bilinmiyorsa null.</summary>
    TimeSpan? RemainingTime { get; }

    BatteryStatus Status { get; }

    event EventHandler<PowerChangedEventArgs>? PowerChanged;
}

public sealed record PowerChangedEventArgs(
    bool HasBattery,
    int RemainingPercent,
    bool IsCharging,
    BatteryStatus Status,
    bool IsAcConnected = false,
    bool IsEnergySaverOn = false,
    TimeSpan? RemainingTime = null);

public static class PowerServiceExtensions
{
    /// <summary>Hizmetin o anki durumundan gösterim kararı.</summary>
    public static BatteryDisplay Display(this IPowerService power) =>
        BatteryDisplayPolicy.Decide(power.HasBattery, power.RemainingPercent, power.IsAcConnected,
            power.IsCharging, power.IsEnergySaverOn, power.RemainingTime);

    /// <summary>Olay verisinden gösterim kararı.</summary>
    public static BatteryDisplay Display(this PowerChangedEventArgs e) =>
        BatteryDisplayPolicy.Decide(e.HasBattery, e.RemainingPercent, e.IsAcConnected,
            e.IsCharging, e.IsEnergySaverOn, e.RemainingTime);
}
